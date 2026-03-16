using System.Text.RegularExpressions;
using Domain.Entities.Riftbound;

namespace Application.Features.Riftbound.Simulation.Effects;

public static partial class RiftboundEffectTextParser
{
    private static readonly HashSet<string> NonDomainBracketTokens = new(
        [
            "action",
            "reaction",
            "repeat",
            "equip",
            "hidden",
            "ganking",
            "assault",
            "tank",
            "shield",
            "vision",
            "mighty",
            "accelerate",
            "weaponmaster",
            "legion",
            "deflect",
            "deathknell",
            "quick-draw",
            "unique",
        ],
        StringComparer.OrdinalIgnoreCase
    );

    [GeneratedRegex(@":rb_energy_(?<value>\d+):", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex EnergyIconRegex();

    [GeneratedRegex(@":rb_rune_(?<domain>[a-z]+):", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex RuneIconRegex();

    [GeneratedRegex(@"\[(?<token>[a-z]+)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex BracketTokenRegex();

    [GeneratedRegex(@"\+(?<value>\d+)\s*:rb_might:", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex PlusMightRegex();

    [GeneratedRegex(@"\b(?<value>\d+)\b", RegexOptions.Compiled)]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"up to\s+(?<value>\d+)\s+units?", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex UpToNumericUnitsRegex();

    public static int? TryExtractEnergyIconValue(string normalizedEffectText)
    {
        var match = EnergyIconRegex().Match(normalizedEffectText);
        if (match.Success && int.TryParse(match.Groups["value"].Value, out var value))
        {
            return value;
        }

        return null;
    }

    public static string? TryExtractRuneDomain(string normalizedEffectText)
    {
        var match = RuneIconRegex().Match(normalizedEffectText);
        if (!match.Success)
        {
            return null;
        }

        var raw = match.Groups["domain"].Value;
        return string.IsNullOrWhiteSpace(raw) ? null : NormalizeDomain(raw);
    }

    public static int? TryExtractMagnitude(string normalizedEffectText)
    {
        var plusMatch = PlusMightRegex().Match(normalizedEffectText);
        if (plusMatch.Success && int.TryParse(plusMatch.Groups["value"].Value, out var plusValue))
        {
            return plusValue;
        }

        var numberMatch = NumberRegex().Match(normalizedEffectText);
        if (numberMatch.Success && int.TryParse(numberMatch.Groups["value"].Value, out var value))
        {
            return value;
        }

        return null;
    }

    public static string? TryExtractBracketDomain(string normalizedEffectText)
    {
        foreach (Match match in BracketTokenRegex().Matches(normalizedEffectText))
        {
            var token = match.Groups["token"].Value.Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (NonDomainBracketTokens.Contains(token))
            {
                continue;
            }

            return NormalizeDomain(token);
        }

        return null;
    }

    public static int TryExtractUnitCount(string normalizedEffectText, int fallback)
    {
        var numericMatch = UpToNumericUnitsRegex().Match(normalizedEffectText);
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

    public static string? TryExtractRepeatSection(string normalizedEffectText)
    {
        var repeatMarker = normalizedEffectText.IndexOf("[Repeat]", StringComparison.OrdinalIgnoreCase);
        return repeatMarker < 0 ? null : normalizedEffectText[repeatMarker..];
    }

    public static string NormalizeDomain(string domain)
    {
        var trimmed = domain.Trim();
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        return char.ToUpperInvariant(trimmed[0]) + trimmed[1..].ToLowerInvariant();
    }

    public static string? ResolvePrimaryDomain(RiftboundCard card, string normalizedEffectText)
    {
        var fromText = TryExtractRuneDomain(normalizedEffectText);
        if (!string.IsNullOrWhiteSpace(fromText))
        {
            return fromText;
        }

        var fromColor = card.Color?
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        return string.IsNullOrWhiteSpace(fromColor) ? null : NormalizeDomain(fromColor);
    }
}
