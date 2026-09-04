using FluentAssertions;
using Klexir.Runtime;
using MonadicSharp;
using Xunit;

namespace Klexir.Lang.Tests;

public sealed class CompilerTests
{
    [Fact]
    public void Compile_and_run_an_int_literal()
    {
        RunCompiled("42").Should().Be(42);
    }

    [Theory]
    [InlineData("true", 1)]
    [InlineData("false", 0)]
    public void Compile_and_run_a_bool_literal(string source, long expected)
    {
        RunCompiled(source).Should().Be(expected);
    }

    [Theory]
    [InlineData("2 + 3", 5)]
    [InlineData("10 - 4", 6)]
    [InlineData("6 * 7", 42)]
    [InlineData("20 / 4", 5)]
    public void Compile_and_run_arithmetic(string source, long expected)
    {
        RunCompiled(source).Should().Be(expected);
    }

    [Theory]
    [InlineData("1 < 2", 1)]
    [InlineData("2 > 1", 1)]
    [InlineData("2 == 2", 1)]
    [InlineData("2 <= 2", 1)]
    [InlineData("2 >= 3", 0)]
    public void Compile_and_run_comparisons(string source, long expected)
    {
        RunCompiled(source).Should().Be(expected);
    }

    [Theory]
    [InlineData("if true then 1 else 2", 1)]
    [InlineData("if false then 1 else 2", 2)]
    [InlineData("if 3 > 1 then 100 else 200", 100)]
    public void Compile_and_run_if(string source, long expected)
    {
        RunCompiled(source).Should().Be(expected);
    }

    [Fact]
    public void Compile_and_run_a_let_binding_and_identifier_lookup()
    {
        RunCompiled("let x = 10 in x + 5").Should().Be(15);
    }

    [Fact]
    public void Compile_and_run_nested_lets()
    {
        RunCompiled("let a = 3 in let b = 4 in a * b").Should().Be(12);
    }

    [Fact]
    public void Compile_and_run_a_non_capturing_closure_application()
    {
        RunCompiled("let f = func(Int x) => x + 1 in f 41").Should().Be(42);
    }

    [Fact]
    public void Compile_and_run_a_capturing_closure()
    {
        RunCompiled("let x = 10 in let f = func(Int y) => x + y in f 5").Should().Be(15);
    }

    [Fact]
    public void Compile_and_run_currying()
    {
        RunCompiled("let add = func(Int x) => func(Int y) => x + y in add 3 4").Should().Be(7);
    }

    [Fact]
    public void Compile_and_run_recursive_factorial_via_let_rec()
    {
        RunCompiled("let rec fact = func(Int n): Int => if n < 2 then 1 else n * fact (n - 1) in fact 5").Should().Be(120);
    }

    [Fact]
    public void Compile_and_run_recursion_with_a_captured_variable()
    {
        // proves let rec's closure can both recurse via its own handle AND read an outer capture.
        RunCompiled(
            "let step = 2 in let rec countUp = func(Int n): Int => if n >= 10 then n else countUp (n + step) in countUp 0")
            .Should().Be(10);
    }

    private static long RunCompiled(string source)
    {
        var typed = CheckSuccessfully(source);
        var compiled = Compiler.Compile(typed);
        compiled.IsSuccess.Should().BeTrue();

        var result = new KlexirVm(compiled.Value.Code, compiled.Value.EntryPoint).Run();
        result.IsSuccess.Should().BeTrue();
        return result.Value;
    }

    private static TypedExpr CheckSuccessfully(string source)
    {
        var tokens = new Lexer(source).Tokenize();
        tokens.IsSuccess.Should().BeTrue();
        var ast = new Parser(tokens.Value).ParseExpression();
        ast.IsSuccess.Should().BeTrue();
        var typed = new TypeChecker().Check(ast.Value);
        typed.IsSuccess.Should().BeTrue();
        return typed.Value;
    }
}
