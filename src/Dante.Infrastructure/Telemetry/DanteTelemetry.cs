using System.Diagnostics;

namespace Dante.Infrastructure;

public static class DanteTelemetry
{
    public const string ActivitySourceName = "Dante";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
