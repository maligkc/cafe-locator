namespace CafeLocator.Api.Models;

public sealed record NearbyCafeSearchResponse(
    IReadOnlyCollection<CafeItem> Cafes,
    int TotalCount,
    DateTime GeneratedAtUtc,
    bool FromCache,
    NearbyCafeQuery Query);

public sealed record CafeItem(
    string PlaceId,
    string Name,
    string Address,
    double Latitude,
    double Longitude,
    double? Rating,
    int? UserRatingCount,
    bool? IsOpenNow,
    int DistanceMeters,
    string? PhotoName,
    string? PhotoProxyUrl);
