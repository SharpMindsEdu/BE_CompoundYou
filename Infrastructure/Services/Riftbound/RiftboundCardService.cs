using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Domain.Entities.Riftbound;
using Domain.Services.Riftbound;

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

        [JsonPropertyName("supertype")]
        public string? Supertype { get; set; }

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
    private static readonly Regex BracketKeywordRegex = new(@"\[(?<keyword>[^\]]+)\]");
    private static readonly HashSet<string> KnownGameplayKeywords = new(
        [
            "Action",
            "Reaction",
            "Accelerate",
            "Ganking",
            "Hidden",
            "Mighty",
            "Deathknell",
            "Deflect",
            "Assault",
            "Tank",
            "Shield",
            "Equip",
            "Weaponmaster",
            "Legion",
            "Vision",
            "Quick-Draw",
            "Repeat",
            "Unique",
        ],
        StringComparer.OrdinalIgnoreCase
    );
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

        if (response is null)
        {
            return [];
        }

        var cards = new List<RiftboundCard>(response.Count);
        foreach (var dto in response.Where(x => !string.IsNullOrWhiteSpace(x.ReferenceId)))
        {
            cards.Add(
                new RiftboundCard
                {
                    ReferenceId = dto.ReferenceId.Trim(),
                    Slug = Normalize(dto.Slug),
                    Name = Normalize(dto.Name) ?? dto.ReferenceId.Trim(),
                    Effect = Normalize(dto.Effect),
                    Color = dto.Color?.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList(),
                    Cost = dto.Cost,
                    Type = Normalize(dto.Type),
                    Supertype = Normalize(dto.Supertype),
                    Might = dto.Might,
                    Tags = dto.Tags?.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToList(),
                    GameplayKeywords = ExtractGameplayKeywords(dto),
                    SetName = Normalize(dto.SetName),
                    Rarity = Normalize(dto.Rarity),
                    Cycle = Normalize(dto.Cycle),
                    Image = Normalize(dto.Image),
                    Promo = dto.Promo,
                    IsActive = true,
                }
            );
        }

        return cards;
    }

    private static List<string> ExtractGameplayKeywords(RiftboundCardDto dto)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(dto.Effect))
        {
            foreach (Match match in BracketKeywordRegex.Matches(dto.Effect))
            {
                var raw = match.Groups["keyword"].Value.Trim();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                // Keep primary keyword token, e.g. "Deflect 2" -> "Deflect".
                var primary = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                if (KnownGameplayKeywords.Contains(primary))
                {
                    result.Add(primary);
                }
            }
        }

        if (dto.Tags is not null)
        {
            foreach (var tag in dto.Tags.Where(t => !string.IsNullOrWhiteSpace(t)))
            {
                var trimmed = tag.Trim();
                if (KnownGameplayKeywords.Contains(trimmed))
                {
                    result.Add(trimmed);
                }
            }
        }

        return result.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
