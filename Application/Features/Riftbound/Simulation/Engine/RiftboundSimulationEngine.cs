using Application.Features.Riftbound.Simulation.Definitions;
using Application.Features.Riftbound.Simulation.Effects;
using Domain.Entities.Riftbound;
using Domain.Simulation;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Application.Features.Riftbound.Simulation.Engine;

public sealed class RiftboundSimulationEngine : IRiftboundSimulationEngine
{
    private const int DuelVictoryScore = 8;
    private const string V2Prefix = "v2:";
    private const string PowerCostPrefix = "powerCost.";
    private const string RepeatPowerCostPrefix = "repeatPowerCost.";
    private const string TopDeckRevealPlayPowerCostPrefix = "topDeckRevealPlay.powerCost.";
    private const string TopDeckRevealPlayEnabledKey = "topDeckRevealPlay.enabled";
    private const string TopDeckRevealPlayEnergyCostKey = "topDeckRevealPlay.energyCost";
    private const string TopDeckRevealAddEnergyKey = "topDeckReveal.addEnergy";
    private const string UnknownRuneDomain = "__unknown__";
    private const string MultiTargetUnitsMarker = "-target-units-";
    private const string TargetUnitMarker = "-target-unit-";
    private const string RepeatActionSuffix = "-repeat";
    private const string AccelerateActionMarker = "-accelerate-";
    private const string AkshanAdditionalCostActionMarker = "-akshan-additional-cost-";
    private delegate void CardEffectHandler(
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    );
    private delegate bool ActivatedAbilityHandler(GameSession session, PlayerState player, CardInstance card);

    private readonly Dictionary<string, CardEffectHandler> _spellOrGearHandlers;
    private readonly Dictionary<string, CardEffectHandler> _unitOnPlayHandlers;
    private readonly Dictionary<string, ActivatedAbilityHandler> _activatedAbilityHandlers;
    private readonly IRiftboundEffectRuntime _effectRuntime;

    public RiftboundSimulationEngine()
    {
        _spellOrGearHandlers = new Dictionary<string, CardEffectHandler>(StringComparer.Ordinal)
        {
            ["spell.vanilla"] = ApplyVanillaSpellEffect,
            ["spell.damage-enemy-unit"] = ApplyDamageEnemyUnitSpellEffect,
            ["spell.buff-friendly-unit"] = ApplyBuffFriendlyUnitSpellEffect,
            ["spell.kill-enemy-unit"] = ApplyKillEnemyUnitSpellEffect,
            ["spell.draw"] = ApplyDrawSpellEffect,
            ["spell.damage-up-to-3-units-same-location"] = ApplySameLocationDamageSpellEffect,
            ["gear.attach-friendly-unit"] = ApplyAttachGearEffect,
            ["named.last-rites"] = ApplyAttachGearEffect,
            ["gear.vanilla"] = ApplyAttachGearEffect,
        };

        _unitOnPlayHandlers = new Dictionary<string, CardEffectHandler>(StringComparer.Ordinal)
        {
            ["unit.play-spell-from-trash-ignore-energy-recycle"] = ApplyPlaySpellFromTrashOnPlay,
        };

        _activatedAbilityHandlers = new Dictionary<string, ActivatedAbilityHandler>(StringComparer.Ordinal)
        {
            ["gear.exhaust-add-power"] = ActivateAddPowerAbility,
        };

        _effectRuntime = new RiftboundEffectRuntimeAdapter(this);
    }

    public GameSession CreateSession(RiftboundSimulationEngineSetup setup)
    {
        var session = new GameSession
        {
            SimulationId = setup.SimulationId,
            RulesetVersion = new RulesetVersion(setup.RulesetVersion),
            TurnPlayerIndex = 0,
            TurnNumber = 1,
            Phase = RiftboundTurnPhase.Setup,
            State = RiftboundTurnState.NeutralOpen,
            Players =
            [
                CreatePlayerState(0, setup.ChallengerDeck, setup.RequestedByUserId, setup.ChallengerPolicy, setup.Seed),
                CreatePlayerState(1, setup.OpponentDeck, setup.RequestedByUserId, setup.OpponentPolicy, setup.Seed),
            ],
            Battlefields = CreateBattlefields(setup),
            Showdown = new ShowdownState(),
            Combat = new CombatState(),
            Chain = [],
            EffectContexts = [],
            UsedScoringKeys = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase),
        };

        // Setup draw and mulligan shortcut for v1 simulation: draw opening hand, no mulligan action tree.
        foreach (var player in session.Players)
        {
            DrawCards(player, 4);
        }

