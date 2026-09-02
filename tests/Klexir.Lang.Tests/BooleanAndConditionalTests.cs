using FluentAssertions;
using MonadicSharp;
using Xunit;

namespace Klexir.Lang.Tests;

public sealed class BooleanAndConditionalTests
{
    [Fact]
    public void Tokenize_recognizes_two_character_comparison_operators_distinct_from_their_single_character_forms()
    {
        var result = new Lexer("1 == 2 <= 3 >= 4 < 5 > 6").Tokenize();

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(t => t.Type).Should().Equal(
            TokenType.Int, TokenType.EqualsEquals, TokenType.Int, TokenType.LessEquals, TokenType.Int,
            TokenType.GreaterEquals, TokenType.Int, TokenType.Less, TokenType.Int, TokenType.Greater, TokenType.Int,
            TokenType.Eof);
    }

    [Fact]
    public void Tokenize_recognizes_boolean_and_if_keywords()
    {
        var result = new Lexer("if true then 1 else 2").Tokenize();

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(t => t.Type).Should().Equal(
            TokenType.If, TokenType.True, TokenType.Then, TokenType.Int, TokenType.Else, TokenType.Int, TokenType.Eof);
    }

    [Fact]
    public void ParseExpression_parses_boolean_literals()
    {
        ParseSuccessfully("true").Should().Be(new BoolLiteral(true));
        ParseSuccessfully("false").Should().Be(new BoolLiteral(false));
    }

    [Fact]
    public void ParseExpression_parses_a_comparison()
    {
        var ast = ParseSuccessfully("1 < 2");

        ast.Should().Be(new ComparisonExpr(ComparisonOperator.LessThan, new IntLiteral(1), new IntLiteral(2)));
    }

    [Fact]
    public void ParseExpression_parses_if_then_else()
    {
        var ast = ParseSuccessfully("if 1 < 2 then 10 else 20");

        ast.Should().Be(new IfExpr(
            new ComparisonExpr(ComparisonOperator.LessThan, new IntLiteral(1), new IntLiteral(2)),
            new IntLiteral(10),
            new IntLiteral(20)));
    }

    [Fact]
    public void Check_types_a_comparison_as_Bool()
    {
        var typed = CheckSuccessfully("1 < 2");

        typed.Type.Should().Be(KlexirType.Bool);
    }

    [Fact]
    public void Check_types_an_if_expression_by_its_branches_common_type()
    {
        var typed = CheckSuccessfully("if true then 1 else 2");

        typed.Type.Should().Be(KlexirType.Int);
    }

    [Fact]
    public void Check_fails_when_if_branches_have_different_types()
    {
        Check("if true then 1 else false").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Check_fails_when_the_if_condition_is_not_Bool()
    {
        Check("if 1 then 2 else 3").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Check_fails_when_a_comparison_operand_is_not_Int()
    {
        Check("1 < true").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Check_fails_when_an_arithmetic_operand_is_not_Int()
    {
        Check("1 + true").IsFailure.Should().BeTrue();
    }

    private static Expr ParseSuccessfully(string source)
    {
        var tokens = new Lexer(source).Tokenize();
        tokens.IsSuccess.Should().BeTrue();
        var result = new Parser(tokens.Value).ParseExpression();
        result.IsSuccess.Should().BeTrue();
        return result.Value;
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
}
