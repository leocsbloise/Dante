using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Dante.Infrastructure;

public static class OpenTelemetryRegistration
{
    public static WebApplicationBuilder AddDanteOpenTelemetry(this WebApplicationBuilder builder)
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;
        Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(
        [
            new TraceContextPropagator(),
            new BaggagePropagator(),
        ]));

        builder.Logging.Configure(options =>
        {
            options.ActivityTrackingOptions =
                ActivityTrackingOptions.TraceId
                | ActivityTrackingOptions.SpanId
                | ActivityTrackingOptions.ParentId;
        });

        var configuration = builder.Configuration;
        var exportOtlp = !string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
        var serviceName = configuration["OTEL_SERVICE_NAME"];
        if (string.IsNullOrWhiteSpace(serviceName))
            serviceName = "Dante";

        var version = typeof(DanteTelemetry).Assembly.GetName().Version?.ToString() ?? "0.0.0";

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            if (exportOtlp)
                logging.AddOtlpExporter();
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion: version)
                .AddAttributes(
                [
                    new KeyValuePair<string, object>(
                        "deployment.environment.name",
                        builder.Environment.EnvironmentName),
                ])
                .AddEnvironmentVariableDetector())
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource(DanteTelemetry.ActivitySourceName);

                if (exportOtlp)
                    tracing.AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (exportOtlp)
                    metrics.AddOtlpExporter();
            });

        return builder;
    }
}
