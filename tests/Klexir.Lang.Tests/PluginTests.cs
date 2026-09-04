using System;
using System.Threading.Tasks;
using FluentAssertions;
using MonadicSharp;
using Xunit;

namespace Klexir.Lang.Tests;

public sealed class PluginTests
{
    [Fact]
    public void KlexirNativeFunctionDef_arity_counts_curried_parameters()
    {
        var zeroArg = new KlexirNativeFunctionDef("now", new FunctionType(KlexirType.Bool, KlexirType.Int),
            _ => Task.FromResult(Result<KlexirValue>.Success(new IntValue(0))));

        var twoArg = new KlexirNativeFunctionDef(
            "add", new FunctionType(KlexirType.Int, new FunctionType(KlexirType.Int, KlexirType.Int)),
            _ => Task.FromResult(Result<KlexirValue>.Success(new IntValue(0))));

        zeroArg.Arity.Should().Be(1);
        twoArg.Arity.Should().Be(2);
    }

    [Fact]
    public void TypeChecker_resolves_a_call_to_a_plugin_function()
    {
        var plugin = new FakePlugin([
            new KlexirNativeFunctionDef("triple", new FunctionType(KlexirType.Int, KlexirType.Int),
                args => Task.FromResult(Result<KlexirValue>.Success(new IntValue(((IntValue)args[0]).Value * 3)))),
        ]);

        var ast = Parse("triple 4");
        var typed = new TypeChecker().Check(ast, [plugin]);

        typed.IsSuccess.Should().BeTrue();
        typed.Value.Type.Should().Be(KlexirType.Int);
    }

    [Fact]
    public void TypeChecker_fails_when_two_plugins_declare_the_same_function_name()
    {
        KlexirNativeFunctionDef Def(string name) => new(name, new FunctionType(KlexirType.Int, KlexirType.Int),
            args => Task.FromResult(Result<KlexirValue>.Success(args[0])));

        var pluginA = new FakePlugin([Def("dup")], "A");
        var pluginB = new FakePlugin([Def("dup")], "B");

        var ast = Parse("dup 1");
        var typed = new TypeChecker().Check(ast, [pluginA, pluginB]);

        typed.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Evaluator_calls_a_zero_arg_plugin_function_and_returns_its_native_result()
    {
        var plugin = new FakePlugin([
            new KlexirNativeFunctionDef("answer", new FunctionType(KlexirType.Bool, KlexirType.Int),
                _ => Task.FromResult(Result<KlexirValue>.Success(new IntValue(42)))),
        ]);

        var typed = CheckSuccessfully("answer true", plugin);
        var result = await new Evaluator().EvaluateAsync(typed, [plugin]);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new IntValue(42));
    }

    [Fact]
    public async Task Evaluator_curries_a_two_arg_plugin_function()
    {
        var plugin = new FakePlugin([
            new KlexirNativeFunctionDef("add", new FunctionType(KlexirType.Int, new FunctionType(KlexirType.Int, KlexirType.Int)),
                args => Task.FromResult(Result<KlexirValue>.Success(
                    new IntValue(((IntValue)args[0]).Value + ((IntValue)args[1]).Value)))),
        ]);

        var typed = CheckSuccessfully("let addFive = add 5 in addFive 8", plugin);
        var result = await new Evaluator().EvaluateAsync(typed, [plugin]);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new IntValue(13));
    }

    [Fact]
    public async Task Evaluator_translates_a_native_function_exception_into_a_failed_result_instead_of_throwing()
    {
        var plugin = new FakePlugin([
            new KlexirNativeFunctionDef("boom", new FunctionType(KlexirType.Int, KlexirType.Int),
                _ => throw new InvalidOperationException("native failure")),
        ]);

        var typed = CheckSuccessfully("boom 1", plugin);
        var result = await new Evaluator().EvaluateAsync(typed, [plugin]);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Evaluator_lets_a_native_function_be_used_as_the_folder_argument_to_fold()
    {
        var plugin = new FakePlugin([
            new KlexirNativeFunctionDef("add", new FunctionType(KlexirType.Int, new FunctionType(KlexirType.Int, KlexirType.Int)),
                args => Task.FromResult(Result<KlexirValue>.Success(
                    new IntValue(((IntValue)args[0]).Value + ((IntValue)args[1]).Value)))),
        ]);

        var typed = CheckSuccessfully("fold([1, 2, 3], 0, add)", plugin);
        var result = await new Evaluator().EvaluateAsync(typed, [plugin]);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new IntValue(6));
    }

    [Fact]
    public async Task Plugin_opaque_type_round_trips_through_a_Klexir_function_parameter()
    {
        var widget = new OpaqueType("Widget");
        var plugin = new FakePlugin(
            functions:
            [
                new KlexirNativeFunctionDef("makeWidget", new FunctionType(KlexirType.Int, widget),
                    args => Task.FromResult(Result<KlexirValue>.Success(new NativeValue(((IntValue)args[0]).Value, widget)))),
                new KlexirNativeFunctionDef("widgetToInt", new FunctionType(widget, KlexirType.Int),
                    args => Task.FromResult(Result<KlexirValue>.Success(new IntValue((long)((NativeValue)args[0]).Payload)))),
            ],
            types: [widget]);

        var typed = CheckSuccessfully(
            "let useWidget = fun (w: Widget) => widgetToInt w in useWidget (makeWidget 7)", plugin);
        var result = await new Evaluator().EvaluateAsync(typed, [plugin]);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new IntValue(7));
    }

    [Fact]
    public void Plugin_opaque_type_value_is_rejected_where_an_Int_is_expected()
    {
        var widget = new OpaqueType("Widget");
        var plugin = new FakePlugin(
            functions:
            [
                new KlexirNativeFunctionDef("makeWidget", new FunctionType(KlexirType.Int, widget),
                    args => Task.FromResult(Result<KlexirValue>.Success(new NativeValue(((IntValue)args[0]).Value, widget)))),
            ],
            types: [widget]);

        var ast = Parse("makeWidget 7 + 1");
        var typed = new TypeChecker().Check(ast, [plugin]);

        typed.IsFailure.Should().BeTrue();
    }

    private static TypedExpr CheckSuccessfully(string source, IKlexirPlugin plugin)
    {
        var ast = Parse(source);
        var typed = new TypeChecker().Check(ast, [plugin]);
        typed.IsSuccess.Should().BeTrue();
        return typed.Value;
    }

    private static Expr Parse(string source)
    {
        var tokens = new Lexer(source).Tokenize();
        tokens.IsSuccess.Should().BeTrue();
        var ast = new Parser(tokens.Value).ParseExpression();
        ast.IsSuccess.Should().BeTrue();
        return ast.Value;
    }

    private sealed class FakePlugin(
        IReadOnlyList<KlexirNativeFunctionDef> functions, string name = "Fake", IReadOnlyList<OpaqueType>? types = null)
        : IKlexirPlugin
    {
        public string Name => name;

        public IReadOnlyList<OpaqueType> Types => types ?? [];

        public IReadOnlyList<KlexirNativeFunctionDef> Functions => functions;
    }
}
