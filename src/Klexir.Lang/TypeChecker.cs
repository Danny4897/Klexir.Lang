using MonadicSharp;

namespace Klexir.Lang;

/// <summary>Structural type checker producing a <see cref="TypedExpr"/> tree over a `let`-scoped environment.</summary>
public sealed class TypeChecker
{
    public Result<TypedExpr> Check(Expr expr) => Check(expr, new Dictionary<string, KlexirType>());

    private static Result<TypedExpr> Check(Expr expr, IReadOnlyDictionary<string, KlexirType> environment) =>
        expr switch
        {
            IntLiteral literal => Result<TypedExpr>.Success(new TypedIntLiteral(literal.Value)),

            StringLiteral literal => Result<TypedExpr>.Success(new TypedStringLiteral(literal.Value)),

            BoolLiteral literal => Result<TypedExpr>.Success(new TypedBoolLiteral(literal.Value)),

            Identifier identifier => environment.TryGetValue(identifier.Name, out var declaredType)
                ? Result<TypedExpr>.Success(new TypedIdentifier(identifier.Name, declaredType))
                : Result<TypedExpr>.Failure(Error.Create($"Unbound identifier '{identifier.Name}'.")),

            BinaryExpr binary => Check(binary.Left, environment)
                .Bind(left => Check(binary.Right, environment)
                    .Bind(right => CheckBinaryOperands(binary.Operator, left, right))),

            ComparisonExpr comparison => Check(comparison.Left, environment)
                .Bind(left => Check(comparison.Right, environment)
                    .Bind(right => CheckComparisonOperands(comparison.Operator, left, right))),

            IfExpr ifExpr => Check(ifExpr.Condition, environment)
                .Bind(condition => condition.Type == KlexirType.Bool
                    ? Check(ifExpr.Then, environment)
                        .Bind(thenBranch => Check(ifExpr.Else, environment)
                            .Bind(elseBranch => thenBranch.Type == elseBranch.Type
                                ? Result<TypedExpr>.Success(new TypedIfExpr(condition, thenBranch, elseBranch, thenBranch.Type))
                                : Result<TypedExpr>.Failure(Error.Create(
                                    $"If branches must have the same type, got {thenBranch.Type} and {elseBranch.Type}."))))
                    : Result<TypedExpr>.Failure(Error.Create($"If condition must be Bool, got {condition.Type}."))),

            LetExpr let => Check(let.Value, environment)
                .Bind(value => Check(let.Body, WithBinding(environment, let.Name, value.Type))
                    .Bind(body => Result<TypedExpr>.Success(new TypedLetExpr(let.Name, value, body, body.Type)))),

            FunExpr fun => Check(fun.Body, WithBinding(environment, fun.ParamName, fun.ParamType))
                .Bind(body => Result<TypedExpr>.Success(
                    new TypedFunExpr(fun.ParamName, fun.ParamType, body, new FunctionType(fun.ParamType, body.Type)))),

            AppExpr app => Check(app.Function, environment)
                .Bind(function => function.Type is FunctionType functionType
                    ? Check(app.Argument, environment)
                        .Bind(argument => argument.Type == functionType.Parameter
                            ? Result<TypedExpr>.Success(new TypedAppExpr(function, argument, functionType.Return))
                            : Result<TypedExpr>.Failure(Error.Create(
                                $"Function expects {functionType.Parameter} but got {argument.Type}.")))
                    : Result<TypedExpr>.Failure(Error.Create($"Cannot apply a non-function value of type {function.Type}."))),

            LetRecExpr letRec => CheckLetRec(letRec, environment),

            SomeExpr some => Check(some.Value, environment)
                .Bind(value => Result<TypedExpr>.Success(new TypedSomeExpr(value, new OptionType(value.Type)))),

            NoneExpr none => Result<TypedExpr>.Success(new TypedNoneExpr(new OptionType(none.ElementType))),

            OkExpr ok => Check(ok.Value, environment)
                .Bind(value => Result<TypedExpr>.Success(new TypedOkExpr(value, new ResultType(value.Type, ok.ErrType)))),

            ErrExpr err => Check(err.Value, environment)
                .Bind(value => Result<TypedExpr>.Success(new TypedErrExpr(value, new ResultType(err.OkType, value.Type)))),

            MatchOptionExpr match => CheckMatchOption(match, environment),

            MatchResultExpr match => CheckMatchResult(match, environment),

            MapExpr map => CheckMap(map, environment),

            BindExpr bind => CheckBind(bind, environment),

            ListExpr list => CheckList(list, environment),

            EmptyListExpr empty => Result<TypedExpr>.Success(new TypedEmptyListExpr(new ListType(empty.ElementType))),

            FilterExpr filter => CheckFilter(filter, environment),

            FoldExpr fold => CheckFold(fold, environment),

            _ => Result<TypedExpr>.Failure(Error.Create($"Unsupported expression node '{expr.GetType().Name}'.")),
        };

