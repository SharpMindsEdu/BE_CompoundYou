using Domain.Entities.Riftbound;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class GearheadEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "gearhead";
    public override string TemplateId => "named.gearhead";
}

