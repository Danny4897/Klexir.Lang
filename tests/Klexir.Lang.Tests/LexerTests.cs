using FluentAssertions;
using Xunit;

namespace Klexir.Lang.Tests;

public sealed class LexerTests
{
    [Fact]
    public void Tokenize_produces_the_expected_token_sequence_for_let_in()
    {
        var result = new Lexer("let x = 5 in x + 3").Tokenize();

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(t => t.Type).Should().Equal(
            TokenType.Let, TokenType.Identifier, TokenType.Equals, TokenType.Int,
            TokenType.In, TokenType.Identifier, TokenType.Plus, TokenType.Int, TokenType.Eof);
    }

    [Fact]
    public void Tokenize_reads_multi_digit_integers_and_multi_character_identifiers()
    {
        var result = new Lexer("total_1 * 42").Tokenize();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().SatisfyRespectively(
            t => (t.Type, t.Text).Should().Be((TokenType.Identifier, "total_1")),
            t => (t.Type, t.Text).Should().Be((TokenType.Star, "*")),
            t => (t.Type, t.Text).Should().Be((TokenType.Int, "42")),
            t => t.Type.Should().Be(TokenType.Eof));
    }

    [Fact]
    public void Tokenize_fails_on_an_unexpected_character()
    {
        var result = new Lexer("1 @ 2").Tokenize();

        result.IsFailure.Should().BeTrue();
    }
}
