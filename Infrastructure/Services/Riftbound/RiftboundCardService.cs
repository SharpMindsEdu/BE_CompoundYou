using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Domain.Entities.Riftbound;
using Domain.Services.Riftbound;

namespace Infrastructure.Services.Riftbound;

public class RiftboundCardService : IRiftboundCardService
{
    private const string BaseUrl = "https://piltoverarchive.com/api/external/v1/cards";
    private const int MaxPageSize = 100;
    private const int MaxPageGuard = 1000;
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
        var cardsByReferenceId = new Dictionary<string, CardCandidate>(StringComparer.OrdinalIgnoreCase);
        var page = 1;
        var pageGuard = 0;

        while (pageGuard < MaxPageGuard)
        {
            pageGuard += 1;
            var url = $"{BaseUrl}?page={page}&limit={MaxPageSize}";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<PiltoverCardsResponseDto>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken
            );

            var pageEntries = payload?.Data ?? payload?.Results ?? [];
            foreach (var entry in pageEntries)
            {
                if (!TryMapToDomainCard(entry, out var card, out var priority))
                {
                    continue;
                }

                if (!cardsByReferenceId.TryGetValue(card.ReferenceId, out var existing))
                {
                    cardsByReferenceId[card.ReferenceId] = new CardCandidate(card, priority);
                    continue;
                }

                if (priority > existing.Priority)
                {
                    cardsByReferenceId[card.ReferenceId] = new CardCandidate(card, priority);
                    continue;
                }

                if (priority == existing.Priority)
                {
                    MergeMissingValues(existing.Card, card);
                }
            }

            if (!HasNextPage(payload?.Pagination, pageEntries.Count))
            {
                break;
            }

