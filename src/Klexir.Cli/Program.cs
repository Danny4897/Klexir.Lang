using System.Net;
using System.Text;
using Klexir.Cli;
using Klexir.Lang;
using Klexir.Lang.Plugins;
using Klexir.Runtime;

var pluginNames = args.Where(a => a.StartsWith("--plugin=", StringComparison.OrdinalIgnoreCase))
    .Select(a => a["--plugin=".Length..])
    .ToList();

var portArg = args.FirstOrDefault(a => a.StartsWith("--port=", StringComparison.OrdinalIgnoreCase));
var port = portArg is not null ? int.Parse(portArg["--port=".Length..]) : 5000;

var positional = args.Where(a =>
    !a.StartsWith("--plugin=", StringComparison.OrdinalIgnoreCase)
    && !a.StartsWith("--port=", StringComparison.OrdinalIgnoreCase)).ToArray();

if (positional is ["new", var projectName, ..])
{
    var baseDir = positional.Length > 2 ? positional[2] : Directory.GetCurrentDirectory();
    return CreateProjectFromTemplate(projectName, Path.Combine(baseDir, projectName));
}

if (positional is ["serve", var serveFile])
{
    return await Serve(serveFile, port, pluginNames);
}

var (command, path) = positional switch
{
    ["run", var file] => ("run", file),
    ["compile", var file] => ("compile", file),
    [var file] when file.EndsWith(".klx", StringComparison.OrdinalIgnoreCase) => ("run", file),
    _ => (null, null),
};

if (command is null || path is null)
{
    Console.Error.WriteLine("""
        Usage:
          klexir new <ProjectName> [directory]            Scaffold a clean-architecture project skeleton
          klexir run [--plugin=<name>]... <file.klx>      Run a Klexir program (tree-walking evaluator)
          klexir compile <file.klx>                       Compile to Klexir.Runtime bytecode and run it there
          klexir serve [--port=N] [--plugin=<name>]... <file.klx>
                                                           Host the program's final expression as an HTTP handler
                                                           (HttpRequest -> HttpResponse); Ctrl+C to stop

        Available plugins (run/serve only — compile doesn't support plugins yet):
          clock       now/delay — see Klexir.Lang.Plugins.ClockPlugin
          eventflow   subscribe/publish over a real Klexir.EventFlow.InMemoryEventBus

        Example:
          klexir new MyApp
          klexir run hello.klx
          klexir run --plugin=clock uses-clock.klx
          klexir run --plugin=eventflow events.klx
          klexir compile hello.klx
          klexir serve --port=5000 api.klx
        """);
    return 2;
}

if (!File.Exists(path))
{
    Console.Error.WriteLine($"error: no such file '{path}'");
    return 2;
}

if (command == "compile")
{
    return await RunCompiled(path);
}

var evaluator = new Evaluator();

