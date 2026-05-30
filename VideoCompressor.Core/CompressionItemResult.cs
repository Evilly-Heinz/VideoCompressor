namespace VideoCompressor.Core;

public sealed class CompressionItemResult
{
    public CompressionItemStatus Status { get; init; }

    public string InputPath { get; init; } = "";

    public string? OutputPath { get; init; }

    public string? ErrorMessage { get; init; }

    public static CompressionItemResult Done(string inputPath, string outputPath)
        => new() { Status = CompressionItemStatus.Done, InputPath = inputPath, OutputPath = outputPath };

    public static CompressionItemResult Error(string inputPath, string? message = null)
        => new() { Status = CompressionItemStatus.Error, InputPath = inputPath, ErrorMessage = message };

    public static CompressionItemResult Cancelled(string inputPath)
        => new() { Status = CompressionItemStatus.Cancelled, InputPath = inputPath };
}
