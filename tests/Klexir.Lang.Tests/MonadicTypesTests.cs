using FluentAssertions;
using MonadicSharp;
using Xunit;

namespace Klexir.Lang.Tests;

/// <summary>
/// Option&lt;T&gt; and Result&lt;T, E&gt; as first-class Klexir values, mirroring MonadicSharp's Option&lt;T&gt;/Result&lt;T&gt;:
/// Some/None and Ok/Err constructors, exhaustive `match`, and `map`/`bind` (functor/monad operations).
/// </summary>
public sealed class MonadicTypesTests
{
    [Fact]
    public void Check_types_Some_as_Option_of_the_wrapped_values_type()
    {
        CheckSuccessfully("Some(5)").Type.Should().Be(new OptionType(KlexirType.Int));
    }

    [Fact]
    public void Check_types_None_from_its_explicit_type_argument()
    {
        CheckSuccessfully("None<Int>").Type.Should().Be(new OptionType(KlexirType.Int));
    }

    [Fact]
    public void Check_types_Ok_as_Result_pairing_the_values_type_with_the_explicit_error_type()
    {
        CheckSuccessfully("Ok<Bool>(5)").Type.Should().Be(new ResultType(KlexirType.Int, KlexirType.Bool));
    }

    [Fact]
    public void Check_types_Err_as_Result_pairing_the_explicit_ok_type_with_the_values_type()
    {
        CheckSuccessfully("Err<Int>(true)").Type.Should().Be(new ResultType(KlexirType.Int, KlexirType.Bool));
    }

    [Fact]
    public void Evaluate_match_takes_the_Some_branch_and_binds_the_wrapped_value()
    {
        Run("match Some(5) with Some(x) => x | None => 0").Should().Be(new IntValue(5));
    }

    [Fact]
    public void Evaluate_match_takes_the_None_branch()
    {
        Run("match None<Int> with Some(x) => x | None => 0").Should().Be(new IntValue(0));
    }

    [Fact]
    public void Evaluate_match_takes_the_Ok_branch_and_binds_the_wrapped_value()
    {
        Run("match Ok<Bool>(5) with Ok(x) => x | Err(e) => 0").Should().Be(new IntValue(5));
    }

    [Fact]
    public void Evaluate_match_takes_the_Err_branch_and_binds_the_wrapped_error()
    {
        Run("match Err<Int>(true) with Ok(x) => x | Err(e) => if e then 1 else 0").Should().Be(new IntValue(1));
    }

    [Fact]
    public void Evaluate_map_transforms_a_Some_value()
    {
        Run("match map(Some(5), func(Int x) => x + 1) with Some(x) => x | None => 0")
            .Should().Be(new IntValue(6));
    }

    [Fact]
    public void Evaluate_map_passes_None_through_untouched()
    {
        Run("match map(None<Int>, func(Int x) => x + 1) with Some(x) => x | None => 99")
            .Should().Be(new IntValue(99));
    }

    [Fact]
    public void Evaluate_map_transforms_an_Ok_value_and_passes_Err_through_untouched()
    {
        Run("match map(Ok<Bool>(5), func(Int x) => x + 1) with Ok(x) => x | Err(e) => 0")
            .Should().Be(new IntValue(6));
        Run("match map(Err<Int>(true), func(Int x) => x + 1) with Ok(x) => x | Err(e) => 99")
            .Should().Be(new IntValue(99));
    }

    [Fact]
    public void Evaluate_bind_chains_a_Some_returning_function_and_short_circuits_on_None()
    {
        const string chain =
            "match bind(Some(5), func(Int x) => if x > 0 then Some(x * 2) else None<Int>) " +
            "with Some(x) => x | None => 0";
        Run(chain).Should().Be(new IntValue(10));

        Run("match bind(None<Int>, func(Int x) => Some(x * 2)) with Some(x) => x | None => 99")
            .Should().Be(new IntValue(99));
    }

    [Fact]
    public void Evaluate_bind_chains_railway_oriented_Result_computations_and_short_circuits_on_Err()
    {
        const string chain =
            "match bind(Ok<Bool>(5), func(Int x) => if x > 0 then Ok<Bool>(x * 2) else Err<Int>(false)) " +
            "with Ok(x) => x | Err(e) => 0";
        Run(chain).Should().Be(new IntValue(10));

        const string shortCircuit = "match bind(Err<Int>(true), func(Int x) => Ok<Bool>(x * 2)) " +
            "with Ok(x) => x | Err(e) => 99";
        Run(shortCircuit).Should().Be(new IntValue(99));
    }

    [Fact]
    public void Check_fails_when_match_arms_disagree_on_type()
    {
        Check("match Some(5) with Some(x) => x | None => true").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Check_fails_when_map_is_applied_to_a_non_container_value()
    {
        Check("map(5, func(Int x) => x + 1)").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Check_fails_when_bind_changes_the_Result_error_type()
    {
        Check("bind(Ok<Bool>(5), func(Int x) => Ok<Int>(x))").IsFailure.Should().BeTrue();
    }

    private static TypedExpr CheckSuccessfully(string source)
    {
        var result = Check(source);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    private static Result<TypedExpr> Check(string source)
    {
        var tokens = new Lexer(source).Tokenize();
        tokens.IsSuccess.Should().BeTrue();
        var ast = new Parser(tokens.Value).ParseExpression();
        ast.IsSuccess.Should().BeTrue();
        return new TypeChecker().Check(ast.Value);
    }

    private static KlexirValue Run(string source)
    {
        var typed = CheckSuccessfully(source);
        var result = new Evaluator().Evaluate(typed);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }
}
