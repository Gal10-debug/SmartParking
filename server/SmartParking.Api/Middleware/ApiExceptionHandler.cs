using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SmartParking.Api.Services;

namespace SmartParking.Api.Middleware;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var status = exception is ApiException api ? api.StatusCode : exception is BadHttpRequestException bad ? bad.StatusCode : 500;
        if (status >= 500) logger.LogError(exception, "Request failed with status {Status}", status);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = status < 500 ? "Request could not be completed" : "Service error",
            Detail = exception is ApiException ? exception.Message : status == 413 ? "The upload is too large." : "The request could not be completed. Try again shortly."
        }, ct);
        return true;
    }
}
