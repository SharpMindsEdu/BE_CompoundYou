using System.Text.Json;
using Application.Features.Riftbound.Simulation.Definitions;
using Domain.Entities.Riftbound;

namespace Application.Features.Riftbound.Simulation.Services;

public sealed class FileBackedRiftboundSimulationDefinitionRegistry
    : IRiftboundSimulationDefinitionRegistry
{
    private const string DefinitionFileName = "riftbound-simulation-definitions.v1.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
    private static readonly HashSet<string> CoreSupportedTypes = new(
        ["legend", "battlefield"],
        StringComparer.OrdinalIgnoreCase
    );
    private static readonly HashSet<string> BasicRuneNames = new(
        [
            "Fury Rune",
            "Calm Rune",
            "Mind Rune",
            "Body Rune",
            "Chaos Rune",
            "Order Rune",
        ],
        StringComparer.OrdinalIgnoreCase
    );

    private readonly RiftboundSimulationDefinitionDocument _document;

    public FileBackedRiftboundSimulationDefinitionRegistry()
    {
        _document =
            TryLoadFrom(AppContext.BaseDirectory)
            ?? TryLoadFrom(Directory.GetCurrentDirectory())
            ?? new RiftboundSimulationDefinitionDocument(
                "riftbound-v2.0-2026-03-16+strict",
                "2026-03-16",
                [],
                []
            );
    }

    public string RulesetVersion => _document.RulesetVersion;
    public IReadOnlyCollection<string> SupportedKeywords => _document.SupportedKeywords;
    public IReadOnlyCollection<RiftboundRuleCorrection> RuleCorrections =>
        RiftboundRulesetCorrections.V1Corrections;

    public RiftboundSimulationCardDefinition? FindDefinition(RiftboundCard card)
    {
        var explicitDefinition = _document.Cards.FirstOrDefault(def =>
            Matches(def.ReferenceId, card.ReferenceId) || Matches(def.Name, card.Name)
        );
        if (explicitDefinition is not null)
        {
            return explicitDefinition;
        }

        var resolved = RiftboundEffectTemplateResolver.Resolve(card);
        return new RiftboundSimulationCardDefinition(
            ReferenceId: card.ReferenceId,
            Name: card.Name,
            Type: card.Type,
            Supertype: card.Supertype,
            Supported: resolved.Supported,
            TemplateId: resolved.TemplateId,
            Keywords: resolved.Keywords,
            OverrideData: resolved.Data
        );
    }

    public bool IsCardSupported(RiftboundCard card)
    {
        if (!card.IsActive)
        {
            return false;
        }

        if (
            CoreSupportedTypes.Contains(card.Type ?? string.Empty)
            || string.Equals(card.Supertype, "Champion", StringComparison.OrdinalIgnoreCase)
        )
        {
            return true;
        }

        if (
            string.Equals(card.Type, "Rune", StringComparison.OrdinalIgnoreCase)
            && BasicRuneNames.Contains(card.Name)
        )
        {
            return true;
        }

        return FindDefinition(card)?.Supported == true;
    }

    private static bool Matches(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static RiftboundSimulationDefinitionDocument? TryLoadFrom(string basePath)
    {
        var candidatePath = Path.Combine(basePath, DefinitionFileName);
        if (!File.Exists(candidatePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(candidatePath);
            return JsonSerializer.Deserialize<RiftboundSimulationDefinitionDocument>(
                json,
                JsonOptions
            );
        }
        catch
        {
            return null;
        }
    }
}
