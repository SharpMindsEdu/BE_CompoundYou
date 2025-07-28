using Application.Shared.Services.Files;
using Domain.Enums;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Infrastructure.Services.Attachments;

public class LocalAttachmentService(IFileStorage storage, IConfiguration configuration) : IAttachmentService
{
    private readonly string _basePath = configuration.GetValue<string>("LocalFileStorage:Path") ?? "uploads";
    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    public async Task<(string Path, AttachmentType Type)> SaveAsync(byte[] data, string fileName, CancellationToken ct)
    {
        var path = await storage.SaveAsync(data, fileName, ct);
        var type = AttachmentTypeExtensions.FromFileName(path) ?? AttachmentType.Image;
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (type == AttachmentType.Image)
        {
            using var image = Image.Load(data);
            image.Mutate(x => x.Resize(new ResizeOptions { Size = new Size(320, 0), Mode = ResizeMode.Max }));
            var previewPath = GetPreviewPath(path, ext);
            Directory.CreateDirectory(Path.GetDirectoryName(previewPath)!);
            await image.SaveAsync(previewPath, GetEncoder(ext), ct);
        }
        return (path, type);
    }

    public async Task<(Stream Stream, string ContentType)> GetAsync(string path, bool preview, CancellationToken ct)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var type = AttachmentTypeExtensions.FromFileName(path);
        var filePath = preview && type == AttachmentType.Image ? GetPreviewPath(path, ext) : path;
        if (!File.Exists(filePath))
            throw new FileNotFoundException();
        var stream = File.OpenRead(filePath);
        if (!_contentTypeProvider.TryGetContentType(filePath, out var contentType))
            contentType = "application/octet-stream";
        return (stream, contentType);
    }

    private static string GetPreviewPath(string path, string ext)
        => Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "_preview" + ext);

    private static IImageEncoder GetEncoder(string ext) => ext switch
    {
        ".png" => new SixLabors.ImageSharp.Formats.Png.PngEncoder(),
        ".bmp" => new SixLabors.ImageSharp.Formats.Bmp.BmpEncoder(),
        ".gif" => new SixLabors.ImageSharp.Formats.Gif.GifEncoder(),
        _ => new JpegEncoder(),
    };
}
