namespace Klexir.Lang;

/// <summary>A Klexir type. <see cref="Int"/>/<see cref="Bool"/> are the ground types; <see cref="FunctionType"/> composes them.</summary>
public abstract record KlexirType
{
    public static readonly KlexirType Int = new IntType();

    public static readonly KlexirType Bool = new BoolType();
}

public sealed record IntType : KlexirType
{
    public override string ToString() => "Int";
}

public sealed record BoolType : KlexirType
{
    public override string ToString() => "Bool";
}

public sealed record FunctionType(KlexirType Parameter, KlexirType Return) : KlexirType
{
    public override string ToString() => $"{Parameter} -> {Return}";
}

public abstract record TypedExpr(KlexirType Type);

public sealed record TypedIntLiteral(long Value) : TypedExpr(KlexirType.Int);

public sealed record TypedBoolLiteral(bool Value) : TypedExpr(KlexirType.Bool);

public sealed record TypedIdentifier(string Name, KlexirType DeclaredType) : TypedExpr(DeclaredType);

public sealed record TypedBinaryExpr(BinaryOperator Operator, TypedExpr Left, TypedExpr Right, KlexirType Type) : TypedExpr(Type);

public sealed record TypedLetExpr(string Name, TypedExpr Value, TypedExpr Body, KlexirType Type) : TypedExpr(Type);

public sealed record TypedComparisonExpr(ComparisonOperator Operator, TypedExpr Left, TypedExpr Right) : TypedExpr(KlexirType.Bool);

public sealed record TypedIfExpr(TypedExpr Condition, TypedExpr Then, TypedExpr Else, KlexirType Type) : TypedExpr(Type);

public sealed record TypedFunExpr(string ParamName, KlexirType ParamType, TypedExpr Body, KlexirType Type) : TypedExpr(Type);

public sealed record TypedAppExpr(TypedExpr Function, TypedExpr Argument, KlexirType Type) : TypedExpr(Type);

public sealed record TypedLetRecExpr(
    string Name, string ParamName, KlexirType ParamType, TypedExpr FunctionBody, KlexirType FunctionType, TypedExpr LetBody, KlexirType Type) : TypedExpr(Type);
