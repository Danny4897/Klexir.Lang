namespace Klexir.Lang;

public enum KlexirType
{
    Int,
    Bool,
}

public abstract record TypedExpr(KlexirType Type);

public sealed record TypedIntLiteral(long Value) : TypedExpr(KlexirType.Int);

public sealed record TypedBoolLiteral(bool Value) : TypedExpr(KlexirType.Bool);

public sealed record TypedIdentifier(string Name, KlexirType DeclaredType) : TypedExpr(DeclaredType);

public sealed record TypedBinaryExpr(BinaryOperator Operator, TypedExpr Left, TypedExpr Right, KlexirType Type) : TypedExpr(Type);

public sealed record TypedLetExpr(string Name, TypedExpr Value, TypedExpr Body, KlexirType Type) : TypedExpr(Type);

public sealed record TypedComparisonExpr(ComparisonOperator Operator, TypedExpr Left, TypedExpr Right) : TypedExpr(KlexirType.Bool);

public sealed record TypedIfExpr(TypedExpr Condition, TypedExpr Then, TypedExpr Else, KlexirType Type) : TypedExpr(Type);
