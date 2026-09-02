using FluentAssertions;
using MonadicSharp;
using Xunit;

namespace Klexir.Lang.Tests;

public sealed class StringTests
{
    [Fact]
    public void Tokenize_reads_a_string_literal()
    {
        var tokens = new Lexer("\"hello\"").Tokenize();
        tokens.IsSuccess.Should().BeTrue();
        tokens.Value.Should().Contain(t => t.Type == TokenType.String && t.Text == "hello");
    }

    [Fact]
    public void Tokenize_unescapes_quote_backslash_and_newline_escapes()
    {
        var tokens = new Lexer("\"a\\\"b\\\\c\\nd\"").Tokenize();
        tokens.IsSuccess.Should().BeTrue();
        tokens.Value.Should().Contain(t => t.Type == TokenType.String && t.Text == "a\"b\\c\nd");
    }

    [Fact]
    public void Tokenize_fails_on_an_unterminated_string()
    {
        new Lexer("\"unterminated").Tokenize().IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Check_types_a_string_literal_as_String()
    {
        CheckSuccessfully("\"hi\"").Type.Should().Be(KlexirType.String);
    }

    [Fact]
    public void Evaluate_returns_the_string_value()
    {
        Run("\"hi\"").Should().Be(new StringValue("hi"));
    }

    [Fact]
    public void Evaluate_concatenates_strings_with_plus()
    {
        Run("\"foo\" + \"bar\"").Should().Be(new StringValue("foobar"));
    }

    [Fact]
    public void Check_fails_mixing_String_and_Int_with_plus()
    {
        Check("\"foo\" + 1").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_compares_strings_for_structural_equality()
    {
        Run("\"foo\" == \"foo\"").Should().Be(new BoolValue(true));
        Run("\"foo\" == \"bar\"").Should().Be(new BoolValue(false));
    }

    [Fact]
    public void Check_fails_ordering_comparison_between_strings()
    {
        Check("\"foo\" < \"bar\"").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_lets_bind_and_use_a_string()
    {
        Run("let greeting = \"hi \" + \"there\" in greeting == \"hi there\"").Should().Be(new BoolValue(true));
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
