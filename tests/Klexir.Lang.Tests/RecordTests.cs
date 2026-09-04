using FluentAssertions;
using MonadicSharp;
using Xunit;

namespace Klexir.Lang.Tests;

/// <summary>
/// User-defined record (product) types: <c>record NAME { Field: Type, ... }</c>, construction
/// <c>NAME { Field: expr, ... }</c> (field order doesn't matter), and access via <c>expr.Field</c>.
/// </summary>
public sealed class RecordTests
{
    private const string Declare = "record User { Id: Int, Age: Int };";

    [Fact]
    public void Evaluate_constructs_a_record_and_reads_a_field_back()
    {
        Run(Declare + "let u = User { Id: 1, Age: 25 }; u.Age").Should().Be(new IntValue(25));
    }

    [Fact]
    public void Evaluate_a_records_field_type_referencing_a_previously_declared_record()
    {
        // Regression: a field's type came straight from ParseTypeAnnotation's empty-Fields RecordType placeholder,
        // never resolved against the environment — so Line.From's declared type didn't match a real Point's type.
        var source = """
            record Point { X: Int, Y: Int };
            record Line { From: Point, To: Point };
            let line = Line { From: Point { X: 0, Y: 0 }, To: Point { X: 3, Y: 4 } };
            line.To.X
            """;

        Run(source).Should().Be(new IntValue(3));
    }

    [Fact]
    public void Evaluate_construction_does_not_care_about_field_order()
    {
        Run(Declare + "let u = User { Age: 30, Id: 2 }; u.Id").Should().Be(new IntValue(2));
        Run(Declare + "let u = User { Age: 30, Id: 2 }; u.Age").Should().Be(new IntValue(30));
    }

    [Fact]
    public void Evaluate_returns_a_structurally_equal_RecordValue()
    {
        Run(Declare + "User { Id: 1, Age: 25 }").Should().Be(
            new RecordValue("User", new Dictionary<string, KlexirValue> { ["Id"] = new IntValue(1), ["Age"] = new IntValue(25) }));
    }

    [Fact]
    public void Check_lets_a_function_take_a_record_parameter_and_access_its_fields()
    {
        var source = Declare + """
            let isAdult = func(User u) => u.Age >= 18;
            isAdult (User { Id: 1, Age: 25 })
            """;

        Run(source).Should().Be(new BoolValue(true));
    }

    [Fact]
    public void Check_fails_construction_missing_a_field()
    {
        Check(Declare + "User { Id: 1 }").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Check_fails_construction_with_an_unknown_field()
    {
        Check(Declare + "User { Id: 1, Age: 25, Nickname: 1 }").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Check_fails_construction_with_a_duplicated_field()
    {
        Check(Declare + "User { Id: 1, Id: 2 }").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Check_fails_construction_with_a_field_of_the_wrong_type()
    {
        Check(Declare + "User { Id: true, Age: 25 }").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Check_fails_accessing_an_undeclared_field()
    {
        Check(Declare + "User { Id: 1, Age: 25 }.Nickname").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Check_fails_accessing_a_field_on_a_non_record_value()
    {
        Check("(1).Age").IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Check_fails_constructing_an_undeclared_record_type()
    {
        Check("Ghost { X: 1 }").IsFailure.Should().BeTrue();
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
        var ast = new Parser(tokens.Value).ParseProgram();
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
