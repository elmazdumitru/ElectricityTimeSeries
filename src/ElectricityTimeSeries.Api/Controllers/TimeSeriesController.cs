using System.Globalization;
using ElectricityTimeSeries.Api.Authorization;
using ElectricityTimeSeries.Api.Contracts;
using ElectricityTimeSeries.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectricityTimeSeries.Api.Controllers;

[ApiController]
[Route("api/time-series")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class TimeSeriesController : ControllerBase
{
    private const string TotalCountHeader = "X-Total-Count";

    private readonly TimeSeriesService _service;

    public TimeSeriesController(TimeSeriesService service)
    {
        _service = service;
    }

    /// <summary>Finds time series points, a page at a time: skip and take count points, so a series can be split
    /// across pages. The X-Total-Count header holds how many points match in all.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.CanReadTimeSeries)]
    [ProducesResponseType<IReadOnlyList<TimeSeriesResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<TimeSeriesResponse>>> Search(
        [FromQuery] TimeSeriesQuery query, CancellationToken cancellationToken)
    {
        var page = await _service.SearchAsync(query.ToFilter(), query.Skip, query.Take, cancellationToken);
        Response.Headers[TotalCountHeader] = page.TotalPoints.ToString(CultureInfo.InvariantCulture);
        return page.Series.Select(TimeSeriesResponse.From).ToList();
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.CanReadTimeSeries)]
    [ProducesResponseType<TimeSeriesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TimeSeriesResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var series = await _service.GetAsync(id, cancellationToken);
        return series is null ? NotFound() : TimeSeriesResponse.From(series);
    }

    [HttpPost]
    [Authorize(Policy = Policies.CanWriteTimeSeries)]
    [Consumes("application/json")]
    [ProducesResponseType<TimeSeriesResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TimeSeriesResponse>> Create(TimeSeriesRequest request, CancellationToken cancellationToken)
    {
        var series = await _service.CreateAsync(request.ToDefinition(), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = series.Id }, TimeSeriesResponse.From(series));
    }

    /// <summary>Replaces the whole contents of a time series.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.CanWriteTimeSeries)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [Consumes("application/json")]
    [ProducesResponseType<TimeSeriesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TimeSeriesResponse>> Update(Guid id, TimeSeriesRequest request, CancellationToken cancellationToken)
    {
        var series = await _service.UpdateAsync(id, request.ToDefinition(), cancellationToken);
        return series is null ? NotFound() : TimeSeriesResponse.From(series);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.CanWriteTimeSeries)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await _service.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
}
