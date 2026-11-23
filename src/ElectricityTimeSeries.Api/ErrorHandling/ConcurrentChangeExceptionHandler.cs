using ElectricityTimeSeries.Core.Abstractions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ElectricityTimeSeries.Api.ErrorHandling;

public sealed class ConcurrentChangeExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;

    public ConcurrentChangeExceptionHandler(IProblemDetailsService problemDetails)
    {
        _problemDetails = problemDetails;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ConcurrentChangeException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Changed by another request",
                Detail = "Another request changed this time series at the same time, so this change was not saved. Get the series again and retry.",
            },
        });
    }
}
