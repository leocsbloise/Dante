using System.Text.Json.Serialization;

namespace Dante.Server.Tests;

internal sealed class Envelope<T>
{
    public string Message { get; set; } = "";

    public int Status { get; set; }

    [JsonPropertyName("operation_id")]
    public string OperationId { get; set; } = "";

    public T? Data { get; set; }
}