    private static Result<TypedExpr> CheckList(ListExpr list, IReadOnlyDictionary<string, KlexirType> environment)
    {
        var typedElements = new List<TypedExpr>();
        KlexirType? elementType = null;

        foreach (var element in list.Elements)
        {
            var result = Check(element, environment);
            if (result.IsFailure)
            {
                return result;
            }

            elementType ??= result.Value.Type;
            if (result.Value.Type != elementType)
            {
                return Result<TypedExpr>.Failure(Error.Create(
                    $"List elements must share a type, got {elementType} and {result.Value.Type}."));
            }

            typedElements.Add(result.Value);
        }

        return Result<TypedExpr>.Success(new TypedListExpr(typedElements, new ListType(elementType!)));
    }

    private static Result<TypedExpr> CheckFilter(FilterExpr filter, IReadOnlyDictionary<string, KlexirType> environment)
    {
        var listResult = Check(filter.List, environment);
        if (listResult.IsFailure)
        {
            return listResult;
        }

        if (listResult.Value.Type is not ListType listType)
        {
            return Result<TypedExpr>.Failure(Error.Create($"'filter' requires a List, got {listResult.Value.Type}."));
        }

        var predicateResult = Check(filter.Predicate, environment);
        if (predicateResult.IsFailure)
        {
            return predicateResult;
        }

        if (predicateResult.Value.Type is not FunctionType predicateType
            || predicateType.Parameter != listType.Element || predicateType.Return != KlexirType.Bool)
        {
            return Result<TypedExpr>.Failure(Error.Create(
                $"'filter' requires a {listType.Element} -> Bool predicate, got {predicateResult.Value.Type}."));
        }

        return Result<TypedExpr>.Success(new TypedFilterExpr(listResult.Value, predicateResult.Value, listResult.Value.Type));
    }

    private static Result<TypedExpr> CheckFold(FoldExpr fold, IReadOnlyDictionary<string, KlexirType> environment)
    {
        var listResult = Check(fold.List, environment);
        if (listResult.IsFailure)
        {
            return listResult;
        }

        if (listResult.Value.Type is not ListType listType)
        {
            return Result<TypedExpr>.Failure(Error.Create($"'fold' requires a List, got {listResult.Value.Type}."));
        }

        var initialResult = Check(fold.Initial, environment);
        if (initialResult.IsFailure)
        {
            return initialResult;
        }

        var folderResult = Check(fold.Folder, environment);
        if (folderResult.IsFailure)
        {
            return folderResult;
        }

        if (folderResult.Value.Type is not FunctionType outer
            || outer.Parameter != initialResult.Value.Type
            || outer.Return is not FunctionType inner
            || inner.Parameter != listType.Element
            || inner.Return != initialResult.Value.Type)
        {
            return Result<TypedExpr>.Failure(Error.Create(
                $"'fold' requires a folder of type {initialResult.Value.Type} -> {listType.Element} -> {initialResult.Value.Type}, got {folderResult.Value.Type}."));
        }

        return Result<TypedExpr>.Success(
            new TypedFoldExpr(listResult.Value, initialResult.Value, folderResult.Value, initialResult.Value.Type));
    }

