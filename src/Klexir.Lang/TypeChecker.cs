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

            BoolLiteral literal => Result<TypedExpr>.Success(new TypedBoolLiteral(literal.Value)),

            Identifier identifier => environment.TryGetValue(identifier.Name, out var declaredType)
                ? Result<TypedExpr>.Success(new TypedIdentifier(identifier.Name, declaredType))
                : Result<TypedExpr>.Failure(Error.Create($"Unbound identifier '{identifier.Name}'.")),

            BinaryExpr binary => Check(binary.Left, environment)
                .Bind(left => Check(binary.Right, environment)
                    .Bind(right => left.Type == KlexirType.Int && right.Type == KlexirType.Int
                        ? Result<TypedExpr>.Success(new TypedBinaryExpr(binary.Operator, left, right, KlexirType.Int))
                        : Result<TypedExpr>.Failure(Error.Create(
                            $"Operator '{binary.Operator}' requires Int operands, got {left.Type} and {right.Type}.")))),

            ComparisonExpr comparison => Check(comparison.Left, environment)
                .Bind(left => Check(comparison.Right, environment)
                    .Bind(right => left.Type == KlexirType.Int && right.Type == KlexirType.Int
                        ? Result<TypedExpr>.Success(new TypedComparisonExpr(comparison.Operator, left, right))
                        : Result<TypedExpr>.Failure(Error.Create(
                            $"Comparison '{comparison.Operator}' requires Int operands, got {left.Type} and {right.Type}.")))),

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

            _ => Result<TypedExpr>.Failure(Error.Create($"Unsupported expression node '{expr.GetType().Name}'.")),
        };

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

    private static IReadOnlyDictionary<string, KlexirType> WithBinding(
        IReadOnlyDictionary<string, KlexirType> environment, string name, KlexirType type) =>
        new Dictionary<string, KlexirType>(environment) { [name] = type };
}
