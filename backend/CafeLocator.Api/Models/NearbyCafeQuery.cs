using System.ComponentModel.DataAnnotations;

namespace CafeLocator.Api.Models;

public enum CafeSortBy
{
    Distance,
    Rating
}

public sealed class NearbyCafeQuery
{
    [Required]
    [Range(-90.0, 90.0)]
    public double Latitude { get; init; }

    [Required]
    [Range(-180.0, 180.0)]
    public double Longitude { get; init; }

    [Range(100, 50000)]
    public int RadiusMeters { get; init; } = 1500;

    [Range(0.0, 5.0)]
    public double MinRating { get; init; } = 0;

    public bool OpenNow { get; init; } = false;

    public CafeSortBy SortBy { get; init; } = CafeSortBy.Distance;

    [Range(1, 20)]
    public int Limit { get; init; } = 20;

    public string BuildCacheKey() =>
        $"cafes:{Latitude:F6}:{Longitude:F6}:{RadiusMeters}:{MinRating:F1}:{OpenNow}:{SortBy}:{Limit}";
}
