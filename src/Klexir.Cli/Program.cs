using Klexir.Lang;
using Klexir.Lang.Plugins;

var pluginNames = args.Where(a => a.StartsWith("--plugin=", StringComparison.OrdinalIgnoreCase))
    .Select(a => a["--plugin=".Length..])
    .ToList();

var positional = args.Where(a => !a.StartsWith("--plugin=", StringComparison.OrdinalIgnoreCase)).ToArray();

var (command, path) = positional switch
{
    ["run", var file] => ("run", file),
    [var file] when file.EndsWith(".klx", StringComparison.OrdinalIgnoreCase) => ("run", file),
    _ => (null, null),
};

if (command is null || path is null)
{
    Console.Error.WriteLine("""
        Usage:
          klexir run [--plugin=<name>]... <file.klx>     Run a Klexir program

        Available plugins:
          clock     now/delay — see Klexir.Lang.Plugins.ClockPlugin

        Example:
          klexir run hello.klx
          klexir run --plugin=clock uses-clock.klx
        """);
    return 2;
}

if (!File.Exists(path))
{
    Console.Error.WriteLine($"error: no such file '{path}'");
    return 2;
}

IReadOnlyList<IKlexirPlugin> plugins;
try
{
    plugins = pluginNames.Select(ResolvePlugin).ToList();
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    return 2;
}

var source = await File.ReadAllTextAsync(path);

var tokens = new Lexer(source).Tokenize();
if (tokens.IsFailure)
{
    Console.Error.WriteLine($"{path}: {tokens.Error.Message}");
    return 1;
}

var ast = new Parser(tokens.Value).ParseProgram();
if (ast.IsFailure)
{
    Console.Error.WriteLine($"{path}: {ast.Error.Message}");
    return 1;
}

var typed = new TypeChecker().Check(ast.Value, plugins);
if (typed.IsFailure)
{
    Console.Error.WriteLine($"{path}: type error: {typed.Error.Message}");
    return 1;
}

var result = await new Evaluator().EvaluateAsync(typed.Value, plugins);
if (result.IsFailure)
{
    Console.Error.WriteLine($"{path}: runtime error: {result.Error.Message}");
    return 1;
}

Console.WriteLine(Format(result.Value));
return 0;

static string Format(KlexirValue value) => value switch
{
    IntValue v => v.Value.ToString(),
    BoolValue v => v.Value ? "true" : "false",
    StringValue v => v.Value,
    SomeValue v => $"Some({Format(v.Value)})",
    NoneValue => "None",
    OkValue v => $"Ok({Format(v.Value)})",
    ErrValue v => $"Err({Format(v.Value)})",
    ListValue v => $"[{string.Join(", ", v.Elements.Select(Format))}]",
    RecordValue v => $"{v.TypeName} {{ {string.Join(", ", v.Fields.Select(f => $"{f.Key}: {Format(f.Value)}"))} }}",
    UnionValue v when v.Fields.Count == 0 => v.VariantName,
    UnionValue v => $"{v.VariantName}({string.Join(", ", v.Fields.Select(Format))})",
    ClosureValue => "<function>",
    ConstructorValue v => $"<constructor {v.VariantName}>",
    NativeValue v => $"<{v.Type.Name}>",
    NativeFunctionValue v => $"<function {v.Def.Name}>",
    _ => value.ToString() ?? "?",
};

static IKlexirPlugin ResolvePlugin(string name) => name.ToLowerInvariant() switch
{
    "clock" => new ClockPlugin(),
    _ => throw new ArgumentException($"unknown plugin '{name}' (available: clock)"),
};
