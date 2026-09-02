using MonadicSharp;

namespace Klexir.Lang;

/// <summary>
/// Structural type checker: every literal/operator is Int today (the only type the language has), so the one
/// real check is that every identifier is bound by an enclosing `let` before it's used. Richer type mismatches
/// become checkable once the language grows a second type.
/// </summary>
public sealed class TypeChecker
{
    public Result<TypedExpr> Check(Expr expr) => Check(expr, new Dictionary<string, KlexirType>());

    private static Result<TypedExpr> Check(Expr expr, IReadOnlyDictionary<string, KlexirType> environment) =>
        expr switch
        {
            IntLiteral literal => Result<TypedExpr>.Success(new TypedIntLiteral(literal.Value)),

            Identifier identifier => environment.TryGetValue(identifier.Name, out var declaredType)
                ? Result<TypedExpr>.Success(new TypedIdentifier(identifier.Name, declaredType))
                : Result<TypedExpr>.Failure(Error.Create($"Unbound identifier '{identifier.Name}'.")),

            BinaryExpr binary => Check(binary.Left, environment)
                .Bind(left => Check(binary.Right, environment)
                    .Bind(right => Result<TypedExpr>.Success(new TypedBinaryExpr(binary.Operator, left, right, KlexirType.Int)))),

            LetExpr let => Check(let.Value, environment)
                .Bind(value => Check(let.Body, WithBinding(environment, let.Name, value.Type))
                    .Bind(body => Result<TypedExpr>.Success(new TypedLetExpr(let.Name, value, body, body.Type)))),

            _ => Result<TypedExpr>.Failure(Error.Create($"Unsupported expression node '{expr.GetType().Name}'.")),
        };

    private static IReadOnlyDictionary<string, KlexirType> WithBinding(
        IReadOnlyDictionary<string, KlexirType> environment, string name, KlexirType type) =>
        new Dictionary<string, KlexirType>(environment) { [name] = type };
}
