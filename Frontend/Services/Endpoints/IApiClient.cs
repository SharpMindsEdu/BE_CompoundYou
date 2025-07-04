using Frontend.Models;
using Refit;

namespace Frontend.Services.Endpoints;

[Headers("Content-Type: application/json")]
public interface IApiClient
{
    [Get("/api/weatherforecast")]
    Task<ApiResponse<IImmutableList<WeatherForecast>>> GetWeather(
        CancellationToken cancellationToken = default
    );
}
