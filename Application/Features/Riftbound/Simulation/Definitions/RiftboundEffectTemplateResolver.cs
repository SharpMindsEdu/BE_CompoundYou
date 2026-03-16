using System.Text.RegularExpressions;
using Domain.Entities.Riftbound;

namespace Application.Features.Riftbound.Simulation.Definitions;

public static class RiftboundEffectTemplateResolver
{
    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex MultipleSpacesRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex BracketKeywordRegex = new(@"\[(?<keyword>[^\]]+)\]", RegexOptions.Compiled);
    private static readonly Regex PlusMightRegex = new(@"\+(?<value>\d+)\s*:rb_might:", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NumberRegex = new(@"\b(?<value>\d+)\b", RegexOptions.Compiled);
    private static readonly HashSet<string> SupportedGameplayKeywords = new(
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

    public static RiftboundResolvedEffectTemplate Resolve(RiftboundCard card)
    {
        var effectText = NormalizeText(card.Effect);
        var normalizedType = NormalizeText(card.Type);
        var normalizedSupertype = NormalizeText(card.Supertype);
        var keywordSet = BuildKeywordSet(card, effectText);

        if (
            string.Equals(normalizedType, "legend", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedType, "battlefield", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedType, "rune", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedSupertype, "champion", StringComparison.OrdinalIgnoreCase)
        )
        {
            return new RiftboundResolvedEffectTemplate(
                Supported: true,
                TemplateId: "core.static",
                Keywords: keywordSet.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                Data: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            );
        }

        if (string.IsNullOrWhiteSpace(effectText))
        {
            var template = normalizedType switch
            {
                "unit" => "unit.vanilla",
                "spell" => "spell.vanilla",
                "gear" => "gear.vanilla",
                _ => "unsupported",
            };

            return new RiftboundResolvedEffectTemplate(
                Supported: !string.Equals(template, "unsupported", StringComparison.Ordinal),
                TemplateId: template,
                Keywords: keywordSet.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                Data: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            );
        }

        if (string.Equals(normalizedType, "spell", StringComparison.OrdinalIgnoreCase))
        {
            if (ContainsAll(effectText, "deal", "damage", "enemy", "unit"))
            {
                return BuildWithMagnitude(
                    effectText,
                    "spell.damage-enemy-unit",
                    keywordSet,
                    defaultMagnitude: 1
                );
            }

            if (
                effectText.Contains("give", StringComparison.OrdinalIgnoreCase)
                && effectText.Contains(":rb_might:", StringComparison.OrdinalIgnoreCase)
                && (
                    effectText.Contains("friendly unit", StringComparison.OrdinalIgnoreCase)
                    || effectText.Contains("unit you control", StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                return BuildWithMagnitude(
                    effectText,
                    "spell.buff-friendly-unit",
                    keywordSet,
                    defaultMagnitude: 1
                );
            }

            if (ContainsAll(effectText, "kill", "enemy", "unit"))
            {
                return new RiftboundResolvedEffectTemplate(
                    Supported: true,
                    TemplateId: "spell.kill-enemy-unit",
                    Keywords: keywordSet.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                    Data: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                );
            }

            if (effectText.Contains("draw", StringComparison.OrdinalIgnoreCase))
            {
                return BuildWithMagnitude(
                    effectText,
                    "spell.draw",
                    keywordSet,
                    defaultMagnitude: 1
                );
            }
        }

        if (string.Equals(normalizedType, "gear", StringComparison.OrdinalIgnoreCase))
        {
            if (
                effectText.Contains("[equip]", StringComparison.OrdinalIgnoreCase)
                || keywordSet.Contains("Equip")
            )
            {
                return new RiftboundResolvedEffectTemplate(
                    Supported: true,
                    TemplateId: "gear.attach-friendly-unit",
                    Keywords: keywordSet.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                    Data: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                );
            }
        }

        if (string.Equals(normalizedType, "unit", StringComparison.OrdinalIgnoreCase))
        {
            if (ContainsAll(effectText, "when i hold", "score 1 point"))
            {
                return new RiftboundResolvedEffectTemplate(
                    Supported: true,
                    TemplateId: "unit.hold-score-1",
                    Keywords: keywordSet.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                    Data: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                );
            }
        }

        return new RiftboundResolvedEffectTemplate(
            Supported: false,
            TemplateId: "unsupported",
            Keywords: keywordSet.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            Data: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        );
    }

    private static RiftboundResolvedEffectTemplate BuildWithMagnitude(
        string normalizedEffectText,
        string templateId,
        IReadOnlySet<string> keywords,
        int defaultMagnitude
    )
    {
        var magnitude = TryExtractMagnitude(normalizedEffectText) ?? defaultMagnitude;
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["magnitude"] = magnitude.ToString(),
        };
        return new RiftboundResolvedEffectTemplate(
            Supported: true,
            TemplateId: templateId,
            Keywords: keywords.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            Data: data
        );
    }

    private static int? TryExtractMagnitude(string normalizedEffectText)
    {
        var plusMatch = PlusMightRegex.Match(normalizedEffectText);
        if (plusMatch.Success && int.TryParse(plusMatch.Groups["value"].Value, out var plusValue))
        {
            return plusValue;
        }

        var numberMatch = NumberRegex.Match(normalizedEffectText);
        if (numberMatch.Success && int.TryParse(numberMatch.Groups["value"].Value, out var value))
        {
            return value;
        }

        return null;
    }

    private static HashSet<string> BuildKeywordSet(RiftboundCard card, string normalizedEffectText)
    {
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (card.GameplayKeywords is not null)
        {
            foreach (var keyword in card.GameplayKeywords.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                keywords.Add(keyword.Trim());
            }
        }

        foreach (Match match in BracketKeywordRegex.Matches(normalizedEffectText))
        {
            var raw = match.Groups["keyword"].Value.Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var primary = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            if (SupportedGameplayKeywords.Contains(primary))
            {
                keywords.Add(primary);
            }
        }

        return keywords;
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var replacedBreaks = value.Replace("<br />", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("<br/>", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("<br \\/>", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase);
        var noTags = HtmlTagRegex.Replace(replacedBreaks, " ");
        return MultipleSpacesRegex.Replace(noTags, " ").Trim();
    }

    private static bool ContainsAll(string text, params string[] parts)
    {
        foreach (var part in parts)
        {
            if (!text.Contains(part, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}

public sealed record RiftboundResolvedEffectTemplate(
    bool Supported,
    string TemplateId,
    IReadOnlyCollection<string> Keywords,
    IReadOnlyDictionary<string, string> Data
);
