using FluentAssertions;
using MonadicSharp;
using Xunit;

namespace Klexir.Lang.Tests;

public sealed class EvaluatorTests
{
    [Fact]
    public void Evaluate_computes_arithmetic_respecting_precedence()
    {
        Run("2 + 3 * 4").Should().Be(new IntValue(14));
    }

    [Fact]
    public void Evaluate_resolves_let_bindings()
    {
        Run("let x = 5 in x + 1").Should().Be(new IntValue(6));
    }

    [Theory]
    [InlineData("if 1 < 2 then 10 else 20", 10)]
    [InlineData("if 1 > 2 then 10 else 20", 20)]
    public void Evaluate_takes_the_branch_selected_by_the_condition(string source, long expected)
    {
        Run(source).Should().Be(new IntValue(expected));
    }

    [Fact]
    public void Evaluate_applies_a_closure()
    {
        Run("let double = func(Int x) => x * 2 in double 21").Should().Be(new IntValue(42));
    }

    [Fact]
    public void Evaluate_supports_currying_via_captured_environment()
    {
        // 'add' returns a closure that captures x=3; the returned closure is then applied to y=4.
        Run("let add = func(Int x) => func(Int y) => x + y in add 3 4").Should().Be(new IntValue(7));
    }

    [Fact]
    public void Evaluate_two_calls_to_the_same_curried_function_do_not_share_captured_state()
    {
        Run("let add = func(Int x) => func(Int y) => x + y in (add 3 4) + (add 10 1)").Should().Be(new IntValue(18));
    }

    [Fact]
    public void Evaluate_fails_on_division_by_zero()
    {
        RunResult("1 / 0").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_end_to_end_from_source_text_through_lexer_parser_type_checker_and_evaluator()
    {
        Run("let square = func(Int x) => x * x in if square 5 > 20 then square 5 else 0")
            .Should().Be(new IntValue(25));
    }

    private static KlexirValue Run(string source)
    {
        var result = RunResult(source);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    private static Result<KlexirValue> RunResult(string source)
    {
        var tokens = new Lexer(source).Tokenize();
        tokens.IsSuccess.Should().BeTrue();
        var ast = new Parser(tokens.Value).ParseExpression();
        ast.IsSuccess.Should().BeTrue();
        var typed = new TypeChecker().Check(ast.Value);
        typed.IsSuccess.Should().BeTrue();
        return new Evaluator().Evaluate(typed.Value);
    }
}