    private static Result<TypedExpr> CheckMatchOption(MatchOptionExpr match, IReadOnlyDictionary<string, KlexirType> environment)
    {
        var scrutineeResult = Check(match.Scrutinee, environment);
        if (scrutineeResult.IsFailure)
        {
            return scrutineeResult;
        }

        if (scrutineeResult.Value.Type is not OptionType optionType)
        {
            return Result<TypedExpr>.Failure(Error.Create(
                $"'match ... with Some/None' requires an Option scrutinee, got {scrutineeResult.Value.Type}."));
        }

        var someBodyResult = Check(match.SomeBody, WithBinding(environment, match.SomeBinder, optionType.Element));
        if (someBodyResult.IsFailure)
        {
            return someBodyResult;
        }

        var noneBodyResult = Check(match.NoneBody, environment);
        if (noneBodyResult.IsFailure)
        {
            return noneBodyResult;
        }

        if (someBodyResult.Value.Type != noneBodyResult.Value.Type)
        {
            return Result<TypedExpr>.Failure(Error.Create(
                $"Match arms must have the same type, got {someBodyResult.Value.Type} and {noneBodyResult.Value.Type}."));
        }

        return Result<TypedExpr>.Success(new TypedMatchOptionExpr(
            scrutineeResult.Value, match.SomeBinder, someBodyResult.Value, noneBodyResult.Value, someBodyResult.Value.Type));
    }

    private static Result<TypedExpr> CheckMatchResult(MatchResultExpr match, IReadOnlyDictionary<string, KlexirType> environment)
    {
        var scrutineeResult = Check(match.Scrutinee, environment);
        if (scrutineeResult.IsFailure)
        {
            return scrutineeResult;
        }

        if (scrutineeResult.Value.Type is not ResultType resultType)
        {
            return Result<TypedExpr>.Failure(Error.Create(
                $"'match ... with Ok/Err' requires a Result scrutinee, got {scrutineeResult.Value.Type}."));
        }

        var okBodyResult = Check(match.OkBody, WithBinding(environment, match.OkBinder, resultType.Ok));
        if (okBodyResult.IsFailure)
        {
            return okBodyResult;
        }

        var errBodyResult = Check(match.ErrBody, WithBinding(environment, match.ErrBinder, resultType.Err));
        if (errBodyResult.IsFailure)
        {
            return errBodyResult;
        }

        if (okBodyResult.Value.Type != errBodyResult.Value.Type)
        {
            return Result<TypedExpr>.Failure(Error.Create(
                $"Match arms must have the same type, got {okBodyResult.Value.Type} and {errBodyResult.Value.Type}."));
        }

        return Result<TypedExpr>.Success(new TypedMatchResultExpr(
            scrutineeResult.Value, match.OkBinder, okBodyResult.Value, match.ErrBinder, errBodyResult.Value, okBodyResult.Value.Type));
    }

    private static Result<TypedExpr> CheckMap(MapExpr map, IReadOnlyDictionary<string, KlexirType> environment)
    {
        var containerResult = Check(map.Container, environment);
        if (containerResult.IsFailure)
        {
            return containerResult;
        }

        var mapperResult = Check(map.Mapper, environment);
        if (mapperResult.IsFailure)
        {
            return mapperResult;
        }

        if (mapperResult.Value.Type is not FunctionType mapperType)
        {
            return Result<TypedExpr>.Failure(Error.Create(
                $"'map' requires a function as its second argument, got {mapperResult.Value.Type}."));
        }

        return containerResult.Value.Type switch
        {
            OptionType optionType when mapperType.Parameter == optionType.Element =>
                Result<TypedExpr>.Success(new TypedMapExpr(containerResult.Value, mapperResult.Value, new OptionType(mapperType.Return))),

            ResultType resultType when mapperType.Parameter == resultType.Ok =>
                Result<TypedExpr>.Success(new TypedMapExpr(containerResult.Value, mapperResult.Value, new ResultType(mapperType.Return, resultType.Err))),

            ListType listType when mapperType.Parameter == listType.Element =>
                Result<TypedExpr>.Success(new TypedMapExpr(containerResult.Value, mapperResult.Value, new ListType(mapperType.Return))),

            _ => Result<TypedExpr>.Failure(Error.Create(
                $"'map' cannot apply a function from {mapperType.Parameter} over {containerResult.Value.Type}.")),
        };
    }

