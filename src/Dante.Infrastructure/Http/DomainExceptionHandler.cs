using System.Diagnostics;
using Dante.Accounts;
using Dante.Banks;
using Dante.Ledger;
using Dante.Users;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Dante.Infrastructure;

public sealed class DomainExceptionHandler(ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            UsernameAlreadyInUseException => StatusCodes.Status409Conflict,
            InvalidCredentialsException => StatusCodes.Status401Unauthorized,
            AccountNotFoundException => StatusCodes.Status404NotFound,
            InvalidUsernameException or InvalidPasswordException or InvalidAccountException or BankNotFoundException or InvalidBankException or InvalidLedgerException
                => StatusCodes.Status400BadRequest,
            _ => 0,
        };

        if (status == 0)
            return false;

        ExceptionTelemetry.Tag(Activity.Current, exception, status, failed: false);
        logger.LogWarning(
            exception,
            "Request failed with status {StatusCode} from {ExceptionType}.",
            status,
            exception.GetType().FullName);

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new ErrorBody(exception.Message), cancellationToken);
        return true;
    }

    private sealed record ErrorBody(string Error);
}

public sealed class UnhandledExceptionHandler(ILogger<UnhandledExceptionHandler> logger) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ExceptionTelemetry.Tag(Activity.Current, exception, StatusCodes.Status500InternalServerError, failed: true);
        logger.LogError(
            exception,
            "Request failed with status {StatusCode} from {ExceptionType}.",
            StatusCodes.Status500InternalServerError,
            exception.GetType().FullName);
        return ValueTask.FromResult(false);
    }
}

internal static class ExceptionTelemetry
{
    public static void Tag(Activity? activity, Exception exception, int statusCode, bool failed)
    {
        if (failed)
            activity?.SetStatus(ActivityStatusCode.Error);

        if (activity?.IsAllDataRequested != true)
            return;

        activity.SetTag("exception.type", exception.GetType().FullName);
        activity.SetTag("http.response.status_code", statusCode);
    }
}
