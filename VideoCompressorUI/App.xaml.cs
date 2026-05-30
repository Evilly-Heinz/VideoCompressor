using System.Windows;
using VideoCompressor.Core;
using Application = System.Windows.Application;

namespace VideoCompressorUI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            FfmpegBootstrap.ConfigurePaths();
        }
    }
}
