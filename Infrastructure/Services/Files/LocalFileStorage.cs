using Application.Shared.Services.Files;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace Infrastructure.Services.Files;

public class LocalFileStorage(IConfiguration configuration) : IFileStorage
{
    private readonly string _basePath = configuration.GetValue<string>("LocalFileStorage:Path") ?? "uploads";

    public async Task<string> SaveAsync(byte[] data, string fileName, CancellationToken ct)
    {
        Directory.CreateDirectory(_basePath);
        var filePath = Path.Combine(_basePath, fileName);
        await File.WriteAllBytesAsync(filePath, data, ct);
        return filePath;
    }
}
