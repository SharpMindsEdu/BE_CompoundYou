using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.Entities.Riftbound;
using Domain.Services.Riftbound;
using Mapster;

namespace Infrastructure.Services.Riftbound;

public class RiftboundCardService : IRiftboundCardService
{
    public sealed class RiftboundCardDto
    {
        [JsonPropertyName("id")]
        public string ReferenceId { get; set; } = default!;

        [JsonPropertyName("slug")]
        public string Slug { get; set; } = default!;

        [JsonPropertyName("name")]
        public string Name { get; set; } = default!;

        [JsonPropertyName("effect")]
        public string? Effect { get; set; }

        [JsonPropertyName("color")]
        public List<string>? Color { get; set; }

        // cost kommt als String, wird aber normalerweise als Zahl genutzt
        [JsonPropertyName("cost")]
        public string? CostRaw { get; set; }

        [JsonIgnore]
        public int? Cost => int.TryParse(CostRaw, out var v) ? v : null;

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("might")]
        public string? MightRaw { get; set; }

        [JsonIgnore]
        public int? Might => int.TryParse(MightRaw, out var v) ? v : null;

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [JsonPropertyName("set_name")]
        public string? SetName { get; set; }

        [JsonPropertyName("rarity")]
        public string? Rarity { get; set; }

        [JsonPropertyName("cycle")]
        public string? Cycle { get; set; }

        [JsonPropertyName("image")]
        public string? Image { get; set; }

        [JsonPropertyName("promo")]
        public string? PromoRaw { get; set; }

        [JsonIgnore]
        public bool Promo => PromoRaw == "1";

        [JsonPropertyName("hasNormal")]
        public string? HasNormalRaw { get; set; }

        [JsonIgnore]
        public bool HasNormal => HasNormalRaw == "1";

        [JsonPropertyName("hasFoil")]
        public string? HasFoilRaw { get; set; }

        [JsonIgnore]
        public bool HasFoil => HasFoilRaw == "1";
    }

    private const string Url = "https://api.dotgg.gg/cgfw/getcards?game=riftbound";
    private readonly HttpClient _httpClient;

    public RiftboundCardService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<RiftboundCard>> GetCardsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var json = await _httpClient.GetStringAsync(Url, cancellationToken);

        var response = JsonSerializer.Deserialize<List<RiftboundCardDto>>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        return response.Adapt<List<RiftboundCard>>();
    }

    private static int? ParseInt(string? s) =>
        int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static decimal? ParseDecimal(string? s) =>
        decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static List<string>? DeserializeList(string? json)
    {
        if (json == null)
            return null;
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
        }
        catch
        {
            return null;
        }
    }
}
