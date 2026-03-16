using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

internal static class RiftboundEffectCardLookup
{
    public static IEnumerable<CardInstance> EnumerateAllCards(GameSession session)
    {
        foreach (var player in session.Players)
        {
            foreach (var card in player.HandZone.Cards)
            {
                yield return card;
            }

            foreach (var card in player.BaseZone.Cards)
            {
                yield return card;
            }

            foreach (var card in player.TrashZone.Cards)
            {
                yield return card;
            }

            foreach (var card in player.MainDeckZone.Cards)
            {
                yield return card;
            }

            foreach (var card in player.RuneDeckZone.Cards)
            {
                yield return card;
            }

            foreach (var card in player.ChampionZone.Cards)
            {
                yield return card;
            }

            foreach (var card in player.LegendZone.Cards)
            {
                yield return card;
            }
        }

        foreach (var battlefield in session.Battlefields)
        {
            foreach (var card in battlefield.Units)
            {
                yield return card;
            }

            foreach (var card in battlefield.Gear)
            {
                yield return card;
            }

            foreach (var card in battlefield.HiddenCards)
            {
                yield return card;
            }
        }
    }

    public static CardInstance? FindCardByInstanceId(GameSession session, Guid instanceId)
    {
        return EnumerateAllCards(session).FirstOrDefault(x => x.InstanceId == instanceId);
    }
}
