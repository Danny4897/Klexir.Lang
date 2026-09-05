using Klexir.Workflow;
using Klexir.Workflow.Abstractions;
using MonadicSharp;

namespace Klexir.Lang.Plugins;

/// <summary>
/// Bridges <c>Klexir.Workflow</c>'s real step engine into Klexir. <c>Workflow.Define&lt;TRequest&gt;()...Step&lt;TNext&gt;()</c>
/// is a compile-time type-safe fluent builder — Klexir has no generics to drive it with, so every step here is
/// fixed to <c>String -&gt; Result&lt;String, String&gt;</c>, and the builder for a given workflow name is
/// accumulated across <c>defineStep</c> calls (each returns a *new* <see cref="WorkflowBuilder{TRequest,TCurrent}"/>,
/// reassigned into the plugin's own per-name slot, since <c>TCurrent</c> never actually changes type here).
/// <c>runWorkflow</c> runs the accumulated steps through a real <see cref="WorkflowEngine"/> with an
/// <see cref="InMemoryWorkflowStore"/> — checkpointed after every step exactly as it would be for a C# caller —
/// and reads the final value back from the checkpoint, since <see cref="WorkflowEngine.StartAsync{TRequest}"/>
/// itself only returns the instance id, not the result.
/// </summary>
public sealed class WorkflowPlugin : IKlexirPlugin
{
    private readonly Evaluator _evaluator;
    private readonly Dictionary<string, WorkflowBuilder<string, string>> _builders = [];

    public WorkflowPlugin(Evaluator evaluator)
    {
        _evaluator = evaluator;

        Functions =
        [
            new KlexirNativeFunctionDef(
                "defineStep",
                new FunctionType(KlexirType.String, new FunctionType(KlexirType.String,
                    new FunctionType(new FunctionType(KlexirType.String, new ResultType(KlexirType.String, KlexirType.String)), KlexirType.Bool))),
                args =>
                {
                    var workflowName = ((StringValue)args[0]).Value;
                    var stepName = ((StringValue)args[1]).Value;
                    var stepClosure = args[2];

                    var builder = _builders.TryGetValue(workflowName, out var existing)
                        ? existing
                        : Klexir.Workflow.Workflow.Define<string>(workflowName);

                    _builders[workflowName] = builder.Step<string>(stepName, async input =>
                    {
                        var applied = await _evaluator.ApplyAsync(stepClosure, new StringValue(input));
                        if (applied.IsFailure)
                        {
                            return Result<string>.Failure(applied.Error);
                        }

                        return applied.Value switch
                        {
                            OkValue { Value: StringValue ok } => Result<string>.Success(ok.Value),
                            ErrValue { Value: StringValue err } => Result<string>.Failure(Error.Create(err.Value)),
                            _ => Result<string>.Failure(Error.Create("A workflow step must return Result<String, String>.")),
                        };
                    });

                    return Task.FromResult(Result<KlexirValue>.Success(new BoolValue(true)));
                }),

            new KlexirNativeFunctionDef(
                "runWorkflow",
                new FunctionType(KlexirType.String, new FunctionType(KlexirType.String, new ResultType(KlexirType.String, KlexirType.String))),
                async args =>
                {
                    var workflowName = ((StringValue)args[0]).Value;
                    var input = ((StringValue)args[1]).Value;

                    if (!_builders.TryGetValue(workflowName, out var builder))
                    {
                        return Result<KlexirValue>.Failure(Error.Create($"no workflow named '{workflowName}' — call defineStep first."));
                    }

                    var definition = builder.Build();
                    var store = new InMemoryWorkflowStore();
                    var engine = new WorkflowEngine(store);

                    var started = await engine.StartAsync(definition, input);
                    if (started.IsFailure)
                    {
                        return Result<KlexirValue>.Failure(started.Error);
                    }

                    var checkpoint = await store.LoadAsync(started.Value);
                    if (checkpoint.IsFailure)
                    {
                        return Result<KlexirValue>.Failure(checkpoint.Error);
                    }

                    KlexirValue outcome = checkpoint.Value.Status == WorkflowStatus.Completed
                        ? new OkValue(new StringValue((string)checkpoint.Value.CurrentValue))
                        : new ErrValue(new StringValue(
                            $"workflow '{workflowName}' {checkpoint.Value.Status} after {checkpoint.Value.CompletedStepCount} step(s); last value: {checkpoint.Value.CurrentValue}"));

                    return Result<KlexirValue>.Success(outcome);
                }),
        ];
    }

    public string Name => "Workflow";

    public IReadOnlyList<OpaqueType> Types => [];

    public IReadOnlyList<KlexirNativeFunctionDef> Functions { get; }
}
