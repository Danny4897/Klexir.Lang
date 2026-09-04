using FluentAssertions;
using Klexir.Cli;
using Xunit;

namespace Klexir.Lang.Tests;

public sealed class HttpBridgeTests
{
    [Fact]
    public void ToRequestRecord_builds_an_HttpRequest_record_with_the_expected_fields()
    {
        var record = HttpBridge.ToRequestRecord("POST", "/users", "{}");

        record.Should().Be(new RecordValue("HttpRequest", new Dictionary<string, KlexirValue>
        {
            ["Method"] = new StringValue("POST"),
            ["Path"] = new StringValue("/users"),
            ["Body"] = new StringValue("{}"),
        }));
    }

    [Fact]
    public void FromResponseRecord_extracts_status_and_body_from_an_HttpResponse_record()
    {
        var response = new RecordValue("HttpResponse", new Dictionary<string, KlexirValue>
        {
            ["Status"] = new IntValue(201),
            ["Body"] = new StringValue("created"),
        });

        var result = HttpBridge.FromResponseRecord(response);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be((201, "created"));
    }

    [Fact]
    public void FromResponseRecord_fails_when_the_value_is_not_an_HttpResponse_record()
    {
        HttpBridge.FromResponseRecord(new IntValue(42)).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void FromResponseRecord_fails_when_the_record_has_the_wrong_type_name()
    {
        var wrongType = new RecordValue("SomethingElse", new Dictionary<string, KlexirValue>
        {
            ["Status"] = new IntValue(200),
            ["Body"] = new StringValue(""),
        });

        HttpBridge.FromResponseRecord(wrongType).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void FromResponseRecord_fails_when_Status_is_not_an_Int()
    {
        var badStatus = new RecordValue("HttpResponse", new Dictionary<string, KlexirValue>
        {
            ["Status"] = new StringValue("200"),
            ["Body"] = new StringValue(""),
        });

        HttpBridge.FromResponseRecord(badStatus).IsFailure.Should().BeTrue();
    }
}
