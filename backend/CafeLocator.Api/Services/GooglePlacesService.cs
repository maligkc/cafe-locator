using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CafeLocator.Api.Exceptions;
using CafeLocator.Api.Models;
using CafeLocator.Api.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CafeLocator.Api.Services;

public sealed class GooglePlacesService : IGooglePlacesService
{
    private const string NearbyFieldMask = "places.id,places.displayName,places.formattedAddress,places.location,places.rating,places.userRatingCount,places.photos.name,places.currentOpeningHours.openNow";
    private static readonly Regex PhotoNamePattern = new("^places\\/[A-Za-z0-9._-]+\\/photos\\/[A-Za-z0-9._-]+$", RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GooglePlacesService> _logger;
    private readonly GooglePlacesOptions _options;
    private readonly Uri _baseUri;

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GooglePlacesService(
        HttpClient httpClient,
        IMemoryCache cache,
        IOptions<GooglePlacesOptions> options,
        ILogger<GooglePlacesService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("GooglePlaces:ApiKey yapılandırması boş olamaz.");
        }

        _baseUri = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
    }

    public async Task<NearbyCafeSearchResponse> SearchNearbyAsync(NearbyCafeQuery query, CancellationToken cancellationToken)
    {
        var cacheKey = query.BuildCacheKey();

        if (_cache.TryGetValue<NearbyCafeSearchResponse>(cacheKey, out var cachedResult) && cachedResult is not null)
        {
            return cachedResult with { FromCache = true };
        }

        var payload = new NearbySearchPayload
        {
            IncludedTypes = ["cafe", "coffee_shop"],
            MaxResultCount = query.Limit,
            MinRating = query.MinRating > 0 ? query.MinRating : null,
            OpenNow = query.OpenNow ? true : null,
            RankPreference = query.SortBy == CafeSortBy.Distance ? "DISTANCE" : "POPULARITY",
            LocationRestriction = new LocationRestriction
            {
                Circle = new Circle
                {
                    Radius = query.RadiusMeters,
                    Center = new LatLng
                    {
                        Latitude = query.Latitude,
                        Longitude = query.Longitude
                    }
                }
            }
        };

        var requestUrl = $"{_baseUri.AbsoluteUri}places:searchNearby";
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        request.Headers.Add("X-Goog-Api-Key", _options.ApiKey);
        request.Headers.Add("X-Goog-FieldMask", NearbyFieldMask);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(JsonSerializer.Serialize(payload, _serializerOptions), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Google Places search failed. Status={StatusCode}, Body={Body}", response.StatusCode, responseBody);
            throw new ExternalServiceException(MapGoogleErrorMessage(responseBody));
        }

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var googleResponse = await JsonSerializer.DeserializeAsync<NearbySearchResponsePayload>(contentStream, _serializerOptions, cancellationToken)
                            ?? new NearbySearchResponsePayload();

        var cafes = (googleResponse.Places ?? [])
            .Select(place => MapCafe(place, query.Latitude, query.Longitude))
            .Where(cafe => cafe is not null)
            .Select(cafe => cafe!)
            .ToList();

        cafes = query.SortBy switch
        {
            CafeSortBy.Rating => cafes
                .OrderByDescending(c => c.Rating ?? 0)
                .ThenBy(c => c.DistanceMeters)
                .ToList(),
            _ => cafes
                .OrderBy(c => c.DistanceMeters)
                .ThenByDescending(c => c.Rating ?? 0)
                .ToList()
        };

        var result = new NearbyCafeSearchResponse(
            Cafes: cafes,
            TotalCount: cafes.Count,
            GeneratedAtUtc: DateTime.UtcNow,
            FromCache: false,
            Query: query);

        _cache.Set(cacheKey, result, TimeSpan.FromSeconds(_options.SearchCacheSeconds));
        return result;
    }

