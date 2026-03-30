namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class MachineEvangelEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "machine-evangel";
    public override string TemplateId => "named.machine-evangel";

    public override void OnDeath(
        IRiftboundEffectRuntime runtime,
        Domain.Simulation.GameSession session,
        Domain.Simulation.PlayerState player,
        Domain.Simulation.CardInstance card
    )
    {
        for (var i = 0; i < 3; i += 1)
        {
            player.BaseZone.Cards.Add(
                RiftboundTokenFactory.CreateRecruitUnitToken(
                    ownerPlayerIndex: player.PlayerIndex,
                    controllerPlayerIndex: player.PlayerIndex,
                    might: 1,
                    exhausted: true
                )
            );
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "Deathknell",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
                ["template"] = card.EffectTemplateId,
                ["spawnedRecruitTokens"] = "3",
            }
        );
    }
}
