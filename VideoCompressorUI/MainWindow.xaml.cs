using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace VideoCompressorUI
{
    public partial class MainWindow : Window
    {
        // ── State ────────────────────────────────────────────────────────────
        private string?  _inputFile;
        private bool     _isCompressing;
        private Process? _compressorProcess;

        private static readonly string[] VideoExtensions =
            { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv", ".webm", ".m4v", ".ts", ".mts" };

        // ── Init ─────────────────────────────────────────────────────────────
        public MainWindow()
        {
            InitializeComponent();

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

            DropIcon.Text  = "\uE8A5"; // Video icon (Segoe Fluent)
            DropLabel.Text = $"{fi.Name}   ({mb} MB)";
            DropLabel.Foreground = FindResource("TextPrimaryBrush") as Brush;

            CompressBtn.IsEnabled       = true;
            ProgressPanel.Visibility    = Visibility.Collapsed;
            SizeLabel.Text              = "";
            SetStatus($"Selected: {fi.Name}  ·  {mb} MB");
        }

        // ── Compression ──────────────────────────────────────────────────────
        private void CompressBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isCompressing) { _compressorProcess?.Kill(true); return; }
            StartCompression();
        }

        private async void StartCompression()
        {
            if (_inputFile == null) return;

            int    crf    = (int)CrfSlider.Value;
            string preset = GetSelectedPreset();
            string output = GetOutputPath(_inputFile);

            // UI → busy
            _isCompressing          = true;
            CompressBtn.Content     = "■  Cancel";
            BrowseBtn.IsEnabled     = false;
            ProgressPanel.Visibility = Visibility.Visible;
            ProgressBar.Value       = 0;
            ProgressPct.Text        = "0%";
            ProgressTitle.Text      = "Compressing…";
            ProgressDetail.Text     = $"CRF {crf}  ·  {preset}  →  {Path.GetFileName(output)}";
            SizeLabel.Text          = "";
            SetStatus("Compressing — please wait…");

            // Locate VideoCompressor.exe (same folder as this exe)
            string exeDir     = AppDomain.CurrentDomain.BaseDirectory;
            string compressor = Path.Combine(exeDir, "VideoCompressor.exe");

            if (!File.Exists(compressor))
            {
                ShowError(
                    $"VideoCompressor.exe not found in:\n{exeDir}\n\n" +
                    "Build the C++ project first (Release|x64) so both EXEs land in bin\\Release\\.");
                ResetUI();
                return;
            }

            bool success = false;
            try
            {
                _compressorProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName               = compressor,
                        Arguments              = $"\"{_inputFile}\" --crf {crf} --preset {preset}",
                        UseShellExecute        = false,
                        CreateNoWindow         = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8,
                    }
                };
                _compressorProcess.Start();

                // Drain stderr silently
                _compressorProcess.BeginErrorReadLine();

                await System.Threading.Tasks.Task.Run(() =>
                {
                    string? line;
                    while ((line = _compressorProcess!.StandardOutput.ReadLine()) != null)
                        Dispatcher.Invoke(() => HandleLine(line));
                    _compressorProcess.WaitForExit();
                    success = _compressorProcess.ExitCode == 0;
                });
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }

            _compressorProcess = null;
            _isCompressing     = false;

            if (success && File.Exists(output))
                OnDone(output);
            else if (!success)
            {
                ProgressTitle.Text = "Failed or cancelled";
                ProgressPct.Text   = "✕";
                SetStatus("Compression failed or was cancelled.", warn: true);
            }

            ResetUI();
        }

        private void HandleLine(string line)
        {
            if (line.StartsWith("PROGRESS:") && int.TryParse(line[9..], out int pct))
            {
                pct = Math.Clamp(pct, 0, 100);
                ProgressBar.Value = pct;
                ProgressPct.Text  = $"{pct}%";
            }
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
                Process.Start("explorer.exe", $"/select,\"{outputPath}\"");
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
            CompressBtn.Content   = "▶  Compress";
            BrowseBtn.IsEnabled   = true;
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
    }
}
