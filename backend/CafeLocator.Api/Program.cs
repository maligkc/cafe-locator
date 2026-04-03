using System.Text.Json.Serialization;
using CafeLocator.Api.Options;
using CafeLocator.Api.Services;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<GooglePlacesOptions>()
    .Bind(builder.Configuration.GetSection(GooglePlacesOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey), "GooglePlaces:ApiKey boş olamaz.")
    .ValidateOnStart();

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IGooglePlacesService, GooglePlacesService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod
                            | HttpLoggingFields.RequestPath
                            | HttpLoggingFields.ResponseStatusCode
                            | HttpLoggingFields.Duration;
});

var permitLimit = builder.Configuration.GetValue<int?>("RateLimiting:PermitLimitPerMinute") ?? 60;
var queueLimit = builder.Configuration.GetValue<int?>("RateLimiting:QueueLimit") ?? 0;
var isDevelopment = builder.Environment.IsDevelopment();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("api", config =>
    {
        config.PermitLimit = permitLimit;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        config.QueueLimit = queueLimit;
    });
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy.AllowAnyMethod().AllowAnyHeader();

        if (allowedOrigins.Length == 0)
        {
            if (isDevelopment)
            {
                policy.SetIsOriginAllowed(IsLocalhostOrigin);
                return;
            }

            policy.AllowAnyOrigin();
            return;
        }

        if (isDevelopment)
        {
            var allowed = allowedOrigins.ToHashSet(StringComparer.OrdinalIgnoreCase);
            policy.SetIsOriginAllowed(origin => allowed.Contains(origin) || IsLocalhostOrigin(origin));
            return;
        }

        policy.WithOrigins(allowedOrigins);
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpLogging();
app.UseHttpsRedirection();
app.UseCors("frontend");
app.UseRateLimiter();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTime.UtcNow }));
app.MapGet("/health/config", (IConfiguration configuration) =>
{
    var key = configuration["GooglePlaces:ApiKey"] ?? string.Empty;
    return Results.Ok(new
    {
        googlePlacesApiKeyConfigured = !string.IsNullOrWhiteSpace(key),
        googlePlacesApiKeyLength = key.Length
    });
});
app.MapControllers().RequireRateLimiting("api");

app.Run();

static bool IsLocalhostOrigin(string? origin)
{
    if (string.IsNullOrWhiteSpace(origin))
    {
        return false;
    }

    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
    {
        return false;
    }

    var isHttp = uri.Scheme is "http" or "https";
    var isLocalHost = uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                      || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
                      || uri.Host.Equals("::1", StringComparison.OrdinalIgnoreCase);

    return isHttp && isLocalHost;
}
