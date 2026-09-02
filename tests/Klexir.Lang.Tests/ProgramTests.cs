using FluentAssertions;
using MonadicSharp;
using Xunit;

namespace Klexir.Lang.Tests;

/// <summary>
/// A Klexir <em>program</em>: a sequence of top-level <c>let</c>/<c>let rec</c> declarations (no trailing <c>in</c>
/// needed between them) followed by a final expression — desugars to the exact same nested-let AST a single
/// <c>let ... in ...</c> expression already produces, so the type checker and evaluator need no changes at all.
/// </summary>
public sealed class ProgramTests
{
    [Fact]
    public void ParseProgram_desugars_top_level_lets_into_nested_LetExpr_without_requiring_in()
    {
        var ast = ParseSuccessfully("""
            let x = 1;
            let y = 2;
            x + y
            """);

        ast.Should().Be(new LetExpr("x", new IntLiteral(1),
            new LetExpr("y", new IntLiteral(2),
                new BinaryExpr(BinaryOperator.Add, new Identifier("x"), new Identifier("y")))));
    }

    [Fact]
    public void ParseProgram_supports_top_level_let_rec_declarations()
    {
        var ast = ParseSuccessfully("""
            let rec fact = fun (n: Int): Int => if n < 2 then 1 else n * fact (n - 1);
            fact 5
            """);

        ast.Should().Be(new LetRecExpr(
            "fact", "n", KlexirType.Int, KlexirType.Int,
            new IfExpr(
                new ComparisonExpr(ComparisonOperator.LessThan, new Identifier("n"), new IntLiteral(2)),
                new IntLiteral(1),
                new BinaryExpr(BinaryOperator.Mul, new Identifier("n"),
                    new AppExpr(new Identifier("fact"), new BinaryExpr(BinaryOperator.Sub, new Identifier("n"), new IntLiteral(1))))),
            new AppExpr(new Identifier("fact"), new IntLiteral(5))));
    }

    [Fact]
    public void ParseProgram_supports_a_program_that_is_just_a_single_final_expression()
    {
        ParseSuccessfully("1 + 2").Should().Be(new BinaryExpr(BinaryOperator.Add, new IntLiteral(1), new IntLiteral(2)));
    }

    [Fact]
    public void ParseProgram_fails_on_trailing_garbage_after_the_final_expression()
    {
        Parse("let x = 1;\nx x 1 2 3 =").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_a_layered_style_program_end_to_end()
    {
        const string program = """
            let findUserAge = fun (userId: Int) =>
                if userId == 1 then Some(17) else if userId == 2 then Some(25) else None<Int>;
            let toLookupResult = fun (age: Option<Int>) =>
                match age with Some(x) => Ok<Int>(x) | None => Err<Int>(1);
            let checkAdult = fun (age: Int) =>
                if age >= 18 then Ok<Int>(age) else Err<Int>(2);
            let getAdultAge = fun (userId: Int) =>
                bind(toLookupResult (findUserAge userId), checkAdult);
            match getAdultAge 2 with Ok(age) => age | Err(code) => code
            """;

        Run(program).Should().Be(new IntValue(25));
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
        return new Parser(tokens.Value).ParseProgram();
    }

    private static KlexirValue Run(string source)
    {
        var ast = ParseSuccessfully(source);
        var typed = new TypeChecker().Check(ast);
        typed.IsSuccess.Should().BeTrue();
        var result = new Evaluator().Evaluate(typed.Value);
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }
}
