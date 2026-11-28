using Microsoft.AspNetCore.Http;
using Supermarket.BuildingBlocks.Operations;

namespace Central.Infrastructure.Execution;

/// <summary>
/// Creates one operation context per DI scope. Incoming identifiers are accepted only as GUIDs;
/// otherwise the server generates an operation ID and uses it as the correlation ID.
/// </summary>
public sealed class HttpOperationContext : IOperationContext
{
    public HttpOperationContext(IHttpContextAccessor httpContextAccessor)
    {
        var httpContext = httpContextAccessor.HttpContext;

        OperationId = ReadGuidHeader(httpContext, "X-Operation-Id") ?? Guid.NewGuid();
        CorrelationId = ReadGuidHeader(httpContext, "X-Correlation-Id") ?? OperationId;
        DeviceId = httpContext?.User?.Identity?.IsAuthenticated == true
            ? httpContext.User.FindFirst("device_id")?.Value
            : null;
    }

    public Guid OperationId { get; }
    public Guid CorrelationId { get; }
    public string? DeviceId { get; }

    private static Guid? ReadGuidHeader(HttpContext? context, string headerName)
    {
        if (context is null || !context.Request.Headers.TryGetValue(headerName, out var values))
            return null;

        return Guid.TryParse(values.ToString(), out var id) ? id : null;
    }
}
