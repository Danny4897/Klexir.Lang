using FluentAssertions;
using Klexir.Lang.Plugins;
using MonadicSharp;
using Xunit;

namespace Klexir.Lang.Tests;

/// <summary>
/// <see cref="WorkflowPlugin"/> runs a Klexir-defined sequence of steps through a real
/// <c>Klexir.Workflow.WorkflowEngine</c> (with checkpointing under the hood) — <c>defineStep</c> accumulates a
/// step at a time, <c>runWorkflow</c> executes the accumulated definition end to end.
/// </summary>
public sealed class WorkflowPluginTests
{
    [Fact]
    public async Task RunWorkflow_threads_a_single_steps_output_through()
    {
        var evaluator = new Evaluator();
        var plugin = new WorkflowPlugin(evaluator);

        const string source = """
            let defined = defineStep "greet" "shout" (func(String name) => Ok<String>(name + "!")) in
            runWorkflow "greet" "ciao"
            """;

        (await RunOk(source, evaluator, plugin)).Should().Be("ciao!");
    }

    [Fact]
    public async Task RunWorkflow_runs_multiple_steps_in_order()
    {
        var evaluator = new Evaluator();
        var plugin = new WorkflowPlugin(evaluator);

        const string source = """
            let step1 = defineStep "pipeline" "upper-ish" (func(String s) => Ok<String>(s + "-A")) in
            let step2 = defineStep "pipeline" "again" (func(String s) => Ok<String>(s + "-B")) in
            runWorkflow "pipeline" "x"
            """;

        (await RunOk(source, evaluator, plugin)).Should().Be("x-A-B");
    }

    [Fact]
    public async Task RunWorkflow_fails_when_a_step_returns_Err()
    {
        var evaluator = new Evaluator();
        var plugin = new WorkflowPlugin(evaluator);

        const string source = """
            let step1 = defineStep "risky" "fails" (func(String s) => Err<String>("boom")) in
            runWorkflow "risky" "x"
            """;

        (await RunErr(source, evaluator, plugin)).Should().Contain("Failed");
    }

    [Fact]
    public async Task RunWorkflow_stops_at_the_failing_step_without_running_later_ones()
    {
        var evaluator = new Evaluator();
        var plugin = new WorkflowPlugin(evaluator);

        const string source = """
            let step1 = defineStep "gated" "fails" (func(String s) => Err<String>("nope")) in
            let step2 = defineStep "gated" "should-not-run" (func(String s) => Ok<String>(s + "-should-not-appear")) in
            runWorkflow "gated" "x"
            """;

        var message = await RunErr(source, evaluator, plugin);
        message.Should().NotContain("should-not-appear");
    }

    [Fact]
    public async Task RunWorkflow_fails_for_a_workflow_that_was_never_defined()
    {
        var evaluator = new Evaluator();
        var plugin = new WorkflowPlugin(evaluator);

        var result = await RunResult("runWorkflow \"ghost\" \"x\"", evaluator, plugin);

        result.IsFailure.Should().BeTrue();
    }

    private static async Task<string> RunOk(string source, Evaluator evaluator, WorkflowPlugin plugin)
    {
        var value = await Run(source, evaluator, plugin);
        value.Should().BeOfType<OkValue>();
        return ((StringValue)((OkValue)value).Value).Value;
    }

    private static async Task<string> RunErr(string source, Evaluator evaluator, WorkflowPlugin plugin)
    {
        var value = await Run(source, evaluator, plugin);
        value.Should().BeOfType<ErrValue>();
        return ((StringValue)((ErrValue)value).Value).Value;
    }

    private static async Task<KlexirValue> Run(string source, Evaluator evaluator, WorkflowPlugin plugin)
    {
        var result = await RunResult(source, evaluator, plugin);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    private static async Task<Result<KlexirValue>> RunResult(string source, Evaluator evaluator, WorkflowPlugin plugin)
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
