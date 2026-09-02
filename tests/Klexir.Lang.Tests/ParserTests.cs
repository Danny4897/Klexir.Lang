using FluentAssertions;
using MonadicSharp;
using Xunit;

namespace Klexir.Lang.Tests;

public sealed class ParserTests
{
    [Fact]
    public void ParseExpression_parses_a_simple_addition()
    {
        var ast = ParseSuccessfully("5 + 3");

        ast.Should().Be(new BinaryExpr(BinaryOperator.Add, new IntLiteral(5), new IntLiteral(3)));
    }

    [Fact]
    public void ParseExpression_respects_multiplicative_precedence_over_additive()
    {
        var ast = ParseSuccessfully("1 + 2 * 3");

        ast.Should().Be(new BinaryExpr(
            BinaryOperator.Add,
            new IntLiteral(1),
            new BinaryExpr(BinaryOperator.Mul, new IntLiteral(2), new IntLiteral(3))));
    }

    [Fact]
    public void ParseExpression_lets_parentheses_override_precedence()
    {
        var ast = ParseSuccessfully("(1 + 2) * 3");

        ast.Should().Be(new BinaryExpr(
            BinaryOperator.Mul,
            new BinaryExpr(BinaryOperator.Add, new IntLiteral(1), new IntLiteral(2)),
            new IntLiteral(3)));
    }

    [Fact]
    public void ParseExpression_parses_let_in_matching_the_study_plan_example()
    {
        var ast = ParseSuccessfully("let x = 5 in x + 3");

        ast.Should().Be(new LetExpr(
            "x",
            new IntLiteral(5),
            new BinaryExpr(BinaryOperator.Add, new Identifier("x"), new IntLiteral(3))));
    }

    [Fact]
    public void ParseExpression_fails_on_a_dangling_operator()
    {
        Parse("1 +").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ParseExpression_fails_when_let_is_missing_the_equals_sign()
    {
        Parse("let x 5 in x").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ParseExpression_fails_on_a_trailing_token_after_a_complete_expression()
    {
        Parse("5 3").IsFailure.Should().BeTrue();
    }

    private static Expr ParseSuccessfully(string source)
    {
        var result = Parse(source);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    private static Result<Expr> Parse(string source)
    {
        var tokens = new Lexer(source).Tokenize();
        tokens.IsSuccess.Should().BeTrue();
        return new Parser(tokens.Value).ParseExpression();
    }
}
