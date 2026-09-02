using MonadicSharp;

namespace Klexir.Lang;

/// <summary>
/// Tree-walking evaluator over a <see cref="TypedExpr"/> tree. Expects its input already passed
/// <see cref="TypeChecker"/> — operand-type guards below exist so a bug upstream fails the <see cref="Result{T}"/>
/// instead of throwing an <see cref="InvalidCastException"/>, not because this is meant to re-validate types.
/// </summary>
public sealed class Evaluator
{
    public Result<KlexirValue> Evaluate(TypedExpr expr) => Evaluate(expr, new Dictionary<string, KlexirValue>());

    private static Result<KlexirValue> Evaluate(TypedExpr expr, IReadOnlyDictionary<string, KlexirValue> environment) =>
        expr switch
        {
            TypedIntLiteral literal => Result<KlexirValue>.Success(new IntValue(literal.Value)),

            TypedStringLiteral literal => Result<KlexirValue>.Success(new StringValue(literal.Value)),

            TypedBoolLiteral literal => Result<KlexirValue>.Success(new BoolValue(literal.Value)),

            TypedIdentifier identifier => environment.TryGetValue(identifier.Name, out var value)
                ? Result<KlexirValue>.Success(value)
                : Result<KlexirValue>.Failure(Error.Create($"Unbound identifier '{identifier.Name}' at evaluation time.")),

            TypedBinaryExpr binary => Evaluate(binary.Left, environment)
                .Bind(left => Evaluate(binary.Right, environment)
                    .Bind(right => ApplyBinary(binary.Operator, left, right))),

            TypedComparisonExpr comparison => Evaluate(comparison.Left, environment)
                .Bind(left => Evaluate(comparison.Right, environment)
                    .Bind(right => ApplyComparison(comparison.Operator, left, right))),

            TypedIfExpr ifExpr => Evaluate(ifExpr.Condition, environment)
                .Bind(condition => condition is BoolValue { Value: true }
                    ? Evaluate(ifExpr.Then, environment)
                    : condition is BoolValue { Value: false }
                        ? Evaluate(ifExpr.Else, environment)
                        : Result<KlexirValue>.Failure(Error.Create("If condition did not evaluate to Bool."))),

            TypedLetExpr let => Evaluate(let.Value, environment)
                .Bind(value => Evaluate(let.Body, WithBinding(environment, let.Name, value))),

            TypedFunExpr fun => Result<KlexirValue>.Success(new ClosureValue(fun.ParamName, fun.Body, environment)),

            TypedAppExpr app => Evaluate(app.Function, environment)
                .Bind(function => Evaluate(app.Argument, environment)
                    .Bind(argument => ApplyClosure(function, argument))),

            TypedLetRecExpr letRec => EvaluateLetRec(letRec, environment),

            TypedSomeExpr some => Evaluate(some.Value, environment)
                .Bind(value => Result<KlexirValue>.Success(new SomeValue(value))),

            TypedNoneExpr => Result<KlexirValue>.Success(new NoneValue()),

            TypedOkExpr ok => Evaluate(ok.Value, environment)
                .Bind(value => Result<KlexirValue>.Success(new OkValue(value))),

            TypedErrExpr err => Evaluate(err.Value, environment)
                .Bind(value => Result<KlexirValue>.Success(new ErrValue(value))),

            TypedMatchOptionExpr match => Evaluate(match.Scrutinee, environment)
                .Bind(scrutinee => scrutinee switch
                {
                    SomeValue some => Evaluate(match.SomeBody, WithBinding(environment, match.SomeBinder, some.Value)),
                    NoneValue => Evaluate(match.NoneBody, environment),
                    _ => Result<KlexirValue>.Failure(Error.Create("Match scrutinee did not evaluate to an Option value.")),
                }),

            TypedMatchResultExpr match => Evaluate(match.Scrutinee, environment)
                .Bind(scrutinee => scrutinee switch
                {
                    OkValue ok => Evaluate(match.OkBody, WithBinding(environment, match.OkBinder, ok.Value)),
                    ErrValue err => Evaluate(match.ErrBody, WithBinding(environment, match.ErrBinder, err.Value)),
                    _ => Result<KlexirValue>.Failure(Error.Create("Match scrutinee did not evaluate to a Result value.")),
                }),

            TypedMapExpr map => Evaluate(map.Container, environment)
                .Bind(container => Evaluate(map.Mapper, environment)
                    .Bind(mapper => ApplyMap(container, mapper))),

            TypedBindExpr bind => Evaluate(bind.Container, environment)
                .Bind(container => Evaluate(bind.Mapper, environment)
                    .Bind(mapper => ApplyBind(container, mapper))),

            TypedListExpr list => EvaluateList(list.Elements, environment),

            TypedEmptyListExpr => Result<KlexirValue>.Success(new ListValue(Array.Empty<KlexirValue>())),

            TypedFilterExpr filter => Evaluate(filter.List, environment)
                .Bind(list => Evaluate(filter.Predicate, environment)
                    .Bind(predicate => ApplyFilter(list, predicate))),

            TypedFoldExpr fold => Evaluate(fold.List, environment)
                .Bind(list => Evaluate(fold.Initial, environment)
                    .Bind(initial => Evaluate(fold.Folder, environment)
                        .Bind(folder => ApplyFold(list, initial, folder)))),

            TypedRecordConstructExpr construct => EvaluateRecordConstruct(construct, environment),

            TypedFieldAccessExpr access => Evaluate(access.Receiver, environment)
                .Bind(receiver => receiver is RecordValue record && record.Fields.TryGetValue(access.FieldName, out var value)
                    ? Result<KlexirValue>.Success(value)
                    : Result<KlexirValue>.Failure(Error.Create($"Attempted to access field '{access.FieldName}' on a non-record value."))),

            TypedUnionDeclExpr decl => EvaluateUnionDecl(decl, environment),

            TypedMatchUnionExpr match => Evaluate(match.Scrutinee, environment)
                .Bind(scrutinee => scrutinee is UnionValue union
                    ? EvaluateMatchUnionArm(match.Arms, union, environment)
                    : Result<KlexirValue>.Failure(Error.Create("Match scrutinee did not evaluate to a union value."))),

            _ => Result<KlexirValue>.Failure(Error.Create($"Unsupported typed node '{expr.GetType().Name}'.")),
        };

