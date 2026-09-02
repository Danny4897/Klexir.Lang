using FluentAssertions;
using MonadicSharp;
using Xunit;

namespace Klexir.Lang.Tests;

/// <summary>
/// User-defined union (sum) types: <c>union NAME { Variant1(T1, T2), Variant2, ... };</c>. Variants with fields
/// construct via ordinary curried application (<c>Circle 4</c>, <c>Rectangle 3 5</c>) since Klexir's calls are
/// already juxtaposition; a zero-field variant is a bare value. <c>match</c> is exhaustive over all variants,
/// in declaration order, with positional binders per variant.
/// </summary>
public sealed class UnionTests
{
    private const string TrafficLight = "union TrafficLight { Red, Yellow, Green };";
    private const string Shape = "union Shape { Circle(Int), Rectangle(Int, Int) };";

    [Fact]
    public void Evaluate_matches_a_zero_field_variant()
    {
        Run(TrafficLight + "match Red with Red => 0 | Yellow => 1 | Green => 2").Should().Be(new IntValue(0));
        Run(TrafficLight + "match Green with Red => 0 | Yellow => 1 | Green => 2").Should().Be(new IntValue(2));
    }

    [Fact]
    public void Evaluate_a_function_over_a_zero_field_variant_union()
    {
        var source = TrafficLight + """
            let next = fun (l: TrafficLight) => match l with Red => Green | Yellow => Red | Green => Yellow;
            match next Red with Red => 0 | Yellow => 1 | Green => 2
            """;

        Run(source).Should().Be(new IntValue(2));
    }

    [Fact]
    public void Evaluate_constructs_a_single_field_variant_via_plain_application()
    {
        var source = Shape + """
            let area = fun (s: Shape) => match s with Circle(r) => r * r * 3 | Rectangle(w, h) => w * h;
            area (Circle 4)
            """;

        Run(source).Should().Be(new IntValue(48));
    }

    [Fact]
    public void Evaluate_constructs_a_multi_field_variant_via_curried_application()
    {
        var source = Shape + """
            let area = fun (s: Shape) => match s with Circle(r) => r * r * 3 | Rectangle(w, h) => w * h;
            area (Rectangle 3 5)
            """;

        Run(source).Should().Be(new IntValue(15));
    }

    [Fact]
    public void Check_fails_when_a_match_omits_a_variant()
    {
        Check(TrafficLight + "match Red with Red => 0 | Yellow => 1").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Check_fails_when_a_match_lists_an_unknown_variant()
    {
        Check(TrafficLight + "match Red with Red => 0 | Yellow => 1 | Purple => 2").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Check_fails_when_a_match_repeats_a_variant()
    {
        Check(TrafficLight + "match Red with Red => 0 | Red => 1 | Green => 2").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Check_fails_when_a_variant_pattern_has_the_wrong_number_of_binders()
    {
        Check(Shape + "match Circle(4) with Circle(r, extra) => r | Rectangle(w, h) => w").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Check_fails_when_match_arms_disagree_on_type()
    {
        Check(TrafficLight + "match Red with Red => 0 | Yellow => true | Green => 2").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Check_fails_applying_a_constructor_to_the_wrong_argument_type()
    {
        Check(Shape + "Circle true").IsFailure.Should().BeTrue();
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
        var ast = new Parser(tokens.Value).ParseProgram();
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
