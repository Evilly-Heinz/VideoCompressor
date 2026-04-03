using System.IO;
using System.Windows;
using Xabe.FFmpeg;
using Application = System.Windows.Application;

namespace VideoCompressorUI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Point Xabe.FFmpeg at the local ffmpeg subfolder.
            // Binaries are downloaded on first compression if not present.
            string ffmpegDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg");
            Directory.CreateDirectory(ffmpegDir);
            FFmpeg.SetExecutablesPath(ffmpegDir);
        }
    }
}
