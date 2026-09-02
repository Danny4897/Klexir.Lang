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

            _ => Result<TypedExpr>.Failure(Error.Create($"Unsupported expression node '{expr.GetType().Name}'.")),
        };

    private static IReadOnlyDictionary<string, KlexirType> WithBinding(
        IReadOnlyDictionary<string, KlexirType> environment, string name, KlexirType type) =>
        new Dictionary<string, KlexirType>(environment) { [name] = type };
}
