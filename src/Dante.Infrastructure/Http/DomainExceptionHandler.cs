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
        var fields = FieldsOf(exception);
        if (fields.Count == 0)
            await httpContext.Response.WriteAsJsonAsync(ApiResponse.WithoutData(status, exception.Message), cancellationToken);
        else
            await httpContext.Response.WriteAsJsonAsync(ApiResponse.ForFields(status, fields, exception.Message), cancellationToken);

        return true;
    }

    private static IReadOnlyList<string> FieldsOf(Exception exception) => exception switch
    {
        InvalidUsernameException or UsernameAlreadyInUseException => ["username"],
        InvalidPasswordException => ["password"],
        InvalidAccountException account => account.Fields,
        InvalidLedgerException ledger => ledger.Fields,
        BankNotFoundException => ["bankId"],
        _ => [],
    };
}

public sealed class UnhandledExceptionHandler(ILogger<UnhandledExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        const int status = StatusCodes.Status500InternalServerError;
        ExceptionTelemetry.Tag(Activity.Current, exception, status, failed: true);
        logger.LogError(
            exception,
            "Request failed with status {StatusCode} from {ExceptionType}.",
            status,
            exception.GetType().FullName);

        if (httpContext.Response.HasStarted)
            return true;

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            ApiResponse.WithoutData(status, ApiResponse.UnexpectedMessage),
            cancellationToken);
        return true;
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
