using MonadicSharp;

namespace Klexir.Lang;

/// <summary>
/// Tree-walking evaluator over a <see cref="TypedExpr"/> tree. Expects its input already passed
/// <see cref="TypeChecker"/> — operand-type guards below exist so a bug upstream fails the <see cref="Result{T}"/>
/// instead of throwing an <see cref="InvalidCastException"/>, not because this is meant to re-validate types.
/// Async throughout because a plugin's native function (see <see cref="IKlexirPlugin"/>) may do real I/O; the
/// synchronous <see cref="Evaluate(TypedExpr)"/> overload just blocks on it for plugin-free programs, where nothing
/// ever actually suspends.
/// </summary>
public sealed class Evaluator
{
    public Result<KlexirValue> Evaluate(TypedExpr expr) =>
        EvaluateAsync(expr, new Dictionary<string, KlexirValue>()).GetAwaiter().GetResult();

    public Task<Result<KlexirValue>> EvaluateAsync(TypedExpr expr) =>
        EvaluateAsync(expr, Array.Empty<IKlexirPlugin>());

    /// <summary>Applies an already-evaluated function value to an argument — the same application logic <c>f x</c>
    /// uses internally, exposed for a host that got a closure back from <see cref="Evaluate(TypedExpr)"/> (e.g. an
    /// HTTP request handler) and needs to call it again for each new argument without re-evaluating the program.</summary>
    public Task<Result<KlexirValue>> ApplyAsync(KlexirValue function, KlexirValue argument) =>
        ApplyClosureAsync(function, argument);

    /// <summary>Evaluates <paramref name="expr"/> with the given plugins' functions bound into the initial value
    /// environment — see <see cref="IKlexirPlugin"/>. <paramref name="expr"/> should have been type-checked against
    /// the same plugin list via <see cref="TypeChecker.Check(Expr, IReadOnlyList{IKlexirPlugin})"/>.</summary>
    public Task<Result<KlexirValue>> EvaluateAsync(TypedExpr expr, IReadOnlyList<IKlexirPlugin> plugins)
    {
        var environment = BuildPluginEnvironment(plugins);
        return environment.IsFailure
            ? Task.FromResult(Result<KlexirValue>.Failure(environment.Error))
            : EvaluateAsync(expr, environment.Value);
    }

    private static Result<IReadOnlyDictionary<string, KlexirValue>> BuildPluginEnvironment(IReadOnlyList<IKlexirPlugin> plugins)
    {
        var environment = new Dictionary<string, KlexirValue>();

        foreach (var plugin in plugins)
        {
            foreach (var function in plugin.Functions)
            {
                if (!environment.TryAdd(function.Name, new NativeFunctionValue(function, Array.Empty<KlexirValue>())))
                {
                    return Result<IReadOnlyDictionary<string, KlexirValue>>.Failure(
                        Error.Create($"Plugin '{plugin.Name}' declares function '{function.Name}', which is already bound."));
                }
            }
        }

        return Result<IReadOnlyDictionary<string, KlexirValue>>.Success(environment);
    }

