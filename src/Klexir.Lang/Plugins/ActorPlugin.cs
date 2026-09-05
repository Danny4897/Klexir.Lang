using Klexir.Actor;
using MonadicSharp;

namespace Klexir.Lang.Plugins;

/// <summary>
/// An actor whose behavior IS a Klexir closure — <c>Klexir.Actor</c>'s <see cref="Klexir.Actor.Actor{TMessage,TState}"/>
/// is generic per subclass, and Klexir can't define new .NET types, so every actor spawned from Klexir shares this
/// one concrete <c>Actor&lt;string, string&gt;</c>, with the actual "what happens on a message" logic supplied as a
/// curried <c>String -&gt; String -&gt; String</c> closure (message, then current state, returning next state).
/// </summary>
internal sealed class KlexirActorBehavior(Evaluator evaluator, KlexirValue behavior) : Klexir.Actor.Actor<string, string>
{
    public override async ValueTask<string> ReceiveAsync(string message, string state, CancellationToken cancellationToken)
    {
        var afterMessage = await evaluator.ApplyAsync(behavior, new StringValue(message));
        if (afterMessage.IsFailure)
        {
            throw new InvalidOperationException(afterMessage.Error.Message);
        }

        var result = await evaluator.ApplyAsync(afterMessage.Value, new StringValue(state));
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Message);
        }

        if (result.Value is not StringValue newState)
        {
            throw new InvalidOperationException("An actor's behavior must return a String state.");
        }

        return newState.Value;
    }
}

/// <summary>
/// Bridges <c>Klexir.Actor</c>'s real channel-backed mailboxes into Klexir: <c>spawn</c> starts a named actor whose
/// behavior is a Klexir closure, <c>tell</c> sends it a message and moves on, <c>ask</c> sends a message and waits
/// for the resulting state. Single-threaded state transitions per actor come from <see cref="InMemoryActorRef{TMessage,TState}"/>
/// itself (a <c>Channel</c>-backed mailbox) — this plugin adds no synchronization of its own, same as it wouldn't
/// need to in C#. Like <see cref="EventFlowPlugin"/>, it holds the <see cref="Evaluator"/> the program runs on, so
/// a spawned actor's behavior can be applied to real messages as they arrive.
/// </summary>
public sealed class ActorPlugin : IKlexirPlugin, IAsyncDisposable
{
    private readonly Evaluator _evaluator;
    private readonly ActorRegistry _registry = new();

    public ActorPlugin(Evaluator evaluator)
    {
        _evaluator = evaluator;

        Functions =
        [
            new KlexirNativeFunctionDef(
                "spawn",
                new FunctionType(KlexirType.String, new FunctionType(KlexirType.String,
                    new FunctionType(new FunctionType(KlexirType.String, new FunctionType(KlexirType.String, KlexirType.String)), KlexirType.Bool))),
                args =>
                {
                    var name = ((StringValue)args[0]).Value;
                    var initialState = ((StringValue)args[1]).Value;
                    var behavior = args[2];

                    _registry.GetOrCreate<string, string>(name, () => new KlexirActorBehavior(_evaluator, behavior), initialState);
                    return Task.FromResult(Result<KlexirValue>.Success(new BoolValue(true)));
                }),

            new KlexirNativeFunctionDef(
                "tell",
                new FunctionType(KlexirType.String, new FunctionType(KlexirType.String, KlexirType.Bool)),
                async args =>
                {
                    var name = ((StringValue)args[0]).Value;
                    var message = ((StringValue)args[1]).Value;

                    if (!_registry.TryGet<string>(name, out var actorRef) || actorRef is null)
                    {
                        return Result<KlexirValue>.Failure(Error.Create($"no actor named '{name}' — spawn it first."));
                    }

                    await actorRef.TellAsync(message);
                    return Result<KlexirValue>.Success(new BoolValue(true));
                }),

            new KlexirNativeFunctionDef(
                "ask",
                new FunctionType(KlexirType.String, new FunctionType(KlexirType.String, KlexirType.String)),
                async args =>
                {
                    var name = ((StringValue)args[0]).Value;
                    var message = ((StringValue)args[1]).Value;

                    if (!_registry.TryGet<string>(name, out var actorRef) || actorRef is not InMemoryActorRef<string, string> concrete)
                    {
                        return Result<KlexirValue>.Failure(Error.Create($"no actor named '{name}' — spawn it first."));
                    }

                    try
                    {
                        var newState = await concrete.AskAsync(message);
                        return Result<KlexirValue>.Success(new StringValue(newState));
                    }
                    catch (Exception ex)
                    {
                        return Result<KlexirValue>.Failure(Error.Create(ex.Message));
                    }
                }),
        ];
    }

    public string Name => "Actor";

    public IReadOnlyList<OpaqueType> Types => [];

    public IReadOnlyList<KlexirNativeFunctionDef> Functions { get; }

    public ValueTask DisposeAsync() => _registry.DisposeAsync();
}
