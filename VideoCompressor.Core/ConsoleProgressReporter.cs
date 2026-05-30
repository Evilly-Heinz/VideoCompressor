namespace VideoCompressor.Core;

public sealed class ConsoleProgressReporter : IProgress<int>
{
    private int _lastReported = -1;

    public void Report(int value)
    {
        value = Math.Clamp(value, 0, 100);
        if (value == _lastReported)
            return;

        _lastReported = value;
        Console.Error.WriteLine($"{value}%");
    }
}
