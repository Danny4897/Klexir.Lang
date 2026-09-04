using FluentAssertions;
using MonadicSharp;
using Xunit;

namespace Klexir.Lang.Tests;

/// <summary>The bridge between Klexir's Option/Result values and real MonadicSharp Option&lt;T&gt;/Result&lt;T&gt;.</summary>
public sealed class KlexirInteropTests
{
    [Fact]
    public void AsInt_extracts_the_long_from_an_IntValue()
    {
        KlexirInterop.AsInt(new IntValue(42)).Value.Should().Be(42);
    }

    [Fact]
    public void AsInt_fails_for_a_non_Int_value()
    {
        KlexirInterop.AsInt(new BoolValue(true)).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AsBool_extracts_the_bool_from_a_BoolValue()
    {
        KlexirInterop.AsBool(new BoolValue(true)).Value.Should().BeTrue();
    }

    [Fact]
    public void AsString_extracts_the_string_from_a_StringValue()
    {
        KlexirInterop.AsString(new StringValue("hi")).Value.Should().Be("hi");
    }

    [Fact]
    public void ToOption_converts_Some_to_a_real_Option_with_the_projected_value()
    {
        var option = KlexirInterop.ToOption(new SomeValue(new IntValue(5)), KlexirInterop.AsInt);

        option.IsSuccess.Should().BeTrue();
        option.Value.Should().Be(Option<long>.Some(5));
    }

    [Fact]
    public void ToOption_converts_None_to_a_real_Option_None()
    {
        var option = KlexirInterop.ToOption(new NoneValue(), KlexirInterop.AsInt);

        option.IsSuccess.Should().BeTrue();
        option.Value.Should().Be(Option<long>.None);
    }

    [Fact]
    public void ToOption_fails_when_given_a_non_Option_value()
    {
        KlexirInterop.ToOption(new IntValue(1), KlexirInterop.AsInt).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ToResult_converts_Ok_to_a_real_successful_Result_with_the_projected_value()
    {
        var result = KlexirInterop.ToResult(new OkValue(new IntValue(5)), KlexirInterop.AsInt, e => e.ToString());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(5);
    }

    [Fact]
    public void ToResult_converts_Err_to_a_real_failed_Result_carrying_the_described_error()
    {
        var result = KlexirInterop.ToResult(
            new ErrValue(new StringValue("not found")), KlexirInterop.AsInt, e => ((StringValue)e).Value);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("not found");
    }

    [Fact]
    public void FromOption_lifts_a_real_Some_back_into_a_Klexir_Some_value()
    {
        KlexirInterop.FromOption(Option<long>.Some(7), v => new IntValue(v)).Should().Be(new SomeValue(new IntValue(7)));
    }

    [Fact]
    public void FromOption_lifts_a_real_None_back_into_a_Klexir_None_value()
    {
        KlexirInterop.FromOption(Option<long>.None, v => new IntValue(v)).Should().Be(new NoneValue());
    }

    [Fact]
    public void FromResult_lifts_a_real_successful_Result_back_into_a_Klexir_Ok_value()
    {
        KlexirInterop.FromResult(Result<long>.Success(7), v => new IntValue(v), e => new StringValue(e.Message))
            .Should().Be(new OkValue(new IntValue(7)));
    }

    [Fact]
    public void FromResult_lifts_a_real_failed_Result_back_into_a_Klexir_Err_value()
    {
        KlexirInterop.FromResult(Result<long>.Failure("boom"), v => new IntValue(v), e => new StringValue(e.Message))
            .Should().Be(new ErrValue(new StringValue("boom")));
    }

    [Fact]
    public void Interop_round_trips_a_Klexir_program_result_through_MonadicSharp_and_back()
    {
        var tokens = new Lexer(
            "let safeDiv = func(Int n) => if n == 0 then Err<Int>(true) else Ok<Bool>(100 / n) in safeDiv 4")
            .Tokenize();
        var ast = new Parser(tokens.Value).ParseExpression();
        var typed = new TypeChecker().Check(ast.Value);
        var klexirValue = new Evaluator().Evaluate(typed.Value).Value;

        var bridged = KlexirInterop.ToResult(klexirValue, KlexirInterop.AsInt, e => ((BoolValue)e).Value ? "div by zero" : "?");

        bridged.IsSuccess.Should().BeTrue();
        bridged.Value.Should().Be(25);
    }
}
