using Application.Features.Riftbound.Simulation.Definitions;
using Domain.Entities.Riftbound;

namespace Application.Features.Riftbound.Simulation.Effects;

public static class RiftboundNamedCardEffectCatalog
{
    private static readonly IReadOnlyCollection<IRiftboundNamedCardEffect> Effects =
    [
        new AcceptableLossesEffect(),
        new AdaptatronEffect(),
        new AgainstTheOddsEffect(),
        new AhriAlluringEffect(),
        new AhriInquisitiveEffect(),
        new AhriNineTailedFoxEffect(),
        new AkshanMischievousEffect(),
        new AlbusFerrosEffect(),
        new AltarOfMemoriesEffect(),
        new AltarToUnityEffect(),
        new AncientHengeEffect(),
        new AncientWarmongerEffect(),
        new AngleShotEffect(),
        new AniviaPrimalEffect(),
        new AnnieDarkChildEffect(),
        new AnnieFieryEffect(),
        new AnnieStubbornEffect(),
        new ApheliosExaltedEffect(),
        new ApprenticeSmithEffect(),
        new AspiringEngineerEffect(),
        new AriseEffect(),
        new ArcaneShiftEffect(),
        new ArenaBarEffect(),
        new ArmedAssailantEffect(),
        new AssemblyRigEffect(),
        new AspirantsClimbEffect(),
        new AvaAchieverEffect(),
        new AzirAscendantEffect(),
        new AzirEmperorOfTheSandsEffect(),
        new AzirSovereignEffect(),
        new BackAlleyBarEffect(),
        new BackToBackEffect(),
        new BaitedHookEffect(),
        new RiftboundDeclaredCardEffect(
            "battering-ram",
            "named.battering-ram",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["energyDiscountPerCardsPlayedThisTurn"] = "1",
                ["energyMinimumAfterDiscount"] = "1",
            }
        ),
        new BellowsBreathEffect(),
        new BrynhirThundersongEffect(),
        new BushwhackEffect(),
        new StackedDeckEffect(),
        new CalledShotEffect(),
        new RiftboundDeclaredCardEffect(
            "darius-trifarian",
            "named.darius-trifarian",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["onSecondCardPlay.tempMight"] = "2",
                ["onSecondCardPlay.ready"] = "true",
            }
        ),
        new RiftboundDeclaredCardEffect("draven-vanquisher", "named.draven-vanquisher"),
        new EzrealProdigyEffect(),
        new DisciplineEffect(),
        new FallingStarEffect(),
        new RiftboundDeclaredCardEffect(
            "ferrous-forerunner",
            "named.ferrous-forerunner",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["onDeath.spawnMechTokens"] = "2",
                ["onDeath.tokenMight"] = "3",
            }
        ),
        new FightOrFlightEffect(),
        new FaithfulManufactorEffect(),
        new HardBargainEffect(),
        new RiftboundDeclaredCardEffect(
            "kai-sa-survivor",
            "named.kai-sa-survivor",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["onConquer.draw"] = "1",
            }
        ),
        new MeditationEffect(),
        new MindsplitterEffect(),
        new RiftboundDeclaredCardEffect(
            "noxus-hopeful",
            "named.noxus-hopeful",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["energyDiscountIfPlayedAnotherCardThisTurn"] = "2",
            }
        ),
        new RiftboundDeclaredCardEffect("overzealous-fan", "named.overzealous-fan"),
        new RebukeEffect(),
        new ReaversRowEffect(),
        new RiftboundDeclaredCardEffect(
            "rhasa-the-sunderer",
            "named.rhasa-the-sunderer",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["energyDiscountPerCardsInOwnTrash"] = "1",
            }
        ),
        new RideTheWindEffect(),
        new ScrapheapEffect(),
        new SpinningAxeEffect(),
        new VoidRushEffect(),
        new WindWallEffect(),
        new SwitcherooEffect(),
        new ThermoBeamEffect(),
        new TargonsPeakEffect(),
        new TideturnerEffect(),
        new TrifarianWarCampEffect(),
        new RiftboundDeclaredCardEffect(
            "traveling-merchant",
            "named.traveling-merchant",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["onMove.loot"] = "1",
            }
        ),
        new RiftboundDeclaredCardEffect(
            "treasure-hunter",
            "named.treasure-hunter",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["onMove.playGoldToken"] = "1",
            }
        ),
        new EnGardeEffect(),
        new SealOfDiscordEffect(),
        new IreliaFerventEffect(),
        new NocturneHorrifyingEffect(),
        new RekSaiBreacherEffect(),
        new RekSaiSwarmQueenEffect(),
        new RekSaiVoidBurrowerEffect(),
        new ObeliskOfPowerEffect(),
        new UndertitanEffect(),
        new ZaunWarrensEffect(),
    ];

    private static readonly IReadOnlyDictionary<string, IRiftboundNamedCardEffect> EffectsByNameIdentifier = Effects.ToDictionary(
        effect => effect.NameIdentifier,
        effect => effect,
        StringComparer.OrdinalIgnoreCase
    );

    private static readonly IReadOnlyDictionary<string, IRiftboundNamedCardEffect> EffectsByTemplateId = Effects.ToDictionary(
        effect => effect.TemplateId,
        effect => effect,
        StringComparer.Ordinal
    );

    public static bool TryResolve(
        RiftboundCard card,
        string normalizedEffectText,
        IReadOnlySet<string> baseKeywords,
        out RiftboundResolvedEffectTemplate resolved
    )
    {
        var identifier = RiftboundCardNameIdentifier.FromName(card.Name);
        if (EffectsByNameIdentifier.TryGetValue(identifier, out var effect))
        {
            resolved = effect.ResolveTemplate(card, normalizedEffectText, baseKeywords);
            return true;
        }

        resolved = default!;
        return false;
    }

    public static bool TryGetByTemplateId(
        string templateId,
        out IRiftboundNamedCardEffect effect
    )
    {
        return EffectsByTemplateId.TryGetValue(templateId, out effect!);
    }

    public static bool TryGetByNameIdentifier(
        string nameIdentifier,
        out IRiftboundNamedCardEffect effect
    )
    {
        return EffectsByNameIdentifier.TryGetValue(nameIdentifier, out effect!);
    }
}
