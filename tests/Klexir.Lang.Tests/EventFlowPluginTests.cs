using FluentAssertions;
using Klexir.Lang.Plugins;
using MonadicSharp;
using Xunit;

namespace Klexir.Lang.Tests;

/// <summary>
/// <see cref="EventFlowPlugin"/> is the first plugin where .NET calls back INTO a running program rather than the
/// other way around — a <c>subscribe</c>d Klexir closure gets applied via the same <see cref="Evaluator"/> the
/// program itself runs on, every time a matching <c>publish</c> fires, routed through a real
/// <c>Klexir.EventFlow.InMemoryEventBus</c>.
/// </summary>
public sealed class EventFlowPluginTests
{
    [Fact]
    public async Task Publish_invokes_a_subscribed_handler_with_the_payload()
    {
        var evaluator = new Evaluator();
        var plugin = new EventFlowPlugin(evaluator);

        const string source = """
            let handled = subscribe "UserCreated" (func(String payload) => payload == "alice") in
            publish "UserCreated" "alice"
            """;

        (await Run(source, evaluator, plugin)).Should().Be(new BoolValue(true));
    }

    [Fact]
    public async Task Publish_with_no_subscribers_still_succeeds()
    {
        var evaluator = new Evaluator();
        var plugin = new EventFlowPlugin(evaluator);

        (await Run("publish \"NothingListening\" \"x\"", evaluator, plugin)).Should().Be(new BoolValue(true));
    }

    [Fact]
    public async Task Publish_invokes_every_subscriber_registered_for_that_event_type()
    {
        var evaluator = new Evaluator();
        var plugin = new EventFlowPlugin(evaluator);

        const string source = """
            let sub1 = subscribe "Ping" (func(String p) => p == "pong") in
            let sub2 = subscribe "Ping" (func(String p) => p == "pong") in
            publish "Ping" "pong"
            """;

        (await Run(source, evaluator, plugin)).Should().Be(new BoolValue(true));
    }

    [Fact]
    public async Task Publish_fails_when_a_subscriber_returns_false()
    {
        var evaluator = new Evaluator();
        var plugin = new EventFlowPlugin(evaluator);

        const string source = """
            let sub = subscribe "Ping" (func(String p) => false) in
            publish "Ping" "anything"
            """;

        var result = await RunResult(source, evaluator, plugin);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task A_subscriber_is_not_invoked_for_a_different_event_type()
    {
        var evaluator = new Evaluator();
        var plugin = new EventFlowPlugin(evaluator);

        const string source = """
            let sub = subscribe "TypeA" (func(String p) => false) in
            publish "TypeB" "anything"
            """;

        // sub's handler would fail if invoked (always returns false) — succeeding proves TypeB never reached it.
        (await Run(source, evaluator, plugin)).Should().Be(new BoolValue(true));
    }

    private static async Task<KlexirValue> Run(string source, Evaluator evaluator, EventFlowPlugin plugin)
    {
        var result = await RunResult(source, evaluator, plugin);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    private static async Task<Result<KlexirValue>> RunResult(string source, Evaluator evaluator, EventFlowPlugin plugin)
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
