using FluentAssertions;
using MonadicSharp;
using Xunit;

namespace Klexir.Lang.Tests;

public sealed class ClosureTests
{
    [Fact]
    public void ParseExpression_parses_a_function_literal()
    {
        var ast = ParseSuccessfully("fun (x: Int) => x + 1");

        ast.Should().Be(new FunExpr(
            "x", KlexirType.Int, new BinaryExpr(BinaryOperator.Add, new Identifier("x"), new IntLiteral(1))));
    }

    [Fact]
    public void ParseExpression_parses_function_application_by_juxtaposition()
    {
        var ast = ParseSuccessfully("f 5");

        ast.Should().Be(new AppExpr(new Identifier("f"), new IntLiteral(5)));
    }

    [Fact]
    public void ParseExpression_parses_application_tighter_than_addition()
    {
        var ast = ParseSuccessfully("f x + 1");

        ast.Should().Be(new BinaryExpr(
            BinaryOperator.Add, new AppExpr(new Identifier("f"), new Identifier("x")), new IntLiteral(1)));
    }

    [Fact]
    public void Check_types_a_function_literal_as_a_function_type()
    {
        var typed = CheckSuccessfully("fun (x: Int) => x + 1");

        typed.Type.Should().Be(new FunctionType(KlexirType.Int, KlexirType.Int));
    }

    [Fact]
    public void Check_types_a_function_application_by_the_functions_return_type()
    {
        var typed = CheckSuccessfully("let f = fun (x: Int) => x + 1 in f 5");

        typed.Type.Should().Be(KlexirType.Int);
    }

    [Fact]
    public void Check_fails_applying_a_non_function_value()
    {
        Check("1 2").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Check_fails_applying_a_function_to_an_argument_of_the_wrong_type()
    {
        Check("let f = fun (x: Int) => x in f true").IsFailure.Should().BeTrue();
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
