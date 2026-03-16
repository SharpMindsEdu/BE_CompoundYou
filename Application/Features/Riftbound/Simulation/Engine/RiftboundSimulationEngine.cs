using Application.Features.Riftbound.Simulation.Definitions;
using Domain.Entities.Riftbound;
using Domain.Simulation;
using System.Text.RegularExpressions;

namespace Application.Features.Riftbound.Simulation.Engine;

public sealed class RiftboundSimulationEngine : IRiftboundSimulationEngine
{
    private const int DuelVictoryScore = 8;
    private const string V2Prefix = "v2:";
    private const string PowerCostPrefix = "powerCost.";
    private const string RepeatPowerCostPrefix = "repeatPowerCost.";
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

            foreach (var card in player.BaseZone.Cards.Where(IsActivatableCard))
            {
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

        foreach (var card in player.HandZone.Cards.Where(card =>
            !isPriorityWindowOpen || IsQuickSpeedCard(card)
        ))
        {
            if (!CanPlayCard(card, player))
            {
                continue;
            }

            if (string.Equals(card.Type, "Unit", StringComparison.OrdinalIgnoreCase))
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{V2Prefix}play-{card.InstanceId}-to-base",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name} to base"
                    )
                );

                foreach (var battlefield in session.Battlefields.Where(b =>
                    b.ControlledByPlayerIndex == player.PlayerIndex
                ))
                {
                    actions.Add(
                        new RiftboundLegalAction(
                            $"{V2Prefix}play-{card.InstanceId}-to-bf-{battlefield.Index}",
                            RiftboundActionType.PlayCard,
                            player.PlayerIndex,
                            $"Play {card.Name} to battlefield {battlefield.Name}"
                        )
                    );
                }
            }
            else if (string.Equals(card.Type, "Spell", StringComparison.OrdinalIgnoreCase) || string.Equals(card.Type, "Gear", StringComparison.OrdinalIgnoreCase))
            {
                var targetMode = ResolveTargetMode(card);
                if (targetMode is TargetMode.None)
                {
                    actions.Add(
                        new RiftboundLegalAction(
                            $"{V2Prefix}play-{card.InstanceId}-spell",
                            RiftboundActionType.PlayCard,
                            player.PlayerIndex,
                            $"Play spell {card.Name}"
                        )
                    );
                }
                else
                {
                    foreach (var target in EnumerateUnitTargets(session, player.PlayerIndex, targetMode))
                    {
                        actions.Add(
                            new RiftboundLegalAction(
                                $"{V2Prefix}play-{card.InstanceId}-spell-target-unit-{target.InstanceId}",
                                RiftboundActionType.PlayCard,
                                player.PlayerIndex,
                                $"Play {card.Name} targeting {target.Name}"
                            )
                        );
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
                    actions.Add(
                        new RiftboundLegalAction(
                            $"{V2Prefix}move-{unit.InstanceId}-to-base",
                            RiftboundActionType.StandardMove,
                            player.PlayerIndex,
                            $"Move {unit.Name} to base"
                        )
                    );

                    if (unit.Keywords.Contains("Ganking", StringComparer.OrdinalIgnoreCase))
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

        return actions;
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

        if (
            normalizedActionId.StartsWith($"{V2Prefix}activate-rune-", StringComparison.Ordinal)
            || normalizedActionId.StartsWith($"{V2Prefix}activate-ability-", StringComparison.Ordinal)
        )
        {
            ActivateResource(session, normalizedActionId);
            ResolveCleanup(session);
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
                Cards = deck.Champion is null
                    ? []
                    : [BuildCardInstance(deck.Champion, playerIndex, playerIndex)],
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
        session.Phase = RiftboundTurnPhase.Awaken;
        ReadyCards(player.BaseZone.Cards);
        foreach (var battlefield in session.Battlefields)
        {
            ReadyCards(battlefield.Units.Where(x => x.ControllerPlayerIndex == player.PlayerIndex));
        }

        session.Phase = RiftboundTurnPhase.Beginning;
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

    private static bool CanPlayCard(CardInstance card, PlayerState player)
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

        return CanAffordCost(player, ResolveBasePlayCost(card));
    }

    private bool IsActivatableCard(CardInstance card)
    {
        return !card.IsExhausted && _activatedAbilityHandlers.ContainsKey(card.EffectTemplateId);
    }

    private static bool IsQuickSpeedCard(CardInstance card)
    {
        return card.Keywords.Contains("Action", StringComparer.OrdinalIgnoreCase)
            || card.Keywords.Contains("Reaction", StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsMovableUnit(CardInstance card)
    {
        return string.Equals(card.Type, "Unit", StringComparison.OrdinalIgnoreCase) && !card.IsExhausted;
    }

    private static bool IsUnitCard(CardInstance card)
    {
        return string.Equals(card.Type, "Unit", StringComparison.OrdinalIgnoreCase);
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
        var card = player.BaseZone.Cards.Single(x => x.InstanceId == abilityInstanceId);
        if (_activatedAbilityHandlers.TryGetValue(card.EffectTemplateId, out var handler))
        {
            var activated = handler(session, player, card);
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
    }

    private void PlayCard(GameSession session, string actionId)
    {
        var player = session.Players[GetPriorityPlayerIndex(session)];
        var instanceId = ParseGuidFrom(actionId, $"{V2Prefix}play-");
        var card = player.HandZone.Cards.Single(x => x.InstanceId == instanceId);

        var baseCost = ResolveBasePlayCost(card);
        SpendCost(player, baseCost);

        player.HandZone.Cards.Remove(card);
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

        if (actionId.EndsWith("-to-base", StringComparison.Ordinal))
        {
            card.IsExhausted = !card.Keywords.Contains("Accelerate", StringComparer.OrdinalIgnoreCase);
            player.BaseZone.Cards.Add(card);
            ApplyUnitOnPlayEffects(session, player, card, actionId);
        }
        else if (actionId.Contains("-to-bf-", StringComparison.Ordinal))
        {
            var battlefieldIndex = ParseBattlefieldIndex(actionId);
            var battlefield = session.Battlefields.Single(x => x.Index == battlefieldIndex);
            card.IsExhausted = !card.Keywords.Contains("Accelerate", StringComparer.OrdinalIgnoreCase);
            battlefield.Units.Add(card);

            if (battlefield.ControlledByPlayerIndex != player.PlayerIndex)
            {
                battlefield.ContestedByPlayerIndex = player.PlayerIndex;
            }

            ApplyUnitOnPlayEffects(session, player, card, actionId);
        }
        else
        {
            ApplySpellOrGearEffect(session, player, card, actionId);
            player.TrashZone.Cards.Add(card);
        }
    }

    private void MoveUnit(GameSession session, string actionId)
    {
        var player = session.Players[GetPriorityPlayerIndex(session)];
        var instanceId = ParseGuidFrom(actionId, $"{V2Prefix}move-");
        var unit = FindUnit(session, player.PlayerIndex, instanceId);
        AddPendingChainItem(session, actionId, player.PlayerIndex, unit.InstanceId, "StandardMove");
        OpenOrAdvancePriorityWindow(session, player.PlayerIndex);
        unit.IsExhausted = true;

        if (actionId.EndsWith("-to-base", StringComparison.Ordinal))
        {
            RemoveUnitFromCurrentLocation(session, unit);
            player.BaseZone.Cards.Add(unit);
            return;
        }

        var battlefieldIndex = ParseBattlefieldIndex(actionId);
        var battlefield = session.Battlefields.Single(x => x.Index == battlefieldIndex);
        RemoveUnitFromCurrentLocation(session, unit);
        battlefield.Units.Add(unit);

        if (battlefield.ControlledByPlayerIndex != player.PlayerIndex)
        {
            battlefield.ContestedByPlayerIndex = player.PlayerIndex;
        }
    }

    private void ApplyUnitOnPlayEffects(
        GameSession session,
        PlayerState player,
        CardInstance unit,
        string actionId
    )
    {
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
        if (_spellOrGearHandlers.TryGetValue(card.EffectTemplateId, out var handler))
        {
            handler(session, player, card, actionId);
        }
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
        if (!string.IsNullOrWhiteSpace(runeDomain))
        {
            AddPower(player.RunePool.PowerByDomain, runeDomain!, 1);
        }

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

    private static ResourceCost ResolveBasePlayCost(CardInstance card)
    {
        return new ResourceCost(
            card.Cost.GetValueOrDefault(),
            ReadDomainCostMap(card.EffectData, PowerCostPrefix)
        );
    }

    private static ResourceCost ResolveBasePlayCost(CardInstance card, bool ignoreEnergyCost)
    {
        var baseCost = ResolveBasePlayCost(card);
        if (!ignoreEnergyCost)
        {
            return baseCost;
        }

        return new ResourceCost(0, baseCost.PowerByDomain);
    }

    private static ResourceCost ResolveRepeatCost(CardInstance card)
    {
        var energy = ReadIntEffectData(card, "repeatEnergyCost", fallback: 0);
        return new ResourceCost(
            energy,
            ReadDomainCostMap(card.EffectData, RepeatPowerCostPrefix)
        );
    }

    private static bool CanAffordCost(PlayerState player, ResourceCost cost)
    {
        if (player.RunePool.Energy < cost.Energy)
        {
            return false;
        }

        foreach (var requirement in cost.PowerByDomain)
        {
            if (
                !player.RunePool.PowerByDomain.TryGetValue(requirement.Key, out var available)
                || available < requirement.Value
            )
            {
                return false;
            }
        }

        return true;
    }

    private static void SpendCost(PlayerState player, ResourceCost cost)
    {
        player.RunePool.Energy -= cost.Energy;
        foreach (var requirement in cost.PowerByDomain)
        {
            if (!player.RunePool.PowerByDomain.TryGetValue(requirement.Key, out var current))
            {
                continue;
            }

            var next = current - requirement.Value;
            if (next <= 0)
            {
                player.RunePool.PowerByDomain.Remove(requirement.Key);
            }
            else
            {
                player.RunePool.PowerByDomain[requirement.Key] = next;
            }
        }
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

        fallbackEnemy.MarkedDamage += 1;
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

        targetUnit.MarkedDamage += ReadMagnitude(card, fallback: 1);
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

        targetUnit.MarkedDamage = EffectiveMight(targetUnit);
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
    }

    private void ApplySameLocationDamageSpellEffect(
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var magnitude = ReadMagnitude(card, fallback: 1);
        var maxTargets = ReadIntEffectData(card, "maxTargets", fallback: 3);
        var firstLocation = SelectBestEnemyUnitLocation(session, player.PlayerIndex, null);
        if (firstLocation is null)
        {
            return;
        }

        var firstTargets = SelectEnemyTargetsAtLocation(firstLocation, player.PlayerIndex, maxTargets);
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
                ["location"] = firstLocation.Key,
                ["targets"] = firstTargets.Count.ToString(),
                ["repeat"] = "false",
            }
        );

        var repeatCost = ResolveRepeatCost(card);
        if (!repeatCost.HasAnyCost)
        {
            return;
        }

        var repeatLocation = SelectBestEnemyUnitLocation(session, player.PlayerIndex, firstLocation.Key);
        if (repeatLocation is null)
        {
            return;
        }

        if (!TryEnsureCostCanBePaidWithReadyRunes(session, player, repeatCost))
        {
            return;
        }

        SpendCost(player, repeatCost);
        var repeatTargets = SelectEnemyTargetsAtLocation(repeatLocation, player.PlayerIndex, maxTargets);
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
                ["location"] = repeatLocation.Key,
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

        var spell = player
            .TrashZone.Cards.Where(x =>
                string.Equals(x.Type, "Spell", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(x.EffectTemplateId, "unsupported", StringComparison.Ordinal)
                && x.Cost.GetValueOrDefault() <= maxEnergyCost
            )
            .OrderByDescending(x => x.Cost.GetValueOrDefault())
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .FirstOrDefault();
        if (spell is null)
        {
            return;
        }

        var ignoreEnergyCost = ReadBoolEffectData(card, "ignoreEnergyCostForTrashSpell", fallback: true);
        var recycleAfterPlay = ReadBoolEffectData(card, "recyclePlayedTrashSpell", fallback: true);
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

    private bool TryEnsureCostCanBePaidWithReadyRunes(
        GameSession session,
        PlayerState player,
        ResourceCost cost
    )
    {
        if (CanAffordCost(player, cost))
        {
            return true;
        }

        var availableRunes = player.BaseZone.Cards
            .Where(c =>
                !c.IsExhausted && string.Equals(c.Type, "Rune", StringComparison.OrdinalIgnoreCase)
            )
            .ToList();

        while (!CanAffordCost(player, cost) && availableRunes.Count > 0)
        {
            var nextRune = SelectBestRuneForMissingCost(player, cost, availableRunes);
            if (nextRune is null)
            {
                break;
            }

            availableRunes.Remove(nextRune);
            ActivateRuneCard(session, player, nextRune, "auto-cost");
        }

        return CanAffordCost(player, cost);
    }

    private static CardInstance? SelectBestRuneForMissingCost(
        PlayerState player,
        ResourceCost requiredCost,
        IReadOnlyCollection<CardInstance> candidates
    )
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        var missingByDomain = requiredCost.PowerByDomain.ToDictionary(
            x => x.Key,
            x =>
            {
                player.RunePool.PowerByDomain.TryGetValue(x.Key, out var available);
                return Math.Max(0, x.Value - available);
            },
            StringComparer.OrdinalIgnoreCase
        );

        var bestMissingDomain = missingByDomain
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .FirstOrDefault();

        if (bestMissingDomain.Value > 0)
        {
            var matchingRune = candidates.FirstOrDefault(r =>
                string.Equals(ResolveRuneDomain(r), bestMissingDomain.Key, StringComparison.OrdinalIgnoreCase)
            );
            if (matchingRune is not null)
            {
                return matchingRune;
            }
        }

        return candidates.OrderBy(r => r.Name, StringComparer.Ordinal).First();
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
        var context = new EffectContext
        {
            Source = source,
            ControllerPlayerIndex = controllerPlayerIndex,
            Timing = timing,
            Metadata = metadata is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase),
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
            FinalizeChain(session);
            ResolveCleanup(session);
            return;
        }

        session.Showdown.FocusPlayerIndex = passingPlayerIndex;
        session.Showdown.PriorityPlayerIndex = GetOpponentPlayerIndex(session, passingPlayerIndex);
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

    private void ResolveCleanup(GameSession session)
    {
        foreach (var player in session.Players)
        {
            var deadBaseUnits = player.BaseZone.Cards.Where(card => IsUnitCard(card) && IsDead(card)).ToList();
            foreach (var dead in deadBaseUnits)
            {
                DetachAttachedGearToTrash(session, dead.InstanceId);
                player.BaseZone.Cards.Remove(dead);
                player.TrashZone.Cards.Add(dead);
            }
        }

        foreach (var battlefield in session.Battlefields)
        {
            var deadUnits = battlefield.Units.Where(IsDead).ToList();
            foreach (var dead in deadUnits)
            {
                DetachAttachedGearToTrash(session, dead.InstanceId);
                battlefield.Units.Remove(dead);
                session.Players[dead.OwnerPlayerIndex].TrashZone.Cards.Add(dead);
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
                ResolveCombat(session, battlefield);
                continue;
            }

            if (distinctControllers.Count == 1)
            {
                var winner = distinctControllers[0];
                battlefield.ControlledByPlayerIndex = winner;
                TryScore(session, winner, battlefield.Index);
            }

            battlefield.ContestedByPlayerIndex = null;
            battlefield.IsShowdownStaged = false;
            battlefield.IsCombatStaged = false;
        }

        if (session.Players.Any(p => p.Score >= DuelVictoryScore))
        {
            session.Phase = RiftboundTurnPhase.Completed;
            session.State = RiftboundTurnState.NeutralOpen;
        }
    }

    private void ResolveCombat(GameSession session, BattlefieldState battlefield)
    {
        battlefield.IsCombatStaged = true;
        battlefield.IsShowdownStaged = true;
        session.Showdown.IsOpen = true;
        session.Showdown.BattlefieldIndex = battlefield.Index;
        session.Combat.IsOpen = true;
        session.Combat.BattlefieldIndex = battlefield.Index;

        var grouped = battlefield
            .Units.GroupBy(x => x.ControllerPlayerIndex)
            .Select(g => new { Player = g.Key, Might = g.Sum(EffectiveMight) })
            .OrderByDescending(x => x.Might)
            .ToList();

        if (grouped.Count < 2)
        {
            return;
        }

        var top = grouped[0];
        var second = grouped[1];
        if (top.Might == second.Might)
        {
            var units = battlefield.Units.ToList();
            battlefield.Units.Clear();
            foreach (var unit in units)
            {
                DetachAttachedGearToTrash(session, unit.InstanceId);
                session.Players[unit.OwnerPlayerIndex].TrashZone.Cards.Add(unit);
            }

            battlefield.ControlledByPlayerIndex = null;
        }
        else
        {
            var losers = battlefield.Units.Where(u => u.ControllerPlayerIndex != top.Player).ToList();
            foreach (var loser in losers)
            {
                battlefield.Units.Remove(loser);
                DetachAttachedGearToTrash(session, loser.InstanceId);
                session.Players[loser.OwnerPlayerIndex].TrashZone.Cards.Add(loser);
            }

            battlefield.ControlledByPlayerIndex = top.Player;
            TryScore(session, top.Player, battlefield.Index);
        }

        battlefield.ContestedByPlayerIndex = null;
        battlefield.IsCombatStaged = false;
        battlefield.IsShowdownStaged = false;
        session.Showdown.IsOpen = false;
        session.Showdown.BattlefieldIndex = null;
        session.Combat.IsOpen = false;
        session.Combat.BattlefieldIndex = null;
    }

    private static bool IsDead(CardInstance unit)
    {
        var effectiveMight = EffectiveMight(unit);
        return effectiveMight > 0 && unit.MarkedDamage >= effectiveMight;
    }

    private static int EffectiveMight(CardInstance unit)
    {
        return unit.Might.GetValueOrDefault(0) + unit.PermanentMightModifier + unit.TemporaryMightModifier;
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

    private static void ScoreHoldings(GameSession session, int playerIndex)
    {
        foreach (var battlefield in session.Battlefields.Where(b => b.ControlledByPlayerIndex == playerIndex))
        {
            TryScore(session, playerIndex, battlefield.Index);
        }
    }

    private static void TryScore(GameSession session, int playerIndex, int battlefieldIndex)
    {
        var key = $"{session.TurnNumber}:{playerIndex}:{battlefieldIndex}";
        if (!session.UsedScoringKeys.TryAdd(key, true))
        {
            return;
        }

        session.Players[playerIndex].Score += 1;

        var battlefield = session.Battlefields.FirstOrDefault(x => x.Index == battlefieldIndex);
        if (battlefield is null)
        {
            return;
        }

        var holdBonus = battlefield.Units.Count(unit =>
            unit.ControllerPlayerIndex == playerIndex
            && string.Equals(unit.EffectTemplateId, "unit.hold-score-1", StringComparison.Ordinal)
        );
        session.Players[playerIndex].Score += holdBonus;
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
            }
        }

        foreach (var player in session.Players)
        {
            foreach (var unit in player.BaseZone.Cards.Where(IsUnitCard))
            {
                unit.TemporaryMightModifier = 0;
            }
        }
        
        foreach (var player in session.Players)
        {
            EmptyRunePool(player);
        }

        if (session.Players.Any(p => p.Score >= DuelVictoryScore))
        {
            session.Phase = RiftboundTurnPhase.Completed;
            return;
        }

        session.TurnPlayerIndex = (session.TurnPlayerIndex + 1) % session.Players.Count;
        session.TurnNumber += 1;
        session.UsedScoringKeys.Clear();
        StartTurn(session);
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
        )
        {
            return $"{V2Prefix}{actionId}";
        }

        return actionId;
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

    private static int ParseBattlefieldIndex(string actionId)
    {
        var marker = "-to-bf-";
        var idx = actionId.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
        {
            throw new InvalidOperationException($"Unable to parse battlefield index from action '{actionId}'.");
        }

        var value = actionId[(idx + marker.Length)..];
        return int.Parse(value);
    }

    private sealed record ResourceCost(int Energy, IReadOnlyDictionary<string, int> PowerByDomain)
    {
        public bool HasAnyCost => Energy > 0 || PowerByDomain.Any(x => x.Value > 0);
    }

    private sealed record UnitLocation(string Key, IReadOnlyCollection<CardInstance> Units);

    private enum TargetMode
    {
        None,
        FriendlyUnit,
        EnemyUnit,
    }
}
