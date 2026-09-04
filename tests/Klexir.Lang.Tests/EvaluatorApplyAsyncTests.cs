using FluentAssertions;
using Xunit;

namespace Klexir.Lang.Tests;

/// <summary>
/// <see cref="Evaluator.ApplyAsync"/> exists so a host (the HTTP server, for one) can get a closure back from
/// evaluating a program once, then call it again for each new argument without re-running the whole program.
/// </summary>
public sealed class EvaluatorApplyAsyncTests
{
    [Fact]
    public async Task ApplyAsync_calls_a_closure_returned_by_evaluation()
    {
        var ast = Parse("func(Int x) => x + 1");
        var typed = new TypeChecker().Check(ast);
        typed.IsSuccess.Should().BeTrue();

        var evaluator = new Evaluator();
        var closureResult = evaluator.Evaluate(typed.Value);
        closureResult.IsSuccess.Should().BeTrue();

        var applied = await evaluator.ApplyAsync(closureResult.Value, new IntValue(41));

        applied.IsSuccess.Should().BeTrue();
        applied.Value.Should().Be(new IntValue(42));
    }

    [Fact]
    public async Task ApplyAsync_can_be_called_more_than_once_against_the_same_closure()
    {
        var ast = Parse("func(Int x) => x * 2");
        var typed = new TypeChecker().Check(ast);
        var evaluator = new Evaluator();
        var closure = evaluator.Evaluate(typed.Value).Value;

        (await evaluator.ApplyAsync(closure, new IntValue(3))).Value.Should().Be(new IntValue(6));
        (await evaluator.ApplyAsync(closure, new IntValue(10))).Value.Should().Be(new IntValue(20));
    }

    private static Expr Parse(string source)
    {
        var tokens = new Lexer(source).Tokenize();
        tokens.IsSuccess.Should().BeTrue();
        var ast = new Parser(tokens.Value).ParseExpression();
        ast.IsSuccess.Should().BeTrue();
        return ast.Value;
    }
}
