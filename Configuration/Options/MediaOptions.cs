using System.ComponentModel.DataAnnotations;

namespace OpenAcademy.Configuration.Options;

/// <summary>Hard ceilings. Kestrel request limits on the upload actions use these, so options cannot exceed them.</summary>
public static class MediaLimits
{
    public const int ImageCeilingMegabytes = 25;
    public const int VideoCeilingMegabytes = 500;
    public const long ImageCeilingBytes = ImageCeilingMegabytes * 1024L * 1024;
    public const long VideoCeilingBytes = VideoCeilingMegabytes * 1024L * 1024;
}

public sealed class MediaOptions
{
    public const string SectionName = "Media";

    // Images (jpeg/png in, webp out)
    [Range(1, MediaLimits.ImageCeilingMegabytes)] public int MaxImageMegabytes { get; init; } = 5;
    [Range(1, 200)] public int MaxImageMegapixels { get; init; } = 40;      // decompression-bomb guard
    [Range(64, 8192)] public int ImageMaxDimension { get; init; } = 1920;   // longest side after resize
    [Range(1, 100)] public int WebpQuality { get; init; } = 80;

    // Public images (avatars, thumbnails): stored in the public bucket, smaller by default
    [Range(1, MediaLimits.ImageCeilingMegabytes)] public int MaxPublicImageMegabytes { get; init; } = 2;
    [Range(64, 4096)] public int PublicImageMaxDimension { get; init; } = 512;

    // Video (mp4 in, H.264/AAC mp4 out)
    [Range(1, MediaLimits.VideoCeilingMegabytes)] public int MaxVideoMegabytes { get; init; } = 100;
    [Range(1, 7200)] public int MaxVideoDurationSeconds { get; init; } = 900;
    [Range(144, 2160)] public int VideoMaxHeight { get; init; } = 720;
    [Range(18, 40)] public int VideoCrf { get; init; } = 28;
    [Required, RegularExpression("^(ultrafast|superfast|veryfast|faster|fast|medium|slow|slower)$")]
    public string VideoPreset { get; init; } = "veryfast";
    [Range(10, 3600)] public int TranscodeTimeoutSeconds { get; init; } = 300;

    // Presigned GET lifetime. A <video> element keeps using the same URL for range requests
    // (seeking/buffering), so this must comfortably exceed a viewing session.
    [Range(1, 720)] public int PresignedUrlLifetimeMinutes { get; init; } = 60;

    // Binaries (resolved via PATH by default)
    [Required] public string FfmpegPath { get; init; } = "ffmpeg";
    [Required] public string FfprobePath { get; init; } = "ffprobe";
}