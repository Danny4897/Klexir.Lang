using MonadicSharp;

namespace Klexir.Lang;

/// <summary>
/// The bridge between Klexir's own Option/Result <see cref="KlexirValue"/>s and the real
/// <see cref="MonadicSharp.Option{T}"/>/<see cref="MonadicSharp.Result{T}"/> a hosting .NET application already
/// works with — so a Klexir program's result can be handed straight to C# code as the type it expects, not as an
/// opaque <see cref="KlexirValue"/> tree the caller has to pattern-match itself.
/// </summary>
public static class KlexirInterop
{
    public static Result<long> AsInt(KlexirValue value) =>
        value is IntValue intValue
            ? Result<long>.Success(intValue.Value)
            : Result<long>.Failure(Error.Create($"Expected an Int value, got {value.GetType().Name}."));

    public static Result<bool> AsBool(KlexirValue value) =>
        value is BoolValue boolValue
            ? Result<bool>.Success(boolValue.Value)
            : Result<bool>.Failure(Error.Create($"Expected a Bool value, got {value.GetType().Name}."));

    public static Result<string> AsString(KlexirValue value) =>
        value is StringValue stringValue
            ? Result<string>.Success(stringValue.Value)
            : Result<string>.Failure(Error.Create($"Expected a String value, got {value.GetType().Name}."));

    /// <summary>
    /// Converts a Klexir <c>Some</c>/<c>None</c> value to a real <see cref="Option{T}"/>, projecting the wrapped
    /// value with <paramref name="project"/> (e.g. <see cref="AsInt"/>).
    /// </summary>
    public static Result<Option<T>> ToOption<T>(KlexirValue value, Func<KlexirValue, Result<T>> project) =>
        value switch
        {
            SomeValue some => project(some.Value).Map(Option<T>.Some),
            NoneValue => Result<Option<T>>.Success(Option<T>.None),
            _ => Result<Option<T>>.Failure(Error.Create($"Expected a Klexir Option value, got {value.GetType().Name}.")),
        };

    /// <summary>
    /// Converts a Klexir <c>Ok</c>/<c>Err</c> value to a real <see cref="Result{T}"/>. MonadicSharp's
    /// <see cref="Result{T}"/> always carries an <see cref="Error"/> (not a generic error type like Klexir's
    /// <c>Result&lt;T, E&gt;</c> does), so the <c>Err</c> payload becomes that <see cref="Error"/>'s message via
    /// <paramref name="describeErr"/>.
    /// </summary>
    public static Result<T> ToResult<T>(
        KlexirValue value, Func<KlexirValue, Result<T>> projectOk, Func<KlexirValue, string> describeErr) =>
        value switch
        {
            OkValue ok => projectOk(ok.Value),
            ErrValue err => Result<T>.Failure(Error.Create(describeErr(err.Value))),
            _ => Result<T>.Failure(Error.Create($"Expected a Klexir Result value, got {value.GetType().Name}.")),
        };

    /// <summary>The reverse direction: lifts a real <see cref="Option{T}"/> back into a Klexir <c>Some</c>/<c>None</c> value.</summary>
    public static KlexirValue FromOption<T>(Option<T> option, Func<T, KlexirValue> project) =>
        option.Match<KlexirValue>(value => new SomeValue(project(value)), () => new NoneValue());

    /// <summary>The reverse direction: lifts a real <see cref="Result{T}"/> back into a Klexir <c>Ok</c>/<c>Err</c> value.</summary>
    public static KlexirValue FromResult<T>(Result<T> result, Func<T, KlexirValue> projectOk, Func<Error, KlexirValue> projectErr) =>
        result.Match<KlexirValue>(value => new OkValue(projectOk(value)), error => new ErrValue(projectErr(error)));
}
