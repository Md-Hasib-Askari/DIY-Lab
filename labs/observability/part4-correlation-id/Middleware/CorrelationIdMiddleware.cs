using Serilog.Context;

namespace ObservabilityPart4.Middleware;

public static class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";

    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            // Reuse the caller's ID when it sent one, otherwise this request starts the trace
            var incoming = context.Request.Headers[HeaderName].FirstOrDefault();
            var correlationId = string.IsNullOrWhiteSpace(incoming)
                ? Guid.NewGuid().ToString()
                : incoming;

            // Readable by everything later in the pipeline, including the outgoing HttpClient handler
            context.Items[ItemKey] = correlationId;

            // Echoed back so the caller can quote the ID in a bug report
            context.Response.Headers[HeaderName] = correlationId;

            // Every log line written inside this block carries the ID, with no manual passing
            using (LogContext.PushProperty(ItemKey, correlationId))
            {
                await next();
            }
        });
}
