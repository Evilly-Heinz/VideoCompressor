using Xabe.FFmpeg;

namespace VideoCompressor.Core;

public sealed class MediaInfoSummary
{
    public int Width { get; init; }

    public int Height { get; init; }

    public long Bitrate { get; init; }

    public TimeSpan Duration { get; init; }

    public string ResolutionDisplay => $"{Width}×{Height}";
}

public sealed class MediaProbeService
{
    public async Task<MediaInfoSummary?> ProbeAsync(
        string inputPath,
        CancellationToken cancellationToken = default)
    {
        if (!FfmpegLocator.IsFfprobeAvailable)
            return null;

        try
        {
            IMediaInfo info = await FFmpeg.GetMediaInfo(inputPath, cancellationToken);
            var vid = info.VideoStreams.FirstOrDefault();
            if (vid == null)
                return null;

            return new MediaInfoSummary
            {
                Width = vid.Width,
                Height = vid.Height,
                Bitrate = vid.Bitrate,
                Duration = info.Duration,
            };
        }
        catch
        {
            return null;
        }
    }
}
