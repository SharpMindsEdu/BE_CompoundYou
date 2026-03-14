using Domain.Entities.Riftbound;
using Domain.Simulation;
using System.Text.RegularExpressions;

namespace Application.Features.Riftbound.Simulation.Engine;

public sealed class RiftboundSimulationEngine : IRiftboundSimulationEngine
{
    private const int DuelVictoryScore = 8;

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

        var player = session.Players[session.TurnPlayerIndex];
        var actions = new List<RiftboundLegalAction>();

        foreach (var rune in player.BaseZone.Cards.Where(c =>
            string.Equals(c.Type, "Rune", StringComparison.OrdinalIgnoreCase) && !c.IsExhausted
        ))
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"activate-rune-{rune.InstanceId}",
                    RiftboundActionType.ActivateRune,
                    player.PlayerIndex,
                    $"Activate rune {rune.Name}"
                )
            );
        }

        foreach (var card in player.HandZone.Cards)
        {
            if (!CanPlayCard(card, player))
            {
                continue;
            }

            if (string.Equals(card.Type, "Unit", StringComparison.OrdinalIgnoreCase))
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"play-{card.InstanceId}-to-base",
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
                            $"play-{card.InstanceId}-to-bf-{battlefield.Index}",
                            RiftboundActionType.PlayCard,
                            player.PlayerIndex,
                            $"Play {card.Name} to battlefield {battlefield.Name}"
                        )
                    );
                }
            }
            else if (string.Equals(card.Type, "Spell", StringComparison.OrdinalIgnoreCase))
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"play-{card.InstanceId}-spell",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play spell {card.Name}"
                    )
                );
            }
        }

        foreach (var unit in player.BaseZone.Cards.Where(IsMovableUnit))
        {
            foreach (var battlefield in session.Battlefields)
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"move-{unit.InstanceId}-to-bf-{battlefield.Index}",
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
                        $"move-{unit.InstanceId}-to-base",
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
                                $"move-{unit.InstanceId}-to-bf-{target.Index}",
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

        return actions;
    }

    public RiftboundSimulationEngineResult ApplyAction(GameSession session, string actionId)
    {
        var legalActions = GetLegalActions(session);
        if (!legalActions.Any(x => string.Equals(x.ActionId, actionId, StringComparison.Ordinal)))
        {
            return new RiftboundSimulationEngineResult(
                false,
                "Action is not legal in the current state.",
                session,
                legalActions
            );
        }

        if (actionId == "end-turn")
        {
            EndTurn(session);
            return new RiftboundSimulationEngineResult(
                true,
                null,
                session,
                GetLegalActions(session)
            );
        }

        if (actionId.StartsWith("activate-rune-", StringComparison.Ordinal))
        {
            ActivateRune(session, actionId);
            ResolveCleanup(session);
            return new RiftboundSimulationEngineResult(
                true,
                null,
                session,
                GetLegalActions(session)
            );
        }

        if (actionId.StartsWith("play-", StringComparison.Ordinal))
        {
            PlayCard(session, actionId);
            ResolveCleanup(session);
            return new RiftboundSimulationEngineResult(
                true,
                null,
                session,
                GetLegalActions(session)
            );
        }

        if (actionId.StartsWith("move-", StringComparison.Ordinal))
        {
            MoveUnit(session, actionId);
            ResolveCleanup(session);
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
            Keywords = card?.Tags?.ToList() ?? [],
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

        return string.Equals(card.Type, "Unit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(card.Type, "Spell", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMovableUnit(CardInstance card)
    {
        return string.Equals(card.Type, "Unit", StringComparison.OrdinalIgnoreCase) && !card.IsExhausted;
    }

    private void ActivateRune(GameSession session, string actionId)
    {
        var player = session.Players[session.TurnPlayerIndex];
        var instanceId = ParseGuidFrom(actionId, "activate-rune-");
        var rune = player.BaseZone.Cards.Single(x => x.InstanceId == instanceId);
        rune.IsExhausted = true;
        player.RunePool.Energy += 1;
    }

    private void PlayCard(GameSession session, string actionId)
    {
        var player = session.Players[session.TurnPlayerIndex];
        var instanceId = ParseGuidFrom(actionId, "play-");
        var card = player.HandZone.Cards.Single(x => x.InstanceId == instanceId);

        player.HandZone.Cards.Remove(card);
        session.State = RiftboundTurnState.NeutralClosed;

        session.Chain.Add(
            new ChainItem
            {
                ActionId = actionId,
                ControllerPlayerIndex = player.PlayerIndex,
                CardInstanceId = card.InstanceId,
                Kind = "PlayCard",
                IsPending = true,
                IsFinalized = false,
            }
        );

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
            // Basic v1 spell handling: deal 1 to first enemy unit if available.
            var enemyUnit = session
                .Battlefields.SelectMany(b => b.Units)
                .FirstOrDefault(u => u.ControllerPlayerIndex != player.PlayerIndex);
            if (enemyUnit is not null)
            {
                enemyUnit.MarkedDamage += 1;
            }

            player.TrashZone.Cards.Add(card);
        }

        FinalizeChain(session);
    }

    private void MoveUnit(GameSession session, string actionId)
    {
        var player = session.Players[session.TurnPlayerIndex];
        var instanceId = ParseGuidFrom(actionId, "move-");
        var unit = FindUnit(session, player.PlayerIndex, instanceId);
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
        session.State = RiftboundTurnState.NeutralOpen;
    }

    private void ResolveCleanup(GameSession session)
    {
        foreach (var battlefield in session.Battlefields)
        {
            var deadUnits = battlefield.Units.Where(IsDead).ToList();
            foreach (var dead in deadUnits)
            {
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
            .Select(g => new { Player = g.Key, Might = g.Sum(u => u.Might.GetValueOrDefault(0)) })
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
        return unit.Might.GetValueOrDefault(0) > 0 && unit.MarkedDamage >= unit.Might.GetValueOrDefault(0);
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
}
