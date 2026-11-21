using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Dante.Infrastructure;

public static class ModelStateResponses
{
    public static void Configure(ApiBehaviorOptions options)
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var fields = new Dictionary<string, string>();
            foreach (var entry in context.ModelState)
            {
                var error = entry.Value?.Errors.FirstOrDefault();
                if (error is null)
                    continue;

                var message = string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? "This value is not valid."
                    : error.ErrorMessage;
                fields[JsonName(entry.Key)] = message;
            }

            return fields.Count == 0
                ? new BadRequestObjectResult(ApiResponse.WithoutData(StatusCodes.Status400BadRequest, ApiResponse.FieldMessage))
                : new BadRequestObjectResult(ApiResponse.WithData(StatusCodes.Status400BadRequest, ApiResponse.FieldMessage, fields));
        };
    }

    private static string JsonName(string key)
    {
        var name = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key;
        var dot = name.LastIndexOf('.');
        if (dot >= 0)
            name = name[(dot + 1)..];

        if (name.Length == 0)
            return "request";

        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
