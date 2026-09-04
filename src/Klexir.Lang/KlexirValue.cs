namespace Klexir.Lang;

public abstract record KlexirValue;

public sealed record IntValue(long Value) : KlexirValue;

public sealed record StringValue(string Value) : KlexirValue;

public sealed record BoolValue(bool Value) : KlexirValue;

/// <summary>A function value: its parameter, its (unevaluated) body, and the environment captured at the point it was created.</summary>
public sealed record ClosureValue(string ParamName, TypedExpr Body, IReadOnlyDictionary<string, KlexirValue> Environment) : KlexirValue
{
    // Records auto-generate structural equality over all members, including the environment dictionary —
    // two closures are almost never meant to compare equal by captured state, and a Dictionary doesn't have
    // value equality anyway (it would fall back to reference equality, silently). Compare by identity instead.
    public bool Equals(ClosureValue? other) => ReferenceEquals(this, other);

    public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
}

public sealed record SomeValue(KlexirValue Value) : KlexirValue;

public sealed record NoneValue : KlexirValue;

public sealed record OkValue(KlexirValue Value) : KlexirValue;

public sealed record ErrValue(KlexirValue Value) : KlexirValue;

/// <summary>A list of values. Overrides equality to compare elements in order — the default record equality would
/// compare <see cref="IReadOnlyList{T}"/> by reference, like <see cref="Dictionary{TKey,TValue}"/> does for
/// <see cref="ClosureValue"/> — except here structural, element-wise equality is exactly what callers expect.</summary>
public sealed record ListValue(IReadOnlyList<KlexirValue> Elements) : KlexirValue
{
    public bool Equals(ListValue? other) => other is not null && Elements.SequenceEqual(other.Elements);

    public override int GetHashCode() => Elements.Aggregate(17, (hash, element) => hash * 31 + element.GetHashCode());
}

/// <summary>A record value: its declared type name plus its field values by name. Overrides equality to compare
/// fields by content (see <see cref="ListValue"/> for why the default record equality wouldn't do that).</summary>
public sealed record RecordValue(string TypeName, IReadOnlyDictionary<string, KlexirValue> Fields) : KlexirValue
{
    public bool Equals(RecordValue? other) =>
        other is not null && TypeName == other.TypeName && Fields.Count == other.Fields.Count
        && Fields.All(field => other.Fields.TryGetValue(field.Key, out var value) && field.Value.Equals(value));

    public override int GetHashCode() =>
        Fields.Aggregate(TypeName.GetHashCode(), (hash, field) => hash ^ (field.Key.GetHashCode() * 31 + field.Value.GetHashCode()));
}

/// <summary>A union value: which variant, plus its positional field values.</summary>
public sealed record UnionValue(string VariantName, IReadOnlyList<KlexirValue> Fields) : KlexirValue
{
    public bool Equals(UnionValue? other) =>
        other is not null && VariantName == other.VariantName && Fields.SequenceEqual(other.Fields);

    public override int GetHashCode() =>
        Fields.Aggregate(VariantName.GetHashCode(), (hash, field) => hash * 31 + field.GetHashCode());
}

/// <summary>
/// A union constructor mid-currying: <c>Arity</c> fields declared, <c>AppliedArgs</c> supplied so far. Applying
/// it (see <see cref="Evaluator"/>'s <c>ApplyClosure</c>) either yields another <see cref="ConstructorValue"/> with
/// one more argument, or — once <c>AppliedArgs</c> reaches <c>Arity</c> — the finished <see cref="UnionValue"/>.
/// </summary>
public sealed record ConstructorValue(string VariantName, int Arity, IReadOnlyList<KlexirValue> AppliedArgs) : KlexirValue;

/// <summary>Wraps an arbitrary native .NET object behind a plugin's <see cref="OpaqueType"/> — Klexir code can hold,
/// pass, and return it, but only a plugin's own functions can look inside <see cref="Payload"/>.</summary>
public sealed record NativeValue(object Payload, OpaqueType Type) : KlexirValue;

/// <summary>
/// A plugin's native function mid-currying, mirroring <see cref="ConstructorValue"/>: <c>Def.Arity</c> arguments
/// declared, <c>AppliedArgs</c> supplied so far. Applying it (see <see cref="Evaluator"/>'s <c>ApplyClosureAsync</c>)
/// either yields another <see cref="NativeFunctionValue"/> with one more argument, or — once <c>AppliedArgs</c>
/// reaches <c>Def.Arity</c> — awaits <see cref="KlexirNativeFunctionDef.Invoke"/> with the full argument list.
/// </summary>
public sealed record NativeFunctionValue(KlexirNativeFunctionDef Def, IReadOnlyList<KlexirValue> AppliedArgs) : KlexirValue;
