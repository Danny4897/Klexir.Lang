using FluentAssertions;
using MonadicSharp;
using Xunit;

namespace Klexir.Lang.Tests;

public sealed class ListTests
{
    [Fact]
    public void Check_types_an_empty_list_from_its_explicit_type_argument()
    {
        CheckSuccessfully("[]<Int>").Type.Should().Be(new ListType(KlexirType.Int));
    }

    [Fact]
    public void Check_types_a_list_literal_from_its_elements()
    {
        CheckSuccessfully("[1, 2, 3]").Type.Should().Be(new ListType(KlexirType.Int));
    }

    [Fact]
    public void Check_fails_when_list_elements_disagree_on_type()
    {
        Check("[1, true]").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_returns_the_list_of_values()
    {
        Run("[1, 2, 3]").Should().Be(new ListValue([new IntValue(1), new IntValue(2), new IntValue(3)]));
    }

    [Fact]
    public void Evaluate_returns_an_empty_list()
    {
        Run("[]<Int>").Should().Be(new ListValue([]));
    }

    [Fact]
    public void Evaluate_map_transforms_every_element()
    {
        Run("map([1, 2, 3], fun (x: Int) => x * 2)")
            .Should().Be(new ListValue([new IntValue(2), new IntValue(4), new IntValue(6)]));
    }

    [Fact]
    public void Evaluate_filter_keeps_only_matching_elements()
    {
        Run("filter([1, 2, 3, 4], fun (x: Int) => x > 2)")
            .Should().Be(new ListValue([new IntValue(3), new IntValue(4)]));
    }

    [Fact]
    public void Evaluate_filter_on_an_empty_list_returns_empty()
    {
        Run("filter([]<Int>, fun (x: Int) => x > 2)").Should().Be(new ListValue([]));
    }

    [Fact]
    public void Evaluate_fold_reduces_the_list_to_a_single_value()
    {
        Run("fold([1, 2, 3, 4], 0, fun (acc: Int) => fun (x: Int) => acc + x)").Should().Be(new IntValue(10));
    }

    [Fact]
    public void Evaluate_fold_on_an_empty_list_returns_the_initial_value()
    {
        Run("fold([]<Int>, 99, fun (acc: Int) => fun (x: Int) => acc + x)").Should().Be(new IntValue(99));
    }

    [Fact]
    public void Check_fails_when_map_function_does_not_match_the_list_element_type()
    {
        Check("map([1, 2, 3], fun (x: Bool) => x)").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Check_fails_when_filter_predicate_does_not_return_Bool()
    {
        Check("filter([1, 2, 3], fun (x: Int) => x)").IsFailure.Should().BeTrue();
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