IReadOnlyList<IKlexirPlugin> plugins;
try
{
    plugins = pluginNames.Select(name => ResolvePlugin(name, evaluator)).ToList();
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

var result = await evaluator.EvaluateAsync(typed.Value, plugins);
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

static async Task<int> RunCompiled(string path)
{
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

    var compiled = Compiler.Compile(typed.Value);
    if (compiled.IsFailure)
    {
        Console.Error.WriteLine($"{path}: compile error: {compiled.Error.Message}");
        return 1;
    }

    var result = new KlexirVm(compiled.Value.Code, compiled.Value.EntryPoint).Run();
    if (result.IsFailure)
    {
        Console.Error.WriteLine($"{path}: bytecode runtime error: {result.Error.Message}");
        return 1;
    }

    Console.WriteLine(result.Value);
    return 0;
}

/// <summary>
/// Hosts a Klexir program's final expression as a live HTTP handler. The program must declare its own
/// 'HttpRequest'/'HttpResponse' records matching <see cref="HttpBridge"/>'s field contract (Klexir has no way for
/// a plugin to hand the program a type it didn't itself declare) and its final expression must evaluate to a
/// function — evaluated once here, then applied fresh via <see cref="Evaluator.ApplyAsync"/> for each request that
/// arrives, sequentially, so nothing about the evaluator's concurrency story needs deciding for this first cut.
/// </summary>
static async Task<int> Serve(string path, int port, IReadOnlyList<string> pluginNames)
{
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"error: no such file '{path}'");
        return 2;
    }

    var evaluator = new Evaluator();

    IReadOnlyList<IKlexirPlugin> plugins;
    try
    {
        plugins = pluginNames.Select(name => ResolvePlugin(name, evaluator)).ToList();
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

    if (typed.Value.Type is not FunctionType)
    {
        Console.Error.WriteLine(
            $"{path}: 'serve' requires the program's final expression to be a function ({HttpBridge.RequestTypeName} -> {HttpBridge.ResponseTypeName}), got {typed.Value.Type}.");
        return 1;
    }

    var handlerResult = await evaluator.EvaluateAsync(typed.Value, plugins);
    if (handlerResult.IsFailure)
    {
        Console.Error.WriteLine($"{path}: runtime error: {handlerResult.Error.Message}");
        return 1;
    }

    var handler = handlerResult.Value;

    using var listener = new HttpListener();
    listener.Prefixes.Add($"http://localhost:{port}/");

    try
    {
        listener.Start();
    }
    catch (HttpListenerException ex)
    {
        Console.Error.WriteLine($"error: couldn't listen on port {port}: {ex.Message}");
        return 1;
    }

    Console.WriteLine($"Klexir serving {path} on http://localhost:{port}/ (Ctrl+C to stop)");

    while (listener.IsListening)
    {
        HttpListenerContext context;
        try
        {
            context = await listener.GetContextAsync();
        }
        catch (HttpListenerException)
        {
            break;
        }
        catch (ObjectDisposedException)
        {
            break;
        }

        await HandleRequestAsync(context, evaluator, handler);
    }

    return 0;
}

static async Task HandleRequestAsync(HttpListenerContext context, Evaluator evaluator, KlexirValue handler)
{
    string requestBody;
    using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
    {
        requestBody = await reader.ReadToEndAsync();
    }

    var requestRecord = HttpBridge.ToRequestRecord(
        context.Request.HttpMethod, context.Request.Url?.AbsolutePath ?? "/", requestBody);

    var applied = await evaluator.ApplyAsync(handler, requestRecord);

    var (status, responseBody) = applied.IsFailure
        ? (500, $"Klexir runtime error: {applied.Error.Message}")
        : HttpBridge.FromResponseRecord(applied.Value) switch
        {
            { IsSuccess: true } parsed => parsed.Value,
            var failed => (500, $"Klexir handler error: {failed.Error.Message}"),
        };

    context.Response.StatusCode = status;
    var buffer = Encoding.UTF8.GetBytes(responseBody);
    context.Response.ContentLength64 = buffer.Length;
    await context.Response.OutputStream.WriteAsync(buffer);
    context.Response.OutputStream.Close();
}

static int CreateProjectFromTemplate(string projectName, string targetDir)
{
    var templateDir = Path.Combine(AppContext.BaseDirectory, "Templates", "CleanArchitecture");
    if (!Directory.Exists(templateDir))
    {
        Console.Error.WriteLine($"error: template not found at '{templateDir}' — is the CLI built/published correctly?");
        return 2;
    }

    if (Directory.Exists(targetDir) && Directory.EnumerateFileSystemEntries(targetDir).Any())
    {
        Console.Error.WriteLine($"error: '{targetDir}' already exists and isn't empty.");
        return 2;
    }

    Directory.CreateDirectory(targetDir);

    foreach (var sourceFile in Directory.EnumerateFiles(templateDir, "*", SearchOption.AllDirectories))
    {
        var relativePath = Path.GetRelativePath(templateDir, sourceFile);
        var destFile = Path.Combine(targetDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);

        var content = File.ReadAllText(sourceFile).Replace("{{PROJECT_NAME}}", projectName);
        File.WriteAllText(destFile, content);
    }

    Console.WriteLine($"Created '{projectName}' at {targetDir}");
    Console.WriteLine();
    Console.WriteLine("Next:");
    Console.WriteLine($"  cd {Path.GetRelativePath(Directory.GetCurrentDirectory(), targetDir)}");
    Console.WriteLine("  klexir run Program.klx");
    return 0;
}

static IKlexirPlugin ResolvePlugin(string name, Evaluator evaluator) => name.ToLowerInvariant() switch
{
    "clock" => new ClockPlugin(),
    "eventflow" => new EventFlowPlugin(evaluator),
    _ => throw new ArgumentException($"unknown plugin '{name}' (available: clock, eventflow)"),
};
