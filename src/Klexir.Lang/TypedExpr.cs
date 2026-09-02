namespace Klexir.Lang;

/// <summary>A Klexir type. <see cref="Int"/>/<see cref="Bool"/> are the ground types; <see cref="FunctionType"/> composes them.</summary>
public abstract record KlexirType
{
    public static readonly KlexirType Int = new IntType();

    public static readonly KlexirType Bool = new BoolType();

    public static readonly KlexirType String = new StringType();
}

public sealed record IntType : KlexirType
{
    public override string ToString() => "Int";
}

public sealed record BoolType : KlexirType
{
    public override string ToString() => "Bool";
}

public sealed record StringType : KlexirType
{
    public override string ToString() => "String";
}

public sealed record FunctionType(KlexirType Parameter, KlexirType Return) : KlexirType
{
    public override string ToString() => $"{Parameter} -> {Return}";
}

public sealed record OptionType(KlexirType Element) : KlexirType
{
    public override string ToString() => $"Option<{Element}>";
}

public sealed record ResultType(KlexirType Ok, KlexirType Err) : KlexirType
{
    public override string ToString() => $"Result<{Ok}, {Err}>";
}

public sealed record ListType(KlexirType Element) : KlexirType
{
    public override string ToString() => $"List<{Element}>";
}

/// <summary>
/// A user-defined record type, nominal: two <see cref="RecordType"/>s are equal exactly when their
/// <see cref="Name"/>s match (Klexir has one record declaration per name, so this is exactly right — it also
/// lets an unresolved forward reference, an empty <see cref="Fields"/> placeholder from a type annotation parsed
/// before its <c>record</c> declaration is checked, compare equal to the real, fully-populated one).
/// </summary>
public sealed record RecordType(string Name, IReadOnlyList<(string FieldName, KlexirType FieldType)> Fields) : KlexirType
{
    public bool Equals(RecordType? other) => other is not null && Name == other.Name;

    public override int GetHashCode() => Name.GetHashCode();

    public override string ToString() => Name;
}

/// <summary>A user-defined union (sum) type. Nominal, same reasoning as <see cref="RecordType"/>.</summary>
public sealed record UnionType(string Name, IReadOnlyList<(string VariantName, IReadOnlyList<KlexirType> FieldTypes)> Variants) : KlexirType
{
    public bool Equals(UnionType? other) => other is not null && Name == other.Name;

    public override int GetHashCode() => Name.GetHashCode();

    public override string ToString() => Name;
}

public abstract record TypedExpr(KlexirType Type);

public sealed record TypedIntLiteral(long Value) : TypedExpr(KlexirType.Int);

public sealed record TypedStringLiteral(string Value) : TypedExpr(KlexirType.String);

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

public sealed record TypedSomeExpr(TypedExpr Value, KlexirType Type) : TypedExpr(Type);

public sealed record TypedNoneExpr(KlexirType Type) : TypedExpr(Type);

public sealed record TypedOkExpr(TypedExpr Value, KlexirType Type) : TypedExpr(Type);

public sealed record TypedErrExpr(TypedExpr Value, KlexirType Type) : TypedExpr(Type);

public sealed record TypedMatchOptionExpr(
    TypedExpr Scrutinee, string SomeBinder, TypedExpr SomeBody, TypedExpr NoneBody, KlexirType Type) : TypedExpr(Type);

public sealed record TypedMatchResultExpr(
    TypedExpr Scrutinee, string OkBinder, TypedExpr OkBody, string ErrBinder, TypedExpr ErrBody, KlexirType Type) : TypedExpr(Type);

public sealed record TypedMapExpr(TypedExpr Container, TypedExpr Mapper, KlexirType Type) : TypedExpr(Type);

public sealed record TypedBindExpr(TypedExpr Container, TypedExpr Mapper, KlexirType Type) : TypedExpr(Type);

public sealed record TypedListExpr(IReadOnlyList<TypedExpr> Elements, KlexirType Type) : TypedExpr(Type);

public sealed record TypedEmptyListExpr(KlexirType Type) : TypedExpr(Type);

public sealed record TypedFilterExpr(TypedExpr List, TypedExpr Predicate, KlexirType Type) : TypedExpr(Type);

public sealed record TypedFoldExpr(TypedExpr List, TypedExpr Initial, TypedExpr Folder, KlexirType Type) : TypedExpr(Type);

public sealed record TypedRecordConstructExpr(string TypeName, IReadOnlyList<(string FieldName, TypedExpr Value)> Fields, KlexirType Type) : TypedExpr(Type);

public sealed record TypedFieldAccessExpr(TypedExpr Receiver, string FieldName, KlexirType Type) : TypedExpr(Type);

public sealed record TypedUnionDeclExpr(IReadOnlyList<(string VariantName, int Arity)> Constructors, TypedExpr Body, KlexirType Type) : TypedExpr(Type);

public sealed record TypedMatchUnionExpr(
    TypedExpr Scrutinee, IReadOnlyList<(string VariantName, IReadOnlyList<string> Binders, TypedExpr Body)> Arms, KlexirType Type) : TypedExpr(Type);
