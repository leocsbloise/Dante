using System.Diagnostics;
using Dante.Banks;
using Dante.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Dante.Server.Tests;

public sealed class ExceptionLoggingTests
{
    [Fact]
    public async Task Domain_exception_is_logged_and_tagged_on_the_active_span()
    {
        var logs = new CollectingLogger<DomainExceptionHandler>();
        var handler = new DomainExceptionHandler(logs);
        using var activity = new Activity("request").Start();
        var context = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(
            context,
            new BankNotFoundException("Bank was not found."),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        var typeName = typeof(BankNotFoundException).FullName!;
        Assert.Equal(typeName, activity.GetTagItem("exception.type"));
        Assert.Equal(StatusCodes.Status400BadRequest, activity.GetTagItem("http.response.status_code"));
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        var entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(typeName, entry.Message);
        Assert.Contains("400", entry.Message);
    }

    [Fact]
    public async Task Unexpected_exception_is_logged_as_an_error_and_fails_the_span()
    {
        var logs = new CollectingLogger<UnhandledExceptionHandler>();
        var handler = new UnhandledExceptionHandler(logs);
        using var activity = new Activity("request").Start();
        var context = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("database unavailable"),
            CancellationToken.None);

        Assert.False(handled);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        var typeName = typeof(InvalidOperationException).FullName!;
        Assert.Equal(typeName, activity.GetTagItem("exception.type"));
        Assert.Equal(StatusCodes.Status500InternalServerError, activity.GetTagItem("http.response.status_code"));
        var entry = Assert.Single(logs.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.IsType<InvalidOperationException>(entry.Exception);
        Assert.Contains(typeName, entry.Message);
    }

    private sealed class CollectingLogger<T> : ILogger<T>
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
