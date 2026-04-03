using System.ComponentModel.DataAnnotations;
using CafeLocator.Api.Exceptions;
using CafeLocator.Api.Models;
using CafeLocator.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CafeLocator.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class CafesController : ControllerBase
{
    private readonly IGooglePlacesService _googlePlacesService;
    private readonly ILogger<CafesController> _logger;

    public CafesController(IGooglePlacesService googlePlacesService, ILogger<CafesController> logger)
    {
        _googlePlacesService = googlePlacesService;
        _logger = logger;
    }

    [HttpGet("nearby")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<NearbyCafeSearchResponse>> GetNearby([FromQuery] NearbyCafeQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _googlePlacesService.SearchNearbyAsync(query, cancellationToken);
            return Ok(response);
        }
        catch (ValidationException exception)
        {
            return ValidationProblem(detail: exception.Message);
        }
        catch (ExternalServiceException exception)
        {
            _logger.LogWarning(exception, "Nearby search failed for lat={Latitude}, lng={Longitude}", query.Latitude, query.Longitude);
            return Problem(
                title: "Harici servis hatası",
                detail: exception.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    [HttpGet("photo")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetPhoto([FromQuery] string name, [FromQuery] int maxWidthPx = 480, CancellationToken cancellationToken = default)
    {
        try
        {
            var photoUri = await _googlePlacesService.GetPhotoUriAsync(name, maxWidthPx, cancellationToken);
            return Redirect(photoUri.ToString());
        }
        catch (ValidationException exception)
        {
            return ValidationProblem(detail: exception.Message);
        }
        catch (ExternalServiceException exception)
        {
            _logger.LogWarning(exception, "Photo fetch failed. name={PhotoName}", name);
            return Problem(
                title: "Fotoğraf getirilemedi",
                detail: exception.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
