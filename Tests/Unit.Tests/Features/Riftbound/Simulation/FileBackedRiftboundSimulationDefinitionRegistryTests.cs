using Application.Features.Riftbound.Simulation.Services;
using Domain.Entities.Riftbound;

namespace Unit.Tests.Features.Riftbound.Simulation;

[Trait("category", ServiceTestCategories.UnitTests)]
public class FileBackedRiftboundSimulationDefinitionRegistryTests
{
    [Fact]
    public void Registry_LoadsRulesetMetadata()
    {
        var sut = new FileBackedRiftboundSimulationDefinitionRegistry();

        Assert.False(string.IsNullOrWhiteSpace(sut.RulesetVersion));
        Assert.NotEmpty(sut.SupportedKeywords);
        Assert.NotEmpty(sut.RuleCorrections);
    }

    [Fact]
    public void FindDefinition_MatchesByName_CaseInsensitive()
    {
        var sut = new FileBackedRiftboundSimulationDefinitionRegistry();
        var card = new RiftboundCard
        {
            Name = "blastcone fae",
            Type = "Unit",
        };

        var definition = sut.FindDefinition(card);

        Assert.NotNull(definition);
        Assert.Equal("Blastcone Fae", definition!.Name);
    }

    [Fact]
    public void IsCardSupported_HandlesCoreAndBasicRules()
    {
        var sut = new FileBackedRiftboundSimulationDefinitionRegistry();

        var legend = new RiftboundCard { Name = "Any Legend", Type = "Legend" };
        var rune = new RiftboundCard { Name = "Fury Rune", Type = "Rune" };
        var vanillaUnit = new RiftboundCard { Name = "Simple Unit", Type = "Unit", Effect = null };
        var unsupportedSpell = new RiftboundCard
        {
            Name = "Unknown Spell",
            Type = "Spell",
            Effect = "Do something custom",
        };

        Assert.True(sut.IsCardSupported(legend));
        Assert.True(sut.IsCardSupported(rune));
        Assert.True(sut.IsCardSupported(vanillaUnit));
        Assert.False(sut.IsCardSupported(unsupportedSpell));
    }
}
