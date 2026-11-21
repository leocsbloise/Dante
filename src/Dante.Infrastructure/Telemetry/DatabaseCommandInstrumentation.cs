using System.Data.Common;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Dante.Infrastructure;

public sealed class DatabaseCommandInstrumentation(ILogger<DatabaseCommandInstrumentation> logger) : DbCommandInterceptor
{
    private readonly ConditionalWeakTable<DbCommand, ActivityBox> _spans = new();

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Start(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Start(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        Start(command);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Start(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        Start(command);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Start(command);
        return ValueTask.FromResult(result);
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        Stop(command);
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        Stop(command);
        return ValueTask.FromResult(result);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        Stop(command);
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        Stop(command);
        return ValueTask.FromResult(result);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        Stop(command);
        return result;
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        Stop(command);
        return ValueTask.FromResult(result);
    }

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        Fail(command, eventData.Exception);
    }

    public override Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Fail(command, eventData.Exception);
        return Task.CompletedTask;
    }

    private void Start(DbCommand command)
    {
        var operation = DatabaseCommandTelemetry.Operation(command.CommandText);
        var activity = DanteTelemetry.ActivitySource.StartActivity(operation, ActivityKind.Client);
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag("db.system.name", "postgresql");
            activity.SetTag("db.operation.name", operation);
            var database = command.Connection?.Database;
            if (!string.IsNullOrEmpty(database))
                activity.SetTag("db.namespace", database);
        }

        if (_spans.TryGetValue(command, out var existing))
        {
            existing.Activity?.Dispose();
            _spans.Remove(command);
        }

        _spans.Add(command, new ActivityBox(activity));
    }

    private void Stop(DbCommand command)
    {
        if (!_spans.TryGetValue(command, out var box))
            return;

        _spans.Remove(command);
        box.Activity?.Dispose();
    }

    private void Fail(DbCommand command, Exception exception)
    {
        Activity? activity = null;
        if (_spans.TryGetValue(command, out var box))
        {
            _spans.Remove(command);
            activity = box.Activity;
        }

        try
        {
            DatabaseCommandTelemetry.RecordFailure(
                activity,
                command.CommandText,
                command.Connection?.Database,
                exception,
                logger);
        }
        finally
        {
            activity?.Dispose();
        }
    }

    private sealed class ActivityBox(Activity? activity)
    {
        public Activity? Activity { get; } = activity;
    }
}

public static class DatabaseCommandTelemetry
{
    public static void RecordFailure(
        Activity? activity,
        string? commandText,
        string? database,
        Exception exception,
        ILogger logger)
    {
        var operation = Operation(commandText);
        var sqlState = SqlState(exception);
        activity?.SetStatus(ActivityStatusCode.Error);
        if (activity?.IsAllDataRequested == true)
        {
            activity.SetTag("db.system.name", "postgresql");
            activity.SetTag("db.operation.name", operation);
            if (!string.IsNullOrEmpty(database))
                activity.SetTag("db.namespace", database);
            if (sqlState is not null)
                activity.SetTag("db.response.status_code", sqlState);
            activity.SetTag("error.type", exception.GetType().FullName);
            activity.AddException(exception);
        }

        logger.LogError(
            exception,
            "Database command {Operation} failed with SQL state {SqlState} from {ExceptionType}.",
            operation,
            sqlState ?? "",
            exception.GetType().FullName);
    }

    public static string Operation(string? commandText)
    {
        var text = commandText?.TrimStart();
        if (string.IsNullOrEmpty(text))
            return "SQL";

        var length = 0;
        while (length < text.Length && char.IsLetter(text[length]))
            length++;

        return length == 0 ? "SQL" : text[..length].ToUpperInvariant();
    }

    private static string? SqlState(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is Npgsql.PostgresException postgres && !string.IsNullOrEmpty(postgres.SqlState))
                return postgres.SqlState;
        }

        return null;
    }
}
