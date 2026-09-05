using FluentAssertions;
using Klexir.Lang.Plugins;
using MonadicSharp;
using Xunit;

namespace Klexir.Lang.Tests;

/// <summary>
/// <see cref="EnginePlugin"/> is the only plugin that outlives one run — <c>dbOpen</c> opens a real file, and data
/// written with <c>dbPut</c> is still there when a LATER, separate plugin instance opens the same path — proving
/// this is genuine <c>Klexir.Engine</c> persistence, not an in-memory stand-in.
/// </summary>
public sealed class EnginePluginTests : IDisposable
{
    // Forward slashes even on Windows — a raw backslash would need escaping inside a Klexir string literal.
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"klexir-engine-test-{Guid.NewGuid():N}.db").Replace('\\', '/');

    [Fact]
    public async Task DbGet_finds_a_value_written_by_dbPut_in_the_same_run()
    {
        await using var plugin = new EnginePlugin();

        const string source = """
            let opened = dbOpen "{{PATH}}" in
            let written = dbPut 1 100 in
            match dbGet 1 with Ok(v) => v | Err(e) => 0 - 1
            """;

        (await RunOk(source.Replace("{{PATH}}", _dbPath), plugin)).Should().Be(100);
    }

    [Fact]
    public async Task DbPut_returns_Ok_true_on_success()
    {
        await using var plugin = new EnginePlugin();

        const string source = """
            let opened = dbOpen "{{PATH}}" in
            dbPut 9 90
            """;

        var value = await Run(source.Replace("{{PATH}}", _dbPath), plugin);
        value.Should().Be(new OkValue(new BoolValue(true)));
    }

    [Fact]
    public async Task DbGet_fails_for_a_key_that_was_never_written()
    {
        await using var plugin = new EnginePlugin();

        const string source = """
            let opened = dbOpen "{{PATH}}" in
            dbGet 42
            """;

        var value = await Run(source.Replace("{{PATH}}", _dbPath), plugin);
        value.Should().BeOfType<ErrValue>();
    }

    [Fact]
    public async Task DbPut_returns_Err_when_the_key_already_exists_instead_of_a_hard_failure()
    {
        var evaluator = new Evaluator();
        await using var plugin = new EnginePlugin();

        const string source = """
            let opened = dbOpen "{{PATH}}" in
            let first = dbPut 5 50 in
            dbPut 5 999
            """;

        // A duplicate key is a real, catchable domain outcome — the evaluation itself still succeeds.
        var value = await Run(source.Replace("{{PATH}}", _dbPath), evaluator, plugin);
        value.Should().BeOfType<ErrValue>();
    }

    [Fact]
    public async Task Data_written_in_one_run_is_visible_after_reopening_the_same_file()
    {
        await using (var writer = new EnginePlugin())
        {
            const string writeSource = """
                let opened = dbOpen "{{PATH}}" in
                dbPut 7 777
                """;

            (await RunResult(writeSource.Replace("{{PATH}}", _dbPath), new Evaluator(), writer)).IsSuccess.Should().BeTrue();
        }

        // A brand-new plugin instance, own Evaluator, reopening the SAME file — proves real disk persistence.
        await using var reader = new EnginePlugin();

        const string readSource = """
            let opened = dbOpen "{{PATH}}" in
            match dbGet 7 with Ok(v) => v | Err(e) => 0 - 1
            """;

        (await RunOk(readSource.Replace("{{PATH}}", _dbPath), reader)).Should().Be(777);
    }

    private static async Task<long> RunOk(string source, EnginePlugin plugin)
    {
        var value = await Run(source, plugin);
        return ((IntValue)value).Value;
    }

    private static Task<KlexirValue> Run(string source, EnginePlugin plugin) => Run(source, new Evaluator(), plugin);

    private static async Task<KlexirValue> Run(string source, Evaluator evaluator, EnginePlugin plugin)
    {
        var result = await RunResult(source, evaluator, plugin);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    private static async Task<Result<KlexirValue>> RunResult(string source, Evaluator evaluator, EnginePlugin plugin)
    {
        var tokens = new Lexer(source).Tokenize();
        tokens.IsSuccess.Should().BeTrue();
        var ast = new Parser(tokens.Value).ParseExpression();
        ast.IsSuccess.Should().BeTrue();
        var typed = new TypeChecker().Check(ast.Value, [plugin]);
        typed.IsSuccess.Should().BeTrue();
        return await evaluator.EvaluateAsync(typed.Value, [plugin]);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
