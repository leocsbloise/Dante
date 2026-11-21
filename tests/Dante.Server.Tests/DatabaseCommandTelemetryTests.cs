using System.Diagnostics;
using Dante.Infrastructure;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Dante.Server.Tests;

public sealed class DatabaseCommandTelemetryTests
{
    [Fact]
    public void Query_failure_records_the_sql_state_and_exception_without_the_command_text()
    {
        const string statement = "SELECT hunter2";
        var failure = new PostgresException(
            "duplicate key value violates unique constraint",
            "ERROR",
            "ERROR",
            PostgresErrorCodes.UniqueViolation);
        var logs = new CollectingLogger();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DanteTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        using var activity = DanteTelemetry.ActivitySource.StartActivity("request");

        DatabaseCommandTelemetry.RecordFailure(activity, statement, "dante", failure, logs);

        Assert.Equal(ActivityStatusCode.Error, activity?.Status);
        Assert.Equal("postgresql", activity?.GetTagItem("db.system.name"));
        Assert.Equal("dante", activity?.GetTagItem("db.namespace"));
        Assert.Equal("SELECT", activity?.GetTagItem("db.operation.name"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, activity?.GetTagItem("db.response.status_code"));
        Assert.Equal(typeof(PostgresException).FullName, activity?.GetTagItem("error.type"));
        Assert.Contains(activity!.Events, recorded => recorded.Name == "exception");
        var entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(failure, entry.Exception);
        Assert.Contains(PostgresErrorCodes.UniqueViolation, entry.Message);
        Assert.Contains(typeof(PostgresException).FullName!, entry.Message);
        Assert.DoesNotContain("hunter2", entry.Message);
        Assert.DoesNotContain(statement, entry.Message);
    }

    private sealed class CollectingLogger : ILogger
    {
        public List<Entry> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new Entry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record Entry(LogLevel Level, string Message, Exception? Exception);

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
