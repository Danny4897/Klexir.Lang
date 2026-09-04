using System.Threading.Tasks;
using FluentAssertions;
using Klexir.Lang.Plugins;
using MonadicSharp;
using Xunit;

namespace Klexir.Lang.Tests;

public sealed class ClockPluginTests
{
    private static readonly ClockPlugin Plugin = new();

    [Fact]
    public async Task Now_returns_a_positive_Unix_millisecond_timestamp()
    {
        var result = await Run("now true");

        result.IsSuccess.Should().BeTrue();
        ((IntValue)result.Value).Value.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Delay_awaits_then_returns_the_millisecond_count_it_was_given()
    {
        var result = await Run("delay 5");

        result.Should().Be(Result<KlexirValue>.Success(new IntValue(5)));
    }

    [Fact]
    public async Task Delay_rejects_a_negative_millisecond_count()
    {
        var result = await Run("delay (0 - 1)");

        result.IsFailure.Should().BeTrue();
    }

    private static async Task<Result<KlexirValue>> Run(string source)
    {
        var tokens = new Lexer(source).Tokenize();
        tokens.IsSuccess.Should().BeTrue();
        var ast = new Parser(tokens.Value).ParseExpression();
        ast.IsSuccess.Should().BeTrue();
        var typed = new TypeChecker().Check(ast.Value, [Plugin]);
        typed.IsSuccess.Should().BeTrue();
        return await new Evaluator().EvaluateAsync(typed.Value, [Plugin]);
    }
}
