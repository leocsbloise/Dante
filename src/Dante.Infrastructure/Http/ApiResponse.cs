using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Dante.Infrastructure;

public sealed record ApiResponse<T>(
    string Message,
    int Status,
    [property: JsonPropertyName("operation_id")] string OperationId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] T? Data);

public static class ApiResponse
{
    public const string FieldMessage = "Some information needs to be corrected.";

    public const string UnexpectedMessage = "Something went wrong.";

    public const string SignInMessage = "Sign in to continue.";

    public static ApiResponse<T> WithData<T>(int status, string message, T data) =>
        new(message, status, OperationIds.Current(), data);

    public static ApiResponse<object?> WithoutData(int status, string message) =>
        new(message, status, OperationIds.Current(), null);

    public static ApiResponse<IReadOnlyDictionary<string, string>> ForFields(
        int status,
        IReadOnlyList<string> fields,
        string fieldMessage)
    {
        var data = fields.ToDictionary(field => field, _ => fieldMessage);
        return WithData(status, FieldMessage, (IReadOnlyDictionary<string, string>)data);
    }
}

public static class OperationIds
{
    public static string Current()
    {
        var traceId = Activity.Current?.TraceId ?? default;
        return traceId == default ? ActivityTraceId.CreateRandom().ToString() : traceId.ToString();
    }
}