    private static Result<TypedExpr> CheckBind(BindExpr bind, IReadOnlyDictionary<string, KlexirType> environment)
    {
        var containerResult = Check(bind.Container, environment);
        if (containerResult.IsFailure)
        {
            return containerResult;
        }

        var mapperResult = Check(bind.Mapper, environment);
        if (mapperResult.IsFailure)
        {
            return mapperResult;
        }

        if (mapperResult.Value.Type is not FunctionType mapperType)
        {
            return Result<TypedExpr>.Failure(Error.Create(
                $"'bind' requires a function as its second argument, got {mapperResult.Value.Type}."));
        }

        switch (containerResult.Value.Type)
        {
            case OptionType optionType when mapperType.Parameter == optionType.Element && mapperType.Return is OptionType:
                return Result<TypedExpr>.Success(new TypedBindExpr(containerResult.Value, mapperResult.Value, mapperType.Return));

            case ResultType resultType when mapperType.Parameter == resultType.Ok
                && mapperType.Return is ResultType returnResultType && returnResultType.Err == resultType.Err:
                return Result<TypedExpr>.Success(new TypedBindExpr(containerResult.Value, mapperResult.Value, mapperType.Return));

            default:
                return Result<TypedExpr>.Failure(Error.Create(
                    $"'bind' cannot chain a function of type {mapperType} over {containerResult.Value.Type}."));
        }
    }

    private static Result<TypedExpr> CheckLetRec(LetRecExpr letRec, IReadOnlyDictionary<string, KlexirType> environment)
    {
        var functionType = new FunctionType(letRec.ParamType, letRec.ReturnType);
        var bodyEnvironment = WithBinding(WithBinding(environment, letRec.Name, functionType), letRec.ParamName, letRec.ParamType);

        var bodyResult = Check(letRec.FunctionBody, bodyEnvironment);
        if (bodyResult.IsFailure)
        {
            return bodyResult;
        }

        if (bodyResult.Value.Type != letRec.ReturnType)
        {
            return Result<TypedExpr>.Failure(Error.Create(
                $"'{letRec.Name}' declared to return {letRec.ReturnType} but its body is {bodyResult.Value.Type}."));
        }

        var letBodyResult = Check(letRec.LetBody, WithBinding(environment, letRec.Name, functionType));
        if (letBodyResult.IsFailure)
        {
            return letBodyResult;
        }

        return Result<TypedExpr>.Success(new TypedLetRecExpr(
            letRec.Name, letRec.ParamName, letRec.ParamType, bodyResult.Value, functionType, letBodyResult.Value, letBodyResult.Value.Type));
    }

    private static Result<TypedExpr> CheckBinaryOperands(BinaryOperator op, TypedExpr left, TypedExpr right)
    {
        if (left.Type == KlexirType.Int && right.Type == KlexirType.Int)
        {
            return Result<TypedExpr>.Success(new TypedBinaryExpr(op, left, right, KlexirType.Int));
        }

        if (op == BinaryOperator.Add && left.Type == KlexirType.String && right.Type == KlexirType.String)
        {
            return Result<TypedExpr>.Success(new TypedBinaryExpr(op, left, right, KlexirType.String));
        }

        return Result<TypedExpr>.Failure(Error.Create(
            $"Operator '{op}' requires Int operands (or String operands for '+'), got {left.Type} and {right.Type}."));
    }

    private static Result<TypedExpr> CheckComparisonOperands(ComparisonOperator op, TypedExpr left, TypedExpr right)
    {
        if (left.Type == KlexirType.Int && right.Type == KlexirType.Int)
        {
            return Result<TypedExpr>.Success(new TypedComparisonExpr(op, left, right));
        }

        if (op == ComparisonOperator.Equal && left.Type == right.Type
            && left.Type is IntType or BoolType or StringType)
        {
            return Result<TypedExpr>.Success(new TypedComparisonExpr(op, left, right));
        }

        return Result<TypedExpr>.Failure(Error.Create(
            $"Comparison '{op}' requires Int operands (or matching operands for '=='), got {left.Type} and {right.Type}."));
    }

    private static IReadOnlyDictionary<string, KlexirType> WithBinding(
        IReadOnlyDictionary<string, KlexirType> environment, string name, KlexirType type) =>
        new Dictionary<string, KlexirType>(environment) { [name] = type };
}
