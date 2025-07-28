using System.IO;
using System.Linq;

namespace Domain.Enums;

public static class AttachmentTypeExtensions
{
    private static readonly string[] ImageExts = [".jpg", ".jpeg", ".png", ".gif", ".bmp"];
    private static readonly string[] VideoExts = [".mp4", ".mov", ".avi", ".mkv"];
    private static readonly string[] AudioExts = [".mp3", ".wav", ".ogg", ".aac"];

    public static AttachmentType? FromFileName(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ImageExts.Contains(ext)) return AttachmentType.Image;
        if (VideoExts.Contains(ext)) return AttachmentType.Video;
        if (AudioExts.Contains(ext)) return AttachmentType.Audio;
        return null;
    }

    public static bool IsPreviewable(this AttachmentType type)
        => type == AttachmentType.Image || type == AttachmentType.Video;
}
