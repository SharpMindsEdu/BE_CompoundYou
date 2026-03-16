using Application.Features.Riftbound.Simulation.Definitions;
using Domain.Entities.Riftbound;
using Domain.Simulation;
using System.Text.RegularExpressions;

namespace Application.Features.Riftbound.Simulation.Engine;

public sealed class RiftboundSimulationEngine : IRiftboundSimulationEngine
{
    private const int DuelVictoryScore = 8;
    private const string V2Prefix = "v2:";

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

        if (normalizedActionId.StartsWith($"{V2Prefix}activate-rune-", StringComparison.Ordinal))
        {
            ActivateRune(session, normalizedActionId);
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
        if (card.Cost.GetValueOrDefault() > player.RunePool.Energy)
        {
            return false;
        }

        if (string.Equals(card.EffectTemplateId, "unsupported", StringComparison.Ordinal))
        {
            return false;
        }

        return string.Equals(card.Type, "Unit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(card.Type, "Spell", StringComparison.OrdinalIgnoreCase)
            || string.Equals(card.Type, "Gear", StringComparison.OrdinalIgnoreCase);
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

    private void ActivateRune(GameSession session, string actionId)
    {
        var player = session.Players[session.TurnPlayerIndex];
        var instanceId = ParseGuidFrom(actionId, $"{V2Prefix}activate-rune-");
        var rune = player.BaseZone.Cards.Single(x => x.InstanceId == instanceId);
        rune.IsExhausted = true;
        player.RunePool.Energy += 1;
    }

    private void PlayCard(GameSession session, string actionId)
    {
        var player = session.Players[GetPriorityPlayerIndex(session)];
        var instanceId = ParseGuidFrom(actionId, $"{V2Prefix}play-");
        var card = player.HandZone.Cards.Single(x => x.InstanceId == instanceId);

        player.HandZone.Cards.Remove(card);
        AddPendingChainItem(session, actionId, player.PlayerIndex, card.InstanceId, "PlayCard");
        OpenOrAdvancePriorityWindow(session, player.PlayerIndex);

        player.RunePool.Energy -= card.Cost.GetValueOrDefault();

        if (actionId.EndsWith("-to-base", StringComparison.Ordinal))
        {
            card.IsExhausted = !card.Keywords.Contains("Accelerate", StringComparer.OrdinalIgnoreCase);
            player.BaseZone.Cards.Add(card);
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

    private void ApplySpellOrGearEffect(
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var targetUnitId = ParseOptionalGuidFrom(actionId, "-target-unit-");
        var targetUnit = targetUnitId.HasValue ? TryFindUnitByInstanceId(session, targetUnitId.Value) : null;

        switch (card.EffectTemplateId)
        {
            case "spell.vanilla":
            {
                var fallbackEnemy = session
                    .Battlefields.SelectMany(b => b.Units)
                    .FirstOrDefault(u => u.ControllerPlayerIndex != player.PlayerIndex);
                if (fallbackEnemy is not null)
                {
                    fallbackEnemy.MarkedDamage += 1;
                }

                return;
            }
            case "spell.damage-enemy-unit":
            {
                if (targetUnit is null || targetUnit.ControllerPlayerIndex == player.PlayerIndex)
                {
                    return;
                }

                targetUnit.MarkedDamage += ReadMagnitude(card, fallback: 1);
                return;
            }
            case "spell.buff-friendly-unit":
            {
                if (targetUnit is null || targetUnit.ControllerPlayerIndex != player.PlayerIndex)
                {
                    return;
                }

                targetUnit.TemporaryMightModifier += ReadMagnitude(card, fallback: 1);
                return;
            }
            case "spell.kill-enemy-unit":
            {
                if (targetUnit is null || targetUnit.ControllerPlayerIndex == player.PlayerIndex)
                {
                    return;
                }

                targetUnit.MarkedDamage = EffectiveMight(targetUnit);
                return;
            }
            case "spell.draw":
                DrawCards(player, ReadMagnitude(card, fallback: 1));
                return;
            case "gear.attach-friendly-unit":
            case "gear.vanilla":
            {
                if (targetUnit is null || targetUnit.ControllerPlayerIndex != player.PlayerIndex)
                {
                    return;
                }

                card.AttachedToInstanceId = targetUnit.InstanceId;
                card.IsExhausted = true;
                AddGearToTargetLocation(session, targetUnit, card);
                return;
            }
            default:
                return;
        }
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

    private enum TargetMode
    {
        None,
        FriendlyUnit,
        EnemyUnit,
    }
}
