using FluentAssertions;
using MonadicSharp;
using Xunit;

namespace Klexir.Lang.Tests;

public sealed class TypeCheckerTests
{
    [Fact]
    public void Check_succeeds_for_a_well_typed_arithmetic_expression()
    {
        var typed = CheckSuccessfully("5 + 3");

        typed.Type.Should().Be(KlexirType.Int);
        typed.Should().Be(new TypedBinaryExpr(
            BinaryOperator.Add, new TypedIntLiteral(5), new TypedIntLiteral(3), KlexirType.Int));
    }

    [Fact]
    public void Check_resolves_a_let_bound_identifiers_type_inside_the_body()
    {
        var typed = CheckSuccessfully("let x = 5 in x + 3");

        typed.Should().Be(new TypedLetExpr(
            "x",
            new TypedIntLiteral(5),
            new TypedBinaryExpr(BinaryOperator.Add, new TypedIdentifier("x", KlexirType.Int), new TypedIntLiteral(3), KlexirType.Int),
            KlexirType.Int));
    }

    [Fact]
    public void Check_fails_for_an_unbound_identifier()
    {
        var result = Check("x + 1");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Check_does_not_leak_a_lets_binding_outside_its_own_body()
    {
        var result = Check("(let x = 1 in x) + x");

        result.IsFailure.Should().BeTrue();
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
