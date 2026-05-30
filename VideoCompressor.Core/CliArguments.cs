namespace VideoCompressor.Core;

public sealed class CliArguments
{
    public string InputPath { get; init; } = "";

    public int Crf { get; init; } = 23;

    public string Preset { get; init; } = "medium";

    /// <summary>Null when default output path should be resolved by the host.</summary>
    public string? OutputPath { get; init; }

    public bool ShowHelp { get; init; }

    public static bool IsCliMode(IReadOnlyList<string> args)
    {
        foreach (string arg in args)
        {
            if (IsRecognizedFlag(arg))
                return true;
        }

        return false;
    }

    public static bool TryParse(string[] args, out CliArguments? parsed, out string? error)
    {
        parsed = null;
        error = null;

        bool showHelp = false;
        int? crf = null;
        string? preset = null;
        string? outputPath = null;
        string? inputPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (IsHelpFlag(arg))
            {
                showHelp = true;
                continue;
            }

            if (arg is "-q" or "-s" or "-o")
            {
                if (i + 1 >= args.Length)
                {
                    error = $"Missing value for {arg}.";
                    return false;
                }

                string value = args[++i];

                switch (arg)
                {
                    case "-q":
                        if (!int.TryParse(value, out int parsedCrf))
                        {
                            error = $"Invalid CRF value: {value}";
                            return false;
                        }

                        crf = parsedCrf;
                        break;
                    case "-s":
                        preset = value;
                        break;
                    case "-o":
                        outputPath = value;
                        break;
                }

                continue;
            }

            if (arg.StartsWith('-') || arg == "/?")
            {
                error = $"Unknown option: {arg}";
                return false;
            }

            if (inputPath == null)
                inputPath = arg;
            else
            {
                error = $"Unexpected argument: {arg}";
                return false;
            }
        }

        if (showHelp)
        {
            parsed = new CliArguments { ShowHelp = true };
            return true;
        }

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            error = "Input file path is required.";
            return false;
        }

        inputPath = Path.GetFullPath(inputPath);

        if (!File.Exists(inputPath))
        {
            error = $"Input file not found: {inputPath}";
            return false;
        }

        int effectiveCrf = crf ?? 23;
        if (effectiveCrf is < 18 or > 40)
        {
            error = $"CRF must be between 18 and 40 (got {effectiveCrf}).";
            return false;
        }

        string effectivePreset = preset ?? "medium";
        if (!CompressionOptions.ValidPresets.Contains(effectivePreset))
        {
            error = $"Unknown preset: {effectivePreset}";
            return false;
        }

        if (outputPath != null)
        {
            outputPath = Path.GetFullPath(outputPath);
            string? parent = Path.GetDirectoryName(outputPath);

            if (string.IsNullOrWhiteSpace(parent))
            {
                error = "Invalid output path.";
                return false;
            }

            if (!Directory.Exists(parent))
            {
                error = $"Output directory does not exist: {parent}";
                return false;
            }
        }

        parsed = new CliArguments
        {
            InputPath = inputPath,
            Crf = effectiveCrf,
            Preset = effectivePreset,
            OutputPath = outputPath,
        };

        return true;
    }

    private static bool IsRecognizedFlag(string arg)
        => IsHelpFlag(arg) || arg is "-q" or "-s" or "-o";

    private static bool IsHelpFlag(string arg)
        => arg is "-h" or "--help" or "/?";
}
