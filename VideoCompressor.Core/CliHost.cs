namespace VideoCompressor.Core;

public static class CliHost
{
    public static int Run(string[] args)
    {
        if (!CliArguments.TryParse(args, out CliArguments? parsed, out string? error))
        {
            Console.Error.WriteLine(error);
            return (int)CliExitCode.InvalidArguments;
        }

        if (parsed!.ShowHelp)
        {
            Console.Out.WriteLine(GetHelpText());
            return (int)CliExitCode.Success;
        }

        string outputPath;
        if (parsed.OutputPath != null)
        {
            if (File.Exists(parsed.OutputPath))
            {
                Console.Error.WriteLine($"Error: Output file already exists: {parsed.OutputPath}");
                return (int)CliExitCode.GeneralFailure;
            }

            outputPath = parsed.OutputPath;
        }
        else
        {
            try
            {
                outputPath = OutputPathResolver.ResolveUniqueDefault(parsed.InputPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return (int)CliExitCode.GeneralFailure;
            }
        }

        try
        {
            return RunAsync(parsed, outputPath).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            return (int)CliExitCode.Cancelled;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return (int)CliExitCode.GeneralFailure;
        }
    }

    private static async Task<int> RunAsync(CliArguments parsed, string outputPath)
    {
        if (!FfmpegLocator.IsFfmpegAvailable)
            Console.Error.WriteLine("Downloading FFmpeg (~70 MB)...");

        await FfmpegBootstrap.EnsureAvailableAsync();

        var options = new CompressionOptions
        {
            InputPath = parsed.InputPath,
            OutputPath = outputPath,
            Crf = parsed.Crf,
            Preset = parsed.Preset,
        };

        var service = new CompressionService();
        CompressionItemResult result = await service.CompressAsync(
            options,
            new ConsoleProgressReporter());

        if (result.Status == CompressionItemStatus.Done)
            return (int)CliExitCode.Success;

        if (result.Status == CompressionItemStatus.Cancelled)
            return (int)CliExitCode.Cancelled;

        Console.Error.WriteLine($"Error: {result.ErrorMessage ?? "Compression failed."}");
        return (int)CliExitCode.GeneralFailure;
    }

    private static string GetHelpText()
    {
        return """
            Video Compressor - command-line usage

            SYNOPSIS
              VideoCompressorUI.exe "<input>" [-q <crf>] [-s <preset>] [-o "<output>"]
              VideoCompressorUI.exe -h | --help | /?

            FLAGS
              -q <crf>       CRF quality (18-40, default: 23)
              -s <preset>    x264 preset (default: medium)
                             ultrafast, superfast, veryfast, faster, fast,
                             medium, slow, slower, veryslow
              -o <path>      Output file path (must not already exist)
              -h, --help, /? Show this help

            DEFAULT OUTPUT
              When -o is omitted, output is written next to the input file as
              {name}_compressed.mp4. If that file exists, _1, _2, ... is appended.

            PROGRESS
              Encode percent is written to stderr during compression.

            EXIT CODES
              0  Success
              1  General failure (FFmpeg error, I/O, output already exists)
              2  Invalid arguments
              3  Cancelled (reserved)

            EXAMPLES
              VideoCompressorUI.exe "C:\Videos\clip.mp4"
              VideoCompressorUI.exe "C:\Videos\clip.mp4" -q 28 -s fast
              VideoCompressorUI.exe "C:\Videos\clip.mp4" -o "C:\Videos\clip_small.mp4"
            """;
    }
}
