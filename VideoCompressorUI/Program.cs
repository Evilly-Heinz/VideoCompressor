using System.Windows;
using VideoCompressor.Core;

namespace VideoCompressorUI;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (CliArguments.IsCliMode(args))
            Environment.Exit(CliHost.Run(args));

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