        session.Players[1].FirstTurnExtraChannelBonus = true;
        StartTurn(session);
        return session;
    }

    public IReadOnlyCollection<RiftboundLegalAction> GetLegalActions(GameSession session)
    {
        if (session.Phase == RiftboundTurnPhase.Completed)
        {
            return [];
        }

        if (session.Phase != RiftboundTurnPhase.Action)
        {
            return [];
        }

        if (session.PendingChoice is not null)
        {
            return GetPendingChoiceLegalActions(session);
        }

        var priorityPlayerIndex = GetPriorityPlayerIndex(session);
        var player = session.Players[priorityPlayerIndex];
        var actions = new List<RiftboundLegalAction>();
        var isPriorityWindowOpen = IsPriorityWindowOpen(session);

        if (!isPriorityWindowOpen)
        {
            foreach (var rune in player.BaseZone.Cards.Where(c =>
                string.Equals(c.Type, "Rune", StringComparison.OrdinalIgnoreCase) && !c.IsExhausted
            ))
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{V2Prefix}activate-rune-{rune.InstanceId}",
                        RiftboundActionType.ActivateRune,
                        player.PlayerIndex,
                        $"Activate rune {rune.Name}"
                    )
                );
            }

            foreach (
                var card in EnumeratePotentialActivatedAbilityCards(session, player.PlayerIndex)
            )
            {
                if (!IsActivatableCard(session, player.PlayerIndex, card))
                {
                    continue;
                }

                actions.Add(
                    new RiftboundLegalAction(
                        $"{V2Prefix}activate-ability-{card.InstanceId}",
                        RiftboundActionType.ActivateRune,
                        player.PlayerIndex,
                        $"Activate {card.Name}"
                    )
                );
            }
        }

        var isCardPlayLocked = IsPlayerCardPlayLockedForCurrentTurn(session, player.PlayerIndex);
        foreach (var card in player.HandZone.Cards.Where(card =>
            !isPriorityWindowOpen || IsQuickSpeedCard(card)
        ))
        {
            if (isCardPlayLocked)
            {
                continue;
            }

            if (!CanPlayCard(session, card, player))
            {
                continue;
            }

            if (string.Equals(card.Type, "Unit", StringComparison.OrdinalIgnoreCase))
            {
                if (
                    TryResolveNamedCardEffect(card, out var namedUnitEffect)
                    && namedUnitEffect.TryAddUnitPlayLegalActions(
                        _effectRuntime,
                        session,
                        player,
                        card,
                        actions
                    )
                )
                {
                    continue;
                }

                AddDefaultUnitPlayLegalActions(session, player, card, actions);
            }
            else if (string.Equals(card.Type, "Spell", StringComparison.OrdinalIgnoreCase) || string.Equals(card.Type, "Gear", StringComparison.OrdinalIgnoreCase))
            {
                if (
                    TryResolveNamedCardEffect(card, out var namedCardEffect)
                    && namedCardEffect.TryAddLegalActions(
                        _effectRuntime,
                        session,
                        player,
                        card,
                        actions
                    )
                )
                {
                    continue;
                }

                if (
                    string.Equals(
                        card.EffectTemplateId,
                        "spell.damage-up-to-3-units-same-location",
                        StringComparison.Ordinal
                    )
                )
                {
                    var maxTargets = ReadIntEffectData(card, "maxTargets", fallback: 3);
                    var canChooseRepeatForSelection = HasOptionalRepeat(card);
                    var targetSelections = EnumerateSameLocationEnemyTargetSelections(
                        session,
                        player.PlayerIndex,
                        maxTargets
                    );
                    foreach (var selection in targetSelections)
                    {
                        var targetList = string.Join(
                            ',',
                            selection.Targets.Select(x => x.InstanceId.ToString())
                        );
                        var actionId =
                            $"{V2Prefix}play-{card.InstanceId}-spell{MultiTargetUnitsMarker}{targetList}";
                        actions.Add(
                            new RiftboundLegalAction(
                                actionId,
                                RiftboundActionType.PlayCard,
                                player.PlayerIndex,
                                $"Play {card.Name} targeting {selection.Targets.Count} unit(s) at {selection.LocationKey}"
                            )
                        );

                        if (canChooseRepeatForSelection)
                        {
                            actions.Add(
                                new RiftboundLegalAction(
                                    $"{actionId}{RepeatActionSuffix}",
                                    RiftboundActionType.PlayCard,
                                    player.PlayerIndex,
                                    $"Play {card.Name} targeting {selection.Targets.Count} unit(s) at {selection.LocationKey} (repeat)"
                                )
                            );
                        }
                    }

                    continue;
                }

                var targetMode = ResolveTargetMode(card);
                var canChooseRepeat = HasOptionalRepeat(card);
                if (targetMode is TargetMode.None)
                {
                    var actionId = $"{V2Prefix}play-{card.InstanceId}-spell";
                    actions.Add(
                        new RiftboundLegalAction(
                            actionId,
                            RiftboundActionType.PlayCard,
                            player.PlayerIndex,
                            $"Play spell {card.Name}"
                        )
                    );

                    if (canChooseRepeat)
                    {
                        actions.Add(
                            new RiftboundLegalAction(
                                $"{actionId}{RepeatActionSuffix}",
                                RiftboundActionType.PlayCard,
                                player.PlayerIndex,
                                $"Play spell {card.Name} (repeat)"
                            )
                        );
                    }
                }
                else
                {
                    foreach (var target in EnumerateUnitTargets(session, player.PlayerIndex, targetMode))
                    {
                        var actionId = $"{V2Prefix}play-{card.InstanceId}-spell-target-unit-{target.InstanceId}";
                        actions.Add(
                            new RiftboundLegalAction(
                                actionId,
                                RiftboundActionType.PlayCard,
                                player.PlayerIndex,
                                $"Play {card.Name} targeting {target.Name}"
                            )
                        );

                        if (canChooseRepeat)
                        {
                            actions.Add(
                                new RiftboundLegalAction(
                                    $"{actionId}{RepeatActionSuffix}",
                                    RiftboundActionType.PlayCard,
                                    player.PlayerIndex,
                                    $"Play {card.Name} targeting {target.Name} (repeat)"
                                )
                            );
                        }
                    }
                }
            }
        }

        if (!isPriorityWindowOpen)
        {
            foreach (var unit in player.BaseZone.Cards.Where(IsMovableUnit))
            {
                foreach (var battlefield in session.Battlefields)
                {
                    actions.Add(
                        new RiftboundLegalAction(
                            $"{V2Prefix}move-{unit.InstanceId}-to-bf-{battlefield.Index}",
                            RiftboundActionType.StandardMove,
                            player.PlayerIndex,
                            $"Move {unit.Name} to battlefield {battlefield.Name}"
                        )
                    );
                }
            }

            foreach (var battlefield in session.Battlefields)
            {
                foreach (var unit in battlefield.Units.Where(u =>
                    IsMovableUnit(u) && u.ControllerPlayerIndex == player.PlayerIndex
                ))
                {
                    if (!RiftboundEffectUnitTargeting.IsMoveToBaseLocked(session))
                    {
                        actions.Add(
                            new RiftboundLegalAction(
                                $"{V2Prefix}move-{unit.InstanceId}-to-base",
                                RiftboundActionType.StandardMove,
                                player.PlayerIndex,
                                $"Move {unit.Name} to base"
                            )
                        );
                    }

                    if (UnitHasKeyword(session, unit, "Ganking"))
                    {
                        foreach (var target in session.Battlefields.Where(b => b.Index != battlefield.Index))
                        {
                            actions.Add(
                                new RiftboundLegalAction(
                                    $"{V2Prefix}move-{unit.InstanceId}-to-bf-{target.Index}",
                                    RiftboundActionType.StandardMove,
                                    player.PlayerIndex,
                                    $"Gank {unit.Name} to battlefield {target.Name}"
                                )
                            );
                        }
                    }
                }
            }

            actions.Add(
                new RiftboundLegalAction(
                    "end-turn",
                    RiftboundActionType.EndTurn,
                    player.PlayerIndex,
                    "End turn"
                )
            );
        }
        else
        {
            actions.Add(
                new RiftboundLegalAction(
                    "pass-focus",
                    RiftboundActionType.PassFocus,
                    player.PlayerIndex,
                    "Pass priority"
                )
            );
        }

        actions = ExpandConquerChoiceActions(session, player, actions);
        actions = FilterUnpayablePlayActions(session, player, actions);
        return actions;
    }

    private static IReadOnlyCollection<RiftboundLegalAction> GetPendingChoiceLegalActions(
        GameSession session
    )
    {
        var pendingChoice = session.PendingChoice;
        if (pendingChoice is null)
        {
            return [];
        }

        return pendingChoice.Options
            .Select(option =>
                new RiftboundLegalAction(
                    option.ActionId,
                    RiftboundActionType.PlayCard,
                    pendingChoice.PlayerIndex,
                    option.Description
                )
            )
            .ToList();
    }

    public RiftboundSimulationEngineResult ApplyAction(GameSession session, string actionId)
    {
        var normalizedActionId = NormalizeActionId(actionId);
        var legalActions = GetLegalActions(session);
        if (
            !legalActions.Any(x =>
                string.Equals(NormalizeActionId(x.ActionId), normalizedActionId, StringComparison.Ordinal)
            )
        )
        {
            return new RiftboundSimulationEngineResult(
                false,
                "Action is not legal in the current state.",
                session,
                legalActions
            );
        }

        if (string.Equals(normalizedActionId, $"{V2Prefix}end-turn", StringComparison.Ordinal))
        {
            EndTurn(session);
            return new RiftboundSimulationEngineResult(
                true,
                null,
                session,
                GetLegalActions(session)
            );
        }

        if (string.Equals(normalizedActionId, $"{V2Prefix}pass-focus", StringComparison.Ordinal))
        {
            PassFocus(session);
            return new RiftboundSimulationEngineResult(
                true,
                null,
                session,
                GetLegalActions(session)
            );
        }

        if (normalizedActionId.StartsWith($"{V2Prefix}choose-", StringComparison.Ordinal))
        {
            ResolvePendingChoice(session, normalizedActionId);
            return new RiftboundSimulationEngineResult(
                true,
                null,
                session,
                GetLegalActions(session)
            );
        }

        if (
            normalizedActionId.StartsWith($"{V2Prefix}activate-rune-", StringComparison.Ordinal)
            || normalizedActionId.StartsWith($"{V2Prefix}activate-ability-", StringComparison.Ordinal)
        )
        {
            ActivateResource(session, normalizedActionId);
            ResolveCleanup(session, normalizedActionId);
            return new RiftboundSimulationEngineResult(
                true,
                null,
                session,
                GetLegalActions(session)
            );
        }

        if (normalizedActionId.StartsWith($"{V2Prefix}play-", StringComparison.Ordinal))
        {
            PlayCard(session, normalizedActionId);
            return new RiftboundSimulationEngineResult(
                true,
                null,
                session,
                GetLegalActions(session)
            );
        }

        if (normalizedActionId.StartsWith($"{V2Prefix}move-", StringComparison.Ordinal))
        {
            MoveUnit(session, normalizedActionId);
            return new RiftboundSimulationEngineResult(
                true,
                null,
                session,
                GetLegalActions(session)
            );
        }

        return new RiftboundSimulationEngineResult(
            false,
            "Action type is not implemented.",
            session,
            legalActions
        );
    }

    private static PlayerState CreatePlayerState(
        int playerIndex,
        RiftboundDeck deck,
        long fallbackUserId,
        string policy,
        long setupSeed
    )
    {
        var mainDeckCards = BuildCardInstances(deck.Cards, playerIndex, "Unit");
        var runeDeckCards = BuildCardInstances(deck.Runes, playerIndex, "Rune");
        Shuffle(mainDeckCards, setupSeed * 101 + deck.Id * 37 + playerIndex * 13);
        Shuffle(runeDeckCards, setupSeed * 173 + deck.Id * 53 + playerIndex * 17);
        var chosenChampionCards = deck.Champion is null
            ? []
            : new List<CardInstance> { BuildCardInstance(deck.Champion, playerIndex, playerIndex) };
        if (chosenChampionCards.Count > 0)
        {
            chosenChampionCards[0].EffectData["isChosenChampion"] = "true";
        }

        return new PlayerState
        {
            PlayerIndex = playerIndex,
            UserId = deck.OwnerId > 0 ? deck.OwnerId : fallbackUserId,
            DeckId = deck.Id,
            Policy = policy,
            Score = 0,
            FirstTurnExtraChannelBonus = false,
            MainDeckZone = new ZoneState { Name = "MainDeck", Cards = mainDeckCards },
            RuneDeckZone = new ZoneState { Name = "RuneDeck", Cards = runeDeckCards },
            HandZone = new ZoneState { Name = "Hand", Cards = [] },
            BaseZone = new ZoneState { Name = "Base", Cards = [] },
            TrashZone = new ZoneState { Name = "Trash", Cards = [] },
            ChampionZone = new ZoneState
            {
                Name = "ChampionZone",
                Cards = chosenChampionCards,
            },
            LegendZone = new ZoneState
            {
                Name = "LegendZone",
                Cards = deck.Legend is null
                    ? []
                    : [BuildCardInstance(deck.Legend, playerIndex, playerIndex)],
            },
            RunePool = new RunePool(),
        };
    }

    private static List<BattlefieldState> CreateBattlefields(RiftboundSimulationEngineSetup setup)
    {
        var random = new Random(unchecked((int)setup.Seed));
        var challengerChoice = ChooseBattlefield(setup.ChallengerDeck, random);
        var opponentChoice = ChooseBattlefield(setup.OpponentDeck, random);

        return
        [
            new BattlefieldState
            {
                CardId = challengerChoice.CardId,
                Name = challengerChoice.Name,
                Index = 0,
                ControlledByPlayerIndex = 0,
            },
            new BattlefieldState
            {
                CardId = opponentChoice.CardId,
                Name = opponentChoice.Name,
                Index = 1,
                ControlledByPlayerIndex = 1,
            },
        ];
    }

    private static (long CardId, string Name) ChooseBattlefield(RiftboundDeck deck, Random random)
    {
        var options = deck.Battlefields.ToList();
        if (options.Count == 0)
        {
            return (0, "Fallback Battlefield");
        }

        var selected = options[random.Next(options.Count)];
        return (selected.CardId, selected.Card?.Name ?? $"Battlefield-{selected.CardId}");
    }

    private static List<CardInstance> BuildCardInstances(
        IEnumerable<RiftboundDeckCard> cards,
        int ownerPlayerIndex,
        string defaultType
    )
    {
        var list = new List<CardInstance>();
        foreach (var deckCard in cards)
        {
            for (var i = 0; i < deckCard.Quantity; i++)
            {
                list.Add(
                    BuildCardInstance(
                        deckCard.Card,
                        ownerPlayerIndex,
                        ownerPlayerIndex,
                        deckCard.CardId,
                        defaultType
                    )
                );
            }
        }

        return list;
    }

    private static List<CardInstance> BuildCardInstances(
        IEnumerable<RiftboundDeckRune> cards,
        int ownerPlayerIndex,
        string defaultType
    )
    {
        var list = new List<CardInstance>();
        foreach (var deckCard in cards)
        {
            for (var i = 0; i < deckCard.Quantity; i++)
            {
                list.Add(
                    BuildCardInstance(
                        deckCard.Card,
                        ownerPlayerIndex,
                        ownerPlayerIndex,
                        deckCard.CardId,
                        defaultType
                    )
                );
            }
        }

        return list;
    }

    private static CardInstance BuildCardInstance(
        RiftboundCard? card,
        int ownerPlayerIndex,
        int controllerPlayerIndex,
        long fallbackCardId = 0,
        string fallbackType = "Card"
    )
    {
        var resolvedTemplate = card is null
            ? new RiftboundResolvedEffectTemplate(false, "unsupported", [], new Dictionary<string, string>())
            : RiftboundEffectTemplateResolver.Resolve(card);
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (card?.GameplayKeywords is not null)
        {
            foreach (var keyword in card.GameplayKeywords.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                keywords.Add(keyword.Trim());
            }
        }

        if (card?.Tags is not null)
        {
            foreach (var tag in card.Tags.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                keywords.Add(tag.Trim());
            }
        }

        return new CardInstance
        {
            InstanceId = Guid.NewGuid(),
            CardId = card?.Id ?? fallbackCardId,
            Name = card?.Name ?? $"Card-{fallbackCardId}",
            Type = card?.Type ?? fallbackType,
            OwnerPlayerIndex = ownerPlayerIndex,
            ControllerPlayerIndex = controllerPlayerIndex,
            Cost = card?.Cost,
            Power = card?.Power,
            ColorDomains = card?.Color?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [],
            Might = card?.Might,
            Keywords = keywords.ToList(),
            EffectTemplateId = resolvedTemplate.TemplateId,
            EffectData = resolvedTemplate.Data.ToDictionary(
                x => x.Key,
                x => x.Value,
                StringComparer.OrdinalIgnoreCase
            ),
        };
    }

    private static void Shuffle(List<CardInstance> list, long seed)
    {
        var random = new Random(unchecked((int)seed));
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static void DrawCards(PlayerState player, int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (player.MainDeckZone.Cards.Count == 0)
            {
                return;
            }

            var top = player.MainDeckZone.Cards[0];
            player.MainDeckZone.Cards.RemoveAt(0);
            player.HandZone.Cards.Add(top);
        }
    }

    private void StartTurn(GameSession session)
    {
        if (session.Phase == RiftboundTurnPhase.Completed)
        {
            return;
        }

        var player = session.Players[session.TurnPlayerIndex];
        ApplyBeginningPhaseTemporaryEliminations(session, player);
        session.Phase = RiftboundTurnPhase.Awaken;
        var readyTriggerCandidates = player
            .BaseZone.Cards.Where(card => card.IsExhausted && IsUnitCard(card))
            .ToList();
        readyTriggerCandidates.AddRange(
            session
                .Battlefields.SelectMany(x => x.Units)
                .Where(card => card.ControllerPlayerIndex == player.PlayerIndex && card.IsExhausted)
        );

        ReadyCards(player.BaseZone.Cards);
        foreach (var battlefield in session.Battlefields)
        {
            ReadyCards(battlefield.Units.Where(x => x.ControllerPlayerIndex == player.PlayerIndex));
        }

        foreach (var readyCard in readyTriggerCandidates)
        {
            ApplyReadyTriggeredEffects(session, player, readyCard);
        }

        session.Phase = RiftboundTurnPhase.Beginning;
        ApplyBeginningBattlefieldTriggers(session, player);
        ScoreHoldings(session, player.PlayerIndex);

        session.Phase = RiftboundTurnPhase.Channel;
        var channelCount = 2;
        if (player.PlayerIndex == 1 && player.FirstTurnExtraChannelBonus)
        {
            channelCount += 1;
            player.FirstTurnExtraChannelBonus = false;
        }

        ChannelRunes(player, channelCount);

        session.Phase = RiftboundTurnPhase.Draw;
        DrawCards(player, 1);
        EmptyRunePool(player);

        session.Phase = RiftboundTurnPhase.Action;
        session.State = RiftboundTurnState.NeutralOpen;
    }

    private void ApplyBeginningPhaseTemporaryEliminations(GameSession session, PlayerState player)
    {
        var temporaryUnits = player.BaseZone.Cards
            .Where(x =>
                IsUnitCard(x)
                && x.ControllerPlayerIndex == player.PlayerIndex
                && ReadBoolEffectData(x, "temporaryUntilBeginning", fallback: false)
            )
            .ToList();
        temporaryUnits.AddRange(
            session.Battlefields.SelectMany(x => x.Units).Where(x =>
                x.ControllerPlayerIndex == player.PlayerIndex
                && ReadBoolEffectData(x, "temporaryUntilBeginning", fallback: false)
            )
        );

        foreach (var unit in temporaryUnits.DistinctBy(x => x.InstanceId))
        {
            unit.EffectData.Remove("temporaryUntilBeginning");
            RemoveUnitFromCurrentLocation(session, unit);
            ApplyUnitDeathTriggeredEffects(session, unit);
            DetachAttachedGearToTrash(session, unit.InstanceId);
            MoveDeadUnitToDestination(session, unit);
        }

        var temporaryGear = RiftboundEffectGearTargeting.EnumerateAllGear(session)
            .Where(x =>
                x.ControllerPlayerIndex == player.PlayerIndex
                && ReadBoolEffectData(x, "temporaryUntilBeginning", fallback: false)
            )
            .ToList();
        foreach (var gear in temporaryGear)
        {
            gear.EffectData.Remove("temporaryUntilBeginning");
            if (!RiftboundEffectGearTargeting.RemoveGearFromBoard(session, gear))
            {
                continue;
            }

            gear.AttachedToInstanceId = null;
            session.Players[gear.OwnerPlayerIndex].TrashZone.Cards.Add(gear);
        }
    }

    private void ApplyBeginningBattlefieldTriggers(GameSession session, PlayerState player)
    {
        foreach (var battlefield in session.Battlefields)
        {
            if (!TryResolveBattlefieldEffect(battlefield, out var battlefieldEffect))
            {
                continue;
            }

            var battlefieldCard = CreateBattlefieldEffectCard(
                battlefield,
                player.PlayerIndex,
                battlefieldEffect.TemplateId
            );
            battlefieldEffect.OnBattlefieldBeginning(
                _effectRuntime,
                session,
                player,
                battlefieldCard,
                battlefield
            );
        }
    }

    private static void ReadyCards(IEnumerable<CardInstance> cards)
    {
        foreach (var card in cards)
        {
            card.IsExhausted = false;
        }
    }

    private static void ChannelRunes(PlayerState player, int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (player.RuneDeckZone.Cards.Count == 0)
            {
                return;
            }

            var rune = player.RuneDeckZone.Cards[0];
            player.RuneDeckZone.Cards.RemoveAt(0);
            player.BaseZone.Cards.Add(rune);
        }
    }

    private static void EmptyRunePool(PlayerState player)
    {
        player.RunePool.Energy = 0;
        player.RunePool.PowerByDomain.Clear();
    }

    private bool CanPlayCard(GameSession session, CardInstance card, PlayerState player)
    {
        if (string.Equals(card.EffectTemplateId, "unsupported", StringComparison.Ordinal))
        {
            return false;
        }

        if (
            !string.Equals(card.Type, "Unit", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(card.Type, "Spell", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(card.Type, "Gear", StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        return true;
    }

    private int GetSpellAndAbilityBonusDamage(GameSession session, int playerIndex)
    {
        return RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, playerIndex).Count(unit =>
            string.Equals(unit.EffectTemplateId, "named.annie-fiery", StringComparison.Ordinal)
        );
    }

    private int ResolveVictoryScoreTarget(GameSession session)
    {
        var modifier = 0;
        foreach (var battlefield in session.Battlefields)
        {
            if (!TryResolveBattlefieldEffect(battlefield, out var battlefieldEffect))
            {
                continue;
            }

            var card = CreateBattlefieldEffectCard(
                battlefield,
                controllerPlayerIndex: 0,
                battlefieldEffect.TemplateId
            );
            modifier += battlefieldEffect.GetVictoryScoreModifier(
                _effectRuntime,
                session,
                card,
                battlefield
            );
        }

        return DuelVictoryScore + modifier;
    }

    private static bool HasOptionalRepeat(CardInstance card)
    {
        return ResolveRepeatCost(card).HasAnyCost;
    }

    private static bool IsAccelerateRequested(string actionId)
    {
        return actionId.Contains(AccelerateActionMarker, StringComparison.Ordinal);
    }

    private static bool TryResolveNamedCardEffect(
        CardInstance card,
        out IRiftboundNamedCardEffect namedCardEffect
    )
    {
        return RiftboundNamedCardEffectCatalog.TryGetByTemplateId(
            card.EffectTemplateId,
            out namedCardEffect
        );
    }

    private static bool TryResolveBattlefieldEffect(
        BattlefieldState battlefield,
        out IRiftboundNamedCardEffect battlefieldEffect
    )
    {
        var identifier = RiftboundCardNameIdentifier.FromName(battlefield.Name);
        return RiftboundNamedCardEffectCatalog.TryGetByNameIdentifier(identifier, out battlefieldEffect);
    }

    private static CardInstance CreateBattlefieldEffectCard(
        BattlefieldState battlefield,
        int controllerPlayerIndex,
        string templateId
    )
    {
        return new CardInstance
        {
            InstanceId = Guid.NewGuid(),
            CardId = battlefield.CardId,
            Name = battlefield.Name,
            Type = "Battlefield",
            OwnerPlayerIndex = controllerPlayerIndex,
            ControllerPlayerIndex = controllerPlayerIndex,
            Cost = 0,
            Power = 0,
            ColorDomains = [],
            Might = 0,
            EffectTemplateId = templateId,
            EffectData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Keywords = [],
        };
    }

    private bool IsActivatableCard(GameSession session, int playerIndex, CardInstance card)
    {
        if (card.IsExhausted)
        {
            return false;
        }

        if (_activatedAbilityHandlers.ContainsKey(card.EffectTemplateId))
        {
            return true;
        }

        if (TryResolveNamedCardEffect(card, out var namedCardEffect) && namedCardEffect.HasActivatedAbility)
        {
            return true;
        }

        return CanActivateLegendEquipAbility(session, playerIndex, card);
    }

    private static IReadOnlyCollection<CardInstance> EnumeratePotentialActivatedAbilityCards(
        GameSession session,
        int playerIndex
    )
    {
        var cards = new List<CardInstance>();
        cards.AddRange(session.Players[playerIndex].BaseZone.Cards);
        cards.AddRange(session.Players[playerIndex].LegendZone.Cards);
        cards.AddRange(session.Players[playerIndex].ChampionZone.Cards);
        cards.AddRange(
            session.Battlefields.SelectMany(x => x.Units).Where(x => x.ControllerPlayerIndex == playerIndex)
        );
        return cards;
    }

    private static bool IsQuickSpeedCard(CardInstance card)
    {
        return card.Keywords.Contains("Action", StringComparer.OrdinalIgnoreCase)
            || card.Keywords.Contains("Reaction", StringComparer.OrdinalIgnoreCase);
    }

    private void AddDefaultUnitPlayLegalActions(
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var hasAccelerate = card.Keywords.Contains("Accelerate", StringComparer.OrdinalIgnoreCase);
        actions.Add(
            new RiftboundLegalAction(
                $"{V2Prefix}play-{card.InstanceId}-to-base",
                RiftboundActionType.PlayCard,
                player.PlayerIndex,
                $"Play {card.Name} to base"
            )
        );
        if (hasAccelerate)
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"{V2Prefix}play-{card.InstanceId}{AccelerateActionMarker}to-base",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} to base (accelerate)"
                )
            );
        }

        foreach (
            var battlefield in session.Battlefields.Where(b =>
                b.ControlledByPlayerIndex == player.PlayerIndex
            )
        )
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"{V2Prefix}play-{card.InstanceId}-to-bf-{battlefield.Index}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} to battlefield {battlefield.Name}"
                )
            );
            if (hasAccelerate)
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{V2Prefix}play-{card.InstanceId}{AccelerateActionMarker}to-bf-{battlefield.Index}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name} to battlefield {battlefield.Name} (accelerate)"
                    )
                );
            }
        }
    }

    private List<RiftboundLegalAction> FilterUnpayablePlayActions(
        GameSession session,
        PlayerState player,
        IReadOnlyCollection<RiftboundLegalAction> actions
    )
    {
        var filtered = new List<RiftboundLegalAction>(actions.Count);
        foreach (var action in actions)
        {
            if (action.ActionType != RiftboundActionType.PlayCard)
            {
                filtered.Add(action);
                continue;
            }

            Guid cardInstanceId;
            try
            {
                cardInstanceId = ParseGuidFrom(action.ActionId, $"{V2Prefix}play-");
            }
            catch
            {
                filtered.Add(action);
                continue;
            }

            var card = player.HandZone.Cards.FirstOrDefault(x => x.InstanceId == cardInstanceId);
            if (card is null)
            {
                filtered.Add(action);
                continue;
            }

            var baseCost = ResolveBasePlayCost(session, player, card);
            if (string.Equals(card.EffectTemplateId, "named.commander-ledros", StringComparison.Ordinal))
            {
                var ledrosSacrifices = ResolveCommanderLedrosAdditionalSacrifices(
                    session,
                    player,
                    card,
                    action.ActionId
                );
                if (ledrosSacrifices.Count > 0)
                {
                    baseCost = ApplyCommanderLedrosCostReduction(
                        baseCost,
                        ledrosSacrifices.Count,
                        "Order"
                    );
                }
            }

            var totalCost = CombineCosts(
                baseCost,
                ResolveActionAdditionalCost(session, player.PlayerIndex, card, action.ActionId)
            );
            totalCost = NormalizeCostForPayment(totalCost);
            if (CanEventuallyAffordCost(player, totalCost))
            {
                filtered.Add(action);
            }
        }

        return filtered;
    }

    private static List<RiftboundLegalAction> ExpandConquerChoiceActions(
        GameSession session,
        PlayerState player,
        IReadOnlyCollection<RiftboundLegalAction> actions
    )
    {
        var discardChoices = GetZaunWarrensDiscardChoices(session, player);
        var returnChoices = GetEmperorsDaisReturnChoices(session, player);
        if (discardChoices.Count == 0 && returnChoices.Count == 0)
        {
            return actions.ToList();
        }

        var expanded = new List<RiftboundLegalAction>(actions.Count);
        var seenActionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var action in actions)
        {
            TryAddAction(action);
            if (
                action.ActionType is not (
                    RiftboundActionType.PlayCard or RiftboundActionType.StandardMove
                )
                || action.ActionId.Contains(ZaunWarrensEffect.DiscardChoiceMarker, StringComparison.Ordinal)
                || action.ActionId.Contains(EmperorsDaisEffect.ReturnUnitChoiceMarker, StringComparison.Ordinal)
            )
            {
                continue;
            }

            if (discardChoices.Count > 0)
            {
                foreach (var discard in discardChoices)
                {
                    TryAddAction(
                        new RiftboundLegalAction(
                            AppendChoiceMarker(
                                action.ActionId,
                                ZaunWarrensEffect.DiscardChoiceMarker,
                                discard.InstanceId
                            ),
                            action.ActionType,
                            action.PlayerIndex,
                            $"{action.Description} [choose discard: {discard.Name}]"
                        )
                    );
                }
            }

            if (returnChoices.Count > 0)
            {
                foreach (var unit in returnChoices)
                {
                    TryAddAction(
                        new RiftboundLegalAction(
                            AppendChoiceMarker(
                                action.ActionId,
                                EmperorsDaisEffect.ReturnUnitChoiceMarker,
                                unit.InstanceId
                            ),
                            action.ActionType,
                            action.PlayerIndex,
                            $"{action.Description} [choose return: {unit.Name}]"
                        )
                    );
                }
            }

            if (discardChoices.Count > 0 && returnChoices.Count > 0)
            {
                foreach (var discard in discardChoices)
                {
                    foreach (var unit in returnChoices)
                    {
                        var withDiscard = AppendChoiceMarker(
                            action.ActionId,
                            ZaunWarrensEffect.DiscardChoiceMarker,
                            discard.InstanceId
                        );
                        TryAddAction(
                            new RiftboundLegalAction(
                                AppendChoiceMarker(
                                    withDiscard,
                                    EmperorsDaisEffect.ReturnUnitChoiceMarker,
                                    unit.InstanceId
                                ),
                                action.ActionType,
                                action.PlayerIndex,
                                $"{action.Description} [choose discard: {discard.Name}; return: {unit.Name}]"
                            )
                        );
                    }
                }
            }
        }

        return expanded;

        void TryAddAction(RiftboundLegalAction candidate)
        {
            if (seenActionIds.Add(candidate.ActionId))
            {
                expanded.Add(candidate);
            }
        }
    }

    private static IReadOnlyList<CardInstance> GetZaunWarrensDiscardChoices(
        GameSession session,
        PlayerState player
    )
    {
        var hasZaunWarrens = session.Battlefields.Any(x =>
            string.Equals(
                RiftboundCardNameIdentifier.FromName(x.Name),
                "zaun-warrens",
                StringComparison.OrdinalIgnoreCase
            )
        );
        if (!hasZaunWarrens || player.HandZone.Cards.Count == 0)
        {
            return [];
        }

        return player.HandZone.Cards
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .ToList();
    }

    private static IReadOnlyList<CardInstance> GetEmperorsDaisReturnChoices(
        GameSession session,
        PlayerState player
    )
    {
        var choices = new List<CardInstance>();
        foreach (var battlefield in session.Battlefields)
        {
            if (
                !string.Equals(
                    RiftboundCardNameIdentifier.FromName(battlefield.Name),
                    "emperor-s-dais",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                continue;
            }

            choices.AddRange(
                battlefield.Units.Where(x => x.ControllerPlayerIndex == player.PlayerIndex)
            );
        }

        return choices
            .DistinctBy(x => x.InstanceId)
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .ToList();
    }

    private static string AppendChoiceMarker(string actionId, string marker, Guid choiceId)
    {
        var suffix = $"{marker}{choiceId}";
        var hasRepeatSuffix = actionId.EndsWith(RepeatActionSuffix, StringComparison.Ordinal);
        var coreActionId = hasRepeatSuffix ? actionId[..^RepeatActionSuffix.Length] : actionId;
        var insertionIndex = ResolveChoiceMarkerInsertionIndex(coreActionId);
        var actionWithMarker = insertionIndex >= 0
            ? coreActionId.Insert(insertionIndex, suffix)
            : $"{coreActionId}{suffix}";
        return hasRepeatSuffix ? $"{actionWithMarker}{RepeatActionSuffix}" : actionWithMarker;
    }

    private static int ResolveChoiceMarkerInsertionIndex(string actionId)
    {
        var toBaseIndex = actionId.LastIndexOf("-to-base", StringComparison.Ordinal);
        if (toBaseIndex >= 0)
        {
            return toBaseIndex;
        }

        var toBattlefieldIndex = actionId.LastIndexOf("-to-bf-", StringComparison.Ordinal);
        return toBattlefieldIndex;
    }

    private static bool IsMovableUnit(CardInstance card)
    {
        return string.Equals(card.Type, "Unit", StringComparison.OrdinalIgnoreCase) && !card.IsExhausted;
    }

    private static bool IsUnitCard(CardInstance card)
    {
        return string.Equals(card.Type, "Unit", StringComparison.OrdinalIgnoreCase);
    }

    private bool UnitHasKeyword(GameSession session, CardInstance unit, string keyword)
    {
        if (unit.Keywords.Contains(keyword, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (
            TryResolveNamedCardEffect(unit, out var namedUnitEffect)
            && session.Players.FirstOrDefault(x => x.PlayerIndex == unit.ControllerPlayerIndex)
                is { } controllingPlayer
            && namedUnitEffect.HasKeyword(_effectRuntime, session, controllingPlayer, unit, keyword)
        )
        {
            return true;
        }

        if (HasAttachedGearWithKeyword(session, unit, keyword))
        {
            return true;
        }

        return HasFriendlyAuraKeyword(session, unit, keyword);
    }

    private bool HasFriendlyAuraKeyword(GameSession session, CardInstance unit, string keyword)
    {
        var controller = session.Players.FirstOrDefault(x => x.PlayerIndex == unit.ControllerPlayerIndex);
        if (controller is null)
        {
            return false;
        }

        var auraSources = controller.BaseZone.Cards
            .Concat(controller.LegendZone.Cards)
            .Concat(controller.ChampionZone.Cards)
            .Concat(
                session.Battlefields.SelectMany(x => x.Units).Where(x =>
                    x.ControllerPlayerIndex == controller.PlayerIndex
                )
            )
            .Concat(
                session.Battlefields.SelectMany(x => x.Gear).Where(x =>
                    x.ControllerPlayerIndex == controller.PlayerIndex
                )
            )
            .GroupBy(x => x.InstanceId)
            .Select(x => x.First())
            .ToList();
        foreach (var source in auraSources)
        {
            if (!TryResolveNamedCardEffect(source, out var namedSourceEffect))
            {
                continue;
            }

            if (
                namedSourceEffect.GrantsKeywordToFriendlyUnit(
                    _effectRuntime,
                    session,
                    controller,
                    source,
                    unit,
                    keyword
                )
            )
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAttachedGearWithKeyword(
        GameSession session,
        CardInstance unit,
        string keyword
    )
    {
        return session.Players.SelectMany(x => x.BaseZone.Cards).Any(x =>
                string.Equals(x.Type, "Gear", StringComparison.OrdinalIgnoreCase)
                && x.AttachedToInstanceId == unit.InstanceId
                && (x.Keywords.Contains(keyword, StringComparer.OrdinalIgnoreCase)
                    || (
                        string.Equals(keyword, "Ganking", StringComparison.OrdinalIgnoreCase)
                        && x.EffectData.TryGetValue("grantsGanking", out var grantsGanking)
                        && string.Equals(grantsGanking, "true", StringComparison.OrdinalIgnoreCase)
                    ))
            )
            || session.Battlefields.SelectMany(x => x.Gear).Any(x =>
                string.Equals(x.Type, "Gear", StringComparison.OrdinalIgnoreCase)
                && x.AttachedToInstanceId == unit.InstanceId
                && (x.Keywords.Contains(keyword, StringComparer.OrdinalIgnoreCase)
                    || (
                        string.Equals(keyword, "Ganking", StringComparison.OrdinalIgnoreCase)
                        && x.EffectData.TryGetValue("grantsGanking", out var grantsGanking)
                        && string.Equals(grantsGanking, "true", StringComparison.OrdinalIgnoreCase)
                    ))
            );
    }

    private static bool IsRuneCard(CardInstance card)
    {
        return string.Equals(card.Type, "Rune", StringComparison.OrdinalIgnoreCase);
    }

    private void ActivateResource(GameSession session, string actionId)
    {
        var player = session.Players[session.TurnPlayerIndex];

        if (actionId.StartsWith($"{V2Prefix}activate-rune-", StringComparison.Ordinal))
        {
            var instanceId = ParseGuidFrom(actionId, $"{V2Prefix}activate-rune-");
            var rune = player.BaseZone.Cards.Single(x => x.InstanceId == instanceId);
            ActivateRuneCard(session, player, rune, "manual");
            return;
        }

        var abilityInstanceId = ParseGuidFrom(actionId, $"{V2Prefix}activate-ability-");
        var card =
            player.BaseZone.Cards.FirstOrDefault(x => x.InstanceId == abilityInstanceId)
            ?? player.LegendZone.Cards.FirstOrDefault(x => x.InstanceId == abilityInstanceId)
            ?? player.ChampionZone.Cards.FirstOrDefault(x => x.InstanceId == abilityInstanceId)
            ?? session.Battlefields.SelectMany(x => x.Units).FirstOrDefault(x =>
                x.InstanceId == abilityInstanceId && x.ControllerPlayerIndex == player.PlayerIndex
            );
        if (card is null)
        {
            return;
        }

        var activated = false;
        if (TryResolveNamedCardEffect(card, out var namedCardEffect))
        {
            activated = namedCardEffect.TryActivateAbility(_effectRuntime, session, player, card);
        }
        else if (_activatedAbilityHandlers.TryGetValue(card.EffectTemplateId, out var handler))
        {
            activated = handler(session, player, card);
        }

        if (!activated)
        {
            activated = TryActivateLegendEquipAbility(session, player, card);
        }

        if (activated)
        {
            AddEffectContext(
                session,
                card.Name,
                player.PlayerIndex,
                "Activate",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["template"] = card.EffectTemplateId,
                    ["source"] = "manual",
                }
            );
        }
    }

    private void PlayCard(GameSession session, string actionId)
    {
        var player = session.Players[GetPriorityPlayerIndex(session)];
        var instanceId = ParseGuidFrom(actionId, $"{V2Prefix}play-");
        var card = player.HandZone.Cards.Single(x => x.InstanceId == instanceId);
        var brazenDiscard = ResolveBrazenAdditionalDiscard(session, player, card, actionId);
        var callToGloryBuffSource = ResolveCallToGlorySpentBuffSource(
            session,
            player,
            card,
            actionId
        );
        var cruelPatronSacrifice = ResolveCruelPatronAdditionalSacrifice(
            session,
            player,
            card,
            actionId
        );
        var commanderLedrosSacrifices = ResolveCommanderLedrosAdditionalSacrifices(
            session,
            player,
            card,
            actionId
        );
        if (
            actionId.Contains(BrazenBuccaneerEffect.DiscardMarker, StringComparison.Ordinal)
            && brazenDiscard is null
        )
        {
            return;
        }

        if (
            actionId.Contains(CallToGloryEffect.SpendBuffMarker, StringComparison.Ordinal)
            && callToGloryBuffSource is null
        )
        {
            return;
        }
        if (
            actionId.Contains(CruelPatronEffect.SacrificeMarker, StringComparison.Ordinal)
            && cruelPatronSacrifice is null
        )
        {
            return;
        }
        if (
            actionId.Contains(CommanderLedrosEffect.SacrificeListMarker, StringComparison.Ordinal)
            && commanderLedrosSacrifices.Count == 0
        )
        {
            return;
        }

        var baseCost = ResolveBasePlayCost(session, player, card);
        if (commanderLedrosSacrifices.Count > 0)
        {
            baseCost = ApplyCommanderLedrosCostReduction(
                baseCost,
                commanderLedrosSacrifices.Count,
                "Order"
            );
        }
        var additionalActionCost = ResolveActionAdditionalCost(
            session,
            player.PlayerIndex,
            card,
            actionId
        );
        var totalCost = CombineCosts(baseCost, additionalActionCost);
        totalCost = NormalizeCostForPayment(totalCost);
        if (!TryEnsureCostCanBePaidWithReadyRunes(session, player, totalCost))
        {
            return;
        }

        SpendCost(player, totalCost);
        if (callToGloryBuffSource is not null && callToGloryBuffSource.PermanentMightModifier > 0)
        {
            callToGloryBuffSource.PermanentMightModifier -= 1;
        }

        if (brazenDiscard is not null)
        {
            DiscardFromHandAndApplyEffects(
                session,
                player,
                brazenDiscard,
                reason: "additional-cost-discard",
                sourceCard: card
            );
        }
        if (cruelPatronSacrifice is not null)
        {
            SacrificeUnitAsAdditionalCost(session, cruelPatronSacrifice);
        }
        foreach (var sacrificed in commanderLedrosSacrifices)
        {
            SacrificeUnitAsAdditionalCost(session, sacrificed);
        }

        player.HandZone.Cards.Remove(card);
        ApplyChooseTriggeredEffects(session, player, card, actionId);
        AddPendingChainItem(session, actionId, player.PlayerIndex, card.InstanceId, "PlayCard");
        OpenOrAdvancePriorityWindow(session, player.PlayerIndex);
        AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Play",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["actionId"] = actionId,
            }
        );

        var entersReadyViaAccelerate =
            IsAccelerateRequested(actionId)
            && card.Keywords.Contains("Accelerate", StringComparer.OrdinalIgnoreCase);
        var entersReadyViaAura = HasFriendlyUnitsEnterReadyThisTurn(session, player.PlayerIndex);
        var entersReady = entersReadyViaAccelerate || entersReadyViaAura;
        if (string.Equals(card.Type, "Unit", StringComparison.OrdinalIgnoreCase))
        {
            if (actionId.EndsWith("-to-base", StringComparison.Ordinal))
            {
                card.IsExhausted = !entersReady;
                player.BaseZone.Cards.Add(card);
                ApplyUnitOnPlayEffects(session, player, card, actionId);
            }
            else if (actionId.Contains("-to-bf-", StringComparison.Ordinal))
            {
                var battlefieldIndex = ParseBattlefieldIndex(actionId);
                var battlefield = session.Battlefields.Single(x => x.Index == battlefieldIndex);
                card.IsExhausted = !entersReady;
                battlefield.Units.Add(card);

                if (battlefield.ControlledByPlayerIndex != player.PlayerIndex)
                {
                    battlefield.ContestedByPlayerIndex = player.PlayerIndex;
                }

                ApplyUnitOnPlayEffects(session, player, card, actionId);
            }
        }
            else
            {
                if (!ShouldResolveOnChainFinalize(card))
                {
                    ApplySpellOrGearEffect(session, player, card, actionId);
                }
                player.TrashZone.Cards.Add(card);
                if (ReadBoolEffectData(card, "banishSelfAfterResolve", fallback: false))
                {
                    player.TrashZone.Cards.RemoveAll(x => x.InstanceId == card.InstanceId);
                }
            }

        ApplyFriendlyCardPlayTriggeredEffects(session, player, card, actionId);
        ApplySecondCardPlayTriggeredEffects(session, player);
    }

    private void MoveUnit(GameSession session, string actionId)
    {
        var player = session.Players[GetPriorityPlayerIndex(session)];
        var instanceId = ParseGuidFrom(actionId, $"{V2Prefix}move-");
        var unit = FindUnit(session, player.PlayerIndex, instanceId);
        if (
            actionId.EndsWith("-to-base", StringComparison.Ordinal)
            && RiftboundEffectUnitTargeting.IsMoveToBaseLocked(session)
        )
        {
            return;
        }

        var previousLocationKey = ResolveUnitLocationKey(session, unit.InstanceId);
        AddPendingChainItem(session, actionId, player.PlayerIndex, unit.InstanceId, "StandardMove");
        OpenOrAdvancePriorityWindow(session, player.PlayerIndex);
        unit.IsExhausted = true;

        if (actionId.EndsWith("-to-base", StringComparison.Ordinal))
        {
            RemoveUnitFromCurrentLocation(session, unit);
            player.BaseZone.Cards.Add(unit);
            MoveAttachedGearWithUnit(session, unit, destinationBattlefield: null);
            ApplyUnitMoveTriggeredEffects(session, player, unit, previousLocationKey);
            return;
        }

        var battlefieldIndex = ParseBattlefieldIndex(actionId);
        var battlefield = session.Battlefields.Single(x => x.Index == battlefieldIndex);
        RemoveUnitFromCurrentLocation(session, unit);
        battlefield.Units.Add(unit);
        MoveAttachedGearWithUnit(session, unit, destinationBattlefield: battlefield);

        if (battlefield.ControlledByPlayerIndex != player.PlayerIndex)
        {
            battlefield.ContestedByPlayerIndex = player.PlayerIndex;
        }

        ApplyUnitMoveTriggeredEffects(session, player, unit, previousLocationKey);
    }

    private void ApplyUnitOnPlayEffects(
        GameSession session,
        PlayerState player,
        CardInstance unit,
        string actionId
    )
    {
        if (TryResolveNamedCardEffect(unit, out var namedCardEffect))
        {
            namedCardEffect.OnUnitPlay(_effectRuntime, session, player, unit, actionId);
            return;
        }

        if (_unitOnPlayHandlers.TryGetValue(unit.EffectTemplateId, out var handler))
        {
            handler(session, player, unit, actionId);
        }
    }

    private void ApplySpellOrGearEffect(
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        if (TryResolveNamedCardEffect(card, out var namedCardEffect))
        {
            namedCardEffect.OnSpellOrGearPlay(_effectRuntime, session, player, card, actionId);
            return;
        }

        if (_spellOrGearHandlers.TryGetValue(card.EffectTemplateId, out var handler))
        {
            handler(session, player, card, actionId);
        }
    }

    private void DiscardFromHandAndApplyEffects(
        GameSession session,
        PlayerState player,
        CardInstance discardedCard,
        string reason,
        CardInstance? sourceCard = null
    )
    {
        if (!player.HandZone.Cards.Remove(discardedCard))
        {
            return;
        }

        player.TrashZone.Cards.Add(discardedCard);
        ApplyDiscardFromHandTriggeredEffects(session, player, discardedCard, sourceCard, reason);
    }

    private void ApplyDiscardFromHandTriggeredEffects(
        GameSession session,
        PlayerState player,
        CardInstance discardedCard,
        CardInstance? sourceCard,
        string reason
    )
    {
        if (!TryResolveNamedCardEffect(discardedCard, out var namedCardEffect))
        {
            return;
        }

        namedCardEffect.OnDiscardFromHand(
            _effectRuntime,
            session,
            player,
            discardedCard,
            sourceCard,
            reason
        );
    }

    private void SacrificeUnitAsAdditionalCost(GameSession session, CardInstance unit)
    {
        RemoveUnitFromCurrentLocation(session, unit);
        ApplyUnitDeathTriggeredEffects(session, unit);
        DetachAttachedGearToTrash(session, unit.InstanceId);
        MoveDeadUnitToDestination(session, unit);
    }

    private bool TryActivateLegendEquipAbility(GameSession session, PlayerState player, CardInstance card)
    {
        if (!CanActivateLegendEquipAbility(session, player.PlayerIndex, card))
        {
            return false;
        }

        var targetUnit = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex)
            .OrderByDescending(x => EffectiveMight(session, x))
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (targetUnit is null)
        {
            return false;
        }

        var equipment = RiftboundEffectGearTargeting.EnumerateControlledGear(session, player.PlayerIndex)
            .Where(IsEquipmentCard)
            .OrderByDescending(x => x.Cost.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
        if (equipment is null)
        {
            return false;
        }

        card.IsExhausted = true;
        RiftboundEffectGearTargeting.RemoveGearFromBoard(session, equipment);
        equipment.AttachedToInstanceId = targetUnit.InstanceId;
        var battlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(
            session,
            targetUnit.InstanceId
        );
        if (battlefield is not null)
        {
            battlefield.Gear.Add(equipment);
        }
        else
        {
            player.BaseZone.Cards.Add(equipment);
        }

        ApplyGearAttachedTriggeredEffects(session, equipment, targetUnit);
        return true;
    }

    private bool CanActivateLegendEquipAbility(GameSession session, int playerIndex, CardInstance card)
    {
        if (!string.Equals(card.Type, "Legend", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var hasForgeOfTheFluft = session.Battlefields.Any(x =>
            x.ControlledByPlayerIndex == playerIndex
            && string.Equals(
                RiftboundCardNameIdentifier.FromName(x.Name),
                "forge-of-the-fluft",
                StringComparison.OrdinalIgnoreCase
            )
        );
        if (!hasForgeOfTheFluft)
        {
            return false;
        }

        var hasFriendlyUnit = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, playerIndex).Any();
        if (!hasFriendlyUnit)
        {
            return false;
        }

        return RiftboundEffectGearTargeting.EnumerateControlledGear(session, playerIndex).Any(
            IsEquipmentCard
        );
    }

    private static bool IsEquipmentCard(CardInstance card)
    {
        return string.Equals(card.Type, "Gear", StringComparison.OrdinalIgnoreCase)
            && (
                card.Keywords.Contains("Equip", StringComparer.OrdinalIgnoreCase)
                || string.Equals(card.EffectTemplateId, "gear.attach-friendly-unit", StringComparison.Ordinal)
            );
    }

    private static CardInstance? ResolveBrazenAdditionalDiscard(
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        if (!string.Equals(card.EffectTemplateId, "named.brazen-buccaneer", StringComparison.Ordinal))
        {
            return null;
        }

        var discardId = ParseOptionalGuidFrom(actionId, BrazenBuccaneerEffect.DiscardMarker);
        if (!discardId.HasValue)
        {
            return null;
        }

        return player.HandZone.Cards.FirstOrDefault(x =>
            x.InstanceId == discardId.Value && x.InstanceId != card.InstanceId
        );
    }

    private static CardInstance? ResolveCallToGlorySpentBuffSource(
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        if (!string.Equals(card.EffectTemplateId, "named.call-to-glory", StringComparison.Ordinal))
        {
            return null;
        }

        var buffSourceId = ParseOptionalGuidFrom(actionId, CallToGloryEffect.SpendBuffMarker);
        if (!buffSourceId.HasValue)
        {
            return null;
        }

        return RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex)
            .FirstOrDefault(x => x.InstanceId == buffSourceId.Value && x.PermanentMightModifier > 0);
    }

    private static CardInstance? ResolveCruelPatronAdditionalSacrifice(
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        if (!string.Equals(card.EffectTemplateId, "named.cruel-patron", StringComparison.Ordinal))
        {
            return null;
        }

        var sacrificeId = ParseOptionalGuidFrom(actionId, CruelPatronEffect.SacrificeMarker);
        if (!sacrificeId.HasValue)
        {
            return null;
        }

        return RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex)
            .FirstOrDefault(x => x.InstanceId == sacrificeId.Value && x.InstanceId != card.InstanceId);
    }

    private static IReadOnlyCollection<CardInstance> ResolveCommanderLedrosAdditionalSacrifices(
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        if (!string.Equals(card.EffectTemplateId, "named.commander-ledros", StringComparison.Ordinal))
        {
            return [];
        }

        var markerIndex = actionId.IndexOf(CommanderLedrosEffect.SacrificeListMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return [];
        }

        var segmentStart = markerIndex + CommanderLedrosEffect.SacrificeListMarker.Length;
        var destinationIndex = actionId.IndexOf("-to-", segmentStart, StringComparison.Ordinal);
        var segment = destinationIndex > segmentStart
            ? actionId[segmentStart..destinationIndex]
            : actionId[segmentStart..];
        if (string.IsNullOrWhiteSpace(segment))
        {
            return [];
        }

        var parsed = new HashSet<Guid>();
        foreach (var raw in segment.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(raw, out var id))
            {
                return [];
            }

            parsed.Add(id);
        }

        if (parsed.Count == 0)
        {
            return [];
        }

        return RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex)
            .Where(x => parsed.Contains(x.InstanceId) && x.InstanceId != card.InstanceId)
            .ToList();
    }

    private static ResourceCost ApplyCommanderLedrosCostReduction(
        ResourceCost baseCost,
        int sacrificedUnits,
        string domain
    )
    {
        if (sacrificedUnits <= 0 || baseCost.PowerRequirements.Count == 0)
        {
            return baseCost;
        }

        var remainingReduction = sacrificedUnits;
        var updated = new List<PowerRequirement>(baseCost.PowerRequirements.Count);
        foreach (var requirement in baseCost.PowerRequirements)
        {
            if (remainingReduction <= 0 || requirement.Amount <= 0)
            {
                updated.Add(requirement);
                continue;
            }

            if (!AllowsDomain(requirement.AllowedDomains, domain))
            {
                updated.Add(requirement);
                continue;
            }

            var reducedAmount = Math.Max(0, requirement.Amount - remainingReduction);
            remainingReduction -= requirement.Amount - reducedAmount;
            updated.Add(requirement with { Amount = reducedAmount });
        }

        return new ResourceCost(baseCost.Energy, updated);
    }

    private static bool AllowsDomain(IReadOnlyCollection<string>? allowedDomains, string domain)
    {
        return allowedDomains is null
            || allowedDomains.Count == 0
            || allowedDomains.Contains(domain, StringComparer.OrdinalIgnoreCase);
    }

    private void ResolvePendingChainEffects(GameSession session)
    {
        var pendingPlayItems = session.Chain.Where(x => x.IsPending && x.Kind == "PlayCard").ToList();
        foreach (var chainItem in pendingPlayItems)
        {
            var card = RiftboundEffectCardLookup.FindCardByInstanceId(session, chainItem.CardInstanceId);
            if (card is null)
            {
                continue;
            }

            if (chainItem.IsCountered)
            {
                AddEffectContext(
                    session,
                    card.Name,
                    chainItem.ControllerPlayerIndex,
                    "Countered",
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["template"] = card.EffectTemplateId,
                        ["actionId"] = chainItem.ActionId,
                    }
                );
                continue;
            }

            if (!ShouldResolveOnChainFinalize(card))
            {
                continue;
            }

            if (
                !string.Equals(card.Type, "Spell", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(card.Type, "Gear", StringComparison.OrdinalIgnoreCase)
            )
            {
                continue;
            }

            var controller = session.Players.FirstOrDefault(x =>
                x.PlayerIndex == chainItem.ControllerPlayerIndex
            );
            if (controller is null)
            {
                continue;
            }

            ApplySpellOrGearEffect(session, controller, card, chainItem.ActionId);
        }
    }

    private void ApplyChooseTriggeredEffects(
        GameSession session,
        PlayerState player,
        CardInstance sourceCard,
        string actionId
    )
    {
        foreach (var target in ResolveTargetUnitsFromAction(session, actionId))
        {
            if (target.ControllerPlayerIndex != player.PlayerIndex)
            {
                continue;
            }

            var bonus = ReadIntEffectData(target, "onChosenTempMight", fallback: 0);
            if (bonus <= 0)
            {
                continue;
            }

            target.TemporaryMightModifier += bonus;
            AddEffectContext(
                session,
                target.Name,
                target.ControllerPlayerIndex,
                "WhenChosen",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["template"] = target.EffectTemplateId,
                    ["sourceCard"] = sourceCard.Name,
                    ["magnitude"] = bonus.ToString(),
                }
            );
        }
    }

    private void ApplyFriendlyCardPlayTriggeredEffects(
        GameSession session,
        PlayerState player,
        CardInstance playedCard,
        string actionId
    )
    {
        var listeners = player.BaseZone.Cards
            .Concat(player.LegendZone.Cards)
            .Concat(player.ChampionZone.Cards)
            .Concat(
                session
                    .Battlefields.SelectMany(x => x.Units)
                    .Where(x => x.ControllerPlayerIndex == player.PlayerIndex)
            )
            .Concat(
                session
                    .Battlefields.SelectMany(x => x.Gear)
                    .Where(x => x.ControllerPlayerIndex == player.PlayerIndex)
            )
            .GroupBy(x => x.InstanceId)
            .Select(x => x.First())
            .ToList();

        foreach (var listener in listeners)
        {
            if (!TryResolveNamedCardEffect(listener, out var namedCardEffect))
            {
                continue;
            }

            namedCardEffect.OnFriendlyCardPlayed(
                _effectRuntime,
                session,
                player,
                listener,
                playedCard,
                actionId
            );
        }
    }

    private void ApplyReadyTriggeredEffects(GameSession session, PlayerState player, CardInstance card)
    {
        var bonus = ReadIntEffectData(card, "onReadyTempMight", fallback: 0);
        if (bonus <= 0 || card.IsExhausted)
        {
            return;
        }

        card.TemporaryMightModifier += bonus;
        AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenReadied",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["magnitude"] = bonus.ToString(),
            }
        );
    }

    private int CountCardsPlayedThisTurn(GameSession session, int playerIndex)
    {
        return session.EffectContexts.Count(context =>
            context.ControllerPlayerIndex == playerIndex
            && string.Equals(context.Timing, "Play", StringComparison.OrdinalIgnoreCase)
            && IsCurrentTurnContext(session, context)
        );
    }

    private bool IsPlayerCardPlayLockedForCurrentTurn(GameSession session, int playerIndex)
    {
        return session.EffectContexts.Any(context =>
            string.Equals(context.Timing, "Aura", StringComparison.OrdinalIgnoreCase)
            && IsCurrentTurnContext(session, context)
            && context.Metadata.TryGetValue("lockOpponentCardPlayThisTurn", out var locked)
            && bool.TryParse(locked, out var isLocked)
            && isLocked
            && context.Metadata.TryGetValue("affectedPlayerIndex", out var affectedText)
            && int.TryParse(affectedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var affected)
            && affected == playerIndex
        );
    }

    private bool HasFriendlyUnitsEnterReadyThisTurn(GameSession session, int playerIndex)
    {
        return session.EffectContexts.Any(context =>
            context.ControllerPlayerIndex == playerIndex
            && string.Equals(context.Timing, "Aura", StringComparison.OrdinalIgnoreCase)
            && IsCurrentTurnContext(session, context)
            && context.Metadata.TryGetValue("friendlyUnitsEnterReadyThisTurn", out var value)
            && bool.TryParse(value, out var enabled)
            && enabled
        );
    }

    private void ApplySecondCardPlayTriggeredEffects(GameSession session, PlayerState player)
    {
        if (CountCardsPlayedThisTurn(session, player.PlayerIndex) != 2)
        {
            return;
        }

        foreach (var unit in RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex))
        {
            var tempMight = ReadIntEffectData(unit, "onSecondCardPlay.tempMight", fallback: 0);
            var ready = ReadBoolEffectData(unit, "onSecondCardPlay.ready", fallback: false);
            if (tempMight <= 0 && !ready)
            {
                continue;
            }

            if (tempMight > 0)
            {
                unit.TemporaryMightModifier += tempMight;
            }

            if (ready)
            {
                unit.IsExhausted = false;
            }

            AddEffectContext(
                session,
                unit.Name,
                player.PlayerIndex,
                "SecondCardPlayed",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["template"] = unit.EffectTemplateId,
                    ["magnitude"] = tempMight.ToString(CultureInfo.InvariantCulture),
                    ["readied"] = ready ? "true" : "false",
                }
            );
        }
    }

    private void ApplyUnitMoveTriggeredEffects(
        GameSession session,
        PlayerState movingPlayer,
        CardInstance unit,
        string? previousLocationKey
    )
    {
        if (unit.ControllerPlayerIndex != movingPlayer.PlayerIndex)
        {
            return;
        }

        if (TryResolveNamedCardEffect(unit, out var namedCardEffect))
        {
            namedCardEffect.OnUnitMove(_effectRuntime, session, movingPlayer, unit);
        }

        var attachedGearListeners = RiftboundEffectGearTargeting.EnumerateAllGear(session)
            .Where(x =>
                x.AttachedToInstanceId == unit.InstanceId
                && x.ControllerPlayerIndex == movingPlayer.PlayerIndex
            )
            .ToList();
        foreach (var attachedGear in attachedGearListeners)
        {
            if (!TryResolveNamedCardEffect(attachedGear, out var namedGearEffect))
            {
                continue;
            }

            namedGearEffect.OnUnitMove(_effectRuntime, session, movingPlayer, attachedGear);
        }

        if (TryResolveBattlefieldIndex(previousLocationKey, out var previousBattlefieldIndex))
        {
            var previousBattlefield = session.Battlefields.FirstOrDefault(x =>
                x.Index == previousBattlefieldIndex
            );
            if (previousBattlefield is not null && TryResolveBattlefieldEffect(previousBattlefield, out var battlefieldEffect))
            {
                var battlefieldCard = CreateBattlefieldEffectCard(
                    previousBattlefield,
                    movingPlayer.PlayerIndex,
                    battlefieldEffect.TemplateId
                );
                battlefieldEffect.OnUnitMoveFromBattlefield(
                    _effectRuntime,
                    session,
                    movingPlayer,
                    battlefieldCard,
                    previousBattlefield,
                    unit
                );
            }
        }

        var loot = ReadIntEffectData(unit, "onMove.loot", fallback: 0);
        var goldTokens = ReadIntEffectData(unit, "onMove.playGoldToken", fallback: 0);
        if (loot <= 0 && goldTokens <= 0)
        {
            return;
        }

        if (loot > 0)
        {
            if (movingPlayer.HandZone.Cards.Count > 0)
            {
                var discarded = movingPlayer.HandZone.Cards
                    .OrderBy(x => x.Cost.GetValueOrDefault())
                    .ThenBy(x => x.Name, StringComparer.Ordinal)
                    .ThenBy(x => x.InstanceId)
                    .First();
                DiscardFromHandAndApplyEffects(
                    session,
                    movingPlayer,
                    discarded,
                    reason: "OnMoveLoot",
                    sourceCard: unit
                );
            }

            DrawCards(movingPlayer, loot);
        }

        for (var i = 0; i < goldTokens; i += 1)
        {
            movingPlayer.BaseZone.Cards.Add(
                RiftboundTokenFactory.CreateGoldGearToken(
                    ownerPlayerIndex: movingPlayer.PlayerIndex,
                    controllerPlayerIndex: movingPlayer.PlayerIndex,
                    exhausted: true
                )
            );
        }

        AddEffectContext(
            session,
            unit.Name,
            movingPlayer.PlayerIndex,
            "WhenMove",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = unit.EffectTemplateId,
                ["loot"] = loot.ToString(CultureInfo.InvariantCulture),
                ["goldTokens"] = goldTokens.ToString(CultureInfo.InvariantCulture),
            }
        );
    }

    private void ApplyUnitDeathTriggeredEffects(GameSession session, CardInstance deadUnit)
    {
        var owner = session.Players.FirstOrDefault(x => x.PlayerIndex == deadUnit.OwnerPlayerIndex);
        if (owner is null)
        {
            return;
        }

        if (TryResolveNamedCardEffect(deadUnit, out var deadUnitEffect))
        {
            deadUnitEffect.OnDeath(_effectRuntime, session, owner, deadUnit);
        }

        ApplyFriendlyUnitDeathTriggeredEffects(session, owner, deadUnit);

        var spawnMechTokens = ReadIntEffectData(deadUnit, "onDeath.spawnMechTokens", fallback: 0);
        if (spawnMechTokens > 0)
        {
            var tokenMight = ReadIntEffectData(deadUnit, "onDeath.tokenMight", fallback: 3);
            for (var i = 0; i < spawnMechTokens; i += 1)
            {
                owner.BaseZone.Cards.Add(
                    RiftboundTokenFactory.CreateMechUnitToken(
                        ownerPlayerIndex: owner.PlayerIndex,
                        controllerPlayerIndex: owner.PlayerIndex,
                        might: tokenMight,
                        exhausted: true
                    )
                );
            }

            AddEffectContext(
                session,
                deadUnit.Name,
                owner.PlayerIndex,
                "Deathknell",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["template"] = deadUnit.EffectTemplateId,
                    ["spawnedMechTokens"] = spawnMechTokens.ToString(CultureInfo.InvariantCulture),
                    ["tokenMight"] = tokenMight.ToString(CultureInfo.InvariantCulture),
                }
            );
        }

        if (ReadBoolEffectData(deadUnit, "onDeath.readyAllRunes", fallback: false))
        {
            foreach (var rune in owner.BaseZone.Cards.Where(x =>
                         IsRuneCard(x) && x.IsExhausted
                     ))
            {
                rune.IsExhausted = false;
            }
        }

        if (ReadBoolEffectData(deadUnit, "onDeath.recycleSelf", fallback: false))
        {
            deadUnit.EffectData["deathDestination"] = "main-deck";
            AddEffectContext(
                session,
                deadUnit.Name,
                owner.PlayerIndex,
                "Deathknell",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["template"] = deadUnit.EffectTemplateId,
                    ["recycleSelf"] = "true",
                    ["readyAllRunes"] = "true",
                }
            );
        }
    }

    private void ApplyFriendlyUnitDeathTriggeredEffects(
        GameSession session,
        PlayerState owner,
        CardInstance deadUnit
    )
    {
        var listenerCards = owner.BaseZone.Cards
            .Concat(owner.LegendZone.Cards)
            .Concat(owner.ChampionZone.Cards)
            .Concat(
                session
                    .Battlefields.SelectMany(x => x.Units)
                    .Where(x => x.ControllerPlayerIndex == owner.PlayerIndex)
            )
            .Concat(
                session
                    .Battlefields.SelectMany(x => x.Gear)
                    .Where(x => x.ControllerPlayerIndex == owner.PlayerIndex)
            )
            .Where(x => x.InstanceId != deadUnit.InstanceId)
            .GroupBy(x => x.InstanceId)
            .Select(x => x.First())
            .ToList();

        foreach (var listener in listenerCards)
        {
            if (!TryResolveNamedCardEffect(listener, out var namedCardEffect))
            {
                continue;
            }

            namedCardEffect.OnFriendlyUnitDeath(
                _effectRuntime,
                session,
                owner,
                listener,
                deadUnit
            );
        }
    }

    private static void MoveDeadUnitToDestination(GameSession session, CardInstance deadUnit)
    {
        var owner = session.Players[deadUnit.OwnerPlayerIndex];
        var toMainDeck =
            deadUnit.EffectData.TryGetValue("deathDestination", out var destination)
            && string.Equals(destination, "main-deck", StringComparison.OrdinalIgnoreCase);
        deadUnit.EffectData.Remove("deathDestination");
        if (toMainDeck)
        {
            owner.MainDeckZone.Cards.Add(deadUnit);
            return;
        }

        owner.TrashZone.Cards.Add(deadUnit);
    }

    private void ApplyConquerTriggeredEffects(
        GameSession session,
        BattlefieldState battlefield,
        int conqueringPlayerIndex,
        string? sourceActionId
    )
    {
        var player = session.Players.FirstOrDefault(x => x.PlayerIndex == conqueringPlayerIndex);
        if (player is null)
        {
            return;
        }

        var resolvedSourceActionId = ResolveConquerSourceActionId(
            session,
            conqueringPlayerIndex,
            sourceActionId
        );

        foreach (var unit in battlefield.Units.Where(x => x.ControllerPlayerIndex == conqueringPlayerIndex))
        {
            if (TryResolveNamedCardEffect(unit, out var namedUnitEffect))
            {
                namedUnitEffect.OnConquer(
                    _effectRuntime,
                    session,
                    player,
                    unit,
                    battlefield,
                    resolvedSourceActionId
                );
            }

            ApplyAttachedGearConquerTriggeredEffects(
                session,
                player,
                unit,
                battlefield,
                resolvedSourceActionId
            );

            var draw = ReadIntEffectData(unit, "onConquer.draw", fallback: 0);
            if (draw <= 0)
            {
                continue;
            }

            DrawCards(player, draw);
            AddEffectContext(
                session,
                unit.Name,
                conqueringPlayerIndex,
                "WhenConquer",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["template"] = unit.EffectTemplateId,
                    ["draw"] = draw.ToString(CultureInfo.InvariantCulture),
                    ["battlefield"] = battlefield.Name,
                }
            );
        }

        foreach (var globalListener in player.LegendZone.Cards.Concat(player.ChampionZone.Cards))
        {
            if (!TryResolveNamedCardEffect(globalListener, out var namedCardEffect))
            {
                continue;
            }

            namedCardEffect.OnConquer(
                _effectRuntime,
                session,
                player,
                globalListener,
                battlefield,
                resolvedSourceActionId
            );
        }

        ApplyBattlefieldConquerTriggeredEffects(
            session,
            player,
            battlefield,
            resolvedSourceActionId
        );
    }

    private void ApplyAttachedGearConquerTriggeredEffects(
        GameSession session,
        PlayerState owner,
        CardInstance conqueringUnit,
        BattlefieldState battlefield,
        string? sourceActionId
    )
    {
        var attachedGear = owner.BaseZone.Cards
            .Concat(session.Battlefields.SelectMany(x => x.Gear))
            .Where(x =>
                string.Equals(x.Type, "Gear", StringComparison.OrdinalIgnoreCase)
                && x.ControllerPlayerIndex == owner.PlayerIndex
                && x.AttachedToInstanceId == conqueringUnit.InstanceId
            )
            .ToList();
        foreach (var gear in attachedGear)
        {
            if (!TryResolveNamedCardEffect(gear, out var namedGearEffect))
            {
                continue;
            }

            namedGearEffect.OnConquer(
                _effectRuntime,
                session,
                owner,
                gear,
                battlefield,
                sourceActionId
            );
        }
    }

    private void ApplyBattlefieldConquerTriggeredEffects(
        GameSession session,
        PlayerState conqueringPlayer,
        BattlefieldState battlefield,
        string? sourceActionId
    )
    {
        if (!TryResolveBattlefieldEffect(battlefield, out var battlefieldEffect))
        {
            return;
        }

        var battlefieldCard = CreateBattlefieldEffectCard(
            battlefield,
            conqueringPlayer.PlayerIndex,
            battlefieldEffect.TemplateId
        );
        battlefieldEffect.OnConquer(
            _effectRuntime,
            session,
            conqueringPlayer,
            battlefieldCard,
            battlefield,
            sourceActionId
        );
    }

    private static string? ResolveConquerSourceActionId(
        GameSession session,
        int conqueringPlayerIndex,
        string? sourceActionId
    )
    {
        if (!string.IsNullOrWhiteSpace(sourceActionId))
        {
            return sourceActionId;
        }

        for (var index = session.Chain.Count - 1; index >= 0; index -= 1)
        {
            var item = session.Chain[index];
            if (item.ControllerPlayerIndex == conqueringPlayerIndex)
            {
                return item.ActionId;
            }
        }

        return null;
    }

    private void ApplyWinCombatTriggeredEffects(
        GameSession session,
        BattlefieldState battlefield,
        int winningPlayerIndex
    )
    {
        var winner = session.Players.FirstOrDefault(x => x.PlayerIndex == winningPlayerIndex);
        if (winner is null)
        {
            return;
        }

        var hasDravenLegend = winner.LegendZone.Cards.Any(card =>
            string.Equals(card.Name, "Draven, Glorious Executioner", StringComparison.OrdinalIgnoreCase)
            || string.Equals(card.EffectTemplateId, "named.draven-glorious-executioner", StringComparison.Ordinal)
        );
        if (hasDravenLegend)
        {
            DrawCards(winner, 1);
            AddEffectContext(
                session,
                "Draven, Glorious Executioner",
                winner.PlayerIndex,
                "WhenWinCombat",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["draw"] = "1",
                    ["battlefield"] = battlefield.Name,
                }
            );
        }

        var listeners = winner.BaseZone.Cards
            .Concat(winner.LegendZone.Cards)
            .Concat(winner.ChampionZone.Cards)
            .Concat(
                battlefield.Units.Where(x => x.ControllerPlayerIndex == winner.PlayerIndex)
            )
            .GroupBy(x => x.InstanceId)
            .Select(x => x.First())
            .ToList();
        foreach (var listener in listeners)
        {
            if (!TryResolveNamedCardEffect(listener, out var namedCardEffect))
            {
                continue;
            }

            namedCardEffect.OnWinCombat(_effectRuntime, session, winner, listener, battlefield);
        }
    }

    private static bool IsCurrentTurnContext(GameSession session, EffectContext context)
    {
        return TryReadTurn(context, out var turn) && turn == session.TurnNumber;
    }

    private static bool TryReadTurn(EffectContext context, out int turn)
    {
        turn = -1;
        if (
            !context.Metadata.TryGetValue("turn", out var text)
            || string.IsNullOrWhiteSpace(text)
        )
        {
            return false;
        }

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out turn);
    }

    private static bool ShouldResolveOnChainFinalize(CardInstance card)
    {
        return ReadBoolEffectData(card, "resolveOnChainFinalize", fallback: false);
    }

    private ResourceCost ResolveActionAdditionalCost(
        GameSession session,
        int actingPlayerIndex,
        CardInstance card,
        string actionId
    )
    {
        var total = new ResourceCost(0, []);

        if (
            string.Equals(card.Type, "Unit", StringComparison.OrdinalIgnoreCase)
            && IsAccelerateRequested(actionId)
            && card.Keywords.Contains("Accelerate", StringComparer.OrdinalIgnoreCase)
        )
        {
            total = CombineCosts(total, ResolveAccelerateCost(card));
        }

        if (
            string.Equals(card.EffectTemplateId, "named.akshan-mischievous", StringComparison.Ordinal)
            && actionId.Contains(AkshanAdditionalCostActionMarker, StringComparison.Ordinal)
        )
        {
            total = CombineCosts(total, new ResourceCost(0, [new PowerRequirement(2, ["Body"])]));
        }

        if (
            string.Equals(card.EffectTemplateId, "named.blast-corps-cadet", StringComparison.Ordinal)
            && actionId.Contains("-blast-corps-additional-cost-", StringComparison.Ordinal)
        )
        {
            total = CombineCosts(total, new ResourceCost(1, [new PowerRequirement(1, ["Fury"])]));
        }

        if (
            string.Equals(card.EffectTemplateId, "named.clockwork-keeper", StringComparison.Ordinal)
            && actionId.Contains(ClockworkKeeperEffect.AdditionalCostMarker, StringComparison.Ordinal)
        )
        {
            total = CombineCosts(total, new ResourceCost(0, [new PowerRequirement(1, ["Calm"])]));
        }

        if (
            string.Equals(card.EffectTemplateId, "named.frostcoat-cub", StringComparison.Ordinal)
            && actionId.Contains(FrostcoatCubEffect.AdditionalCostMarker, StringComparison.Ordinal)
        )
        {
            total = CombineCosts(total, new ResourceCost(0, [new PowerRequirement(1, ["Mind"])]));
        }

        if (
            string.Equals(card.EffectTemplateId, "named.brazen-buccaneer", StringComparison.Ordinal)
            && actionId.Contains(BrazenBuccaneerEffect.DiscardMarker, StringComparison.Ordinal)
        )
        {
            var actingPlayer = session.Players.FirstOrDefault(x => x.PlayerIndex == actingPlayerIndex);
            var discardCardId = ParseOptionalGuidFrom(actionId, BrazenBuccaneerEffect.DiscardMarker);
            if (
                actingPlayer is not null
                && discardCardId.HasValue
                && actingPlayer.HandZone.Cards.Any(x =>
                    x.InstanceId == discardCardId.Value && x.InstanceId != card.InstanceId
                )
            )
            {
                total = CombineCosts(total, new ResourceCost(-2, []));
            }
        }

        if (
            string.Equals(card.EffectTemplateId, "named.call-to-glory", StringComparison.Ordinal)
            && actionId.Contains(CallToGloryEffect.SpendBuffMarker, StringComparison.Ordinal)
        )
        {
            var buffSourceId = ParseOptionalGuidFrom(actionId, CallToGloryEffect.SpendBuffMarker);
            if (buffSourceId.HasValue)
            {
                var buffSource = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(
                    session,
                    actingPlayerIndex
                ).FirstOrDefault(x => x.InstanceId == buffSourceId.Value);
                if (buffSource?.PermanentMightModifier > 0)
                {
                    total = CombineCosts(total, new ResourceCost(-card.Cost.GetValueOrDefault(), []));
                }
            }
        }

        if (
            !string.Equals(card.Type, "Spell", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(card.Type, "Gear", StringComparison.OrdinalIgnoreCase)
        )
        {
            return ApplyEzrealProdigyAdditionalCostReduction(session, actingPlayerIndex, total);
        }

        var deflectTargets = ResolveTargetUnitsFromAction(session, actionId)
            .Where(x =>
                x.ControllerPlayerIndex != actingPlayerIndex
                && UnitHasKeyword(session, x, "Deflect")
            )
            .DistinctBy(x => x.InstanceId)
            .ToList();
        if (deflectTargets.Count == 0)
        {
            return ApplyEzrealProdigyAdditionalCostReduction(session, actingPlayerIndex, total);
        }

        var requirements = Enumerable
            .Range(0, deflectTargets.Count)
            .Select(_ => new PowerRequirement(1, null))
            .ToList();
        total = CombineCosts(total, new ResourceCost(0, requirements));
        return ApplyEzrealProdigyAdditionalCostReduction(session, actingPlayerIndex, total);
    }

    private static ResourceCost ApplyEzrealProdigyAdditionalCostReduction(
        GameSession session,
        int actingPlayerIndex,
        ResourceCost total
    )
    {
        if (!total.HasAnyCost)
        {
            return total;
        }

        var hasEzrealProdigy = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, actingPlayerIndex)
            .Any(x => string.Equals(x.EffectTemplateId, "named.ezreal-prodigy", StringComparison.Ordinal));
        if (!hasEzrealProdigy)
        {
            return total;
        }

        if (total.Energy > 0)
        {
            return new ResourceCost(total.Energy - 1, total.PowerRequirements);
        }

        if (total.PowerRequirements.Count == 0)
        {
            return total;
        }

        var requirements = total.PowerRequirements.ToList();
        requirements.RemoveAt(0);
        return new ResourceCost(total.Energy, requirements);
    }

    private static ResourceCost ResolveAccelerateCost(CardInstance card)
    {
        var allowedDomains = card.ColorDomains
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        IReadOnlyCollection<string>? requirementDomains =
            allowedDomains.Count == 0 ? null : allowedDomains;
        return new ResourceCost(1, [new PowerRequirement(1, requirementDomains)]);
    }

    private static ResourceCost CombineCosts(ResourceCost left, ResourceCost right)
    {
        return new ResourceCost(
            left.Energy + right.Energy,
            left.PowerRequirements.Concat(right.PowerRequirements).ToList()
        );
    }

    private static ResourceCost NormalizeCostForPayment(ResourceCost cost)
    {
        return cost.Energy < 0
            ? new ResourceCost(0, cost.PowerRequirements)
            : cost;
    }

    private static IReadOnlyCollection<CardInstance> ResolveTargetUnitsFromAction(
        GameSession session,
        string actionId
    )
    {
        var unitIds = new HashSet<Guid>();
        var singleTarget = ParseOptionalGuidFrom(actionId, TargetUnitMarker);
        if (singleTarget.HasValue)
        {
            unitIds.Add(singleTarget.Value);
        }

        foreach (var selected in ParseGuidListFrom(actionId, MultiTargetUnitsMarker))
        {
            unitIds.Add(selected);
        }

        if (unitIds.Count == 0)
        {
            return [];
        }

        return unitIds
            .Select(unitId => TryFindUnitByInstanceId(session, unitId))
            .Where(unit => unit is not null)
            .Cast<CardInstance>()
            .ToList();
    }

    private void ActivateRuneCard(
        GameSession session,
        PlayerState player,
        CardInstance rune,
        string source
    )
    {
        if (rune.IsExhausted)
        {
            return;
        }

        rune.IsExhausted = true;
        player.RunePool.Energy += 1;
        var runeDomain = ResolveRuneDomain(rune);

        AddEffectContext(
            session,
            rune.Name,
            player.PlayerIndex,
            "Activate",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = rune.EffectTemplateId,
                ["source"] = source,
                ["domain"] = runeDomain ?? string.Empty,
            }
        );
    }

    private bool ActivateAddPowerAbility(GameSession session, PlayerState player, CardInstance card)
    {
        if (card.IsExhausted)
        {
            return false;
        }

        var addPowerDomain = ReadEffectDataString(card, "addPowerDomain");
        var amount = ReadIntEffectData(card, "addPowerAmount", fallback: 1);
        if (string.IsNullOrWhiteSpace(addPowerDomain))
        {
            return false;
        }

        card.IsExhausted = true;
        AddPower(player.RunePool.PowerByDomain, addPowerDomain!, amount);
        return true;
    }

    private ResourceCost ResolveBasePlayCost(GameSession session, PlayerState player, CardInstance card)
    {
        var baseCost = ResolveBasePlayCost(card);
        var reducedEnergy = ApplyDynamicEnergyCostReductions(session, player, card, baseCost.Energy);
        if (reducedEnergy == baseCost.Energy)
        {
            return baseCost;
        }

        return new ResourceCost(reducedEnergy, baseCost.PowerRequirements);
    }

    private int ApplyDynamicEnergyCostReductions(
        GameSession session,
        PlayerState player,
        CardInstance card,
        int baseEnergyCost
    )
    {
        var reduced = baseEnergyCost;
        if (reduced <= 0)
        {
            return 0;
        }

        var cardsPlayedThisTurn = CountCardsPlayedThisTurn(session, player.PlayerIndex);

        var discountPerCardPlayed = ReadIntEffectData(
            card,
            "energyDiscountPerCardsPlayedThisTurn",
            fallback: 0
        );
        if (discountPerCardPlayed > 0 && cardsPlayedThisTurn > 0)
        {
            reduced -= discountPerCardPlayed * cardsPlayedThisTurn;
        }

        var discountIfPlayedAnother = ReadIntEffectData(
            card,
            "energyDiscountIfPlayedAnotherCardThisTurn",
            fallback: 0
        );
        if (discountIfPlayedAnother > 0 && cardsPlayedThisTurn > 0)
        {
            reduced -= discountIfPlayedAnother;
        }

        if (
            string.Equals(card.EffectTemplateId, "named.find-your-center", StringComparison.Ordinal)
            && session.Players.Any(x =>
                x.PlayerIndex != player.PlayerIndex
                && ResolveVictoryScoreTarget(session) - x.Score <= 3
            )
        )
        {
            reduced -= 2;
        }

        var discountPerOwnTrashCard = ReadIntEffectData(
            card,
            "energyDiscountPerCardsInOwnTrash",
            fallback: 0
        );
        if (discountPerOwnTrashCard > 0 && player.TrashZone.Cards.Count > 0)
        {
            reduced -= discountPerOwnTrashCard * player.TrashZone.Cards.Count;
        }

        if (string.Equals(card.Type, "Spell", StringComparison.OrdinalIgnoreCase))
        {
            var battlefieldAuras = session.Battlefields.SelectMany(x => x.Units)
                .Where(x => x.ControllerPlayerIndex == player.PlayerIndex)
                .Select(x => new
                {
                    Discount = ReadIntEffectData(x, "spellEnergyAuraDiscount", fallback: 0),
                    Minimum = ReadIntEffectData(x, "spellEnergyAuraMinimum", fallback: 1),
                })
                .Where(x => x.Discount > 0)
                .ToList();
            if (battlefieldAuras.Count > 0)
            {
                reduced -= battlefieldAuras.Sum(x => x.Discount);
                reduced = Math.Max(
                    battlefieldAuras.Max(x => Math.Max(0, x.Minimum)),
                    reduced
                );
            }
        }

        var minimum = ReadIntEffectData(card, "energyMinimumAfterDiscount", fallback: 0);
        return Math.Max(minimum, Math.Max(0, reduced));
    }

    private static ResourceCost ResolveBasePlayCost(CardInstance card)
    {
        var requirements = new List<PowerRequirement>();
        var basePowerCost = card.Power.GetValueOrDefault();
        if (basePowerCost > 0)
        {
            var allowedDomains = card.ColorDomains
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (
                card.Keywords.Contains("Hidden", StringComparer.OrdinalIgnoreCase)
                || allowedDomains.Count == 0
            )
            {
                requirements.Add(new PowerRequirement(basePowerCost, null));
            }
            else
            {
                requirements.Add(new PowerRequirement(basePowerCost, allowedDomains));
            }
        }
        else
        {
            var legacyPowerCosts = ReadDomainCostMap(card.EffectData, PowerCostPrefix);
            requirements.AddRange(
                legacyPowerCosts.Select(x => new PowerRequirement(x.Value, [x.Key]))
            );
        }

        return new ResourceCost(card.Cost.GetValueOrDefault(), requirements);
    }

    private static ResourceCost ResolveBasePlayCost(CardInstance card, bool ignoreEnergyCost)
    {
        var baseCost = ResolveBasePlayCost(card);
        if (!ignoreEnergyCost)
        {
            return baseCost;
        }

        return new ResourceCost(0, baseCost.PowerRequirements);
    }

    private static ResourceCost ResolveRepeatCost(CardInstance card)
    {
        var energy = ReadIntEffectData(card, "repeatEnergyCost", fallback: 0);
        var requirements = ReadDomainCostMap(card.EffectData, RepeatPowerCostPrefix)
            .Select(x => new PowerRequirement(x.Value, [x.Key]))
            .ToList();
        return new ResourceCost(energy, requirements);
    }

    private static bool CanAffordCost(PlayerState player, ResourceCost cost)
    {
        if (player.RunePool.Energy < cost.Energy)
        {
            return false;
        }

        if (!cost.HasPowerCost)
        {
            return true;
        }

        return TryPlanPowerPayment(player, cost, out _);
    }

    private static void SpendCost(PlayerState player, ResourceCost cost)
    {
        player.RunePool.Energy -= cost.Energy;
        if (!cost.HasPowerCost)
        {
            return;
        }

        if (!TryPlanPowerPayment(player, cost, out var plan))
        {
            throw new InvalidOperationException("Unable to spend card cost with current rune state.");
        }

        foreach (var spentPool in plan.PoolSpendByDomain)
        {
            if (!player.RunePool.PowerByDomain.TryGetValue(spentPool.Key, out var current))
            {
                continue;
            }

            var next = current - spentPool.Value;
            if (next <= 0)
            {
                player.RunePool.PowerByDomain.Remove(spentPool.Key);
            }
            else
            {
                player.RunePool.PowerByDomain[spentPool.Key] = next;
            }
        }

        RecycleRunesForPowerPayment(player, plan.RecycledRuneByDomain);
    }

    private bool TryPayEffectCost(
        GameSession session,
        PlayerState player,
        int energyCost,
        IReadOnlyCollection<EffectPowerRequirement>? powerRequirements = null
    )
    {
        var mappedRequirements = powerRequirements?
            .Where(x => x.Amount > 0)
            .Select(x => new PowerRequirement(x.Amount, x.AllowedDomains))
            .ToList() ?? [];
        var cost = new ResourceCost(Math.Max(0, energyCost), mappedRequirements);
        if (!TryEnsureCostCanBePaidWithReadyRunes(session, player, cost))
        {
            return false;
        }

        SpendCost(player, cost);
        return true;
    }

    private void ApplyVanillaSpellEffect(
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var fallbackEnemy = session
            .Battlefields.SelectMany(b => b.Units)
            .FirstOrDefault(u => u.ControllerPlayerIndex != player.PlayerIndex);
        if (fallbackEnemy is null)
        {
            return;
        }

        fallbackEnemy.MarkedDamage += 1 + GetSpellAndAbilityBonusDamage(session, player.PlayerIndex);
    }

    private void ApplyDamageEnemyUnitSpellEffect(
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var targetUnit = ResolveTargetUnitFromAction(session, actionId);
        if (targetUnit is null || targetUnit.ControllerPlayerIndex == player.PlayerIndex)
        {
            return;
        }

        var magnitude = ReadMagnitude(card, fallback: 1)
            + GetSpellAndAbilityBonusDamage(session, player.PlayerIndex);
        targetUnit.MarkedDamage += magnitude;
    }

    private void ApplyBuffFriendlyUnitSpellEffect(
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var targetUnit = ResolveTargetUnitFromAction(session, actionId);
        if (targetUnit is null || targetUnit.ControllerPlayerIndex != player.PlayerIndex)
        {
            return;
        }

        targetUnit.TemporaryMightModifier += ReadMagnitude(card, fallback: 1);
    }

    private void ApplyKillEnemyUnitSpellEffect(
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var targetUnit = ResolveTargetUnitFromAction(session, actionId);
        if (targetUnit is null || targetUnit.ControllerPlayerIndex == player.PlayerIndex)
        {
            return;
        }

        targetUnit.MarkedDamage = EffectiveMight(session, targetUnit);
    }

    private static void ApplyDrawSpellEffect(
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        DrawCards(player, ReadMagnitude(card, fallback: 1));
    }

    private void ApplyAttachGearEffect(
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var targetUnit = ResolveTargetUnitFromAction(session, actionId);
        if (targetUnit is null || targetUnit.ControllerPlayerIndex != player.PlayerIndex)
        {
            return;
        }

        card.AttachedToInstanceId = targetUnit.InstanceId;
        card.IsExhausted = true;
        AddGearToTargetLocation(session, targetUnit, card);
        var apheliosAttachContextsBefore = CountApheliosAttachContextsThisTurn(session, targetUnit);
        ApplyGearAttachedTriggeredEffects(session, card, targetUnit);
        if (
            string.Equals(targetUnit.EffectTemplateId, "named.aphelios-exalted", StringComparison.Ordinal)
            && CountApheliosAttachContextsThisTurn(session, targetUnit) == apheliosAttachContextsBefore
        )
        {
            ApplyApheliosAttachFallback(session, player, targetUnit, card);
        }
    }

    private void ApplyGearAttachedTriggeredEffects(
        GameSession session,
        CardInstance attachedGear,
        CardInstance targetUnit
    )
    {
        var owner = session.Players.FirstOrDefault(x => x.PlayerIndex == targetUnit.ControllerPlayerIndex);
        if (owner is null)
        {
            return;
        }

        var listeners = session.Battlefields.SelectMany(x => x.Units)
            .Where(x => x.ControllerPlayerIndex == owner.PlayerIndex)
            .ToList();
        listeners.AddRange(owner.BaseZone.Cards.Where(IsUnitCard));
        listeners.AddRange(owner.LegendZone.Cards);
        listeners = listeners
            .GroupBy(x => x.InstanceId)
            .Select(x => x.First())
            .ToList();

        foreach (var listener in listeners)
        {
            if (!TryResolveNamedCardEffect(listener, out var namedCardEffect))
            {
                continue;
            }

            namedCardEffect.OnGearAttached(
                _effectRuntime,
                session,
                owner,
                listener,
                attachedGear,
                targetUnit
            );
        }
    }

    private static int CountApheliosAttachContextsThisTurn(GameSession session, CardInstance aphelios)
    {
        return session.EffectContexts.Count(x =>
            string.Equals(x.Source, aphelios.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Timing, "WhenEquipAttached", StringComparison.OrdinalIgnoreCase)
            && x.Metadata.TryGetValue("instanceId", out var instanceId)
            && string.Equals(instanceId, aphelios.InstanceId.ToString(), StringComparison.OrdinalIgnoreCase)
            && x.Metadata.TryGetValue("turn", out var turnText)
            && int.TryParse(turnText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var turn)
            && turn == session.TurnNumber
        );
    }

    private static void ApplyApheliosAttachFallback(
        GameSession session,
        PlayerState player,
        CardInstance aphelios,
        CardInstance attachedGear
    )
    {
        var used = session.EffectContexts
            .Where(x =>
                string.Equals(x.Source, aphelios.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Timing, "WhenEquipAttached", StringComparison.OrdinalIgnoreCase)
                && x.Metadata.TryGetValue("instanceId", out var instanceId)
                && string.Equals(instanceId, aphelios.InstanceId.ToString(), StringComparison.OrdinalIgnoreCase)
                && x.Metadata.TryGetValue("turn", out var turnText)
                && int.TryParse(turnText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var turn)
                && turn == session.TurnNumber
                && x.Metadata.TryGetValue("choice", out _)
            )
            .Select(x => x.Metadata["choice"])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orderedChoices = new[] { "ready-runes", "channel-exhausted", "buff-friendly" };
        var selectedChoice = orderedChoices.FirstOrDefault(x => !used.Contains(x));
        if (string.IsNullOrWhiteSpace(selectedChoice))
        {
            return;
        }

        if (string.Equals(selectedChoice, "ready-runes", StringComparison.Ordinal))
        {
            foreach (
                var rune in player.BaseZone.Cards
                    .Where(x =>
                        string.Equals(x.Type, "Rune", StringComparison.OrdinalIgnoreCase) && x.IsExhausted
                    )
                    .Take(2)
            )
            {
                rune.IsExhausted = false;
            }
        }
        else if (string.Equals(selectedChoice, "channel-exhausted", StringComparison.Ordinal))
        {
            if (player.RuneDeckZone.Cards.Count > 0)
            {
                var rune = player.RuneDeckZone.Cards[0];
                player.RuneDeckZone.Cards.RemoveAt(0);
                rune.IsExhausted = true;
                player.BaseZone.Cards.Add(rune);
            }
        }
        else
        {
            var buffTarget = session.Players[player.PlayerIndex]
                .BaseZone.Cards.Where(IsUnitCard)
                .OrderByDescending(x => x.Might.GetValueOrDefault() + x.PermanentMightModifier + x.TemporaryMightModifier)
                .ThenBy(x => x.Name, StringComparer.Ordinal)
                .ThenBy(x => x.InstanceId)
                .FirstOrDefault();
            if (buffTarget is not null)
            {
                buffTarget.PermanentMightModifier += 1;
            }
        }

        AddEffectContext(
            session,
            aphelios.Name,
            player.PlayerIndex,
            "WhenEquipAttached",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = aphelios.EffectTemplateId,
                ["choice"] = selectedChoice,
                ["instanceId"] = aphelios.InstanceId.ToString(),
                ["gear"] = attachedGear.Name,
                ["fallback"] = "true",
            }
        );
    }

    private void ApplySameLocationDamageSpellEffect(
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var magnitude = ReadMagnitude(card, fallback: 1)
            + GetSpellAndAbilityBonusDamage(session, player.PlayerIndex);
        var maxTargets = ReadIntEffectData(card, "maxTargets", fallback: 3);
        var firstTargets = ResolveSelectedSameLocationEnemyTargets(
            session,
            player.PlayerIndex,
            actionId,
            maxTargets,
            out var firstLocationKey
        );
        if (firstTargets.Count == 0)
        {
            return;
        }

        foreach (var target in firstTargets)
        {
            target.MarkedDamage += magnitude;
        }

        AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["location"] = firstLocationKey,
                ["targets"] = firstTargets.Count.ToString(),
                ["repeat"] = "false",
            }
        );

        if (!IsRepeatRequested(actionId))
        {
            return;
        }

        var repeatCost = ResolveRepeatCost(card);
        if (!repeatCost.HasAnyCost)
        {
            return;
        }

        if (!TryEnsureCostCanBePaidWithReadyRunes(session, player, repeatCost))
        {
            return;
        }

        SpendCost(player, repeatCost);
        var repeatTargets = firstTargets.ToList();
        foreach (var target in repeatTargets)
        {
            target.MarkedDamage += magnitude;
        }

        AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Resolve",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["location"] = firstLocationKey,
                ["targets"] = repeatTargets.Count.ToString(),
                ["repeat"] = "true",
            }
        );
    }

    private void ApplyPlaySpellFromTrashOnPlay(
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var maxEnergyCost = ReadIntEffectData(card, "trashSpellMaxEnergyCost", fallback: 0);
        if (maxEnergyCost <= 0)
        {
            return;
        }

        var ignoreEnergyCost = ReadBoolEffectData(card, "ignoreEnergyCostForTrashSpell", fallback: true);
        var recycleAfterPlay = ReadBoolEffectData(card, "recyclePlayedTrashSpell", fallback: true);
        var spell = player
            .TrashZone.Cards.Where(x =>
                string.Equals(x.Type, "Spell", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(x.EffectTemplateId, "unsupported", StringComparison.Ordinal)
                && x.Cost.GetValueOrDefault() <= maxEnergyCost
            )
            .Reverse()
            .FirstOrDefault(x => CanEventuallyAffordCost(player, ResolveBasePlayCost(x, ignoreEnergyCost)));

        if (spell is null)
        {
            return;
        }

        var spellCost = ResolveBasePlayCost(spell, ignoreEnergyCost);
        if (!TryEnsureCostCanBePaidWithReadyRunes(session, player, spellCost))
        {
            return;
        }

        SpendCost(player, spellCost);
        player.TrashZone.Cards.Remove(spell);
        AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["playedSpell"] = spell.Name,
                ["ignoreEnergyCost"] = ignoreEnergyCost.ToString(),
                ["recycleAfterPlay"] = recycleAfterPlay.ToString(),
            }
        );

        ApplySpellOrGearEffect(
            session,
            player,
            spell,
            $"{V2Prefix}trigger-{card.InstanceId}-cast-{spell.InstanceId}"
        );
        if (recycleAfterPlay)
        {
            player.MainDeckZone.Cards.Add(spell);
        }
        else
        {
            player.TrashZone.Cards.Add(spell);
        }
    }

    private RiftboundRevealResolution ResolveTopDeckRevealEffects(
        GameSession session,
        PlayerState player,
        CardInstance revealedCard,
        CardInstance sourceCard
    )
    {
        var addedEnergy = ReadIntEffectData(revealedCard, TopDeckRevealAddEnergyKey, fallback: 0);
        if (addedEnergy > 0)
        {
            player.RunePool.Energy += addedEnergy;
            AddEffectContext(
                session,
                revealedCard.Name,
                player.PlayerIndex,
                "Reveal",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["template"] = revealedCard.EffectTemplateId,
                    ["trigger"] = "look-reveal-top-deck",
                    ["sourceCard"] = sourceCard.Name,
                    ["addEnergy"] = addedEnergy.ToString(),
                }
            );
        }

        var playedCard = false;
        if (ReadBoolEffectData(revealedCard, TopDeckRevealPlayEnabledKey, fallback: false))
        {
            var revealCost = ResolveTopDeckRevealPlayCost(revealedCard);
            if (TryEnsureCostCanBePaidWithReadyRunes(session, player, revealCost))
            {
                SpendCost(player, revealCost);
                if (player.MainDeckZone.Cards.Remove(revealedCard))
                {
                    PlayCardFromTopDeckReveal(
                        session,
                        player,
                        revealedCard,
                        sourceCard,
                        entersReadyViaAccelerate: false,
                        preferredBattlefieldIndex: null
                    );
                    playedCard = true;
                }
            }
        }

        return new RiftboundRevealResolution(playedCard, addedEnergy);
    }

    private bool TryPlayCardFromReveal(
        GameSession session,
        PlayerState player,
        CardInstance revealedCard,
        CardInstance sourceCard,
        int energyCostReduction = 0,
        bool payAccelerateAdditionalCost = false,
        int? preferredBattlefieldIndex = null
    )
    {
        if (!player.MainDeckZone.Cards.Contains(revealedCard))
        {
            return false;
        }

        var baseCost = ResolveBasePlayCost(session, player, revealedCard);
        var adjustedCost = new ResourceCost(
            Math.Max(0, baseCost.Energy - Math.Max(0, energyCostReduction)),
            baseCost.PowerRequirements
        );
        var entersReadyViaAccelerate =
            payAccelerateAdditionalCost
            && CanUseAccelerateForRevealPlay(session, player.PlayerIndex, revealedCard);
        if (entersReadyViaAccelerate)
        {
            adjustedCost = CombineCosts(adjustedCost, ResolveAccelerateCost(revealedCard));
        }

        if (!TryEnsureCostCanBePaidWithReadyRunes(session, player, adjustedCost))
        {
            return false;
        }

        SpendCost(player, adjustedCost);
        if (!player.MainDeckZone.Cards.Remove(revealedCard))
        {
            return false;
        }

        PlayCardFromTopDeckReveal(
            session,
            player,
            revealedCard,
            sourceCard,
            entersReadyViaAccelerate,
            preferredBattlefieldIndex
        );
        return true;
    }

    private bool TryPlayCardFromRevealIgnoringCost(
        GameSession session,
        PlayerState player,
        CardInstance revealedCard,
        CardInstance sourceCard,
        int? preferredBattlefieldIndex = null
    )
    {
        if (!player.MainDeckZone.Cards.Remove(revealedCard))
        {
            return false;
        }

        PlayCardFromTopDeckReveal(
            session,
            player,
            revealedCard,
            sourceCard,
            entersReadyViaAccelerate: false,
            preferredBattlefieldIndex
        );
        return true;
    }

    private static ResourceCost ResolveTopDeckRevealPlayCost(CardInstance card)
    {
        var energy = ReadIntEffectData(card, TopDeckRevealPlayEnergyCostKey, fallback: 0);
        var powerRequirements = ReadDomainCostMap(card.EffectData, TopDeckRevealPlayPowerCostPrefix)
            .Select(x => new PowerRequirement(x.Value, [x.Key]))
            .ToList();
        return new ResourceCost(energy, powerRequirements);
    }

    private void PlayCardFromTopDeckReveal(
        GameSession session,
        PlayerState player,
        CardInstance revealedCard,
        CardInstance sourceCard,
        bool entersReadyViaAccelerate,
        int? preferredBattlefieldIndex
    )
    {
        AddEffectContext(
            session,
            revealedCard.Name,
            player.PlayerIndex,
            "RevealPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = revealedCard.EffectTemplateId,
                ["trigger"] = "look-reveal-top-deck",
                ["sourceCard"] = sourceCard.Name,
            }
        );

        if (string.Equals(revealedCard.Type, "Unit", StringComparison.OrdinalIgnoreCase))
        {
            revealedCard.ControllerPlayerIndex = player.PlayerIndex;
            var entersReady = entersReadyViaAccelerate || HasFriendlyUnitsEnterReadyThisTurn(session, player.PlayerIndex);
            revealedCard.IsExhausted = !entersReady;

            var destination = SelectPreferredFriendlyBattlefield(
                session,
                player.PlayerIndex,
                preferredBattlefieldIndex
            );
            if (destination is not null)
            {
                destination.Units.Add(revealedCard);
                if (destination.ControlledByPlayerIndex != player.PlayerIndex)
                {
                    destination.ContestedByPlayerIndex = player.PlayerIndex;
                }

                ApplyUnitOnPlayEffects(
                    session,
                    player,
                    revealedCard,
                    $"{V2Prefix}trigger-reveal-play-{revealedCard.InstanceId}-to-bf-{destination.Index}"
                );
            }
            else
            {
                player.BaseZone.Cards.Add(revealedCard);
                ApplyUnitOnPlayEffects(
                    session,
                    player,
                    revealedCard,
                    $"{V2Prefix}trigger-reveal-play-{revealedCard.InstanceId}-to-base"
                );
            }

            return;
        }

        if (
            string.Equals(revealedCard.Type, "Spell", StringComparison.OrdinalIgnoreCase)
            || string.Equals(revealedCard.Type, "Gear", StringComparison.OrdinalIgnoreCase)
        )
        {
            ApplySpellOrGearEffect(
                session,
                player,
                revealedCard,
                $"{V2Prefix}trigger-reveal-play-{revealedCard.InstanceId}-spell"
            );
            player.TrashZone.Cards.Add(revealedCard);
            return;
        }

        player.BaseZone.Cards.Add(revealedCard);
    }

    private static bool CanUseAccelerateForRevealPlay(
        GameSession session,
        int playerIndex,
        CardInstance revealedCard
    )
    {
        return revealedCard.Keywords.Contains("Accelerate", StringComparer.OrdinalIgnoreCase)
            || HasAccelerateAuraForNonHandPlay(session, playerIndex);
    }

    private static bool HasAccelerateAuraForNonHandPlay(GameSession session, int playerIndex)
    {
        return RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, playerIndex).Any(unit =>
            ReadBoolEffectData(unit, "grantAccelerateForNonHandPlay", fallback: false)
        );
    }

    private static BattlefieldState? SelectPreferredFriendlyBattlefield(
        GameSession session,
        int playerIndex,
        int? preferredBattlefieldIndex = null
    )
    {
        if (preferredBattlefieldIndex.HasValue)
        {
            var preferred = session.Battlefields.FirstOrDefault(x =>
                x.Index == preferredBattlefieldIndex.Value
            );
            if (preferred is not null)
            {
                return preferred;
            }
        }

        return session.Battlefields
            .Where(x => x.ControlledByPlayerIndex == playerIndex)
            .OrderBy(x => x.Index)
            .FirstOrDefault()
            ?? session.Battlefields.OrderBy(x => x.Index).FirstOrDefault();
    }

    private static bool CanEventuallyAffordCost(PlayerState player, ResourceCost cost)
    {
        var readyRuneCount = player.BaseZone.Cards.Count(c => !c.IsExhausted && IsRuneCard(c));
        if (player.RunePool.Energy + readyRuneCount < cost.Energy)
        {
            return false;
        }

        return !cost.HasPowerCost || TryPlanPowerPayment(player, cost, out _);
    }

    private bool TryEnsureCostCanBePaidWithReadyRunes(
        GameSession session,
        PlayerState player,
        ResourceCost cost
    )
    {
        var missingEnergy = Math.Max(0, cost.Energy - player.RunePool.Energy);
        if (missingEnergy > 0)
        {
            var availableRunes = player.BaseZone.Cards
                .Where(c => !c.IsExhausted && IsRuneCard(c))
                .OrderBy(c => c.Name, StringComparer.Ordinal)
                .ThenBy(c => c.InstanceId)
                .ToList();

            while (missingEnergy > 0 && availableRunes.Count > 0)
            {
                var nextRune = availableRunes[0];
                availableRunes.RemoveAt(0);
                ActivateRuneCard(session, player, nextRune, "auto-cost");
                missingEnergy -= 1;
            }
        }

        return CanAffordCost(player, cost);
    }

    private static bool TryPlanPowerPayment(
        PlayerState player,
        ResourceCost cost,
        out PowerPaymentPlan plan
    )
    {
        plan = PowerPaymentPlan.Empty;
        if (!cost.HasPowerCost)
        {
            return true;
        }

        var powerTokens = BuildPowerTokens(cost);
        if (powerTokens.Count == 0)
        {
            return true;
        }

        var remainingPool = player.RunePool.PowerByDomain
            .Where(x => x.Value > 0)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        var remainingRunes = player.BaseZone.Cards
            .Where(IsRuneCard)
            .GroupBy(
                rune => NormalizeRuneDomainForPayment(ResolveRuneDomain(rune)),
                StringComparer.OrdinalIgnoreCase
            )
            .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase);

        var spentPool = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var recycledRunes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var visitedStates = new HashSet<string>(StringComparer.Ordinal);
        var orderedTokens = powerTokens
            .OrderBy(x => x.FlexibilityRank)
            .ThenBy(x => x.AllowedDomains?.FirstOrDefault() ?? string.Empty, StringComparer.Ordinal)
            .ToList();

        if (
            !TryAllocatePowerTokens(
                0,
                orderedTokens,
                remainingPool,
                remainingRunes,
                spentPool,
                recycledRunes,
                visitedStates
            )
        )
        {
            return false;
        }

        plan = new PowerPaymentPlan(spentPool, recycledRunes);
        return true;
    }

    private static List<PowerToken> BuildPowerTokens(ResourceCost cost)
    {
        var tokens = new List<PowerToken>();
        foreach (var requirement in cost.PowerRequirements)
        {
            if (requirement.Amount <= 0)
            {
                continue;
            }

            IReadOnlyCollection<string>? allowedDomains = null;
            if (requirement.AllowedDomains is not null && requirement.AllowedDomains.Count > 0)
            {
                allowedDomains = requirement.AllowedDomains
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            for (var i = 0; i < requirement.Amount; i++)
            {
                tokens.Add(new PowerToken(allowedDomains));
            }
        }

        return tokens;
    }

    private static bool TryAllocatePowerTokens(
        int index,
        IReadOnlyList<PowerToken> tokens,
        Dictionary<string, int> remainingPool,
        Dictionary<string, int> remainingRunes,
        Dictionary<string, int> spentPool,
        Dictionary<string, int> recycledRunes,
        HashSet<string> visitedStates
    )
    {
        if (index >= tokens.Count)
        {
            return true;
        }

        var stateKey = BuildPowerAllocationStateKey(index, remainingPool, remainingRunes);
        if (!visitedStates.Add(stateKey))
        {
            return false;
        }

        var token = tokens[index];
        var poolDomains = remainingPool
            .Where(x => x.Value > 0 && token.AllowsDomain(x.Key))
            .Select(x => x.Key)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        foreach (var domain in poolDomains)
        {
            UpdateCount(remainingPool, domain, -1);
            AddPower(spentPool, domain, 1);
            if (
                TryAllocatePowerTokens(
                    index + 1,
                    tokens,
                    remainingPool,
                    remainingRunes,
                    spentPool,
                    recycledRunes,
                    visitedStates
                )
            )
            {
                return true;
            }

            UpdateCount(spentPool, domain, -1);
            UpdateCount(remainingPool, domain, 1);
        }

        var runeDomains = remainingRunes
            .Where(x => x.Value > 0 && token.AllowsDomain(x.Key))
            .Select(x => x.Key)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        foreach (var domain in runeDomains)
        {
            UpdateCount(remainingRunes, domain, -1);
            AddPower(recycledRunes, domain, 1);
            if (
                TryAllocatePowerTokens(
                    index + 1,
                    tokens,
                    remainingPool,
                    remainingRunes,
                    spentPool,
                    recycledRunes,
                    visitedStates
                )
            )
            {
                return true;
            }

            UpdateCount(recycledRunes, domain, -1);
            UpdateCount(remainingRunes, domain, 1);
        }

        return false;
    }

    private static string BuildPowerAllocationStateKey(
        int index,
        IReadOnlyDictionary<string, int> remainingPool,
        IReadOnlyDictionary<string, int> remainingRunes
    )
    {
        static string BuildMapKey(IReadOnlyDictionary<string, int> source)
        {
            return string.Join(
                ",",
                source
                    .Where(x => x.Value > 0)
                    .OrderBy(x => x.Key, StringComparer.Ordinal)
                    .Select(x => $"{x.Key}:{x.Value}")
            );
        }

        return $"{index}|{BuildMapKey(remainingPool)}|{BuildMapKey(remainingRunes)}";
    }

    private static void RecycleRunesForPowerPayment(
        PlayerState player,
        IReadOnlyDictionary<string, int> recycledRuneByDomain
    )
    {
        foreach (var requirement in recycledRuneByDomain.Where(x => x.Value > 0))
        {
            for (var i = 0; i < requirement.Value; i++)
            {
                var runeToRecycle = player.BaseZone.Cards.FirstOrDefault(card =>
                    IsRuneCard(card)
                    && string.Equals(
                        NormalizeRuneDomainForPayment(ResolveRuneDomain(card)),
                        requirement.Key,
                        StringComparison.OrdinalIgnoreCase
                    )
                );
                if (runeToRecycle is null)
                {
                    throw new InvalidOperationException(
                        "Unable to recycle rune for resolved power payment."
                    );
                }

                player.BaseZone.Cards.Remove(runeToRecycle);
                runeToRecycle.IsExhausted = false;
                player.RuneDeckZone.Cards.Add(runeToRecycle);
            }
        }
    }

    private static string NormalizeRuneDomainForPayment(string? domain)
    {
        return string.IsNullOrWhiteSpace(domain) ? UnknownRuneDomain : domain.Trim();
    }

    private static void UpdateCount(IDictionary<string, int> counts, string key, int delta)
    {
        if (delta == 0)
        {
            return;
        }

        counts.TryGetValue(key, out var current);
        var next = current + delta;
        if (next <= 0)
        {
            counts.Remove(key);
            return;
        }

        counts[key] = next;
    }

    private static UnitLocation? SelectBestEnemyUnitLocation(
        GameSession session,
        int actingPlayerIndex,
        string? excludeKey
    )
    {
        return EnumerateUnitLocations(session)
            .Where(location => !string.Equals(location.Key, excludeKey, StringComparison.Ordinal))
            .Select(location => new
            {
                Location = location,
                EnemyUnits = location.Units.Count(unit => unit.ControllerPlayerIndex != actingPlayerIndex),
                AllUnits = location.Units.Count,
            })
            .Where(x => x.EnemyUnits > 0)
            .OrderByDescending(x => x.EnemyUnits)
            .ThenByDescending(x => x.AllUnits)
            .ThenBy(x => x.Location.Key, StringComparer.Ordinal)
            .Select(x => x.Location)
            .FirstOrDefault();
    }

    private static IReadOnlyCollection<CardInstance> SelectEnemyTargetsAtLocation(
        UnitLocation location,
        int actingPlayerIndex,
        int maxTargets
    )
    {
        return location.Units
            .Where(unit => unit.ControllerPlayerIndex != actingPlayerIndex)
            .OrderBy(unit => unit.Name, StringComparer.Ordinal)
            .ThenBy(unit => unit.InstanceId)
            .Take(Math.Max(1, maxTargets))
            .ToList();
    }

    private static IEnumerable<UnitLocation> EnumerateUnitLocations(GameSession session)
    {
        foreach (var player in session.Players)
        {
            yield return new UnitLocation(
                $"base-{player.PlayerIndex}",
                player.BaseZone.Cards.Where(IsUnitCard).ToList()
            );
        }

        foreach (var battlefield in session.Battlefields)
        {
            yield return new UnitLocation($"bf-{battlefield.Index}", battlefield.Units.ToList());
        }
    }

    private static IReadOnlyCollection<UnitTargetSelection> EnumerateSameLocationEnemyTargetSelections(
        GameSession session,
        int actingPlayerIndex,
        int maxTargets
    )
    {
        var safeMaxTargets = Math.Max(1, maxTargets);
        var selections = new List<UnitTargetSelection>();
        foreach (
            var location in EnumerateUnitLocations(session).OrderBy(x => x.Key, StringComparer.Ordinal)
        )
        {
            var enemyUnits = location.Units
                .Where(unit => unit.ControllerPlayerIndex != actingPlayerIndex)
                .OrderBy(unit => unit.Name, StringComparer.Ordinal)
                .ThenBy(unit => unit.InstanceId)
                .ToList();
            if (enemyUnits.Count == 0)
            {
                continue;
            }

            var upperBound = Math.Min(safeMaxTargets, enemyUnits.Count);
            for (var count = 1; count <= upperBound; count++)
            {
                foreach (var combination in EnumerateUnitCombinations(enemyUnits, count))
                {
                    selections.Add(new UnitTargetSelection(location.Key, combination));
                }
            }
        }

        return selections;
    }

    private static IEnumerable<IReadOnlyList<CardInstance>> EnumerateUnitCombinations(
        IReadOnlyList<CardInstance> units,
        int count
    )
    {
        if (count <= 0 || count > units.Count)
        {
            yield break;
        }

        var indices = Enumerable.Range(0, count).ToArray();
        while (true)
        {
            yield return indices.Select(index => units[index]).ToList();

            var pivot = count - 1;
            while (pivot >= 0 && indices[pivot] == units.Count - count + pivot)
            {
                pivot--;
            }

            if (pivot < 0)
            {
                yield break;
            }

            indices[pivot]++;
            for (var i = pivot + 1; i < count; i++)
            {
                indices[i] = indices[i - 1] + 1;
            }
        }
    }

    private IReadOnlyCollection<CardInstance> ResolveSelectedSameLocationEnemyTargets(
        GameSession session,
        int actingPlayerIndex,
        string actionId,
        int maxTargets,
        out string locationKey
    )
    {
        if (
            TryResolveSelectedSameLocationEnemyTargetsFromAction(
                session,
                actingPlayerIndex,
                actionId,
                maxTargets,
                out var selectedTargets,
                out locationKey
            )
        )
        {
            return selectedTargets;
        }

        var firstLocation = SelectBestEnemyUnitLocation(session, actingPlayerIndex, null);
        if (firstLocation is null)
        {
            locationKey = string.Empty;
            return [];
        }

        locationKey = firstLocation.Key;
        return SelectEnemyTargetsAtLocation(firstLocation, actingPlayerIndex, maxTargets);
    }

    private static bool TryResolveSelectedSameLocationEnemyTargetsFromAction(
        GameSession session,
        int actingPlayerIndex,
        string actionId,
        int maxTargets,
        out IReadOnlyCollection<CardInstance> targets,
        out string locationKey
    )
    {
        targets = [];
        locationKey = string.Empty;

        var selectedUnitIds = ParseGuidListFrom(actionId, MultiTargetUnitsMarker);
        if (selectedUnitIds.Count == 0)
        {
            return false;
        }

        var safeMaxTargets = Math.Max(1, maxTargets);
        if (selectedUnitIds.Count > safeMaxTargets)
        {
            return false;
        }

        var seenUnitIds = new HashSet<Guid>();
        var selectedTargets = new List<CardInstance>(selectedUnitIds.Count);
        string? commonLocation = null;

        foreach (var unitId in selectedUnitIds)
        {
            if (!seenUnitIds.Add(unitId))
            {
                return false;
            }

            var unit = TryFindUnitByInstanceId(session, unitId);
            if (unit is null || unit.ControllerPlayerIndex == actingPlayerIndex)
            {
                return false;
            }

            var unitLocation = ResolveUnitLocationKey(session, unitId);
            if (string.IsNullOrWhiteSpace(unitLocation))
            {
                return false;
            }

            if (
                commonLocation is not null
                && !string.Equals(commonLocation, unitLocation, StringComparison.Ordinal)
            )
            {
                return false;
            }

            commonLocation = unitLocation;
            selectedTargets.Add(unit);
        }

        if (string.IsNullOrWhiteSpace(commonLocation))
        {
            return false;
        }

        targets = selectedTargets;
        locationKey = commonLocation;
        return true;
    }

    private static CardInstance? ResolveTargetUnitFromAction(GameSession session, string actionId)
    {
        var targetUnitId = ParseOptionalGuidFrom(actionId, "-target-unit-");
        if (!targetUnitId.HasValue)
        {
            return null;
        }

        return TryFindUnitByInstanceId(session, targetUnitId.Value);
    }

    private static Dictionary<string, int> ReadDomainCostMap(
        IDictionary<string, string> effectData,
        string keyPrefix
    )
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in effectData)
        {
            if (!pair.Key.StartsWith(keyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var domain = pair.Key[keyPrefix.Length..].Trim();
            if (string.IsNullOrWhiteSpace(domain))
            {
                continue;
            }

            if (!int.TryParse(pair.Value, out var value) || value <= 0)
            {
                continue;
            }

            AddPower(result, domain, value);
        }

        return result;
    }

    private static int ReadIntEffectData(CardInstance card, string key, int fallback)
    {
        return TryReadIntEffectData(card.EffectData, key, out var value) ? value : fallback;
    }

    private static bool ReadBoolEffectData(CardInstance card, string key, bool fallback)
    {
        if (!card.EffectData.TryGetValue(key, out var raw))
        {
            return fallback;
        }

        return bool.TryParse(raw, out var parsed) ? parsed : fallback;
    }

    private static string? ReadEffectDataString(CardInstance card, string key)
    {
        return card.EffectData.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw)
            ? raw.Trim()
            : null;
    }

    private static bool TryReadIntEffectData(
        IDictionary<string, string> effectData,
        string key,
        out int value
    )
    {
        if (effectData.TryGetValue(key, out var raw) && int.TryParse(raw, out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static void AddPower(IDictionary<string, int> powers, string domain, int value)
    {
        if (value <= 0 || string.IsNullOrWhiteSpace(domain))
        {
            return;
        }

        powers.TryGetValue(domain, out var current);
        powers[domain] = current + value;
    }

    private static string? ResolveRuneDomain(CardInstance rune)
    {
        if (rune.EffectData.TryGetValue("runeDomain", out var raw) && !string.IsNullOrWhiteSpace(raw))
        {
            return raw.Trim();
        }

        var cardDomain = rune.ColorDomains.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        if (!string.IsNullOrWhiteSpace(cardDomain))
        {
            return cardDomain.Trim();
        }

        var parts = rune.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        return parts[0];
    }

    private static void AddGearToTargetLocation(GameSession session, CardInstance targetUnit, CardInstance gear)
    {
        var owner = session.Players[targetUnit.ControllerPlayerIndex];
        if (owner.BaseZone.Cards.Any(x => x.InstanceId == targetUnit.InstanceId))
        {
            owner.BaseZone.Cards.Add(gear);
            return;
        }

        var battlefield = session.Battlefields.FirstOrDefault(b =>
            b.Units.Any(u => u.InstanceId == targetUnit.InstanceId)
        );
        if (battlefield is not null)
        {
            battlefield.Gear.Add(gear);
            return;
        }

        owner.BaseZone.Cards.Add(gear);
    }

    private static int ReadMagnitude(CardInstance card, int fallback)
    {
        if (
            card.EffectData.TryGetValue("magnitude", out var raw)
            && int.TryParse(raw, out var value)
            && value > 0
        )
        {
            return value;
        }

        return fallback;
    }

    private static int GetPriorityPlayerIndex(GameSession session)
    {
        if (IsPriorityWindowOpen(session) && session.Showdown.PriorityPlayerIndex.HasValue)
        {
            return session.Showdown.PriorityPlayerIndex.Value;
        }

        return session.TurnPlayerIndex;
    }

    private static TargetMode ResolveTargetMode(CardInstance card)
    {
        return card.EffectTemplateId switch
        {
            "spell.damage-enemy-unit" => TargetMode.EnemyUnit,
            "spell.kill-enemy-unit" => TargetMode.EnemyUnit,
            "spell.buff-friendly-unit" => TargetMode.FriendlyUnit,
            "gear.attach-friendly-unit" => TargetMode.FriendlyUnit,
            "named.last-rites" => TargetMode.FriendlyUnit,
            _ => TargetMode.None,
        };
    }

    private static IReadOnlyCollection<CardInstance> EnumerateUnitTargets(
        GameSession session,
        int actingPlayerIndex,
        TargetMode mode
    )
    {
        if (mode is TargetMode.None)
        {
            return [];
        }

        var allUnits = session.Battlefields.SelectMany(x => x.Units).ToList();
        allUnits.AddRange(session.Players.SelectMany(p => p.BaseZone.Cards).Where(IsUnitCard));

        return mode switch
        {
            TargetMode.EnemyUnit => allUnits
                .Where(u => u.ControllerPlayerIndex != actingPlayerIndex)
                .OrderBy(u => u.Name, StringComparer.Ordinal)
                .ToList(),
            TargetMode.FriendlyUnit => allUnits
                .Where(u => u.ControllerPlayerIndex == actingPlayerIndex)
                .OrderBy(u => u.Name, StringComparer.Ordinal)
                .ToList(),
            _ => [],
        };
    }

    private static bool IsPriorityWindowOpen(GameSession session)
    {
        return session.State == RiftboundTurnState.NeutralClosed && session.Chain.Count > 0;
    }

    private static void AddPendingChainItem(
        GameSession session,
        string actionId,
        int controllerPlayerIndex,
        Guid instanceId,
        string kind
    )
    {
        session.Chain.Add(
            new ChainItem
            {
                ActionId = actionId,
                ControllerPlayerIndex = controllerPlayerIndex,
                CardInstanceId = instanceId,
                Kind = kind,
                IsPending = true,
                IsFinalized = false,
            }
        );
    }

    private static void AddEffectContext(
        GameSession session,
        string source,
        int controllerPlayerIndex,
        string timing,
        IDictionary<string, string>? metadata = null
    )
    {
        var contextMetadata = metadata is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);
        if (!contextMetadata.ContainsKey("turn"))
        {
            contextMetadata["turn"] = session.TurnNumber.ToString(CultureInfo.InvariantCulture);
        }

        var context = new EffectContext
        {
            Source = source,
            ControllerPlayerIndex = controllerPlayerIndex,
            Timing = timing,
            Metadata = contextMetadata,
        };
        session.EffectContexts.Add(context);
    }

    private void OpenOrAdvancePriorityWindow(GameSession session, int actingPlayerIndex)
    {
        session.State = RiftboundTurnState.NeutralClosed;
        session.Showdown.FocusPlayerIndex = null;
        session.Showdown.PriorityPlayerIndex = GetOpponentPlayerIndex(session, actingPlayerIndex);
    }

    private void PassFocus(GameSession session)
    {
        if (!IsPriorityWindowOpen(session))
        {
            return;
        }

        var passingPlayerIndex = GetPriorityPlayerIndex(session);
        if (
            session.Showdown.FocusPlayerIndex.HasValue
            && session.Showdown.FocusPlayerIndex.Value != passingPlayerIndex
        )
        {
            ResolvePendingChainEffects(session);
            ResolveCleanup(session);
            if (session.PendingChoice is not null)
            {
                return;
            }

            FinalizeChain(session);
            return;
        }

        session.Showdown.FocusPlayerIndex = passingPlayerIndex;
        session.Showdown.PriorityPlayerIndex = GetOpponentPlayerIndex(session, passingPlayerIndex);
    }

    private void ResolvePendingChoice(GameSession session, string actionId)
    {
        var pendingChoice = session.PendingChoice;
        if (pendingChoice is null)
        {
            return;
        }

        var option = pendingChoice.Options.FirstOrDefault(x =>
            string.Equals(NormalizeActionId(x.ActionId), actionId, StringComparison.Ordinal)
        );
        if (option is null)
        {
            return;
        }

        session.PendingChoice = null;
        switch (pendingChoice.Kind)
        {
            case ForecasterEffect.PendingChoiceKind:
                ForecasterEffect.ResolvePendingChoice(_effectRuntime, session, pendingChoice, option);
                return;
            case GemcraftSeerEffect.PendingChoiceKind:
                GemcraftSeerEffect.ResolvePendingChoice(_effectRuntime, session, pendingChoice, option);
                return;
            case StackedDeckEffect.PendingChoiceKind:
                StackedDeckEffect.ResolvePendingChoice(_effectRuntime, session, pendingChoice, option);
                return;
            case CalledShotEffect.PendingChoiceKind:
                CalledShotEffect.ResolvePendingChoice(_effectRuntime, session, pendingChoice, option);
                return;
            case ReaversRowEffect.PendingChoiceKind:
                ReaversRowEffect.ResolvePendingChoice(_effectRuntime, session, pendingChoice, option);
                if (
                    !pendingChoice.Metadata.TryGetValue("battlefieldIndex", out var battlefieldIndexText)
                    || !int.TryParse(
                        battlefieldIndexText,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var battlefieldIndex
                    )
                )
                {
                    return;
                }

                var battlefield = session.Battlefields.FirstOrDefault(x => x.Index == battlefieldIndex);
                if (battlefield is null)
                {
                    return;
                }

                var sourceActionId = pendingChoice.Metadata.TryGetValue("sourceActionId", out var sourceAction)
                    ? sourceAction
                    : null;
                ResolveCombat(session, battlefield, sourceActionId, skipShowdownSetup: true);
                if (session.PendingChoice is not null)
                {
                    return;
                }

                if (session.Chain.Count > 0)
                {
                    FinalizeChain(session);
                }

                ResolveCleanup(session, actionId);
                return;
            default:
                return;
        }
    }

    private static CardInstance FindUnit(GameSession session, int playerIndex, Guid instanceId)
    {
        var player = session.Players[playerIndex];
        var unit = player.BaseZone.Cards.FirstOrDefault(x => x.InstanceId == instanceId);
        if (unit is not null)
        {
            return unit;
        }

        return session
            .Battlefields.SelectMany(x => x.Units)
            .Single(x => x.InstanceId == instanceId && x.ControllerPlayerIndex == playerIndex);
    }

    private static CardInstance? TryFindUnitByInstanceId(GameSession session, Guid instanceId)
    {
        var fromBase = session.Players.SelectMany(p => p.BaseZone.Cards).FirstOrDefault(c =>
            c.InstanceId == instanceId && IsUnitCard(c)
        );
        if (fromBase is not null)
        {
            return fromBase;
        }

        return session.Battlefields.SelectMany(x => x.Units).FirstOrDefault(x => x.InstanceId == instanceId);
    }

    private static string? ResolveUnitLocationKey(GameSession session, Guid instanceId)
    {
        foreach (var player in session.Players)
        {
            if (player.BaseZone.Cards.Any(card => card.InstanceId == instanceId && IsUnitCard(card)))
            {
                return $"base-{player.PlayerIndex}";
            }
        }

        foreach (var battlefield in session.Battlefields)
        {
            if (battlefield.Units.Any(unit => unit.InstanceId == instanceId))
            {
                return $"bf-{battlefield.Index}";
            }
        }

        return null;
    }

    private static void RemoveUnitFromCurrentLocation(GameSession session, CardInstance unit)
    {
        var owner = session.Players[unit.ControllerPlayerIndex];
        if (owner.BaseZone.Cards.Remove(unit))
        {
            return;
        }

        foreach (var battlefield in session.Battlefields)
        {
            if (battlefield.Units.Remove(unit))
            {
                return;
            }
        }
    }

    private static void MoveAttachedGearWithUnit(
        GameSession session,
        CardInstance unit,
        BattlefieldState? destinationBattlefield
    )
    {
        var attachedGear = RiftboundEffectGearTargeting.EnumerateAllGear(session)
            .Where(x => x.AttachedToInstanceId == unit.InstanceId)
            .ToList();
        foreach (var gear in attachedGear)
        {
            if (!RiftboundEffectGearTargeting.RemoveGearFromBoard(session, gear))
            {
                continue;
            }

            if (destinationBattlefield is null)
            {
                session.Players[gear.ControllerPlayerIndex].BaseZone.Cards.Add(gear);
            }
            else
            {
                destinationBattlefield.Gear.Add(gear);
            }
        }
    }

    private static void FinalizeChain(GameSession session)
    {
        foreach (var item in session.Chain)
        {
            item.IsPending = false;
            item.IsFinalized = true;
        }

        session.Chain.Clear();
        session.Showdown.FocusPlayerIndex = null;
        session.Showdown.PriorityPlayerIndex = null;
        session.State = RiftboundTurnState.NeutralOpen;
    }

    private void ResolveCleanup(GameSession session, string? sourceActionId = null)
    {
        foreach (var player in session.Players)
        {
            foreach (var unit in player.BaseZone.Cards.Where(IsUnitCard))
            {
                ApplyOneTimeDamagePrevention(unit);
            }

            var deadBaseUnits = player.BaseZone.Cards.Where(card => IsUnitCard(card) && IsDead(session, card)).ToList();
            foreach (var dead in deadBaseUnits)
            {
                if (TryPreventDeathWithGuardianAngel(session, dead))
                {
                    continue;
                }

                ApplyUnitDeathTriggeredEffects(session, dead);
                DetachAttachedGearToTrash(session, dead.InstanceId);
                player.BaseZone.Cards.Remove(dead);
                MoveDeadUnitToDestination(session, dead);
            }
        }

        foreach (var battlefield in session.Battlefields)
        {
            foreach (var unit in battlefield.Units)
            {
                ApplyOneTimeDamagePrevention(unit);
            }

            var deadUnits = battlefield.Units.Where(unit => IsDead(session, unit)).ToList();
            foreach (var dead in deadUnits)
            {
                if (TryPreventDeathWithGuardianAngel(session, dead))
                {
                    continue;
                }

                ApplyUnitDeathTriggeredEffects(session, dead);
                DetachAttachedGearToTrash(session, dead.InstanceId);
                battlefield.Units.Remove(dead);
                MoveDeadUnitToDestination(session, dead);
            }

            if (battlefield.ContestedByPlayerIndex is null)
            {
                if (battlefield.Units.Count == 0)
                {
                    battlefield.ControlledByPlayerIndex = null;
                }

                continue;
            }

            var distinctControllers = battlefield
                .Units.Select(u => u.ControllerPlayerIndex)
                .Distinct()
                .ToList();

            if (distinctControllers.Count >= 2)
            {
                ResolveCombat(session, battlefield, sourceActionId);
                if (session.PendingChoice is not null)
                {
                    return;
                }

                continue;
            }

            if (distinctControllers.Count == 1)
            {
                var winner = distinctControllers[0];
                var previousController = battlefield.ControlledByPlayerIndex;
                battlefield.ControlledByPlayerIndex = winner;
                if (previousController != winner)
                {
                    ApplyConquerTriggeredEffects(session, battlefield, winner, sourceActionId);
                }
                TryScore(session, winner, battlefield.Index);
            }

            battlefield.ContestedByPlayerIndex = null;
            battlefield.IsShowdownStaged = false;
            battlefield.IsCombatStaged = false;
        }

        if (session.Players.Any(p => p.Score >= ResolveVictoryScoreTarget(session)))
        {
            session.Phase = RiftboundTurnPhase.Completed;
            session.State = RiftboundTurnState.NeutralOpen;
        }
    }

    private static void ApplyOneTimeDamagePrevention(CardInstance unit)
    {
        if (
            unit.MarkedDamage <= 0
            || !unit.EffectData.TryGetValue("preventNextDamageThisTurn", out var preventText)
            || !int.TryParse(preventText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var prevention)
            || prevention <= 0
        )
        {
            return;
        }

        unit.MarkedDamage = 0;
        if (prevention <= 1)
        {
            unit.EffectData.Remove("preventNextDamageThisTurn");
            return;
        }

        unit.EffectData["preventNextDamageThisTurn"] = (prevention - 1).ToString(CultureInfo.InvariantCulture);
    }

    private void ResolveCombat(
        GameSession session,
        BattlefieldState battlefield,
        string? sourceActionId,
        bool skipShowdownSetup = false
    )
    {
        if (!skipShowdownSetup)
        {
            battlefield.IsCombatStaged = true;
            battlefield.IsShowdownStaged = true;
            session.Showdown.IsOpen = true;
            session.Showdown.BattlefieldIndex = battlefield.Index;
            session.Combat.IsOpen = true;
            session.Combat.BattlefieldIndex = battlefield.Index;
            EstablishCombatRoles(session, battlefield, out var attackerPlayerIndex, out var defenderPlayerIndex);
            ApplyShowdownStartTriggeredEffects(
                session,
                battlefield,
                attackerPlayerIndex,
                defenderPlayerIndex
            );
            ApplyReaversRowDefenderTrigger(
                session,
                battlefield,
                defenderPlayerIndex,
                sourceActionId
            );
            if (session.PendingChoice is not null)
            {
                return;
            }
        }

        var grouped = battlefield
            .Units.GroupBy(x => x.ControllerPlayerIndex)
            .Select(g => new { Player = g.Key, Might = g.Sum(unit => ResolveCombatContributionMight(session, unit)) })
            .OrderByDescending(x => x.Might)
            .ToList();

        if (grouped.Count < 2)
        {
            if (grouped.Count == 1)
            {
                var winner = grouped[0].Player;
                var previousController = battlefield.ControlledByPlayerIndex;
                battlefield.ControlledByPlayerIndex = winner;
                if (previousController != winner)
                {
                    ApplyConquerTriggeredEffects(session, battlefield, winner, sourceActionId);
                }
                TryScore(session, winner, battlefield.Index);
            }
            else
            {
                battlefield.ControlledByPlayerIndex = null;
            }

            FinalizeCombat(session, battlefield);
            return;
        }

        var top = grouped[0];
        var second = grouped[1];
        if (top.Might == second.Might)
        {
            var units = battlefield.Units.ToList();
            foreach (var unit in units)
            {
                if (TryPreventDeathWithGuardianAngel(session, unit))
                {
                    continue;
                }

                battlefield.Units.Remove(unit);
                ApplyDravenAudaciousDeathTrigger(session, unit);
                ApplyUnitDeathTriggeredEffects(session, unit);
                DetachAttachedGearToTrash(session, unit.InstanceId);
                MoveDeadUnitToDestination(session, unit);
            }

            battlefield.ControlledByPlayerIndex = null;
        }
        else
        {
            var losers = battlefield.Units.Where(u => u.ControllerPlayerIndex != top.Player).ToList();
            foreach (var loser in losers)
            {
                if (TryPreventDeathWithGuardianAngel(session, loser))
                {
                    continue;
                }

                battlefield.Units.Remove(loser);
                ApplyDravenAudaciousDeathTrigger(session, loser);
                ApplyUnitDeathTriggeredEffects(session, loser);
                DetachAttachedGearToTrash(session, loser.InstanceId);
                MoveDeadUnitToDestination(session, loser);
            }

            ApplyWinCombatTriggeredEffects(session, battlefield, top.Player);
            var previousController = battlefield.ControlledByPlayerIndex;
            battlefield.ControlledByPlayerIndex = top.Player;
            if (previousController != top.Player)
            {
                ApplyConquerTriggeredEffects(session, battlefield, top.Player, sourceActionId);
            }
            TryScore(session, top.Player, battlefield.Index);
        }

        FinalizeCombat(session, battlefield);
    }

    private static void ApplyDravenAudaciousDeathTrigger(GameSession session, CardInstance deadUnit)
    {
        if (!string.Equals(deadUnit.EffectTemplateId, "named.draven-audacious", StringComparison.Ordinal))
        {
            return;
        }

        var opponent = session.Players.FirstOrDefault(x => x.PlayerIndex != deadUnit.ControllerPlayerIndex);
        if (opponent is not null)
        {
            opponent.Score += 1;
        }
    }

    private void ApplyShowdownStartTriggeredEffects(
        GameSession session,
        BattlefieldState battlefield,
        int attackerPlayerIndex,
        int defenderPlayerIndex
    )
    {
        var listeners = new List<(PlayerState Player, CardInstance Card, bool IsAttacker, bool IsDefender)>();
        foreach (var unit in battlefield.Units)
        {
            var player = session.Players.FirstOrDefault(x => x.PlayerIndex == unit.ControllerPlayerIndex);
            if (player is null)
            {
                continue;
            }

            listeners.Add(
                (
                    player,
                    unit,
                    unit.ControllerPlayerIndex == attackerPlayerIndex,
                    unit.ControllerPlayerIndex == defenderPlayerIndex
                )
            );
        }

        foreach (var gear in battlefield.Gear.Where(x => x.AttachedToInstanceId.HasValue))
        {
            var attachedUnit = battlefield.Units.FirstOrDefault(unit =>
                unit.InstanceId == gear.AttachedToInstanceId!.Value
            );
            if (attachedUnit is null)
            {
                continue;
            }

            var player = session.Players.FirstOrDefault(x => x.PlayerIndex == gear.ControllerPlayerIndex);
            if (player is null)
            {
                continue;
            }

            listeners.Add(
                (
                    player,
                    gear,
                    attachedUnit.ControllerPlayerIndex == attackerPlayerIndex,
                    attachedUnit.ControllerPlayerIndex == defenderPlayerIndex
                )
            );
        }

        foreach (var player in session.Players)
        {
            foreach (var legend in player.LegendZone.Cards)
            {
                listeners.Add((player, legend, false, false));
            }
        }

        foreach (var (player, card, isAttacker, isDefender) in listeners)
        {
            if (!TryResolveNamedCardEffect(card, out var namedCardEffect))
            {
                continue;
            }

            namedCardEffect.OnShowdownStart(
                _effectRuntime,
                session,
                player,
                card,
                battlefield,
                isAttacker,
                isDefender
            );
        }
    }

    private static void EstablishCombatRoles(
        GameSession session,
        BattlefieldState battlefield,
        out int attackerPlayerIndex,
        out int defenderPlayerIndex
    )
    {
        var defaultAttacker = battlefield.Units.Select(x => x.ControllerPlayerIndex).FirstOrDefault();
        attackerPlayerIndex = battlefield.ContestedByPlayerIndex ?? defaultAttacker;
        var resolvedAttackerPlayerIndex = attackerPlayerIndex;
        defenderPlayerIndex = session.Players
            .Select(x => x.PlayerIndex)
            .FirstOrDefault(x => x != resolvedAttackerPlayerIndex);
        if (defenderPlayerIndex == default && session.Players.Count > 1)
        {
            defenderPlayerIndex = session.Players[1].PlayerIndex;
        }

        session.Combat.AttackerPlayerIndex = attackerPlayerIndex;
        session.Combat.DefenderPlayerIndex = defenderPlayerIndex;
        session.Showdown.FocusPlayerIndex = attackerPlayerIndex;
        session.Showdown.PriorityPlayerIndex = attackerPlayerIndex;

        ClearAttackerDefenderDesignations(session);
        foreach (var unit in battlefield.Units)
        {
            if (unit.ControllerPlayerIndex == attackerPlayerIndex)
            {
                AddKeywordIfMissing(unit, "Attacker");
                RemoveKeyword(unit, "Defender");
            }
            else if (unit.ControllerPlayerIndex == defenderPlayerIndex)
            {
                AddKeywordIfMissing(unit, "Defender");
                RemoveKeyword(unit, "Attacker");
            }
        }

        AddEffectContext(
            session,
            "Combat",
            attackerPlayerIndex,
            "ShowdownStart",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["battlefield"] = battlefield.Name,
                ["attackerPlayerIndex"] = attackerPlayerIndex.ToString(CultureInfo.InvariantCulture),
                ["defenderPlayerIndex"] = defenderPlayerIndex.ToString(CultureInfo.InvariantCulture),
            }
        );
    }

    private void ApplyReaversRowDefenderTrigger(
        GameSession session,
        BattlefieldState battlefield,
        int defenderPlayerIndex,
        string? sourceActionId
    )
    {
        if (!TryResolveBattlefieldEffect(battlefield, out var battlefieldEffect))
        {
            return;
        }

        var battlefieldCard = CreateBattlefieldEffectCard(
            battlefield,
            defenderPlayerIndex,
            battlefieldEffect.TemplateId
        );
        battlefieldEffect.OnBattlefieldShowdownStart(
            _effectRuntime,
            session,
            battlefieldCard,
            battlefield,
            session.Combat.AttackerPlayerIndex ?? 0,
            defenderPlayerIndex,
            sourceActionId
        );
    }

    private static void FinalizeCombat(GameSession session, BattlefieldState battlefield)
    {
        ClearAttackerDefenderDesignations(session);
        battlefield.ContestedByPlayerIndex = null;
        battlefield.IsCombatStaged = false;
        battlefield.IsShowdownStaged = false;
        session.Showdown.IsOpen = false;
        session.Showdown.BattlefieldIndex = null;
        session.Showdown.FocusPlayerIndex = null;
        session.Showdown.PriorityPlayerIndex = null;
        session.Combat.IsOpen = false;
        session.Combat.BattlefieldIndex = null;
        session.Combat.AttackerPlayerIndex = null;
        session.Combat.DefenderPlayerIndex = null;
    }

    private static void ClearAttackerDefenderDesignations(GameSession session)
    {
        foreach (var unit in session.Battlefields.SelectMany(x => x.Units))
        {
            RemoveKeyword(unit, "Attacker");
            RemoveKeyword(unit, "Defender");
        }

        foreach (var unit in session.Players.SelectMany(x => x.BaseZone.Cards).Where(IsUnitCard))
        {
            RemoveKeyword(unit, "Attacker");
            RemoveKeyword(unit, "Defender");
        }
    }

    private static void AddKeywordIfMissing(CardInstance unit, string keyword)
    {
        if (!unit.Keywords.Contains(keyword, StringComparer.OrdinalIgnoreCase))
        {
            unit.Keywords.Add(keyword);
        }
    }

    private static void RemoveKeyword(CardInstance unit, string keyword)
    {
        unit.Keywords.RemoveAll(x => string.Equals(x, keyword, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsDead(GameSession session, CardInstance unit)
    {
        var effectiveMight = EffectiveMight(session, unit);
        return effectiveMight > 0 && unit.MarkedDamage >= effectiveMight;
    }

    private bool TryPreventDeathWithGuardianAngel(GameSession session, CardInstance unit)
    {
        var guardianAngel = RiftboundEffectGearTargeting.EnumerateAllGear(session)
            .FirstOrDefault(x =>
                x.AttachedToInstanceId == unit.InstanceId
                && string.Equals(x.EffectTemplateId, "named.guardian-angel", StringComparison.Ordinal)
            );
        if (guardianAngel is null)
        {
            return false;
        }

        if (!RiftboundEffectGearTargeting.RemoveGearFromBoard(session, guardianAngel))
        {
            return false;
        }

        guardianAngel.AttachedToInstanceId = null;
        var guardianOwner = session.Players.FirstOrDefault(x => x.PlayerIndex == guardianAngel.OwnerPlayerIndex);
        if (guardianOwner is not null)
        {
            guardianOwner.TrashZone.Cards.Add(guardianAngel);
        }

        var otherAttachedGear = RiftboundEffectGearTargeting.EnumerateAllGear(session)
            .Where(x =>
                x.AttachedToInstanceId == unit.InstanceId
                && x.InstanceId != guardianAngel.InstanceId
            )
            .ToList();
        foreach (var gear in otherAttachedGear)
        {
            if (!RiftboundEffectGearTargeting.RemoveGearFromBoard(session, gear))
            {
                continue;
            }

            gear.AttachedToInstanceId = null;
            var gearController = session.Players.FirstOrDefault(x => x.PlayerIndex == gear.ControllerPlayerIndex);
            if (gearController is not null)
            {
                gearController.BaseZone.Cards.Add(gear);
            }
        }

        RemoveUnitFromCurrentLocation(session, unit);
        unit.MarkedDamage = 0;
        unit.IsExhausted = true;
        var owner = session.Players[unit.OwnerPlayerIndex];
        owner.HandZone.Cards.Add(unit);
        AddEffectContext(
            session,
            guardianAngel.Name,
            unit.ControllerPlayerIndex,
            "ReplaceDeath",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = guardianAngel.EffectTemplateId,
                ["savedUnit"] = unit.Name,
            }
        );
        return true;
    }

    private int ResolveCombatContributionMight(GameSession session, CardInstance unit)
    {
        if (ReadBoolEffectData(unit, "stunnedThisTurn", fallback: false))
        {
            return 0;
        }

        if (
            ReadBoolEffectData(unit, "noCombatDamage", fallback: false)
            && (
                unit.Keywords.Contains("Attacker", StringComparer.OrdinalIgnoreCase)
                || unit.Keywords.Contains("Defender", StringComparer.OrdinalIgnoreCase)
            )
        )
        {
            return 0;
        }

        return EffectiveMight(session, unit);
    }

    private int EffectiveMight(GameSession session, CardInstance unit)
    {
        var baseMight = unit.Might.GetValueOrDefault(0) + unit.PermanentMightModifier + unit.TemporaryMightModifier;
        if (unit.Keywords.Contains("Attacker", StringComparer.OrdinalIgnoreCase))
        {
            baseMight += ReadIntEffectData(unit, "temporaryAssaultBonus", fallback: 0);
        }

        if (
            unit.ShieldCount > 0
            && unit.Keywords.Contains("Defender", StringComparer.OrdinalIgnoreCase)
        )
        {
            baseMight += unit.ShieldCount;
        }

        var attachedBonus = 0;
        var doubleEquipmentBonus = string.Equals(
            unit.EffectTemplateId,
            "named.gearhead",
            StringComparison.Ordinal
        );
        foreach (var player in session.Players)
        {
            foreach (var gear in player.BaseZone.Cards.Where(x => x.AttachedToInstanceId == unit.InstanceId))
            {
                var baseBonus = ReadIntEffectData(gear, "attachedMightBonus", fallback: 0);
                if (doubleEquipmentBonus)
                {
                    baseBonus *= 2;
                }

                attachedBonus += baseBonus;
                attachedBonus += ResolveAttachedThisTurnBonus(session, gear);
            }
        }

        foreach (var battlefieldGear in session.Battlefields.SelectMany(x => x.Gear).Where(x => x.AttachedToInstanceId == unit.InstanceId))
        {
            var baseBonus = ReadIntEffectData(battlefieldGear, "attachedMightBonus", fallback: 0);
            if (doubleEquipmentBonus)
            {
                baseBonus *= 2;
            }

            attachedBonus += baseBonus;
            attachedBonus += ResolveAttachedThisTurnBonus(session, battlefieldGear);
        }

        baseMight += attachedBonus;
        var battlefield = session.Battlefields.FirstOrDefault(x =>
            x.Units.Any(y => y.InstanceId == unit.InstanceId)
        );
        if (battlefield is null)
        {
            return baseMight;
        }

        var battlefieldModifier = 0;
        if (!TryResolveBattlefieldEffect(battlefield, out var battlefieldEffect))
        {
            battlefieldModifier = 0;
        }
        else
        {
            var controller = session.Players.FirstOrDefault(x => x.PlayerIndex == unit.ControllerPlayerIndex);
            if (controller is not null)
            {
                var battlefieldCard = CreateBattlefieldEffectCard(
                    battlefield,
                    unit.ControllerPlayerIndex,
                    battlefieldEffect.TemplateId
                );
                battlefieldModifier = battlefieldEffect.GetBattlefieldUnitMightModifier(
                    _effectRuntime,
                    session,
                    controller,
                    battlefieldCard,
                    battlefield,
                    unit
                );
            }
        }

        var unitAuras = 0;
        foreach (var sourceUnit in battlefield.Units)
        {
            if (!TryResolveNamedCardEffect(sourceUnit, out var sourceEffect))
            {
                continue;
            }

            var sourceController = session.Players.FirstOrDefault(x =>
                x.PlayerIndex == sourceUnit.ControllerPlayerIndex
            );
            if (sourceController is null)
            {
                continue;
            }

            unitAuras += sourceEffect.GetBattlefieldUnitMightModifier(
                _effectRuntime,
                session,
                sourceController,
                sourceUnit,
                battlefield,
                unit
            );
        }

        return baseMight + battlefieldModifier + unitAuras;
    }

    private static int ResolveAttachedThisTurnBonus(GameSession session, CardInstance gear)
    {
        var bonus = ReadIntEffectData(gear, "attachedThisTurnMightBonus", fallback: 0);
        if (bonus <= 0)
        {
            return 0;
        }

        if (
            !gear.EffectData.TryGetValue("attachedTurnNumber", out var turnText)
            || !int.TryParse(turnText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var attachedTurn)
        )
        {
            return 0;
        }

        return attachedTurn == session.TurnNumber ? bonus : 0;
    }

    private static void DetachAttachedGearToTrash(GameSession session, Guid unitInstanceId)
    {
        foreach (var player in session.Players)
        {
            var attachedInBase = player.BaseZone.Cards
                .Where(c => c.AttachedToInstanceId == unitInstanceId)
                .ToList();
            foreach (var gear in attachedInBase)
            {
                player.BaseZone.Cards.Remove(gear);
                gear.AttachedToInstanceId = null;
                player.TrashZone.Cards.Add(gear);
            }
        }

        foreach (var battlefield in session.Battlefields)
        {
            var attachedGear = battlefield.Gear.Where(c => c.AttachedToInstanceId == unitInstanceId).ToList();
            foreach (var gear in attachedGear)
            {
                battlefield.Gear.Remove(gear);
                gear.AttachedToInstanceId = null;
                session.Players[gear.OwnerPlayerIndex].TrashZone.Cards.Add(gear);
            }
        }
    }

    private void ScoreHoldings(GameSession session, int playerIndex)
    {
        foreach (var battlefield in session.Battlefields.Where(b => b.ControlledByPlayerIndex == playerIndex))
        {
            TryScore(session, playerIndex, battlefield.Index);
        }
    }

    private void TryScore(GameSession session, int playerIndex, int battlefieldIndex)
    {
        var key = $"{session.TurnNumber}:{playerIndex}:{battlefieldIndex}";
        if (!session.UsedScoringKeys.TryAdd(key, true))
        {
            return;
        }

        var battlefield = session.Battlefields.FirstOrDefault(x => x.Index == battlefieldIndex);
        if (battlefield is null)
        {
            return;
        }

        var scoringPlayer = session.Players[playerIndex];
        if (TryResolveBattlefieldEffect(battlefield, out var battlefieldEffectForScoring))
        {
            var battlefieldCardForScoring = CreateBattlefieldEffectCard(
                battlefield,
                scoringPlayer.PlayerIndex,
                battlefieldEffectForScoring.TemplateId
            );
            if (
                !battlefieldEffectForScoring.CanPlayerScoreAtBattlefield(
                    _effectRuntime,
                    session,
                    battlefieldCardForScoring,
                    battlefield,
                    scoringPlayer
                )
            )
            {
                return;
            }
        }

        scoringPlayer.Score += 1;
        foreach (var unit in battlefield.Units.Where(unit => unit.ControllerPlayerIndex == playerIndex).ToList())
        {
            if (string.Equals(unit.EffectTemplateId, "unit.hold-score-1", StringComparison.Ordinal))
            {
                scoringPlayer.Score += 1;
            }

            if (!TryResolveNamedCardEffect(unit, out var namedCardEffect))
            {
                continue;
            }

            namedCardEffect.OnHoldScore(_effectRuntime, session, scoringPlayer, unit, battlefield);
        }

        ApplyBattlefieldHoldTriggeredEffects(session, scoringPlayer, battlefield);
    }

    private void ApplyBattlefieldHoldTriggeredEffects(
        GameSession session,
        PlayerState scoringPlayer,
        BattlefieldState battlefield
    )
    {
        if (!TryResolveBattlefieldEffect(battlefield, out var battlefieldEffect))
        {
            return;
        }

        var battlefieldCard = CreateBattlefieldEffectCard(
            battlefield,
            scoringPlayer.PlayerIndex,
            battlefieldEffect.TemplateId
        );
        battlefieldEffect.OnHoldScore(
            _effectRuntime,
            session,
            scoringPlayer,
            battlefieldCard,
            battlefield
        );
    }

    private void EndTurn(GameSession session)
    {
        if (session.Phase == RiftboundTurnPhase.Completed)
        {
            return;
        }

        session.Phase = RiftboundTurnPhase.End;
        foreach (var battlefield in session.Battlefields)
        {
            foreach (var unit in battlefield.Units)
            {
                unit.MarkedDamage = 0;
                unit.TemporaryMightModifier = 0;
                ClearEndTurnTemporaryStatuses(unit);
            }
        }

        foreach (var player in session.Players)
        {
            foreach (var unit in player.BaseZone.Cards.Where(IsUnitCard))
            {
                unit.TemporaryMightModifier = 0;
                ClearEndTurnTemporaryStatuses(unit);
            }
        }

        ApplyEndTurnTriggeredEffects(session, session.Players[session.TurnPlayerIndex]);

        foreach (var player in session.Players)
        {
            EmptyRunePool(player);
        }

        if (session.Players.Any(p => p.Score >= ResolveVictoryScoreTarget(session)))
        {
            session.Phase = RiftboundTurnPhase.Completed;
            return;
        }

        session.TurnPlayerIndex = (session.TurnPlayerIndex + 1) % session.Players.Count;
        session.TurnNumber += 1;
        session.UsedScoringKeys.Clear();
        StartTurn(session);
    }

    private static void ClearEndTurnTemporaryStatuses(CardInstance unit)
    {
        unit.ShieldCount = 0;
        unit.EffectData.Remove("temporaryAssaultBonus");
        unit.EffectData.Remove("preventNextDamageThisTurn");
        unit.EffectData.Remove("stunnedThisTurn");
        if (
            unit.EffectData.TryGetValue("temporaryTankGranted", out var temporaryTankText)
            && bool.TryParse(temporaryTankText, out var temporaryTankGranted)
            && temporaryTankGranted
        )
        {
            unit.EffectData.Remove("temporaryTankGranted");
            unit.Keywords.RemoveAll(x =>
                string.Equals(x, "Tank", StringComparison.OrdinalIgnoreCase)
            );
        }

        if (
            unit.EffectData.TryGetValue("temporaryGankingGranted", out var temporaryGankingText)
            && bool.TryParse(temporaryGankingText, out var temporaryGankingGranted)
            && temporaryGankingGranted
        )
        {
            unit.EffectData.Remove("temporaryGankingGranted");
            unit.Keywords.RemoveAll(x =>
                string.Equals(x, "Ganking", StringComparison.OrdinalIgnoreCase)
            );
        }
    }

    private void ApplyEndTurnTriggeredEffects(GameSession session, PlayerState endingPlayer)
    {
        var listeners = endingPlayer.BaseZone.Cards
            .Concat(endingPlayer.LegendZone.Cards)
            .Concat(endingPlayer.ChampionZone.Cards)
            .Concat(
                session
                    .Battlefields.SelectMany(x => x.Units)
                    .Where(x => x.ControllerPlayerIndex == endingPlayer.PlayerIndex)
            )
            .GroupBy(x => x.InstanceId)
            .Select(x => x.First())
            .ToList();
        foreach (var listener in listeners)
        {
            if (!TryResolveNamedCardEffect(listener, out var namedCardEffect))
            {
                continue;
            }

            namedCardEffect.OnEndTurn(_effectRuntime, session, endingPlayer, listener);
        }

        foreach (var battlefield in session.Battlefields)
        {
            if (!TryResolveBattlefieldEffect(battlefield, out var battlefieldEffect))
            {
                continue;
            }

            var battlefieldCard = CreateBattlefieldEffectCard(
                battlefield,
                endingPlayer.PlayerIndex,
                battlefieldEffect.TemplateId
            );
            battlefieldEffect.OnEndTurn(
                _effectRuntime,
                session,
                endingPlayer,
                battlefieldCard
            );
        }
    }

    private static int GetOpponentPlayerIndex(GameSession session, int playerIndex)
    {
        return session.Players.First(x => x.PlayerIndex != playerIndex).PlayerIndex;
    }

    private static string NormalizeActionId(string actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId))
        {
            return actionId;
        }

        if (actionId.StartsWith(V2Prefix, StringComparison.Ordinal))
        {
            return actionId;
        }

        if (string.Equals(actionId, "end-turn", StringComparison.Ordinal))
        {
            return $"{V2Prefix}end-turn";
        }

        if (string.Equals(actionId, "pass-focus", StringComparison.Ordinal))
        {
            return $"{V2Prefix}pass-focus";
        }

        if (
            actionId.StartsWith("activate-rune-", StringComparison.Ordinal)
            || actionId.StartsWith("activate-ability-", StringComparison.Ordinal)
            || actionId.StartsWith("play-", StringComparison.Ordinal)
            || actionId.StartsWith("move-", StringComparison.Ordinal)
            || actionId.StartsWith("choose-", StringComparison.Ordinal)
        )
        {
            return $"{V2Prefix}{actionId}";
        }

        return actionId;
    }

    private static bool IsRepeatRequested(string actionId)
    {
        return actionId.EndsWith(RepeatActionSuffix, StringComparison.Ordinal);
    }

    private static bool TryResolveBattlefieldIndex(string? locationKey, out int battlefieldIndex)
    {
        battlefieldIndex = -1;
        if (
            string.IsNullOrWhiteSpace(locationKey)
            || !locationKey.StartsWith("bf-", StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        return int.TryParse(locationKey[3..], NumberStyles.Integer, CultureInfo.InvariantCulture, out battlefieldIndex);
    }

    private static Guid ParseGuidFrom(string actionId, string prefix)
    {
        var trimmed = actionId.StartsWith(prefix, StringComparison.Ordinal)
            ? actionId[prefix.Length..]
            : actionId;
        var match = Regex.Match(
            trimmed,
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"
        );
        if (!match.Success)
        {
            throw new InvalidOperationException($"Unable to parse guid from action '{actionId}'.");
        }

        return Guid.Parse(match.Value);
    }

    private static Guid? ParseOptionalGuidFrom(string actionId, string marker)
    {
        var markerIndex = actionId.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var fragment = actionId[(markerIndex + marker.Length)..];
        var match = Regex.Match(
            fragment,
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"
        );
        return match.Success ? Guid.Parse(match.Value) : null;
    }

    private static IReadOnlyCollection<Guid> ParseGuidListFrom(string actionId, string marker)
    {
        var markerIndex = actionId.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return [];
        }

        var fragment = actionId[(markerIndex + marker.Length)..];
        if (fragment.EndsWith(RepeatActionSuffix, StringComparison.Ordinal))
        {
            fragment = fragment[..^RepeatActionSuffix.Length];
        }

        if (string.IsNullOrWhiteSpace(fragment))
        {
            return [];
        }

        var values = fragment.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var parsed = new List<Guid>(values.Length);
        foreach (var value in values)
        {
            if (!Guid.TryParse(value, out var unitId))
            {
                return [];
            }

            parsed.Add(unitId);
        }

        return parsed;
    }

    private static int ParseBattlefieldIndex(string actionId)
    {
        var marker = "-to-bf-";
        var idx = actionId.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
        {
            throw new InvalidOperationException($"Unable to parse battlefield index from action '{actionId}'.");
        }

        var value = actionId[(idx + marker.Length)..];
        var digitsLength = 0;
        while (digitsLength < value.Length && char.IsDigit(value[digitsLength]))
        {
            digitsLength += 1;
        }

        if (digitsLength == 0)
        {
            throw new InvalidOperationException($"Unable to parse battlefield index from action '{actionId}'.");
        }

        return int.Parse(value[..digitsLength], NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private sealed class RiftboundEffectRuntimeAdapter(RiftboundSimulationEngine engine)
        : IRiftboundEffectRuntime
    {
        public string ActionPrefix => V2Prefix;
        public string MultiTargetUnitsMarker => RiftboundSimulationEngine.MultiTargetUnitsMarker;
        public string RepeatActionSuffix => RiftboundSimulationEngine.RepeatActionSuffix;

        public int ReadMagnitude(CardInstance card, int fallback)
        {
            return RiftboundSimulationEngine.ReadMagnitude(card, fallback);
        }

        public int ReadIntEffectData(CardInstance card, string key, int fallback)
        {
            return RiftboundSimulationEngine.ReadIntEffectData(card, key, fallback);
        }

        public string? ReadEffectDataString(CardInstance card, string key)
        {
            return RiftboundSimulationEngine.ReadEffectDataString(card, key);
        }

        public bool IsRepeatRequested(string actionId)
        {
            return RiftboundSimulationEngine.IsRepeatRequested(actionId);
        }

        public IReadOnlyCollection<RiftboundTargetSelection> EnumerateSameLocationEnemyTargetSelections(
            GameSession session,
            int actingPlayerIndex,
            int maxTargets
        )
        {
            return RiftboundSimulationEngine
                .EnumerateSameLocationEnemyTargetSelections(session, actingPlayerIndex, maxTargets)
                .Select(selection =>
                    new RiftboundTargetSelection(selection.LocationKey, selection.Targets)
                )
                .ToList();
        }

        public (
            IReadOnlyCollection<CardInstance> Targets,
            string LocationKey
        ) ResolveSelectedSameLocationEnemyTargets(
            GameSession session,
            int actingPlayerIndex,
            string actionId,
            int maxTargets
        )
        {
            var targets = engine.ResolveSelectedSameLocationEnemyTargets(
                session,
                actingPlayerIndex,
                actionId,
                maxTargets,
                out var locationKey
            );
            return (targets, locationKey);
        }

        public bool TryPayRepeatCost(GameSession session, PlayerState player, CardInstance card)
        {
            var repeatCost = RiftboundSimulationEngine.ResolveRepeatCost(card);
            repeatCost = RiftboundSimulationEngine.ApplyEzrealProdigyAdditionalCostReduction(
                session,
                player.PlayerIndex,
                repeatCost
            );
            if (!repeatCost.HasAnyCost)
            {
                return false;
            }

            if (!engine.TryEnsureCostCanBePaidWithReadyRunes(session, player, repeatCost))
            {
                return false;
            }

            RiftboundSimulationEngine.SpendCost(player, repeatCost);
            return true;
        }

        public RiftboundRevealResolution ResolveTopDeckRevealEffects(
            GameSession session,
            PlayerState player,
            CardInstance revealedCard,
            CardInstance sourceCard
        )
        {
            return engine.ResolveTopDeckRevealEffects(
                session,
                player,
                revealedCard,
                sourceCard
            );
        }

        public bool TryPlayCardFromReveal(
            GameSession session,
            PlayerState player,
            CardInstance revealedCard,
            CardInstance sourceCard,
            int energyCostReduction = 0,
            bool payAccelerateAdditionalCost = false,
            int? preferredBattlefieldIndex = null
        )
        {
            return engine.TryPlayCardFromReveal(
                session,
                player,
                revealedCard,
                sourceCard,
                energyCostReduction,
                payAccelerateAdditionalCost,
                preferredBattlefieldIndex
            );
        }

        public bool TryPlayCardFromRevealIgnoringCost(
            GameSession session,
            PlayerState player,
            CardInstance revealedCard,
            CardInstance sourceCard,
            int? preferredBattlefieldIndex = null
        )
        {
            return engine.TryPlayCardFromRevealIgnoringCost(
                session,
                player,
                revealedCard,
                sourceCard,
                preferredBattlefieldIndex
            );
        }

        public bool TryPayCost(
            GameSession session,
            PlayerState player,
            int energyCost,
            IReadOnlyCollection<EffectPowerRequirement>? powerRequirements = null
        )
        {
            return engine.TryPayEffectCost(session, player, energyCost, powerRequirements);
        }

        public void AddEffectContext(
            GameSession session,
            string source,
            int controllerPlayerIndex,
            string timing,
            IDictionary<string, string>? metadata = null
        )
        {
            RiftboundSimulationEngine.AddEffectContext(
                session,
                source,
                controllerPlayerIndex,
                timing,
                metadata
            );
        }

        public void DiscardFromHand(
            GameSession session,
            PlayerState player,
            CardInstance card,
            string reason,
            CardInstance? sourceCard = null
        )
        {
            engine.DiscardFromHandAndApplyEffects(session, player, card, reason, sourceCard);
        }

        public void DrawCards(PlayerState player, int count)
        {
            RiftboundSimulationEngine.DrawCards(player, count);
        }

        public void AddPower(PlayerState player, string domain, int amount)
        {
            RiftboundSimulationEngine.AddPower(player.RunePool.PowerByDomain, domain, amount);
        }

        public int GetSpellAndAbilityBonusDamage(GameSession session, int playerIndex)
        {
            return engine.GetSpellAndAbilityBonusDamage(session, playerIndex);
        }

        public int GetEffectiveMight(GameSession session, CardInstance unit)
        {
            return engine.EffectiveMight(session, unit);
        }

        public void NotifyGearAttached(
            GameSession session,
            CardInstance attachedGear,
            CardInstance targetUnit
        )
        {
            engine.ApplyGearAttachedTriggeredEffects(session, attachedGear, targetUnit);
        }
    }

    private sealed record ResourceCost(int Energy, IReadOnlyCollection<PowerRequirement> PowerRequirements)
    {
        public bool HasPowerCost => PowerRequirements.Any(x => x.Amount > 0);
        public bool HasAnyCost => Energy > 0 || HasPowerCost;
    }

    private sealed record PowerRequirement(
        int Amount,
        IReadOnlyCollection<string>? AllowedDomains
    );

    private sealed record PowerToken(IReadOnlyCollection<string>? AllowedDomains)
    {
        public int FlexibilityRank =>
            AllowedDomains is null || AllowedDomains.Count == 0 ? int.MaxValue : AllowedDomains.Count;

        public bool AllowsDomain(string domain)
        {
            return AllowedDomains is null
                || AllowedDomains.Count == 0
                || AllowedDomains.Contains(domain, StringComparer.OrdinalIgnoreCase);
        }
    }

    private sealed record PowerPaymentPlan(
        IReadOnlyDictionary<string, int> PoolSpendByDomain,
        IReadOnlyDictionary<string, int> RecycledRuneByDomain
    )
    {
        public static PowerPaymentPlan Empty { get; } = new(
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        );
    }

    private sealed record UnitLocation(string Key, IReadOnlyCollection<CardInstance> Units);
    private sealed record UnitTargetSelection(string LocationKey, IReadOnlyList<CardInstance> Targets);

    private enum TargetMode
    {
        None,
        FriendlyUnit,
        EnemyUnit,
    }
}
