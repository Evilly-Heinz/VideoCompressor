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

    /// <summary>
    /// Returns the default CLI output path, appending _1, _2, … when the base name is taken.
    /// </summary>
    public static string ResolveUniqueDefault(string inputPath, string? outputFolder = null, string? suffix = null)
    {
        string basePath = Resolve(inputPath, outputFolder, suffix);
        if (!File.Exists(basePath))
            return basePath;

        string dir = Path.GetDirectoryName(basePath) ?? ".";
        string nameWithoutExt = Path.GetFileNameWithoutExtension(basePath);

        for (int i = 1; i < 10_000; i++)
        {
            string candidate = Path.Combine(dir, $"{nameWithoutExt}_{i}.mp4");
            if (!File.Exists(candidate))
                return candidate;
        }

        throw new IOException("Could not find an available output file name.");
    }
}
