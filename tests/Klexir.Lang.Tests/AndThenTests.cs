using FluentAssertions;
using MonadicSharp;
using Xunit;

namespace Klexir.Lang.Tests;

/// <summary>
/// <c>andThen</c> is pure sugar over <c>bind</c> — <c>a andThen f</c> parses to the exact same
/// <see cref="BindExpr"/> node as <c>bind(a, f)</c>, so it reads left-to-right instead of nested-parens,
/// short-circuiting on the first <c>Err</c>/<c>None</c> exactly like <c>bind</c> already does.
/// </summary>
public sealed class AndThenTests
{
    [Fact]
    public void Tokenize_recognizes_andThen_as_a_keyword()
    {
        var result = new Lexer("x andThen f").Tokenize();

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(t => t.Type).Should().Equal(
            TokenType.Identifier, TokenType.AndThen, TokenType.Identifier, TokenType.Eof);
    }

    [Fact]
    public void Parse_desugars_andThen_to_the_same_BindExpr_as_bind()
    {
        var andThen = Parse("checkAdult user andThen checkCf");
        var bind = Parse("bind(checkAdult user, checkCf)");

        andThen.Should().Be(bind);
    }

    [Fact]
    public void Parse_left_associates_a_chain_of_andThen()
    {
        var chained = Parse("a andThen b andThen c");
        var nested = Parse("bind(bind(a, b), c)");

        chained.Should().Be(nested);
    }

    [Fact]
    public void Evaluate_runs_a_validation_pipeline_short_circuiting_on_the_first_error()
    {
        const string source = """
            let checkAdult = func(Int age) => if age >= 18 then Ok<Bool>(age) else Err<Int>(true) in
            let checkPositive = func(Int age) => if age > 0 then Ok<Bool>(age) else Err<Int>(false) in
            match (checkAdult 25 andThen checkPositive) with Ok(x) => x | Err(e) => 0 - 1
            """;

        Run(source).Should().Be(new IntValue(25));
    }

    [Fact]
    public void Evaluate_short_circuits_andThen_at_the_first_failing_step()
    {
        const string source = """
            let checkAdult = func(Int age) => if age >= 18 then Ok<Bool>(age) else Err<Int>(true) in
            let checkPositive = func(Int age) => if age > 0 then Ok<Bool>(age) else Err<Int>(false) in
            match (checkAdult 5 andThen checkPositive) with Ok(x) => x | Err(e) => 0 - 1
            """;

        Run(source).Should().Be(new IntValue(0 - 1));
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
