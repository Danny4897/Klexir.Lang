using Klexir.Lang;
using MonadicSharp;

namespace Klexir.Cli;

/// <summary>
/// Marshals between a real HTTP request/response and the record shapes a Klexir handler function works with. Pure
/// on purpose — no <c>HttpListener</c> types here — so it's testable without a real socket; <c>Program.cs</c>'s
/// <c>serve</c> command is the thin, mostly-untested shell around this that actually owns the listener.
/// </summary>
public static class HttpBridge
{
    public const string RequestTypeName = "HttpRequest";
    public const string ResponseTypeName = "HttpResponse";

    public static KlexirValue ToRequestRecord(string method, string path, string body) =>
        new RecordValue(RequestTypeName, new Dictionary<string, KlexirValue>
        {
            ["Method"] = new StringValue(method),
            ["Path"] = new StringValue(path),
            ["Body"] = new StringValue(body),
        });

    public static Result<(int Status, string Body)> FromResponseRecord(KlexirValue value)
    {
        if (value is not RecordValue record || record.TypeName != ResponseTypeName)
        {
            return Result<(int, string)>.Failure(Error.Create(
                $"Handler must return an '{ResponseTypeName}' record, got {DescribeShape(value)}."));
        }

        if (!record.Fields.TryGetValue("Status", out var statusValue) || statusValue is not IntValue status)
        {
            return Result<(int, string)>.Failure(Error.Create($"'{ResponseTypeName}.Status' must be an Int field."));
        }

        if (!record.Fields.TryGetValue("Body", out var bodyValue) || bodyValue is not StringValue body)
        {
            return Result<(int, string)>.Failure(Error.Create($"'{ResponseTypeName}.Body' must be a String field."));
        }

        return Result<(int, string)>.Success(((int)status.Value, body.Value));
    }

    private static string DescribeShape(KlexirValue value) => value switch
    {
        RecordValue record => $"record '{record.TypeName}'",
        _ => value.GetType().Name,
    };
}
