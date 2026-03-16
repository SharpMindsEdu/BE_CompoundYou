using System.Text.RegularExpressions;

namespace Application.Features.Riftbound.Simulation.Effects;

public static partial class RiftboundCardNameIdentifier
{
    [GeneratedRegex("[^a-z0-9]+", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex NonIdentifierCharsRegex();

    [GeneratedRegex("-{2,}", RegexOptions.Compiled)]
    private static partial Regex RepeatingDashRegex();

    public static string FromName(string? cardName)
    {
        if (string.IsNullOrWhiteSpace(cardName))
        {
            return string.Empty;
        }

        var lower = cardName.Trim().ToLowerInvariant();
        var dashed = NonIdentifierCharsRegex().Replace(lower, "-");
        var collapsed = RepeatingDashRegex().Replace(dashed, "-");
        return collapsed.Trim('-');
    }
}