    private static async Task<Result<KlexirValue>> EvaluateAsync(TypedExpr expr, IReadOnlyDictionary<string, KlexirValue> environment)
    {
        switch (expr)
        {
            case TypedIntLiteral literal:
                return Result<KlexirValue>.Success(new IntValue(literal.Value));

            case TypedStringLiteral literal:
                return Result<KlexirValue>.Success(new StringValue(literal.Value));

            case TypedBoolLiteral literal:
                return Result<KlexirValue>.Success(new BoolValue(literal.Value));

            case TypedIdentifier identifier:
                return environment.TryGetValue(identifier.Name, out var boundValue)
                    ? Result<KlexirValue>.Success(boundValue)
                    : Result<KlexirValue>.Failure(Error.Create($"Unbound identifier '{identifier.Name}' at evaluation time."));

            case TypedBinaryExpr binary:
            {
                var leftResult = await EvaluateAsync(binary.Left, environment);
                if (leftResult.IsFailure)
                {
                    return leftResult;
                }

                var rightResult = await EvaluateAsync(binary.Right, environment);
                return rightResult.IsFailure ? rightResult : ApplyBinary(binary.Operator, leftResult.Value, rightResult.Value);
            }

            case TypedComparisonExpr comparison:
            {
                var leftResult = await EvaluateAsync(comparison.Left, environment);
                if (leftResult.IsFailure)
                {
                    return leftResult;
                }

                var rightResult = await EvaluateAsync(comparison.Right, environment);
                return rightResult.IsFailure ? rightResult : ApplyComparison(comparison.Operator, leftResult.Value, rightResult.Value);
            }

            case TypedIfExpr ifExpr:
            {
                var conditionResult = await EvaluateAsync(ifExpr.Condition, environment);
                if (conditionResult.IsFailure)
                {
                    return conditionResult;
                }

                return conditionResult.Value switch
                {
                    BoolValue { Value: true } => await EvaluateAsync(ifExpr.Then, environment),
                    BoolValue { Value: false } => await EvaluateAsync(ifExpr.Else, environment),
                    _ => Result<KlexirValue>.Failure(Error.Create("If condition did not evaluate to Bool.")),
                };
            }

            case TypedLetExpr let:
            {
                var valueResult = await EvaluateAsync(let.Value, environment);
                return valueResult.IsFailure
                    ? valueResult
                    : await EvaluateAsync(let.Body, WithBinding(environment, let.Name, valueResult.Value));
            }

            case TypedFunExpr fun:
                return Result<KlexirValue>.Success(new ClosureValue(fun.ParamName, fun.Body, environment));

            case TypedAppExpr app:
            {
                var functionResult = await EvaluateAsync(app.Function, environment);
                if (functionResult.IsFailure)
                {
                    return functionResult;
                }

                var argumentResult = await EvaluateAsync(app.Argument, environment);
                return argumentResult.IsFailure
                    ? argumentResult
                    : await ApplyClosureAsync(functionResult.Value, argumentResult.Value);
            }

            case TypedLetRecExpr letRec:
                return await EvaluateLetRecAsync(letRec, environment);

            case TypedSomeExpr some:
            {
                var valueResult = await EvaluateAsync(some.Value, environment);
                return valueResult.IsFailure ? valueResult : Result<KlexirValue>.Success(new SomeValue(valueResult.Value));
            }

            case TypedNoneExpr:
                return Result<KlexirValue>.Success(new NoneValue());

            case TypedOkExpr ok:
            {
                var valueResult = await EvaluateAsync(ok.Value, environment);
                return valueResult.IsFailure ? valueResult : Result<KlexirValue>.Success(new OkValue(valueResult.Value));
            }

            case TypedErrExpr err:
            {
                var valueResult = await EvaluateAsync(err.Value, environment);
                return valueResult.IsFailure ? valueResult : Result<KlexirValue>.Success(new ErrValue(valueResult.Value));
            }

            case TypedMatchOptionExpr match:
            {
                var scrutineeResult = await EvaluateAsync(match.Scrutinee, environment);
                if (scrutineeResult.IsFailure)
                {
                    return scrutineeResult;
                }

                return scrutineeResult.Value switch
                {
                    SomeValue some => await EvaluateAsync(match.SomeBody, WithBinding(environment, match.SomeBinder, some.Value)),
                    NoneValue => await EvaluateAsync(match.NoneBody, environment),
                    _ => Result<KlexirValue>.Failure(Error.Create("Match scrutinee did not evaluate to an Option value.")),
                };
            }

            case TypedMatchResultExpr match:
            {
                var scrutineeResult = await EvaluateAsync(match.Scrutinee, environment);
                if (scrutineeResult.IsFailure)
                {
                    return scrutineeResult;
                }

                return scrutineeResult.Value switch
                {
                    OkValue ok => await EvaluateAsync(match.OkBody, WithBinding(environment, match.OkBinder, ok.Value)),
                    ErrValue err => await EvaluateAsync(match.ErrBody, WithBinding(environment, match.ErrBinder, err.Value)),
                    _ => Result<KlexirValue>.Failure(Error.Create("Match scrutinee did not evaluate to a Result value.")),
                };
            }

            case TypedMapExpr map:
            {
                var containerResult = await EvaluateAsync(map.Container, environment);
                if (containerResult.IsFailure)
                {
                    return containerResult;
                }

                var mapperResult = await EvaluateAsync(map.Mapper, environment);
                return mapperResult.IsFailure ? mapperResult : await ApplyMapAsync(containerResult.Value, mapperResult.Value);
            }

            case TypedBindExpr bind:
            {
                var containerResult = await EvaluateAsync(bind.Container, environment);
                if (containerResult.IsFailure)
                {
                    return containerResult;
                }

                var mapperResult = await EvaluateAsync(bind.Mapper, environment);
                return mapperResult.IsFailure ? mapperResult : await ApplyBindAsync(containerResult.Value, mapperResult.Value);
            }

            case TypedListExpr list:
                return await EvaluateListAsync(list.Elements, environment);

            case TypedEmptyListExpr:
                return Result<KlexirValue>.Success(new ListValue(Array.Empty<KlexirValue>()));

            case TypedFilterExpr filter:
            {
                var listResult = await EvaluateAsync(filter.List, environment);
                if (listResult.IsFailure)
                {
                    return listResult;
                }

                var predicateResult = await EvaluateAsync(filter.Predicate, environment);
                return predicateResult.IsFailure ? predicateResult : await ApplyFilterAsync(listResult.Value, predicateResult.Value);
            }

            case TypedFoldExpr fold:
            {
                var listResult = await EvaluateAsync(fold.List, environment);
                if (listResult.IsFailure)
                {
                    return listResult;
                }

                var initialResult = await EvaluateAsync(fold.Initial, environment);
                if (initialResult.IsFailure)
                {
                    return initialResult;
                }

                var folderResult = await EvaluateAsync(fold.Folder, environment);
                return folderResult.IsFailure
                    ? folderResult
                    : await ApplyFoldAsync(listResult.Value, initialResult.Value, folderResult.Value);
            }

            case TypedRecordConstructExpr construct:
                return await EvaluateRecordConstructAsync(construct, environment);

            case TypedFieldAccessExpr access:
            {
                var receiverResult = await EvaluateAsync(access.Receiver, environment);
                if (receiverResult.IsFailure)
                {
                    return receiverResult;
                }

                return receiverResult.Value is RecordValue record && record.Fields.TryGetValue(access.FieldName, out var fieldValue)
                    ? Result<KlexirValue>.Success(fieldValue)
                    : Result<KlexirValue>.Failure(Error.Create($"Attempted to access field '{access.FieldName}' on a non-record value."));
            }

            case TypedUnionDeclExpr decl:
                return await EvaluateUnionDeclAsync(decl, environment);

            case TypedMatchUnionExpr match:
            {
                var scrutineeResult = await EvaluateAsync(match.Scrutinee, environment);
                if (scrutineeResult.IsFailure)
                {
                    return scrutineeResult;
                }

                return scrutineeResult.Value is UnionValue union
                    ? await EvaluateMatchUnionArmAsync(match.Arms, union, environment)
                    : Result<KlexirValue>.Failure(Error.Create("Match scrutinee did not evaluate to a union value."));
            }

            default:
                return Result<KlexirValue>.Failure(Error.Create($"Unsupported typed node '{expr.GetType().Name}'."));
        }
    }

