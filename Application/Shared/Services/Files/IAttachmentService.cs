namespace Application.Shared.Services.Files;

public interface IAttachmentService
{
    Task<(string Path, Domain.Enums.AttachmentType Type)> SaveAsync(byte[] data, string fileName, CancellationToken ct);
    Task<(Stream Stream, string ContentType)> GetAsync(string path, bool preview, CancellationToken ct);
}
