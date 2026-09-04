using FluentAssertions;
using MonadicSharp;
using Xunit;

namespace Klexir.Lang.Tests;

/// <summary>
/// <c>func(Type name, ...)</c> is surface sugar over currying, not a new calling convention: a multi-parameter
/// <c>func</c> desugars at parse time into nested <see cref="FunExpr"/> — application stays <c>f x y</c>, never
/// <c>f(x, y)</c>, and everything downstream (TypeChecker/Evaluator/Compiler) sees exactly the same tree it would
/// for hand-nested single-parameter functions.
/// </summary>
public sealed class FuncSyntaxTests
{
    [Fact]
    public void Parse_a_single_parameter_func_matches_the_old_fun_shape()
    {
        var ast = Parse("func(Int x) => x + 1");

        ast.Should().Be(new FunExpr("x", KlexirType.Int, new BinaryExpr(BinaryOperator.Add, new Identifier("x"), new IntLiteral(1))));
    }

    [Fact]
    public void Parse_a_two_parameter_func_desugars_to_nested_FunExpr()
    {
        var flat = Parse("func(Int x, Int y) => x + y");
        var nested = Parse("func(Int x) => func(Int y) => x + y");

        flat.Should().Be(nested);
    }

    [Fact]
    public void Evaluate_a_multi_parameter_func_still_applies_curried_one_argument_at_a_time()
    {
        Run("let add = func(Int x, Int y) => x + y in add 3 4").Should().Be(new IntValue(7));
    }

    [Fact]
    public void Evaluate_a_multi_parameter_func_supports_partial_application()
    {
        Run("let add = func(Int x, Int y) => x + y in let addFive = add 5 in addFive 3").Should().Be(new IntValue(8));
    }

    [Fact]
    public void Evaluate_a_multi_parameter_let_rec_recurses_with_an_accumulator()
    {
        const string source = """
            let rec sumTo = func(Int n, Int acc): Int =>
                if n == 0 then acc else sumTo (n - 1) (acc + n)
            in sumTo 5 0
            """;

        Run(source).Should().Be(new IntValue(15));
    }

    private static Expr Parse(string source)
    {
        var tokens = new Lexer(source).Tokenize();
        tokens.IsSuccess.Should().BeTrue();
        var ast = new Parser(tokens.Value).ParseExpression();
        ast.IsSuccess.Should().BeTrue();
        return ast.Value;
    }

    private static KlexirValue Run(string source)
    {
        var ast = Parse(source);
        var typed = new TypeChecker().Check(ast);
        typed.IsSuccess.Should().BeTrue();
        var result = new Evaluator().Evaluate(typed.Value);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }
}