    private static async Task<Result<KlexirValue>> EvaluateUnionDeclAsync(TypedUnionDeclExpr decl, IReadOnlyDictionary<string, KlexirValue> environment)
    {
        var bodyEnvironment = environment;

        foreach (var (variantName, arity) in decl.Constructors)
        {
            KlexirValue constructorValue = arity == 0
                ? new UnionValue(variantName, Array.Empty<KlexirValue>())
                : new ConstructorValue(variantName, arity, Array.Empty<KlexirValue>());

            bodyEnvironment = WithBinding(bodyEnvironment, variantName, constructorValue);
        }

        return await EvaluateAsync(decl.Body, bodyEnvironment);
    }

    private static async Task<Result<KlexirValue>> EvaluateMatchUnionArmAsync(
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

            return await EvaluateAsync(body, armEnvironment);
        }

        return Result<KlexirValue>.Failure(Error.Create($"No match arm for variant '{union.VariantName}'."));
    }

    private static async Task<Result<KlexirValue>> EvaluateRecordConstructAsync(
        TypedRecordConstructExpr construct, IReadOnlyDictionary<string, KlexirValue> environment)
    {
        var fields = new Dictionary<string, KlexirValue>();

        foreach (var (fieldName, fieldExpr) in construct.Fields)
        {
            var result = await EvaluateAsync(fieldExpr, environment);
            if (result.IsFailure)
            {
                return result;
            }

            fields[fieldName] = result.Value;
        }

        return Result<KlexirValue>.Success(new RecordValue(construct.TypeName, fields));
    }

    private static async Task<Result<KlexirValue>> EvaluateListAsync(
        IReadOnlyList<TypedExpr> elements, IReadOnlyDictionary<string, KlexirValue> environment)
    {
        var values = new List<KlexirValue>(elements.Count);

        foreach (var element in elements)
        {
            var result = await EvaluateAsync(element, environment);
            if (result.IsFailure)
            {
                return result;
            }

            values.Add(result.Value);
        }

        return Result<KlexirValue>.Success(new ListValue(values));
    }

    private static async Task<Result<KlexirValue>> ApplyFilterAsync(KlexirValue container, KlexirValue predicate)
    {
        if (container is not ListValue list)
        {
            return Result<KlexirValue>.Failure(Error.Create("'filter' requires a List value."));
        }

        var kept = new List<KlexirValue>();

        foreach (var element in list.Elements)
        {
            var keptResult = await ApplyClosureAsync(predicate, element);
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

    private static async Task<Result<KlexirValue>> ApplyFoldAsync(KlexirValue container, KlexirValue initial, KlexirValue folder)
    {
        if (container is not ListValue list)
        {
            return Result<KlexirValue>.Failure(Error.Create("'fold' requires a List value."));
        }

        var accumulator = initial;

        foreach (var element in list.Elements)
        {
            var stepResult = await ApplyClosureAsync(folder, accumulator);
            if (stepResult.IsFailure)
            {
                return stepResult;
            }

            var finalResult = await ApplyClosureAsync(stepResult.Value, element);
            if (finalResult.IsFailure)
            {
                return finalResult;
            }

            accumulator = finalResult.Value;
        }

        return Result<KlexirValue>.Success(accumulator);
    }

    private static async Task<Result<KlexirValue>> ApplyClosureAsync(KlexirValue function, KlexirValue argument) =>
        function switch
        {
            ClosureValue closure => await EvaluateAsync(closure.Body, WithBinding(closure.Environment, closure.ParamName, argument)),
            ConstructorValue ctor => ApplyConstructor(ctor, argument),
            NativeFunctionValue native => await ApplyNativeAsync(native, argument),
            _ => Result<KlexirValue>.Failure(Error.Create("Attempted to apply a non-function value.")),
        };

    /// <summary>
    /// Applies one more argument to a plugin's native function, mirroring <see cref="ApplyConstructor"/>: once
    /// <c>AppliedArgs</c> reaches the declared <see cref="KlexirNativeFunctionDef.Arity"/>, awaits
    /// <see cref="KlexirNativeFunctionDef.Invoke"/> — catching any exception it throws and turning it into a failed
    /// <see cref="Result{T}"/>, since Klexir never lets a native call's exception escape as a thrown exception.
    /// </summary>
    private static async Task<Result<KlexirValue>> ApplyNativeAsync(NativeFunctionValue native, KlexirValue argument)
    {
        var appliedArgs = new List<KlexirValue>(native.AppliedArgs) { argument };

        if (appliedArgs.Count < native.Def.Arity)
        {
            return Result<KlexirValue>.Success(new NativeFunctionValue(native.Def, appliedArgs));
        }

        try
        {
            return await native.Def.Invoke(appliedArgs);
        }
        catch (Exception ex)
        {
            return Result<KlexirValue>.Failure(Error.Create($"Native function '{native.Def.Name}' failed: {ex.Message}"));
        }
    }

    private static Result<KlexirValue> ApplyConstructor(ConstructorValue ctor, KlexirValue argument)
    {
        var appliedArgs = new List<KlexirValue>(ctor.AppliedArgs) { argument };

        return Result<KlexirValue>.Success(appliedArgs.Count == ctor.Arity
            ? new UnionValue(ctor.VariantName, appliedArgs)
            : new ConstructorValue(ctor.VariantName, ctor.Arity, appliedArgs));
    }

    /// <summary>The Functor operation: transforms the value inside Some/Ok, leaves None/Err untouched.</summary>
    private static async Task<Result<KlexirValue>> ApplyMapAsync(KlexirValue container, KlexirValue mapper)
    {
        switch (container)
        {
            case SomeValue some:
            {
                var result = await ApplyClosureAsync(mapper, some.Value);
                return result.IsFailure ? result : Result<KlexirValue>.Success(new SomeValue(result.Value));
            }

            case NoneValue:
                return Result<KlexirValue>.Success(container);

            case OkValue ok:
            {
                var result = await ApplyClosureAsync(mapper, ok.Value);
                return result.IsFailure ? result : Result<KlexirValue>.Success(new OkValue(result.Value));
            }

            case ErrValue:
                return Result<KlexirValue>.Success(container);

            case ListValue list:
                return await ApplyMapListAsync(list, mapper);

            default:
                return Result<KlexirValue>.Failure(Error.Create("'map' requires an Option, Result, or List value."));
        }
    }

    private static async Task<Result<KlexirValue>> ApplyMapListAsync(ListValue list, KlexirValue mapper)
    {
        var mapped = new List<KlexirValue>(list.Elements.Count);

        foreach (var element in list.Elements)
        {
            var result = await ApplyClosureAsync(mapper, element);
            if (result.IsFailure)
            {
                return result;
            }

            mapped.Add(result.Value);
        }

        return Result<KlexirValue>.Success(new ListValue(mapped));
    }

    /// <summary>The Monad operation: chains a container-returning function, short-circuiting on None/Err.</summary>
    private static async Task<Result<KlexirValue>> ApplyBindAsync(KlexirValue container, KlexirValue mapper) =>
        container switch
        {
            SomeValue some => await ApplyClosureAsync(mapper, some.Value),
            NoneValue => Result<KlexirValue>.Success(container),
            OkValue ok => await ApplyClosureAsync(mapper, ok.Value),
            ErrValue => Result<KlexirValue>.Success(container),
            _ => Result<KlexirValue>.Failure(Error.Create("'bind' requires an Option or Result value.")),
        };

    /// <summary>
    /// Ties the knot: builds the closure's captured environment as a mutable dictionary, then adds the closure's
    /// own name to that same dictionary after the closure exists — so when the closure is later called, its
    /// captured environment already contains itself, and it can recurse.
    /// </summary>
    private static Task<Result<KlexirValue>> EvaluateLetRecAsync(TypedLetRecExpr letRec, IReadOnlyDictionary<string, KlexirValue> environment)
    {
        var recursiveEnvironment = new Dictionary<string, KlexirValue>(environment);
        var closure = new ClosureValue(letRec.ParamName, letRec.FunctionBody, recursiveEnvironment);
        recursiveEnvironment[letRec.Name] = closure;

        return EvaluateAsync(letRec.LetBody, WithBinding(environment, letRec.Name, closure));
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
