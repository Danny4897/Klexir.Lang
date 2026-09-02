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
                .Bind(function => function is ClosureValue closure
                    ? Evaluate(app.Argument, environment)
                        .Bind(argument => Evaluate(closure.Body, WithBinding(closure.Environment, closure.ParamName, argument)))
                    : Result<KlexirValue>.Failure(Error.Create("Attempted to apply a non-function value."))),

            _ => Result<KlexirValue>.Failure(Error.Create($"Unsupported typed node '{expr.GetType().Name}'.")),
        };

    private static Result<KlexirValue> ApplyBinary(BinaryOperator op, KlexirValue left, KlexirValue right)
    {
        if (left is not IntValue l || right is not IntValue r)
        {
            return Result<KlexirValue>.Failure(Error.Create($"Operator '{op}' requires Int operands at evaluation time."));
        }

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

    private static Result<KlexirValue> ApplyComparison(ComparisonOperator op, KlexirValue left, KlexirValue right)
    {
        if (left is not IntValue l || right is not IntValue r)
        {
            return Result<KlexirValue>.Failure(Error.Create($"Comparison '{op}' requires Int operands at evaluation time."));
        }

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

    private static IReadOnlyDictionary<string, KlexirValue> WithBinding(
        IReadOnlyDictionary<string, KlexirValue> environment, string name, KlexirValue value) =>
        new Dictionary<string, KlexirValue>(environment) { [name] = value };
}
