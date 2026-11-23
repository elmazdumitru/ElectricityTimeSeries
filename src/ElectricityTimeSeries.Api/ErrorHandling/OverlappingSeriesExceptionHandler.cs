using ElectricityTimeSeries.Core.Abstractions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ElectricityTimeSeries.Api.ErrorHandling;

public sealed class OverlappingSeriesExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;

    public OverlappingSeriesExceptionHandler(IProblemDetailsService problemDetails)
    {
        _problemDetails = problemDetails;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not OverlappingSeriesException overlap)
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
                Title = "Overlaps another time series",
                Detail = "Another series of the same type and zones already covers part of this period. Change or delete that one first.",
                Extensions = { ["overlappingIds"] = overlap.OverlappingIds },
            },
        });
    }
}