    private static Result<KlexirValue> EvaluateUnionDecl(TypedUnionDeclExpr decl, IReadOnlyDictionary<string, KlexirValue> environment)
    {
        var bodyEnvironment = environment;

        foreach (var (variantName, arity) in decl.Constructors)
        {
            KlexirValue constructorValue = arity == 0
                ? new UnionValue(variantName, [])
                : new ConstructorValue(variantName, arity, []);

            bodyEnvironment = WithBinding(bodyEnvironment, variantName, constructorValue);
        }

        return Evaluate(decl.Body, bodyEnvironment);
    }

    private static Result<KlexirValue> EvaluateMatchUnionArm(
        IReadOnlyList<(string VariantName, IReadOnlyList<string> Binders, TypedExpr Body)> arms,
        UnionValue union, IReadOnlyDictionary<string, KlexirValue> environment)
    {
        foreach (var (variantName, binders, body) in arms)
        {
            if (variantName != union.VariantName)
            {
                continue;
            }

            var armEnvironment = environment;
            for (var i = 0; i < binders.Count; i++)
            {
                armEnvironment = WithBinding(armEnvironment, binders[i], union.Fields[i]);
            }

            return Evaluate(body, armEnvironment);
        }

        return Result<KlexirValue>.Failure(Error.Create($"No match arm for variant '{union.VariantName}'."));
    }

    private static Result<KlexirValue> EvaluateRecordConstruct(
        TypedRecordConstructExpr construct, IReadOnlyDictionary<string, KlexirValue> environment)
    {
        var fields = new Dictionary<string, KlexirValue>();

        foreach (var (fieldName, fieldExpr) in construct.Fields)
        {
            var result = Evaluate(fieldExpr, environment);
            if (result.IsFailure)
            {
                return result;
            }

            fields[fieldName] = result.Value;
        }

        return Result<KlexirValue>.Success(new RecordValue(construct.TypeName, fields));
    }

