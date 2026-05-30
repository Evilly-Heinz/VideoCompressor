namespace VideoCompressor.Core;

public sealed class BatchCompressionCallbacks
{
    public Action<int, CompressionOptions>? ItemStarting { get; init; }

    public Action<int, int>? ItemProgress { get; init; }

    public Action<int, CompressionItemResult>? ItemCompleted { get; init; }
}

public sealed class BatchCompressionService
{
    private readonly CompressionService _compressionService;

    public BatchCompressionService(CompressionService compressionService)
    {
        _compressionService = compressionService;
    }

    public BatchCompressionService()
        : this(new CompressionService())
    {
    }

    public async Task<IReadOnlyList<CompressionItemResult>> RunAsync(
        IReadOnlyList<CompressionOptions> items,
        BatchCompressionCallbacks? callbacks = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<CompressionItemResult>(items.Count);

        for (int i = 0; i < items.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            CompressionOptions options = items[i];
            callbacks?.ItemStarting?.Invoke(i, options);

            IProgress<int>? progress = callbacks?.ItemProgress != null
                ? new Progress<int>(p => callbacks.ItemProgress!(i, p))
                : null;

            try
            {
                CompressionItemResult result = await _compressionService.CompressAsync(
                    options, progress, cancellationToken);

                results.Add(result);
                callbacks?.ItemCompleted?.Invoke(i, result);

                if (result.Status == CompressionItemStatus.Cancelled)
                    break;
            }
            catch (OperationCanceledException)
            {
                var cancelled = CompressionItemResult.Cancelled(options.InputPath);
                results.Add(cancelled);
                callbacks?.ItemCompleted?.Invoke(i, cancelled);
                break;
            }
        }

        return results;
    }
}
