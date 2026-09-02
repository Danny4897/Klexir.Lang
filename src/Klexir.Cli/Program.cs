using Klexir.Lang;

var (command, path) = args switch
{
    ["run", var file] => ("run", file),
    [var file] when file.EndsWith(".klx", StringComparison.OrdinalIgnoreCase) => ("run", file),
    _ => (null, null),
};

if (command is null || path is null)
{
    Console.Error.WriteLine("""
        Usage:
          klexir run <file.klx>     Run a Klexir program

        Example:
          klexir run hello.klx
        """);
    return 2;
}

if (!File.Exists(path))
{
    Console.Error.WriteLine($"error: no such file '{path}'");
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

var typed = new TypeChecker().Check(ast.Value);
if (typed.IsFailure)
{
    Console.Error.WriteLine($"{path}: type error: {typed.Error.Message}");
    return 1;
}

var result = new Evaluator().Evaluate(typed.Value);
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
    _ => value.ToString() ?? "?",
};
