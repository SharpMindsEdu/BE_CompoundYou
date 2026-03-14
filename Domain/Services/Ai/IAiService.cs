namespace Domain.Services.Ai;

public interface IAiService
{
    Task<string?> SelectActionIdAsync(
        string prompt,
        IReadOnlyCollection<string> legalActionIds,
        CancellationToken cancellationToken = default
    );
}
