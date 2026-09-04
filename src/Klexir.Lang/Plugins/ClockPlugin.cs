using MonadicSharp;

namespace Klexir.Lang.Plugins;

/// <summary>
/// Reference <see cref="IKlexirPlugin"/> implementation — exists to exercise the plugin mechanism end to end
/// (a real async native call, real curried arity) without depending on unfinished work elsewhere in the ecosystem.
/// <c>now</c> takes a <see cref="KlexirType.Bool"/> "unit" argument by convention: Klexir has no zero-arity function
/// value, so a nullary native function still needs one (ignored) parameter to be callable as `now true`.
/// </summary>
public sealed class ClockPlugin : IKlexirPlugin
{
    public string Name => "Clock";

    public IReadOnlyList<OpaqueType> Types => [];

    public IReadOnlyList<KlexirNativeFunctionDef> Functions { get; } =
    [
        new KlexirNativeFunctionDef("now", new FunctionType(KlexirType.Bool, KlexirType.Int),
            _ => Task.FromResult(Result<KlexirValue>.Success(new IntValue(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())))),

        new KlexirNativeFunctionDef("delay", new FunctionType(KlexirType.Int, KlexirType.Int), async args =>
        {
            var milliseconds = ((IntValue)args[0]).Value;
            if (milliseconds < 0)
            {
                return Result<KlexirValue>.Failure(Error.Create("'delay' requires a non-negative millisecond count."));
            }

            await Task.Delay((int)milliseconds);
            return Result<KlexirValue>.Success(new IntValue(milliseconds));
        }),
    ];
}
