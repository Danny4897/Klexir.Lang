namespace Klexir.Lang;

public enum KlexirType
{
    Int,
}

public abstract record TypedExpr(KlexirType Type);

public sealed record TypedIntLiteral(long Value) : TypedExpr(KlexirType.Int);

public sealed record TypedIdentifier(string Name, KlexirType DeclaredType) : TypedExpr(DeclaredType);

public sealed record TypedBinaryExpr(BinaryOperator Operator, TypedExpr Left, TypedExpr Right, KlexirType Type) : TypedExpr(Type);

public sealed record TypedLetExpr(string Name, TypedExpr Value, TypedExpr Body, KlexirType Type) : TypedExpr(Type);
