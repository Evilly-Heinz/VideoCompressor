using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace VideoCompressorUI
{
    public partial class MainWindow : Window
    {
        // ── State ────────────────────────────────────────────────────────────
        private string?                  _inputFile;
        private bool                     _isCompressing;
        private CancellationTokenSource? _cts;

        private static readonly string FfmpegDir =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg");

        private static readonly string[] VideoExtensions =
            { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv", ".webm", ".m4v", ".ts", ".mts" };

        // ── Init ─────────────────────────────────────────────────────────────
        public MainWindow()
        {
            InitializeComponent();
            FFmpeg.SetExecutablesPath(FfmpegDir);

            // Launched from context-menu: VideoCompressorUI.exe "path\to\video.mp4"
            var args = Environment.GetCommandLineArgs();
            if (args.Length >= 2 && File.Exists(args[1]))
                SetInputFile(args[1]);
        }

        // ── Title bar ────────────────────────────────────────────────────────
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void MinBtn_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void MaxBtn_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized
               ? WindowState.Normal : WindowState.Maximized;

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
            => Close();

        // ── File selection ───────────────────────────────────────────────────
        private void BrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title  = "Select a video file",
                Filter = "Video files|*.mp4;*.mov;*.avi;*.mkv;*.wmv;*.flv;*.webm;*.m4v;*.ts;*.mts"
                       + "|All files|*.*"
            };
            if (dlg.ShowDialog(this) == true)
                SetInputFile(dlg.FileName);
        }

        private void DropZone_Click(object sender, MouseButtonEventArgs e)
            => BrowseBtn_Click(sender, e);

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files?.Length > 0 && IsVideoFile(files[0]))
                SetInputFile(files[0]);
            else
                SetStatus("⚠  Unsupported file type — please drop a video file.", warn: true);
        }

        private void SetInputFile(string path)
        {
            _inputFile = path;
            var fi     = new FileInfo(path);
            string mb  = (fi.Length / 1_048_576.0).ToString("F1");

            DropIcon.Text  = "\uE8A5";
            DropLabel.Text = $"{fi.Name}   ({mb} MB)";
            DropLabel.Foreground = FindResource("TextPrimaryBrush") as Brush;

            CompressBtn.IsEnabled    = true;
            ProgressPanel.Visibility = Visibility.Collapsed;
            SizeLabel.Text           = "";
            SetStatus($"Selected: {fi.Name}  ·  {mb} MB");
        }

        // ── Compression ──────────────────────────────────────────────────────
        private void CompressBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isCompressing) { _cts?.Cancel(); return; }
            StartCompression();
        }

        private async void StartCompression()
        {
            if (_inputFile == null) return;

            int    crf    = (int)CrfSlider.Value;
            string preset = GetSelectedPreset();
            string output = GetOutputPath(_inputFile);

            // UI → busy
            _isCompressing           = true;
            CompressBtn.Content      = "■  Cancel";
            BrowseBtn.IsEnabled      = false;
            ProgressPanel.Visibility = Visibility.Visible;
            ProgressBar.Value        = 0;
            ProgressPct.Text         = "0%";
            ProgressTitle.Text       = "Compressing…";
            ProgressDetail.Text      = $"CRF {crf}  ·  {preset}  →  {Path.GetFileName(output)}";
            SizeLabel.Text           = "";
            SetStatus("Compressing — please wait…");

            _cts = new CancellationTokenSource();
            bool success   = false;
            bool cancelled = false;

            try
            {
                // ── Ensure FFmpeg binaries are present ───────────────────────
                Directory.CreateDirectory(FfmpegDir);
                if (!File.Exists(Path.Combine(FfmpegDir, "ffmpeg.exe")))
                {
                    ProgressTitle.Text  = "Downloading FFmpeg…";
                    ProgressDetail.Text = "First-run download from GitHub (~70 MB). Please wait.";
                    SetStatus("Downloading FFmpeg for the first time…");
                    await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, FfmpegDir);
                    FFmpeg.SetExecutablesPath(FfmpegDir);
                    ProgressTitle.Text  = "Compressing…";
                    ProgressDetail.Text = $"CRF {crf}  ·  {preset}  →  {Path.GetFileName(output)}";
                    SetStatus("Compressing — please wait…");
                }

                // ── Get media info ────────────────────────────────────────────
                IMediaInfo info = await FFmpeg.GetMediaInfo(_inputFile!, _cts.Token);

                // ── Build conversion ──────────────────────────────────────────
                var conversion = FFmpeg.Conversions.New()
                    .SetOutput(output)
                    .SetOverwriteOutput(true);

                var video = info.VideoStreams.FirstOrDefault();
                if (video != null)
                {
                    video.SetCodec(VideoCodec.h264);
                    conversion.AddStream(video);
                }

                var audio = info.AudioStreams.FirstOrDefault();
                if (audio != null)
                {
                    audio.SetCodec(AudioCodec.aac);
                    conversion.AddStream(audio);
                }

                conversion
                    .AddParameter($"-crf {crf}",      ParameterPosition.PostInput)
                    .AddParameter($"-preset {preset}", ParameterPosition.PostInput)
                    .AddParameter("-b:a 128k",         ParameterPosition.PostInput)
                    .AddParameter("-movflags +faststart", ParameterPosition.PostInput);

                conversion.OnProgress += (_, args) =>
                    Dispatcher.Invoke(() =>
                    {
                        int pct = Math.Clamp(args.Percent, 0, 100);
                        ProgressBar.Value = pct;
                        ProgressPct.Text  = $"{pct}%";
                    });

                await conversion.Start(_cts.Token);
                success = File.Exists(output);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
            }

            _isCompressing = false;

            if (success)
            {
                OnDone(output);
            }
            else
            {
                ProgressTitle.Text = cancelled ? "Cancelled" : "Failed";
                ProgressPct.Text   = "✕";
                SetStatus(cancelled ? "Compression cancelled." : "Compression failed.", warn: true);
            }

            ResetUI();
        }

        private void OnDone(string outputPath)
        {
            ProgressBar.Value  = 100;
            ProgressPct.Text   = "100%";
            ProgressTitle.Text = "Done ✓";

            long inSz  = new FileInfo(_inputFile!).Length;
            long outSz = new FileInfo(outputPath).Length;
            double saved = inSz > 0 ? (1.0 - (double)outSz / inSz) * 100.0 : 0;
            string inMb  = (inSz  / 1_048_576.0).ToString("F1");
            string outMb = (outSz / 1_048_576.0).ToString("F1");

            ProgressDetail.Text = $"Input: {inMb} MB   →   Output: {outMb} MB";
            SizeLabel.Text      = $"↓ {saved:F0}% smaller";
            SetStatus($"✓  Saved to {Path.GetFileName(outputPath)}  ·  {saved:F0}% smaller");

            var dlg = MessageBox.Show(
                $"Compression complete!\n\n" +
                $"Input:   {inMb} MB\n" +
                $"Output:  {outMb} MB\n" +
                $"Saved:   {saved:F0}%\n\n" +
                $"Open output folder?",
                "Done", MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (dlg == MessageBoxResult.Yes)
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{outputPath}\"");
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private void CrfSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CrfLabel == null) return;
            CrfLabel.Text = ((int)CrfSlider.Value).ToString();
        }

        private string GetSelectedPreset()
        {
            if (PresetCombo.SelectedItem is System.Windows.Controls.ComboBoxItem item)
                return item.Tag?.ToString() ?? "medium";
            return "medium";
        }

        private void ResetUI()
        {
            CompressBtn.Content = "▶  Compress";
            BrowseBtn.IsEnabled = true;
        }

        private void SetStatus(string text, bool warn = false)
        {
            StatusLabel.Text       = text;
            StatusLabel.Foreground = warn
                ? FindResource("DangerBrush") as Brush
                : FindResource("TextTertiaryBrush") as Brush;
        }

        private void ShowError(string msg)
            => MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

        private static string GetOutputPath(string input)
        {
            string dir  = Path.GetDirectoryName(input) ?? ".";
            string name = Path.GetFileNameWithoutExtension(input);
            return Path.Combine(dir, name + "_compressed.mp4");
        }

        private static bool IsVideoFile(string path)
            => Array.IndexOf(VideoExtensions,
               Path.GetExtension(path).ToLowerInvariant()) >= 0;

        // ── Context-menu registration ────────────────────────────────────────
        private async void RegisterCtxBtn_Click(object sender, RoutedEventArgs e)
        {
            RegisterCtxBtn.IsEnabled      = false;
            CtxMenuStatusLabel.Text       = "Registering…";
            CtxMenuStatusLabel.Foreground = FindResource("TextSecondaryBrush") as Brush;

            try
            {
                // Use the running exe path so the entry always points to the correct location.
                string exePath = Process.GetCurrentProcess().MainModule?.FileName
                                 ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VideoCompressorUI.exe");

                // Write the reg file next to the exe so users can also run it manually.
                string regFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                              "install_context_menu.reg");

                await Task.Run(() =>
                    File.WriteAllText(regFile, BuildRegContent(exePath), Encoding.Unicode));

                // regedit /s imports silently; runas triggers UAC for admin rights.
                var psi = new ProcessStartInfo("regedit.exe", $"/s \"{regFile}\"")
                {
                    UseShellExecute = true,
                    Verb            = "runas"
                };

                int exitCode = await Task.Run(() =>
                {
                    using var proc = Process.Start(psi)!;
                    proc.WaitForExit();
                    return proc.ExitCode;
                });

                bool ok = exitCode == 0;
                CtxMenuStatusLabel.Text       = ok
                    ? "✓  Registered — right-click any video file in Explorer to see the option."
                    : "Registration failed. Try running the app as administrator.";
                CtxMenuStatusLabel.Foreground = (ok
                    ? FindResource("SuccessBrush")
                    : FindResource("DangerBrush")) as Brush;
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // User dismissed the UAC prompt.
                CtxMenuStatusLabel.Text       = "Cancelled — administrator permission is required.";
                CtxMenuStatusLabel.Foreground = FindResource("DangerBrush") as Brush;
            }
            catch (Exception ex)
            {
                ShowError($"Failed to register context menu:\n{ex.Message}");
            }
            finally
            {
                RegisterCtxBtn.IsEnabled = true;
            }
        }

        /// <summary>
        /// Generates .reg file content that registers a right-click "Compress this video"
        /// entry for all supported video extensions, pointing to <paramref name="exePath"/>.
        /// </summary>
        private static string BuildRegContent(string exePath)
        {
            // .reg format requires backslashes doubled and quotes escaped with backslash.
            string ep = exePath.Replace(@"\", @"\\");

            string[] exts = { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv", ".webm", ".m4v" };

            var sb = new StringBuilder();
            sb.AppendLine("Windows Registry Editor Version 5.00");

            foreach (string ext in exts)
            {
                sb.AppendLine();
                sb.AppendLine($@"[HKEY_CLASSES_ROOT\SystemFileAssociations\{ext}\shell\VideoCompressor]");
                sb.AppendLine(@"@=""Compress this video""");
                sb.AppendLine($"\"Icon\"=\"{ep},0\"");
                sb.AppendLine($@"[HKEY_CLASSES_ROOT\SystemFileAssociations\{ext}\shell\VideoCompressor\command]");
                sb.AppendLine($"@=\"\\\"{ep}\\\" \\\"%1\\\"\"");
            }

            return sb.ToString();
        }
    }
}
