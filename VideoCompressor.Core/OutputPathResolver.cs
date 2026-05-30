namespace VideoCompressor.Core;

public static class OutputPathResolver
{
    public static string Resolve(string inputPath, string? outputFolder = null, string? suffix = null)
    {
        string dir = outputFolder ?? Path.GetDirectoryName(inputPath) ?? ".";
        string name = Path.GetFileNameWithoutExtension(inputPath);
        string effectiveSuffix = string.IsNullOrWhiteSpace(suffix) ? "_compressed" : suffix.Trim();
        return Path.Combine(dir, name + effectiveSuffix + ".mp4");
    }
}