            page += 1;
        }

        return cardsByReferenceId
            .Values.Select(x => x.Card)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool HasNextPage(PiltoverPaginationDto? pagination, int fetchedCount)
    {
        if (pagination is null)
        {
            return fetchedCount >= MaxPageSize;
        }

        if (pagination.HasNext.HasValue)
        {
            return pagination.HasNext.Value;
        }

        if (
            pagination.TotalPages.HasValue
            && pagination.Page.HasValue
            && pagination.TotalPages.Value > 0
        )
        {
            return pagination.Page.Value < pagination.TotalPages.Value;
        }

        if (pagination.Total.HasValue && pagination.Page.HasValue && pagination.Limit.HasValue)
        {
            return pagination.Page.Value * pagination.Limit.Value < pagination.Total.Value;
        }

        return fetchedCount >= MaxPageSize;
    }

    private static bool TryMapToDomainCard(
        PiltoverCardVariantDto dto,
        out RiftboundCard card,
        out int priority
    )
    {
        var sourceCard = dto.Card;
        if (sourceCard is null)
        {
            card = null!;
            priority = 0;
            return false;
        }

        var referenceId = Normalize(sourceCard.Id);
        if (string.IsNullOrWhiteSpace(referenceId))
        {
            card = null!;
            priority = 0;
            return false;
        }

        var effectText = Normalize(sourceCard.Description) ?? Normalize(sourceCard.Effect);
        var tags = sourceCard.Tags?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var colors = sourceCard.Colors?
            .Select(x => Normalize(x.Name))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var isPromo = IsPromoVariant(dto);

        card = new RiftboundCard
        {
            ReferenceId = referenceId!,
            Slug = Normalize(dto.VariantNumber),
            Name = Normalize(sourceCard.Name) ?? referenceId!,
            Effect = effectText,
            Color = colors,
            Cost = sourceCard.Energy,
            Power = sourceCard.Power,
            Type = Normalize(sourceCard.Type),
            Supertype = Normalize(sourceCard.Super),
            Might = sourceCard.Might,
            Tags = tags,
            GameplayKeywords = ExtractGameplayKeywords(effectText, tags),
            SetName = Normalize(dto.Set?.Name),
            Rarity = Normalize(dto.Rarity),
            Cycle = null,
            Image = Normalize(dto.ImageUrl),
            Promo = isPromo,
            IsActive = true,
        };

        priority = 0;
        if (dto.IsCollectible)
        {
            priority += 100;
        }

        if (dto.ShowInLibrary)
        {
            priority += 10;
        }

        if (!isPromo)
        {
            priority += 1;
        }

        if (!string.IsNullOrWhiteSpace(card.Image))
        {
            priority += 1;
        }

        return true;
    }

    private static void MergeMissingValues(RiftboundCard target, RiftboundCard source)
    {
        if (string.IsNullOrWhiteSpace(target.Image))
        {
            target.Image = source.Image;
        }

        if (string.IsNullOrWhiteSpace(target.Rarity))
        {
            target.Rarity = source.Rarity;
        }

        if (target.Cost is null)
        {
            target.Cost = source.Cost;
        }

        if (target.Power is null)
        {
            target.Power = source.Power;
        }

        if (target.Might is null)
        {
            target.Might = source.Might;
        }

        if (target.Color is null || target.Color.Count == 0)
        {
            target.Color = source.Color?.ToList();
        }

        if (target.Tags is null || target.Tags.Count == 0)
        {
            target.Tags = source.Tags?.ToList();
        }

        if (target.GameplayKeywords is null || target.GameplayKeywords.Count == 0)
        {
            target.GameplayKeywords = source.GameplayKeywords?.ToList();
        }
    }

    private static bool IsPromoVariant(PiltoverCardVariantDto dto)
    {
        if (dto.VariantTypes is null || dto.VariantTypes.Count == 0)
        {
            return false;
        }

        return dto.VariantTypes.Any(x => string.Equals(x, "Promo", StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> ExtractGameplayKeywords(string? effect, IReadOnlyCollection<string>? tags)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(effect))
        {
            foreach (Match match in BracketKeywordRegex.Matches(effect))
            {
                var raw = match.Groups["keyword"].Value.Trim();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                var primary = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                if (KnownGameplayKeywords.Contains(primary))
                {
                    result.Add(primary);
                }
            }
        }

        if (tags is not null)
        {
            foreach (var tag in tags.Where(t => !string.IsNullOrWhiteSpace(t)))
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

    private sealed record CardCandidate(RiftboundCard Card, int Priority);

    private sealed class PiltoverCardsResponseDto
    {
        [JsonPropertyName("data")]
        public List<PiltoverCardVariantDto>? Data { get; set; }

        [JsonPropertyName("results")]
        public List<PiltoverCardVariantDto>? Results { get; set; }

        [JsonPropertyName("pagination")]
        public PiltoverPaginationDto? Pagination { get; set; }
    }

    private sealed class PiltoverPaginationDto
    {
        [JsonPropertyName("total")]
        public int? Total { get; set; }

        [JsonPropertyName("page")]
        public int? Page { get; set; }

        [JsonPropertyName("limit")]
        public int? Limit { get; set; }

        [JsonPropertyName("totalPages")]
        public int? TotalPages { get; set; }

        [JsonPropertyName("hasNext")]
        public bool? HasNext { get; set; }
    }

    private sealed class PiltoverCardVariantDto
    {
        [JsonPropertyName("variantNumber")]
        public string? VariantNumber { get; set; }

        [JsonPropertyName("rarity")]
        public string? Rarity { get; set; }

        [JsonPropertyName("variantTypes")]
        public List<string>? VariantTypes { get; set; }

        [JsonPropertyName("imageUrl")]
        public string? ImageUrl { get; set; }

        [JsonPropertyName("showInLibrary")]
        public bool ShowInLibrary { get; set; }

        [JsonPropertyName("isCollectible")]
        public bool IsCollectible { get; set; }

        [JsonPropertyName("set")]
        public PiltoverSetDto? Set { get; set; }

        [JsonPropertyName("card")]
        public PiltoverCardDto? Card { get; set; }
    }

    private sealed class PiltoverSetDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private sealed class PiltoverCardDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("super")]
        public string? Super { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("effect")]
        public string? Effect { get; set; }

        [JsonPropertyName("energy")]
        public int? Energy { get; set; }

        [JsonPropertyName("power")]
        public int? Power { get; set; }

        [JsonPropertyName("might")]
        public int? Might { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [JsonPropertyName("colors")]
        public List<PiltoverColorDto>? Colors { get; set; }
    }

    private sealed class PiltoverColorDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