    public async Task<Uri> GetPhotoUriAsync(string photoName, int maxWidthPx, CancellationToken cancellationToken)
    {
        if (!IsValidPhotoName(photoName))
        {
            throw new ValidationException("Geçersiz fotoğraf adı formatı.");
        }

        if (maxWidthPx is < 64 or > 1600)
        {
            throw new ValidationException("maxWidthPx 64 ile 1600 arasında olmalıdır.");
        }

        var cacheKey = $"photo:{photoName}:{maxWidthPx}";
        if (_cache.TryGetValue<Uri>(cacheKey, out var cachedUri) && cachedUri is not null)
        {
            return cachedUri;
        }

        var resourceName = photoName.TrimStart('/');
        var mediaPath = $"{resourceName}/media?maxWidthPx={maxWidthPx}&skipHttpRedirect=true";

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseUri, mediaPath));
        request.Headers.Add("X-Goog-Api-Key", _options.ApiKey);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (IsRedirect(response.StatusCode) && response.Headers.Location is not null)
        {
            var redirectUri = response.Headers.Location.IsAbsoluteUri
                ? response.Headers.Location
                : new Uri(_baseUri, response.Headers.Location);

            _cache.Set(cacheKey, redirectUri, TimeSpan.FromMinutes(_options.PhotoUriCacheMinutes));
            return redirectUri;
        }

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Google Places photo failed. Status={StatusCode}, Body={Body}", response.StatusCode, responseBody);
            throw new ExternalServiceException(MapGoogleErrorMessage(responseBody));
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!string.IsNullOrWhiteSpace(mediaType) && mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            var finalUri = response.RequestMessage?.RequestUri;
            if (finalUri is not null && finalUri.IsAbsoluteUri)
            {
                _cache.Set(cacheKey, finalUri, TimeSpan.FromMinutes(_options.PhotoUriCacheMinutes));
                return finalUri;
            }
        }

        var responseBodyJson = await response.Content.ReadAsStringAsync(cancellationToken);
        PhotoMediaResponsePayload? mediaResponse;
        try
        {
            mediaResponse = JsonSerializer.Deserialize<PhotoMediaResponsePayload>(responseBodyJson, _serializerOptions);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Google Places photo json parse failed. Body={Body}", responseBodyJson);
            throw new ExternalServiceException("Google Places fotograf yaniti beklenen formatta degil.");
        }

        if (string.IsNullOrWhiteSpace(mediaResponse?.PhotoUri) || !Uri.TryCreate(mediaResponse.PhotoUri, UriKind.Absolute, out var photoUri))
        {
            throw new ExternalServiceException("Fotoğraf URI bilgisi alınamadı.");
        }

        _cache.Set(cacheKey, photoUri, TimeSpan.FromMinutes(_options.PhotoUriCacheMinutes));
        return photoUri;
    }

    private static CafeItem? MapCafe(GooglePlace place, double userLat, double userLng)
    {
        if (place.Location is null || string.IsNullOrWhiteSpace(place.Id) || string.IsNullOrWhiteSpace(place.DisplayName?.Text))
        {
            return null;
        }

        var distanceMeters = CalculateDistanceMeters(userLat, userLng, place.Location.Latitude, place.Location.Longitude);
        var photoName = place.Photos?.FirstOrDefault()?.Name;

        return new CafeItem(
            PlaceId: place.Id,
            Name: place.DisplayName.Text,
            Address: place.FormattedAddress ?? string.Empty,
            Latitude: place.Location.Latitude,
            Longitude: place.Location.Longitude,
            Rating: place.Rating,
            UserRatingCount: place.UserRatingCount,
            IsOpenNow: place.CurrentOpeningHours?.OpenNow,
            DistanceMeters: distanceMeters,
            PhotoName: photoName,
            PhotoProxyUrl: photoName is null ? null : $"/api/v1/cafes/photo?name={Uri.EscapeDataString(photoName)}&maxWidthPx=480");
    }

    private static bool IsValidPhotoName(string? photoName) =>
        !string.IsNullOrWhiteSpace(photoName)
        && photoName.Length <= 512
        && PhotoNamePattern.IsMatch(photoName);

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.Moved
        || statusCode == HttpStatusCode.Redirect
        || statusCode == HttpStatusCode.RedirectMethod
        || statusCode == HttpStatusCode.TemporaryRedirect
        || statusCode == HttpStatusCode.PermanentRedirect;

    private static string MapGoogleErrorMessage(string responseBody)
    {
        if (responseBody.Contains("API_KEY_HTTP_REFERRER_BLOCKED", StringComparison.OrdinalIgnoreCase)
            || responseBody.Contains("referer <empty> are blocked", StringComparison.OrdinalIgnoreCase)
            || responseBody.Contains("Requests from referer", StringComparison.OrdinalIgnoreCase))
        {
            return "Backend icin browser-referrer kisitli bir key kullaniliyor. Server-side Places key olusturup IP/Application restriction ayarlarini guncelleyin.";
        }

        if (responseBody.Contains("API_KEY_IP_ADDRESS_BLOCKED", StringComparison.OrdinalIgnoreCase))
        {
            return "Server-side Places key IP kisiti nedeniyle reddedildi. Google Cloud'da izinli cikis IP listesini kontrol edin.";
        }

        if (responseBody.Contains("API_KEY_INVALID", StringComparison.OrdinalIgnoreCase)
            || responseBody.Contains("API key not valid", StringComparison.OrdinalIgnoreCase))
        {
            return "Google Places API key gecersiz ya da kisitlari backend cagrisiyla uyumlu degil.";
        }

        if (responseBody.Contains("PERMISSION_DENIED", StringComparison.OrdinalIgnoreCase))
        {
            return "Google Places erisim izni reddedildi. API etkinlestirme ve key kisitlarini kontrol edin.";
        }

        if (responseBody.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase)
            || responseBody.Contains("quota", StringComparison.OrdinalIgnoreCase))
        {
            return "Google Places kotasi asildi veya kota kisiti nedeniyle istek reddedildi.";
        }

        return "Google Places servisi gecici olarak yanit veremiyor.";
    }

    private static int CalculateDistanceMeters(double lat1, double lng1, double lat2, double lng2)
    {
        const double earthRadiusMeters = 6371000;

        var dLat = DegreesToRadians(lat2 - lat1);
        var dLng = DegreesToRadians(lng2 - lng1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2))
                * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return (int)Math.Round(earthRadiusMeters * c);
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

    private sealed class NearbySearchPayload
    {
        [JsonPropertyName("includedTypes")]
        public string[] IncludedTypes { get; set; } = [];

        [JsonPropertyName("maxResultCount")]
        public int MaxResultCount { get; set; }

        [JsonPropertyName("minRating")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? MinRating { get; set; }

        [JsonPropertyName("openNow")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? OpenNow { get; set; }

        [JsonPropertyName("rankPreference")]
        public string RankPreference { get; set; } = "DISTANCE";

        [JsonPropertyName("locationRestriction")]
        public LocationRestriction LocationRestriction { get; set; } = new();
    }

    private sealed class LocationRestriction
    {
        [JsonPropertyName("circle")]
        public Circle Circle { get; set; } = new();
    }

    private sealed class Circle
    {
        [JsonPropertyName("center")]
        public LatLng Center { get; set; } = new();

        [JsonPropertyName("radius")]
        public double Radius { get; set; }
    }

    private sealed class LatLng
    {
        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }
    }

    private sealed class NearbySearchResponsePayload
    {
        [JsonPropertyName("places")]
        public List<GooglePlace>? Places { get; set; }
    }

    private sealed class GooglePlace
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("displayName")]
        public DisplayName? DisplayName { get; set; }

        [JsonPropertyName("formattedAddress")]
        public string? FormattedAddress { get; set; }

        [JsonPropertyName("location")]
        public LatLng? Location { get; set; }

        [JsonPropertyName("rating")]
        public double? Rating { get; set; }

        [JsonPropertyName("userRatingCount")]
        public int? UserRatingCount { get; set; }

        [JsonPropertyName("photos")]
        public List<GooglePhoto>? Photos { get; set; }

        [JsonPropertyName("currentOpeningHours")]
        public GoogleOpeningHours? CurrentOpeningHours { get; set; }
    }

    private sealed class DisplayName
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private sealed class GooglePhoto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private sealed class GoogleOpeningHours
    {
        [JsonPropertyName("openNow")]
        public bool? OpenNow { get; set; }
    }

    private sealed class PhotoMediaResponsePayload
    {
        [JsonPropertyName("photoUri")]
        public string? PhotoUri { get; set; }
    }
}