    private static Result<KlexirValue> EvaluateList(
        IReadOnlyList<TypedExpr> elements, IReadOnlyDictionary<string, KlexirValue> environment)
    {
        var values = new List<KlexirValue>(elements.Count);

        foreach (var element in elements)
        {
            var result = Evaluate(element, environment);
            if (result.IsFailure)
            {
                return result;
            }

            values.Add(result.Value);
        }

        return Result<KlexirValue>.Success(new ListValue(values));
    }

    private static Result<KlexirValue> ApplyFilter(KlexirValue container, KlexirValue predicate)
    {
        if (container is not ListValue list)
        {
            return Result<KlexirValue>.Failure(Error.Create("'filter' requires a List value."));
        }

        var kept = new List<KlexirValue>();

        foreach (var element in list.Elements)
        {
            var keptResult = ApplyClosure(predicate, element);
            if (keptResult.IsFailure)
            {
                return keptResult;
            }

            if (keptResult.Value is not BoolValue boolValue)
            {
                return Result<KlexirValue>.Failure(Error.Create("'filter' predicate did not evaluate to Bool."));
            }

            if (boolValue.Value)
            {
                kept.Add(element);
            }
        }

        return Result<KlexirValue>.Success(new ListValue(kept));
    }

    private static Result<KlexirValue> ApplyFold(KlexirValue container, KlexirValue initial, KlexirValue folder)
    {
        if (container is not ListValue list)
        {
            return Result<KlexirValue>.Failure(Error.Create("'fold' requires a List value."));
        }

        var accumulator = initial;

        foreach (var element in list.Elements)
        {
            var stepped = ApplyClosure(folder, accumulator).Bind(step => ApplyClosure(step, element));
            if (stepped.IsFailure)
            {
                return stepped;
            }

            accumulator = stepped.Value;
        }

        return Result<KlexirValue>.Success(accumulator);
    }

    private static Result<KlexirValue> ApplyClosure(KlexirValue function, KlexirValue argument) =>
        function switch
        {
            ClosureValue closure => Evaluate(closure.Body, WithBinding(closure.Environment, closure.ParamName, argument)),
            ConstructorValue ctor => ApplyConstructor(ctor, argument),
            _ => Result<KlexirValue>.Failure(Error.Create("Attempted to apply a non-function value.")),
        };

    private static Result<KlexirValue> ApplyConstructor(ConstructorValue ctor, KlexirValue argument)
    {
        var appliedArgs = new List<KlexirValue>(ctor.AppliedArgs) { argument };

        return Result<KlexirValue>.Success(appliedArgs.Count == ctor.Arity
            ? new UnionValue(ctor.VariantName, appliedArgs)
            : new ConstructorValue(ctor.VariantName, ctor.Arity, appliedArgs));
    }

    /// <summary>The Functor operation: transforms the value inside Some/Ok, leaves None/Err untouched.</summary>
    private static Result<KlexirValue> ApplyMap(KlexirValue container, KlexirValue mapper) =>
        container switch
        {
            SomeValue some => ApplyClosure(mapper, some.Value).Bind(value => Result<KlexirValue>.Success(new SomeValue(value))),
            NoneValue => Result<KlexirValue>.Success(container),
            OkValue ok => ApplyClosure(mapper, ok.Value).Bind(value => Result<KlexirValue>.Success(new OkValue(value))),
            ErrValue => Result<KlexirValue>.Success(container),
            ListValue list => ApplyMapList(list, mapper),
            _ => Result<KlexirValue>.Failure(Error.Create("'map' requires an Option, Result, or List value.")),
        };

