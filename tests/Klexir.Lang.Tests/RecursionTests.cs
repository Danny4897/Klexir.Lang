using FluentAssertions;
using MonadicSharp;
using Xunit;

namespace Klexir.Lang.Tests;

public sealed class RecursionTests
{
    private const string Factorial =
        "let rec fact = func(Int n): Int => if n < 2 then 1 else n * fact (n - 1) in fact 5";

    [Fact]
    public void ParseExpression_parses_let_rec()
    {
        var ast = ParseSuccessfully(Factorial);

        ast.Should().Be(new LetRecExpr(
            "fact",
            "n",
            KlexirType.Int,
            KlexirType.Int,
            new IfExpr(
                new ComparisonExpr(ComparisonOperator.LessThan, new Identifier("n"), new IntLiteral(2)),
                new IntLiteral(1),
                new BinaryExpr(BinaryOperator.Mul, new Identifier("n"), new AppExpr(new Identifier("fact"), new BinaryExpr(BinaryOperator.Sub, new Identifier("n"), new IntLiteral(1))))),
            new AppExpr(new Identifier("fact"), new IntLiteral(5))));
    }

    [Fact]
    public void Check_types_let_rec_and_allows_the_function_to_call_itself()
    {
        var typed = CheckSuccessfully(Factorial);

        typed.Type.Should().Be(KlexirType.Int);
    }

    [Fact]
    public void Check_fails_when_the_declared_return_type_does_not_match_the_bodys_type()
    {
        Check("let rec f = func(Int n): Bool => n in f 1").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Check_fails_for_self_reference_inside_a_plain_non_recursive_let()
    {
        // Plain 'let' does not bind the name inside its own value — only 'let rec' does.
        Check("let fact = func(Int n) => if n < 2 then 1 else n * fact (n - 1) in fact 5")
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_computes_a_recursive_factorial()
    {
        Run(Factorial).Should().Be(new IntValue(120));
    }

    [Fact]
    public void Evaluate_each_call_to_a_recursive_function_gets_its_own_activation()
    {
        const string source =
            "let rec sum = func(Int n): Int => if n == 0 then 0 else n + sum (n - 1) in sum 10 + sum 5";

        Run(source).Should().Be(new IntValue(55 + 15));
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

    private static KlexirValue Run(string source)
    {
        var typed = CheckSuccessfully(source);
        var result = new Evaluator().Evaluate(typed);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }
}
