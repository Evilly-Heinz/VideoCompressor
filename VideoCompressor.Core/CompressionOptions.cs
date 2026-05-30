namespace VideoCompressor.Core;

public sealed class CompressionOptions
{
    public static IReadOnlySet<string> ValidPresets { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ultrafast", "superfast", "veryfast", "faster", "fast",
        "medium", "slow", "slower", "veryslow",
    };

    public string InputPath { get; init; } = "";

    public string OutputPath { get; init; } = "";

    public int Crf { get; init; } = 23;

    public string Preset { get; init; } = "medium";

    /// <summary>Target height in pixels; 0 keeps source resolution.</summary>
    public int TargetHeight { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(InputPath))
            throw new ArgumentException("Input path is required.", nameof(InputPath));

        if (!File.Exists(InputPath))
            throw new FileNotFoundException("Input file not found.", InputPath);

        if (string.IsNullOrWhiteSpace(OutputPath))
            throw new ArgumentException("Output path is required.", nameof(OutputPath));

        if (Crf is < 18 or > 40)
            throw new ArgumentOutOfRangeException(nameof(Crf), "CRF must be between 18 and 40.");

        if (!ValidPresets.Contains(Preset))
            throw new ArgumentException($"Unknown preset: {Preset}", nameof(Preset));
    }
}
