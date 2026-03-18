using Domain.Entities.Riftbound;
using Domain.Simulation;
using System.Text.RegularExpressions;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class EmperorsDaisEffect : RiftboundNamedCardEffectBase
{
    public const string ReturnUnitChoiceMarker = "-emperors-dais-return-";

    public override string NameIdentifier => "emperor-s-dais";
    public override string TemplateId => "named.emperor-s-dais";

    public override void OnConquer(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        BattlefieldState battlefield,
        string? sourceActionId
    )
    {
        var unitToReturn =
            ResolveChosenReturnUnit(player, battlefield, sourceActionId)
            ?? SelectFallbackReturnUnit(runtime, session, player, battlefield);
        if (unitToReturn is null)
        {
            return;
        }

        if (!runtime.TryPayCost(session, player, energyCost: 1))
        {
            return;
        }

        battlefield.Units.Remove(unitToReturn);
        session.Players[unitToReturn.OwnerPlayerIndex].HandZone.Cards.Add(unitToReturn);
        battlefield.Units.Add(
            RiftboundTokenFactory.CreateSandSoldierUnitToken(
                ownerPlayerIndex: player.PlayerIndex,
                controllerPlayerIndex: player.PlayerIndex,
                might: 2,
                exhausted: true
            )
        );
    }

    private static CardInstance? ResolveChosenReturnUnit(
        PlayerState player,
        BattlefieldState battlefield,
        string? sourceActionId
    )
    {
        if (string.IsNullOrWhiteSpace(sourceActionId))
        {
            return null;
        }

        var markerIndex = sourceActionId.IndexOf(ReturnUnitChoiceMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var fragment = sourceActionId[(markerIndex + ReturnUnitChoiceMarker.Length)..];
        var match = Regex.Match(
            fragment,
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"
        );
        if (!match.Success || !Guid.TryParse(match.Value, out var chosenUnitId))
        {
            return null;
        }

        return battlefield.Units.FirstOrDefault(x =>
            x.InstanceId == chosenUnitId && x.ControllerPlayerIndex == player.PlayerIndex
        );
    }

    private static CardInstance? SelectFallbackReturnUnit(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        BattlefieldState battlefield
    )
    {
        return battlefield.Units
            .Where(x => x.ControllerPlayerIndex == player.PlayerIndex)
            .OrderByDescending(x => runtime.GetEffectiveMight(session, x))
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.InstanceId)
            .FirstOrDefault();
    }
}