    private static Result<KlexirValue> ApplyMapList(ListValue list, KlexirValue mapper)
    {
        var mapped = new List<KlexirValue>(list.Elements.Count);

        foreach (var element in list.Elements)
        {
            var result = ApplyClosure(mapper, element);
            if (result.IsFailure)
            {
                return result;
            }

            mapped.Add(result.Value);
        }

        return Result<KlexirValue>.Success(new ListValue(mapped));
    }

    /// <summary>The Monad operation: chains a container-returning function, short-circuiting on None/Err.</summary>
    private static Result<KlexirValue> ApplyBind(KlexirValue container, KlexirValue mapper) =>
        container switch
        {
            SomeValue some => ApplyClosure(mapper, some.Value),
            NoneValue => Result<KlexirValue>.Success(container),
            OkValue ok => ApplyClosure(mapper, ok.Value),
            ErrValue => Result<KlexirValue>.Success(container),
            _ => Result<KlexirValue>.Failure(Error.Create("'bind' requires an Option or Result value.")),
        };

    /// <summary>
    /// Ties the knot: builds the closure's captured environment as a mutable dictionary, then adds the closure's
    /// own name to that same dictionary after the closure exists — so when the closure is later called, its
    /// captured environment already contains itself, and it can recurse.
    /// </summary>
    private static Result<KlexirValue> EvaluateLetRec(TypedLetRecExpr letRec, IReadOnlyDictionary<string, KlexirValue> environment)
    {
        var recursiveEnvironment = new Dictionary<string, KlexirValue>(environment);
        var closure = new ClosureValue(letRec.ParamName, letRec.FunctionBody, recursiveEnvironment);
        recursiveEnvironment[letRec.Name] = closure;

        return Evaluate(letRec.LetBody, WithBinding(environment, letRec.Name, closure));
    }

    private static Result<KlexirValue> ApplyBinary(BinaryOperator op, KlexirValue left, KlexirValue right)
    {
        if (left is IntValue l && right is IntValue r)
        {
            if (op == BinaryOperator.Div && r.Value == 0)
            {
                return Result<KlexirValue>.Failure(Error.Create("Division by zero."));
            }

            return Result<KlexirValue>.Success(new IntValue(op switch
            {
                BinaryOperator.Add => l.Value + r.Value,
                BinaryOperator.Sub => l.Value - r.Value,
                BinaryOperator.Mul => l.Value * r.Value,
                BinaryOperator.Div => l.Value / r.Value,
                _ => throw new ArgumentOutOfRangeException(nameof(op), op, "Not a binary operator."),
            }));
        }

        if (op == BinaryOperator.Add && left is StringValue ls && right is StringValue rs)
        {
            return Result<KlexirValue>.Success(new StringValue(ls.Value + rs.Value));
        }

        return Result<KlexirValue>.Failure(Error.Create(
            $"Operator '{op}' requires Int operands (or String operands for '+') at evaluation time."));
    }

    private static Result<KlexirValue> ApplyComparison(ComparisonOperator op, KlexirValue left, KlexirValue right)
    {
        if (left is IntValue l && right is IntValue r)
        {
            return Result<KlexirValue>.Success(new BoolValue(op switch
            {
                ComparisonOperator.Equal => l.Value == r.Value,
                ComparisonOperator.LessThan => l.Value < r.Value,
                ComparisonOperator.GreaterThan => l.Value > r.Value,
                ComparisonOperator.LessThanOrEqual => l.Value <= r.Value,
                ComparisonOperator.GreaterThanOrEqual => l.Value >= r.Value,
                _ => throw new ArgumentOutOfRangeException(nameof(op), op, "Not a comparison operator."),
            }));
        }

        if (op == ComparisonOperator.Equal)
        {
            return Result<KlexirValue>.Success(new BoolValue(left.Equals(right)));
        }

        return Result<KlexirValue>.Failure(Error.Create($"Comparison '{op}' requires Int operands at evaluation time."));
    }

    private static IReadOnlyDictionary<string, KlexirValue> WithBinding(
        IReadOnlyDictionary<string, KlexirValue> environment, string name, KlexirValue value) =>
        new Dictionary<string, KlexirValue>(environment) { [name] = value };
}
