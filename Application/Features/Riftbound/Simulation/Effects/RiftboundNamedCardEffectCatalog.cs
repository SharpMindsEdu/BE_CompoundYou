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
        new BandleTreeEffect(),
        new BackAlleyBarEffect(),
        new BackToBackEffect(),
        new BardMercurialEffect(),
        new BaitedHookEffect(),
        new BeastBelowEffect(),
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
        new BilgewaterBullyEffect(),
        new BlackMarketBrokerEffect(),
        new BladeOfTheRuinedKingEffect(),
        new BlastCorpsCadetEffect(),
        new BlastOfPowerEffect(),
        new BlastconeFaeEffect(),
        new BlazingScorcherEffect(),
        new BlindFuryEffect(),
        new BlitzcrankImpassiveEffect(),
        new BlockEffect(),
        new BloodMoneyEffect(),
        new BloodRushEffect(),
        new BondsOfStrengthEffect(),
        new BoneshiverEffect(),
        new BootsOfSwiftnessEffect(),
        new BrazenBuccaneerEffect(),
        new BreakneckMechEffect(),
        new BrutalizerEffect(),
        new BrynhirThundersongEffect(),
        new BuhruCaptainEffect(),
        new BulletTimeEffect(),
        new BubbleBotEffect(),
        new BushwhackEffect(),
        new BuffTokenEffect(),
        new StackedDeckEffect(),
        new CaitlynPatrollingEffect(),
        new CallToGloryEffect(),
        new CalledShotEffect(),
        new CannonBarrageEffect(),
        new CaptainFarronEffect(),
        new CardSharpEffect(),
        new CarnivorousSnapvineEffect(),
        new CatalystOfAeonsEffect(),
        new CemeteryAttendantEffect(),
        new ChallengeEffect(),
        new CharmEffect(),
        new ChemtechCaskEffect(),
        new ChemtechEnforcerEffect(),
        new CithriaOfCloudfieldEffect(),
        new CleaveEffect(),
        new ClockworkKeeperEffect(),
        new ClothArmorEffect(),
        new CombatChefEffect(),
        new CommanderLedrosEffect(),
        new ConfrontEffect(),
        new ConsultThePastEffect(),
        new ConvergentMutationEffect(),
        new CorinaVerazaEffect(),
        new CorruptEnforcerEffect(),
        new CounterStrikeEffect(),
        new CrackshotCorsairEffect(),
        new CruelPatronEffect(),
        new CullEffect(),
        new CullTheWeakEffect(),
        new DariusHandOfNoxusEffect(),
        new DauntlessVanguardEffect(),
        new DazzlingAuroraEffect(),
        new DeadbloomPredatorEffect(),
        new DeathgripEffect(),
        new DecisiveStrikeEffect(),
        new DefiantDanceEffect(),
        new DefyEffect(),
        new DesertsCallEffect(),
        new DetonateEffect(),
        new DirewingEffect(),
        new DisarmingRakeEffect(),
        new DisintegrateEffect(),
        new DivineJudgmentEffect(),
        new DoransBladeEffect(),
        new DoransRingEffect(),
        new DoransShieldEffect(),
        new DownwellEffect(),
        new DragUnderEffect(),
        new DragonsRageEffect(),
        new DrMundoExpertEffect(),
        new DravenAudaciousEffect(),
        new RiftboundDeclaredCardEffect("draven-glorious-executioner", "named.draven-glorious-executioner"),
        new DravenShowboatEffect(),
        new DravenVanquisherEffect(),
        new DropboarderEffect(),
        new DuneDrakeEffect(),
        new DunebreakerEffect(),
        new DangerZoneEffect(),
        new DangerousDuoEffect(),
        new RiftboundDeclaredCardEffect("daring-poro", "named.daring-poro"),
        new RiftboundDeclaredCardEffect(
            "darius-trifarian",
            "named.darius-trifarian",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["onSecondCardPlay.tempMight"] = "2",
                ["onSecondCardPlay.ready"] = "true",
            }
        ),
        new DariusExecutionerEffect(),
        new EagerApprenticeEffect(),
        new EagerDrakehoundEffect(),
        new EclipseHeraldEffect(),
        new EdgeOfNightEffect(),
        new EkkoRecurrentEffect(),
        new EmberMonkEffect(),
        new EminentBenefactorEffect(),
        new EmperorsDaisEffect(),
        new EmperorsDivideEffect(),
        new EzrealProdigyEffect(),
        new EzrealDashingEffect(),
        new EzrealProdigalExplorerEffect(),
        new EnergyConduitEffect(),
        new ExperimentalHexplateEffect(),
        new EyeOfTheHeraldEffect(),
        new FaePorterEffect(),
        new FacebreakerEffect(),
        new FactoryRecallEffect(),
        new FadingMemoriesEffect(),
        new FaeDragonEffect(),
        new FallingCometEffect(),
        new FinalSparkEffect(),
        new FindYourCenterEffect(),
        new FioraGrandDuelistEffect(),
        new FioraPeerlessEffect(),
        new FioraVictoriousEffect(),
        new FioraWorthyEffect(),
        new FirestormEffect(),
        new FirstMateEffect(),
        new FlameChompersEffect(),
        new FlashEffect(),
        new FlurryOfBladesEffect(),
        new ForecasterEffect(),
        new ForgeOfTheFluftEffect(),
        new ForgeOfTheFutureEffect(),
        new ForgefireCapeEffect(),
        new ForgottenMonumentEffect(),
        new FortifiedPositionEffect(),
        new FoxFireEffect(),
        new FrigidTouchEffect(),
        new FrostcoatCubEffect(),
        new GarbageGrabberEffect(),
        new MinotaurReckonerEffect(),
        new DisciplineEffect(),
        new FallingStarEffect(),
        new FeralStrengthEffect(),
        new FerrousForerunnerEffect(),
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
