using ObservabilityPart4.Middleware;

namespace ObservabilityPart4.Infrastructure;

// Stamps the current request's correlation ID onto every outgoing call made by the
// HttpClient it is attached to, so nobody has to remember to add the header by hand.
public class CorrelationIdHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        // The middleware put the ID here at the start of the incoming request
        if (
            httpContextAccessor.HttpContext?.Items[CorrelationIdMiddleware.ItemKey]
                is string correlationId
            && !request.Headers.Contains(CorrelationIdMiddleware.HeaderName)
        )
        {
            request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
