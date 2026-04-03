using CafeLocator.Api.Models;

namespace CafeLocator.Api.Services;

public interface IGooglePlacesService
{
    Task<NearbyCafeSearchResponse> SearchNearbyAsync(NearbyCafeQuery query, CancellationToken cancellationToken);
    Task<Uri> GetPhotoUriAsync(string photoName, int maxWidthPx, CancellationToken cancellationToken);
}
