using Domain.Entities.Riftbound;

namespace Domain.Services.Riftbound;

public interface IRiftboundCardService
{
    public Task<List<RiftboundCard>> GetCardsAsync(CancellationToken cancellationToken = default);
}
