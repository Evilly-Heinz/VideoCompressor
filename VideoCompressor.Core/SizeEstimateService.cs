namespace VideoCompressor.Core;

public sealed class SizeEstimateResult
{
    public long TotalSourceBytes { get; init; }

    public long TotalEstimatedBytes { get; init; }
}

public sealed class SizeEstimateService
{
    private readonly MediaProbeService _probeService = new();

    public async Task<SizeEstimateResult> EstimateAsync(
        IReadOnlyList<string> inputPaths,
        int crf,
        int targetHeight,
        CancellationToken cancellationToken = default)
    {
        long totalSrcBytes = inputPaths.Sum(path => new FileInfo(path).Length);
        long totalEstBytes = 0;

        if (!FfmpegLocator.IsFfprobeAvailable)
        {
            return new SizeEstimateResult
            {
                TotalSourceBytes = totalSrcBytes,
                TotalEstimatedBytes = 0,
            };
        }

        foreach (string path in inputPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var summary = await _probeService.ProbeAsync(path, cancellationToken);
            if (summary == null)
                continue;

            double srcBps = summary.Bitrate > 0
                ? summary.Bitrate
                : new FileInfo(path).Length * 8.0 / Math.Max(summary.Duration.TotalSeconds, 1);

            double crfFactor = 0.45 * Math.Pow(2.0, (23.0 - crf) / 6.0);
            double resFactor = targetHeight > 0 && targetHeight < summary.Height
                ? Math.Pow((double)targetHeight / summary.Height, 2)
                : 1.0;
            double estBps = srcBps * crfFactor * resFactor + 128_000;
            totalEstBytes += (long)(estBps * summary.Duration.TotalSeconds / 8.0);
        }

        return new SizeEstimateResult
        {
            TotalSourceBytes = totalSrcBytes,
            TotalEstimatedBytes = totalEstBytes,
        };
    }
}
