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
    private static readonly Regex EnergyIconRegex = new(@":rb_energy_(?<value>\d+):", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RuneIconRegex = new(@":rb_rune_(?<domain>[a-z]+):", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex UpToNumericUnitsRegex = new(
        @"up to\s+(?<value>\d+)\s+units?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );
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
        var normalizedTypeLower = normalizedType.ToLowerInvariant();
        var keywordSet = BuildKeywordSet(card, effectText);

        if (string.Equals(normalizedType, "rune", StringComparison.OrdinalIgnoreCase))
        {
            var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var domain = card.Color?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(NormalizeDomain)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(domain))
            {
                data["runeDomain"] = domain;
            }

            return new RiftboundResolvedEffectTemplate(
                Supported: true,
                TemplateId: "core.static",
                Keywords: keywordSet.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                Data: data
            );
        }

        if (
            string.Equals(normalizedType, "legend", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedType, "battlefield", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedType, "champion", StringComparison.OrdinalIgnoreCase)
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
            var template = normalizedTypeLower switch
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
            if (ContainsAll(effectText, "deal", "units", "same location"))
            {
                var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["magnitude"] = (TryExtractMagnitude(effectText) ?? 1).ToString(),
                    ["maxTargets"] = TryExtractUnitCount(effectText, fallback: 3).ToString(),
                };
                AddPowerCostFromCardColor(card, data);
                AddRepeatCosts(effectText, data);

                return new RiftboundResolvedEffectTemplate(
                    Supported: true,
                    TemplateId: "spell.damage-up-to-3-units-same-location",
                    Keywords: keywordSet.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                    Data: data
                );
            }

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
            var addedPowerDomain = TryExtractRuneDomain(effectText);
            if (
                effectText.Contains("add", StringComparison.OrdinalIgnoreCase)
                && effectText.Contains("exhaust", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(addedPowerDomain)
            )
            {
                var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["addPowerDomain"] = addedPowerDomain!,
                    ["addPowerAmount"] = "1",
                };
                return new RiftboundResolvedEffectTemplate(
                    Supported: true,
                    TemplateId: "gear.exhaust-add-power",
                    Keywords: keywordSet.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                    Data: data
                );
            }

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
            if (
                ContainsAll(effectText, "when you play me", "play a spell from your trash", "ignoring its energy cost")
                && effectText.Contains("recycle", StringComparison.OrdinalIgnoreCase)
            )
            {
                var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["trashSpellMaxEnergyCost"] = (TryExtractEnergyIconValue(effectText) ?? 3).ToString(),
                    ["ignoreEnergyCostForTrashSpell"] = "true",
                    ["recyclePlayedTrashSpell"] = "true",
                };
                AddPowerCostFromCardColor(card, data);

                return new RiftboundResolvedEffectTemplate(
                    Supported: true,
                    TemplateId: "unit.play-spell-from-trash-ignore-energy-recycle",
                    Keywords: keywordSet.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                    Data: data
                );
            }

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

    private static void AddRepeatCosts(string normalizedEffectText, IDictionary<string, string> data)
    {
        var repeatMarker = normalizedEffectText.IndexOf("[Repeat]", StringComparison.OrdinalIgnoreCase);
        if (repeatMarker < 0)
        {
            return;
        }

        var repeatText = normalizedEffectText[repeatMarker..];
        var repeatEnergy = TryExtractEnergyIconValue(repeatText);
        if (repeatEnergy.HasValue && repeatEnergy.Value > 0)
        {
            data["repeatEnergyCost"] = repeatEnergy.Value.ToString();
        }

        var repeatPowerDomain = TryExtractRuneDomain(repeatText);
        if (!string.IsNullOrWhiteSpace(repeatPowerDomain))
        {
            data[$"repeatPowerCost.{repeatPowerDomain}"] = "1";
        }
    }

    private static void AddPowerCostFromCardColor(RiftboundCard card, IDictionary<string, string> data)
    {
        if (card.Color is null)
        {
            return;
        }

        foreach (var color in card.Color.Where(x => !string.IsNullOrWhiteSpace(x)).Select(NormalizeDomain).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            data[$"powerCost.{color}"] = "1";
        }
    }

    private static int TryExtractUnitCount(string normalizedEffectText, int fallback)
    {
        var numericMatch = UpToNumericUnitsRegex.Match(normalizedEffectText);
        if (numericMatch.Success && int.TryParse(numericMatch.Groups["value"].Value, out var numericValue))
        {
            return numericValue;
        }

        if (normalizedEffectText.Contains("up to one unit", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (normalizedEffectText.Contains("up to two units", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (normalizedEffectText.Contains("up to three units", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (normalizedEffectText.Contains("up to four units", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (normalizedEffectText.Contains("up to five units", StringComparison.OrdinalIgnoreCase))
        {
            return 5;
        }

        return fallback;
    }

    private static int? TryExtractEnergyIconValue(string normalizedEffectText)
    {
        var match = EnergyIconRegex.Match(normalizedEffectText);
        if (match.Success && int.TryParse(match.Groups["value"].Value, out var value))
        {
            return value;
        }

        return null;
    }

    private static string? TryExtractRuneDomain(string normalizedEffectText)
    {
        var match = RuneIconRegex.Match(normalizedEffectText);
        if (!match.Success)
        {
            return null;
        }

        var domain = match.Groups["domain"].Value;
        return string.IsNullOrWhiteSpace(domain) ? null : NormalizeDomain(domain);
    }

    private static string NormalizeDomain(string domain)
    {
        var trimmed = domain.Trim();
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        return char.ToUpperInvariant(trimmed[0]) + trimmed[1..].ToLowerInvariant();
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
