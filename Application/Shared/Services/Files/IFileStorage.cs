namespace Application.Shared.Services.Files;

public interface IFileStorage
{
    Task<string> SaveAsync(byte[] data, string fileName, CancellationToken ct);
}
