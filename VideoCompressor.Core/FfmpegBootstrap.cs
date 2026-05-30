using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace VideoCompressor.Core;

public static class FfmpegBootstrap
{
    public static void ConfigurePaths()
        => FFmpeg.SetExecutablesPath(FfmpegLocator.FfmpegDirectory);

    public static async Task EnsureAvailableAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(FfmpegLocator.FfmpegDirectory);
        ConfigurePaths();

        if (FfmpegLocator.IsFfmpegAvailable)
            return;

        cancellationToken.ThrowIfCancellationRequested();
        await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, FfmpegLocator.FfmpegDirectory);
        ConfigurePaths();
    }
}
