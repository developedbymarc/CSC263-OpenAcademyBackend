using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using ImageMagick;
using Microsoft.Extensions.Options;
using OpenAcademy.Configuration;
using OpenAcademy.Configuration.Options;

namespace OpenAcademy.Services.ExternalServices.MediaService;

public sealed class MediaService(
    IAmazonS3 s3,
    [FromKeyedServices(MediaServiceCollectionExtensions.PublicS3Key)] IAmazonS3 publicS3,
    IOptions<S3Options> s3Options,
    IOptions<MediaOptions> mediaOptions,
    ILogger<MediaService> logger) : IMediaService
{
    private const long MB = 1024L * 1024;

    // One transcode at a time: keeps a small VPS from falling over. Static because the service is scoped.
    private static readonly SemaphoreSlim TranscodeGate = new(1, 1);

    // Image decode/resize/encode is also CPU and memory heavy. Cap concurrent jobs.
    private static readonly SemaphoreSlim ImageGate = new(2, 2);

    // Only these sniffed formats are accepted (the decoder is pinned to the sniffed format below).
    private static readonly HashSet<MagickFormat> AllowedImageFormats =
    [
        MagickFormat.Jpeg, MagickFormat.Jpg,
        MagickFormat.Png, MagickFormat.Png00, MagickFormat.Png8,
        MagickFormat.Png24, MagickFormat.Png32, MagickFormat.Png48, MagickFormat.Png64,
    ];

    // Global ImageMagick resource limits: a hostile upload cannot exhaust the host.
    // Tune to your VPS. Hitting a limit throws MagickResourceLimitErrorException (handled below).
    static MediaService()
    {
        ResourceLimits.Width = 20_000;
        ResourceLimits.Height = 20_000;
        ResourceLimits.Memory = 512UL * 1024 * 1024;
    }

    private readonly S3Options _s3 = s3Options.Value;
    private readonly MediaOptions _opt = mediaOptions.Value;

    // ---------------------------------------------------------------- images

    public Task<StoredMedia> StoreImageAsync(string keyWithoutExtension, IFormFile file, CancellationToken ct)
        => StoreImageCoreAsync(_s3.BucketName, keyWithoutExtension, file,
            _opt.MaxImageMegabytes, _opt.ImageMaxDimension, publicRead: false, ct);

    public Task<StoredMedia> StorePublicImageAsync(string keyWithoutExtension, IFormFile file, CancellationToken ct)
        => StoreImageCoreAsync(_s3.PublicBucketName, keyWithoutExtension, file,
            _opt.MaxPublicImageMegabytes, _opt.PublicImageMaxDimension, publicRead: true, ct);

    private async Task<StoredMedia> StoreImageCoreAsync(
        string bucket, string keyWithoutExtension, IFormFile file,
        int maxMegabytes, int maxDimension, bool publicRead, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            throw new MediaRejectedException("File is required.");
        if (file.Length > maxMegabytes * MB)
            throw new MediaRejectedException($"Image is too large. Maximum size is {maxMegabytes} MB.",
                StatusCodes.Status413PayloadTooLarge);

        await using var input = file.OpenReadStream();

        // Header-only read: real format and dimensions, no pixel decode.
        // Trust the bytes, not the filename or Content-Type header.
        MagickImageInfo info;
        try { info = new MagickImageInfo(input); }
        catch (MagickException)
        {
            throw new MediaRejectedException("Unsupported image type. Upload a JPEG or PNG.",
                StatusCodes.Status415UnsupportedMediaType);
        }

        if (!AllowedImageFormats.Contains(info.Format))
            throw new MediaRejectedException("Unsupported image type. Upload a JPEG or PNG.",
                StatusCodes.Status415UnsupportedMediaType);

        if ((long)info.Width * info.Height > _opt.MaxImageMegapixels * 1_000_000L)
            throw new MediaRejectedException($"Image dimensions are too large (max {_opt.MaxImageMegapixels} megapixels).");

        try
        {
            await using var webp = new MemoryStream();

            await ImageGate.WaitAsync(ct);
            try
            {
                input.Position = 0;

                // Pin the decoder to the sniffed format (no format guessing on untrusted bytes), first frame only.
                var settings = new MagickReadSettings
                {
                    Format = info.Format,
                    FrameIndex = 0,
                    FrameCount = 1,
                };

                using var image = new MagickImage();
                await image.ReadAsync(input, settings, ct);

                // Apply EXIF rotation BEFORE stripping metadata, otherwise phone photos end up sideways.
                image.AutoOrient();

                // Greater = true: only ever shrink, never enlarge. Aspect ratio is kept.
                image.Resize(new MagickGeometry((uint)maxDimension, (uint)maxDimension) { Greater = true });

                // Public images also lose GPS/camera data here: important for profile pictures.
                // Strip() removes EXIF, IPTC, XMP, ICC and text chunks.
                image.Strip();

                image.Format = MagickFormat.WebP;
                image.Quality = (uint)_opt.WebpQuality;

                await image.WriteAsync(webp, ct);
            }
            finally { ImageGate.Release(); }

            webp.Position = 0;

            var key = keyWithoutExtension + ".webp";
            await PutAsync(bucket, key, webp, "image/webp", publicRead, ct);
            return new StoredMedia(key, "image/webp", webp.Length, null);
        }
        catch (MagickResourceLimitErrorException ex)
        {
            logger.LogInformation(ex, "Image rejected by ImageMagick resource limits");
            throw new MediaRejectedException("The image is too large to process.");
        }
        catch (MagickException ex)
        {
            logger.LogInformation(ex, "Image could not be decoded");
            throw new MediaRejectedException("The image file is corrupt or unreadable.");
        }
    }

    // ---------------------------------------------------------------- video

    public async Task<StoredMedia> StoreVideoAsync(string keyWithoutExtension, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            throw new MediaRejectedException("File is required.");
        if (file.Length > _opt.MaxVideoMegabytes * MB)
            throw new MediaRejectedException($"Video is too large. Maximum size is {_opt.MaxVideoMegabytes} MB.",
                StatusCodes.Status413PayloadTooLarge);

        var work = Path.Combine(Path.GetTempPath(), "oa-media-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var inPath = Path.Combine(work, "in.mp4");
        var outPath = Path.Combine(work, "out.mp4");

        try
        {
            await using (var fs = File.Create(inPath))
                await file.CopyToAsync(fs, ct);

            if (!await LooksLikeMp4Async(inPath, ct))
                throw new MediaRejectedException("Unsupported video type. Upload an MP4.",
                    StatusCodes.Status415UnsupportedMediaType);

            await TranscodeGate.WaitAsync(ct);
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(_opt.TranscodeTimeoutSeconds));
                try
                {
                    var duration = await ProbeAsync(inPath, timeout.Token);
                    if (duration > _opt.MaxVideoDurationSeconds)
                        throw new MediaRejectedException(
                            $"Video is too long. Maximum is {_opt.MaxVideoDurationSeconds / 60.0:0.#} minutes.");

                    await TranscodeAsync(inPath, outPath, timeout.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    throw new MediaRejectedException("Video processing timed out.",
                        StatusCodes.Status422UnprocessableEntity);
                }
            }
            finally { TranscodeGate.Release(); }

            await using var output = File.OpenRead(outPath);
            var key = keyWithoutExtension + ".mp4";
            await PutAsync(_s3.BucketName, key, output, "video/mp4", publicRead: false, ct);

            var seconds = (int)Math.Ceiling(await ProbeAsync(outPath, ct));
            return new StoredMedia(key, "video/mp4", output.Length, seconds);
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); }
            catch (Exception ex) { logger.LogWarning(ex, "Could not delete temp dir {Dir}", work); }
        }
    }

    private static async Task<bool> LooksLikeMp4Async(string path, CancellationToken ct)
    {
        var buf = new byte[12];
        await using var fs = File.OpenRead(path);
        var read = await fs.ReadAtLeastAsync(buf, buf.Length, throwOnEndOfStream: false, ct);
        // ISO-BMFF: bytes 4..8 are "ftyp".
        return read == 12 && buf[4] == 'f' && buf[5] == 't' && buf[6] == 'y' && buf[7] == 'p';
    }

    /// <summary>Returns the duration in seconds; rejects files with no video stream.</summary>
    private async Task<double> ProbeAsync(string path, CancellationToken ct)
    {
        var (code, stdout, stderr) = await RunAsync(_opt.FfprobePath,
        [
            "-v", "error",
            "-protocol_whitelist", "file",          // no network/SSRF via crafted inputs
            "-f", "mov",                             // force the MP4 demuxer, no format guessing
            "-show_entries", "format=duration:stream=codec_type",
            "-of", "json",
            path
        ], ct);

        if (code != 0)
        {
            logger.LogInformation("ffprobe rejected upload: {Err}", stderr);
            throw new MediaRejectedException("The video file is corrupt or unreadable.");
        }

        using var doc = JsonDocument.Parse(stdout);
        var hasVideo = doc.RootElement.TryGetProperty("streams", out var streams)
            && streams.EnumerateArray().Any(s => s.TryGetProperty("codec_type", out var t) && t.GetString() == "video");
        if (!hasVideo)
            throw new MediaRejectedException("The file has no video stream.");

        if (doc.RootElement.TryGetProperty("format", out var fmt)
            && fmt.TryGetProperty("duration", out var d)
            && double.TryParse(d.GetString(), System.Globalization.CultureInfo.InvariantCulture, out var seconds))
            return seconds;

        throw new MediaRejectedException("Could not determine the video duration.");
    }

    private async Task TranscodeAsync(string inPath, string outPath, CancellationToken ct)
    {
        // H.264 + AAC in MP4: plays everywhere. +faststart moves the index to the front so
        // browsers can start playback and seek via range requests against the presigned URL.
        var (code, _, stderr) = await RunAsync(_opt.FfmpegPath,
        [
            "-hide_banner", "-loglevel", "error", "-nostdin", "-y",
            "-protocol_whitelist", "file",
            "-f", "mov", "-i", inPath,
            "-map", "0:v:0", "-map", "0:a:0?",
            "-vf", $"scale=-2:'min({_opt.VideoMaxHeight},ih)'",
            "-c:v", "libx264", "-preset", _opt.VideoPreset, "-crf", _opt.VideoCrf.ToString(),
            "-pix_fmt", "yuv420p",
            "-c:a", "aac", "-b:a", "96k",
            "-map_metadata", "-1",
            "-movflags", "+faststart",
            "-f", "mp4", outPath
        ], ct);

        if (code != 0)
        {
            logger.LogWarning("ffmpeg failed ({Code}): {Err}", code, stderr);
            throw new MediaRejectedException("The video could not be processed.",
                StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static async Task<(int Code, string Stdout, string Stderr)> RunAsync(
        string exe, IEnumerable<string> args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a); // argument list: no shell, no injection

        Process p;
        try { p = Process.Start(psi)!; }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException($"Could not start '{exe}'. Is ffmpeg installed?", ex);
        }

        using (p)
        {
            var stdout = p.StandardOutput.ReadToEndAsync(ct);
            var stderr = p.StandardError.ReadToEndAsync(ct);
            try { await p.WaitForExitAsync(ct); }
            catch (OperationCanceledException)
            {
                try { p.Kill(entireProcessTree: true); } catch { /* already gone */ }
                throw;
            }
            return (p.ExitCode, await stdout, await stderr);
        }
    }

    // ---------------------------------------------------------------- storage

    private async Task PutAsync(string bucket, string key, Stream stream, string contentType,
        bool publicRead, CancellationToken ct)
    {
        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            // No ACLs: public access comes from the bucket's anonymous-read rule, nothing else.
        };

        // Public keys are unique per upload (never overwritten), so browsers may cache them forever.
        if (publicRead)
            request.Headers.CacheControl = "public, max-age=31536000, immutable";

        try { await s3.PutObjectAsync(request, ct); }
        catch (AmazonS3Exception ex)
        {
            throw new InvalidOperationException("Error uploading to object storage.", ex);
        }
    }

    public Task DeleteAsync(string blobPath, CancellationToken ct) => DeleteFromAsync(_s3.BucketName, blobPath, ct);

    public Task DeletePublicAsync(string blobPath, CancellationToken ct) => DeleteFromAsync(_s3.PublicBucketName, blobPath, ct);

    private async Task DeleteFromAsync(string bucket, string blobPath, CancellationToken ct)
    {
        try { await s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = bucket, Key = blobPath }, ct); }
        catch (AmazonS3Exception ex)
        {
            throw new InvalidOperationException("Error deleting from object storage.", ex);
        }
    }

    public string GetPublicUrl(string blobPath)
        => $"{(_s3.PublicEndpoint ?? _s3.Endpoint).TrimEnd('/')}/{_s3.PublicBucketName}/{blobPath}";

    public PresignedUrl CreateDownloadUrl(string blobPath)
    {
        var expires = DateTime.UtcNow.AddMinutes(_opt.PresignedUrlLifetimeMinutes);
        var publicEndpoint = _s3.PublicEndpoint ?? _s3.Endpoint;

        var url = publicS3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _s3.BucketName,
            Key = blobPath,
            Verb = HttpVerb.GET,
            Expires = expires,
            Protocol = new Uri(publicEndpoint).Scheme == Uri.UriSchemeHttps ? Protocol.HTTPS : Protocol.HTTP,
        });
        return new PresignedUrl(url, new DateTimeOffset(expires, TimeSpan.Zero));
    }
}
