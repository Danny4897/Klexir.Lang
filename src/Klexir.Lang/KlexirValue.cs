namespace Klexir.Lang;

public abstract record KlexirValue;

public sealed record IntValue(long Value) : KlexirValue;

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
