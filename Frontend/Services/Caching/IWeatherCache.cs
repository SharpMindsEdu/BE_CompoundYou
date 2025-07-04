namespace Frontend.Services.Caching;

using WeatherForecast = Frontend.Client.Models.WeatherForecast;

public interface IWeatherCache
{
    ValueTask<IImmutableList<WeatherForecast>> GetForecast(CancellationToken token);
}
