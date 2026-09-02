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
