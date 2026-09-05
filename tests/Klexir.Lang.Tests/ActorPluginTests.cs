using FluentAssertions;
using Klexir.Lang.Plugins;
using MonadicSharp;
using Xunit;

namespace Klexir.Lang.Tests;

/// <summary>
/// <see cref="ActorPlugin"/> bridges <c>Klexir.Actor</c>'s real channel-backed mailboxes — a spawned actor's
/// behavior is a Klexir closure, applied via the same <see cref="Evaluator"/> the program runs on.
/// </summary>
public sealed class ActorPluginTests
{
    [Fact]
    public async Task Ask_returns_the_state_produced_by_a_single_message()
    {
        var evaluator = new Evaluator();
        await using var plugin = new ActorPlugin(evaluator);

        const string source = """
            let started = spawn "counter" "0" (func(String msg) => func(String state) => msg) in
            ask "counter" "1"
            """;

        (await Run(source, evaluator, plugin)).Should().Be(new StringValue("1"));
    }

    [Fact]
    public async Task Ask_reflects_state_accumulated_across_multiple_messages()
    {
        var evaluator = new Evaluator();
        await using var plugin = new ActorPlugin(evaluator);

        // behavior appends the message to the current state — proves the mailbox threads state message-to-message.
        const string source = """
            let started = spawn "log" "" (func(String msg) => func(String state) => state + msg) in
            let first = ask "log" "a" in
            let second = ask "log" "b" in
            ask "log" "c"
            """;

        (await Run(source, evaluator, plugin)).Should().Be(new StringValue("abc"));
    }

    [Fact]
    public async Task Tell_does_not_return_the_resulting_state()
    {
        var evaluator = new Evaluator();
        await using var plugin = new ActorPlugin(evaluator);

        const string source = """
            let started = spawn "counter" "0" (func(String msg) => func(String state) => msg) in
            tell "counter" "1"
            """;

        (await Run(source, evaluator, plugin)).Should().Be(new BoolValue(true));
    }

    [Fact]
    public async Task Ask_fails_for_an_actor_that_was_never_spawned()
    {
        var evaluator = new Evaluator();
        await using var plugin = new ActorPlugin(evaluator);

        var result = await RunResult("ask \"ghost\" \"x\"", evaluator, plugin);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Spawning_the_same_name_twice_keeps_the_first_actors_state()
    {
        var evaluator = new Evaluator();
        await using var plugin = new ActorPlugin(evaluator);

        const string source = """
            let first = spawn "counter" "seed" (func(String msg) => func(String state) => state + msg) in
            let bumped = ask "counter" "-x" in
            let second = spawn "counter" "ignored-because-already-exists" (func(String msg) => func(String state) => state) in
            ask "counter" ""
            """;

        (await Run(source, evaluator, plugin)).Should().Be(new StringValue("seed-x"));
    }

    private static async Task<KlexirValue> Run(string source, Evaluator evaluator, ActorPlugin plugin)
    {
        var result = await RunResult(source, evaluator, plugin);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    private static async Task<Result<KlexirValue>> RunResult(string source, Evaluator evaluator, ActorPlugin plugin)
    {
        var tokens = new Lexer(source).Tokenize();
        tokens.IsSuccess.Should().BeTrue();
        var ast = new Parser(tokens.Value).ParseExpression();
        ast.IsSuccess.Should().BeTrue();
        var typed = new TypeChecker().Check(ast.Value, [plugin]);
        typed.IsSuccess.Should().BeTrue();
        return await evaluator.EvaluateAsync(typed.Value, [plugin]);
    }
}
