namespace VideoCompressor.Core;

public static class FfmpegLocator
{
    public static string FfmpegDirectory { get; } =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg");

    public static string FfmpegExePath => Path.Combine(FfmpegDirectory, "ffmpeg.exe");

    public static string FfprobeExePath => Path.Combine(FfmpegDirectory, "ffprobe.exe");

    public static bool IsFfmpegAvailable => File.Exists(FfmpegExePath);

    public static bool IsFfprobeAvailable => File.Exists(FfprobeExePath);
}
