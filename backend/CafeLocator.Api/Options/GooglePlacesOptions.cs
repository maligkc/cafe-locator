using System.ComponentModel.DataAnnotations;

namespace CafeLocator.Api.Options;

public sealed class GooglePlacesOptions
{
    public const string SectionName = "GooglePlaces";

    [Required]
    public string ApiKey { get; init; } = string.Empty;

    [Required]
    [Url]
    public string BaseUrl { get; init; } = "https://places.googleapis.com/v1/";

    [Range(10, 300)]
    public int SearchCacheSeconds { get; init; } = 45;

    [Range(1, 1440)]
    public int PhotoUriCacheMinutes { get; init; } = 240;
}
