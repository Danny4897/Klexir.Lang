using MonadicSharp;

namespace Klexir.Lang;

/// <summary>
/// A native capability an embedding host opts a Klexir program into. Plugins are a compile-time whitelist — an
/// enabled list passed straight to <see cref="TypeChecker"/>/<see cref="Evaluator"/> by whoever hosts Klexir, never
/// discovered from a `.klx` source file or a dropped-in assembly. <see cref="TypeChecker"/> seeds its identifier
/// environment from <see cref="Functions"/> and <see cref="Types"/> so plugin functions type-check and apply exactly
/// like any other Klexir function; <see cref="Evaluator"/> seeds its value environment the same way.
/// </summary>
public interface IKlexirPlugin
{
    string Name { get; }

    IReadOnlyList<OpaqueType> Types { get; }

    IReadOnlyList<KlexirNativeFunctionDef> Functions { get; }
}

/// <summary>
/// A native function a plugin exposes under <paramref name="Name"/>, callable like any ordinary Klexir function —
/// curried application, no special AST node or parser/type-checker case. <paramref name="Type"/> must be a
/// <see cref="FunctionType"/>, curried once per argument; <paramref name="Invoke"/> receives the fully-applied
/// argument list in application order and returns asynchronously, since a native function may do real I/O.
/// </summary>
public sealed record KlexirNativeFunctionDef(
    string Name, FunctionType Type, Func<IReadOnlyList<KlexirValue>, Task<Result<KlexirValue>>> Invoke)
{
    /// <summary>How many curried arguments <see cref="Type"/> declares — the length <see cref="Invoke"/> expects
    /// its argument list to reach before it's called.</summary>
    public int Arity
    {
        get
        {
            var arity = 0;
            KlexirType current = Type;

            while (current is FunctionType functionType)
            {
                arity++;
                current = functionType.Return;
            }

            return arity;
        }
    }
}
