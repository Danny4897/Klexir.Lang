using Klexir.EventFlow;
using Klexir.EventFlow.Abstractions;
using MonadicSharp;

namespace Klexir.Lang.Plugins;

/// <summary>
/// The one concrete <see cref="IEvent"/> every Klexir event rides on — Klexir can't define new .NET types, so every
/// published event shares this wire shape; <see cref="TypeTag"/> is what a Klexir program actually distinguishes on.
/// </summary>
public sealed class KlexirEvent(string typeTag, string payload) : IEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;

    public string TypeTag { get; } = typeTag;

    public string Payload { get; } = payload;
}

/// <summary>
/// Bridges <c>Klexir.EventFlow</c>'s real <see cref="InMemoryEventBus"/> into Klexir: <c>subscribe</c> registers a
/// Klexir closure against an event-type tag, <c>publish</c> fires a real <see cref="KlexirEvent"/> through the bus.
/// Unlike <see cref="ClockPlugin"/>, this plugin needs to call back INTO a running program later — it holds the
/// same <see cref="Evaluator"/> that will run the program (the CLI constructs both together, see
/// <c>Klexir.Cli.ResolvePlugin</c>), applying a subscriber's closure via <see cref="Evaluator.ApplyAsync"/>
/// whenever a matching event arrives. A subscriber that fails (returns <see cref="Result{T}.Failure"/>, or whose
/// own body errors) throws — deliberately, since that's how <see cref="InMemoryEventBus"/>'s real retry/dead-letter
/// machinery finds out a handler failed; Klexir's own "never throw for control flow" rule is about Klexir *source*
/// control flow, not this .NET-side adapter boundary.
/// </summary>
public sealed class EventFlowPlugin : IKlexirPlugin, IEventHandler<KlexirEvent>
{
    private readonly Evaluator _evaluator;
    private readonly InMemoryEventBus _bus;
    private readonly Dictionary<string, List<KlexirValue>> _subscribers = [];

    public EventFlowPlugin(Evaluator evaluator, InMemoryEventBus? bus = null)
    {
        _evaluator = evaluator;
        _bus = bus ?? new InMemoryEventBus();
        _bus.Register<KlexirEvent>(this);

        Functions =
        [
            new KlexirNativeFunctionDef(
                "subscribe",
                new FunctionType(KlexirType.String, new FunctionType(new FunctionType(KlexirType.String, KlexirType.Bool), KlexirType.Bool)),
                args =>
                {
                    var eventType = ((StringValue)args[0]).Value;
                    var handler = args[1];

                    if (!_subscribers.TryGetValue(eventType, out var handlers))
                    {
                        handlers = [];
                        _subscribers[eventType] = handlers;
                    }

                    handlers.Add(handler);
                    return Task.FromResult(Result<KlexirValue>.Success(new BoolValue(true)));
                }),

            new KlexirNativeFunctionDef(
                "publish",
                new FunctionType(KlexirType.String, new FunctionType(KlexirType.String, KlexirType.Bool)),
                async publishArgs =>
                {
                    var eventType = ((StringValue)publishArgs[0]).Value;
                    var payload = ((StringValue)publishArgs[1]).Value;

                    await _bus.PublishAsync(new KlexirEvent(eventType, payload));
                    return Result<KlexirValue>.Success(new BoolValue(true));
                }),
        ];
    }

    public string Name => "EventFlow";

    public IReadOnlyList<OpaqueType> Types => [];

    public IReadOnlyList<KlexirNativeFunctionDef> Functions { get; }

    async ValueTask IEventHandler<KlexirEvent>.HandleAsync(KlexirEvent @event, CancellationToken cancellationToken)
    {
        if (!_subscribers.TryGetValue(@event.TypeTag, out var handlers))
        {
            return;
        }

        foreach (var handler in handlers)
        {
            var result = await _evaluator.ApplyAsync(handler, new StringValue(@event.Payload));
            if (result.IsFailure)
            {
                throw new InvalidOperationException($"Subscriber for '{@event.TypeTag}' failed: {result.Error.Message}");
            }

            if (result.Value is not BoolValue { Value: true })
            {
                throw new InvalidOperationException($"Subscriber for '{@event.TypeTag}' returned false.");
            }
        }
    }
}
