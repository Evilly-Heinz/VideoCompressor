using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;
// Disambiguate WPF types from System.Windows.Forms equivalents
using Brush            = System.Windows.Media.Brush;
using Color            = System.Windows.Media.Color;
using DataFormats      = System.Windows.DataFormats;
using DragDropEffects  = System.Windows.DragDropEffects;
using DragEventArgs    = System.Windows.DragEventArgs;
using MessageBox       = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage  = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;
using OpenFileDialog   = Microsoft.Win32.OpenFileDialog;
using SolidColorBrush  = System.Windows.Media.SolidColorBrush;

namespace VideoCompressorUI
{
    public class QueueItem : INotifyPropertyChanged
    {
        // ── Frozen brushes ────────────────────────────────────────────────────
        private static readonly SolidColorBrush _fgSuccess   = F(new SolidColorBrush(Color.FromRgb(0x6C, 0xCB, 0x5F)));
        private static readonly SolidColorBrush _fgDanger    = F(new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A)));
        private static readonly SolidColorBrush _fgWarn      = F(new SolidColorBrush(Color.FromRgb(0xFF, 0xD6, 0x0A)));
        private static readonly SolidColorBrush _fgAccent    = F(new SolidColorBrush(Color.FromRgb(0x60, 0xCD, 0xFF)));
        private static readonly SolidColorBrush _fgTertiary  = F(new SolidColorBrush(Color.FromArgb(0x8C, 0xFF, 0xFF, 0xFF)));

        private static readonly SolidColorBrush _bgSuccess   = F(new SolidColorBrush(Color.FromArgb(0x28, 0x6C, 0xCB, 0x5F)));
        private static readonly SolidColorBrush _bgDanger    = F(new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0x45, 0x3A)));
        private static readonly SolidColorBrush _bgWarn      = F(new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xD6, 0x0A)));
        private static readonly SolidColorBrush _bgAccent    = F(new SolidColorBrush(Color.FromArgb(0x28, 0x60, 0xCD, 0xFF)));
        private static readonly SolidColorBrush _bgPending   = F(new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)));

        private static T F<T>(T obj) where T : System.Windows.Freezable { obj.Freeze(); return obj; }

        // ── Data ──────────────────────────────────────────────────────────────
        public string  FilePath   { get; init; } = "";
        public string  FileName   { get; init; } = "";
        public string  SizeMb     { get; init; } = "";
        public string? OutputPath { get; set;  }

        private string        _status           = "Pending";
        private double        _progress;
        private BitmapSource? _thumbnail;
        private string?       _sourceResolution;

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsProcessing));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(StatusBackground));
            }
        }

        public double Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); }
        }

        public BitmapSource? Thumbnail
        {
            get => _thumbnail;
            set { _thumbnail = value; OnPropertyChanged(); }
        }

        public string? SourceResolution
        {
            get => _sourceResolution;
            set { _sourceResolution = value; OnPropertyChanged(); }
        }

        public bool  IsProcessing    => Status == "Compressing";

        public Brush StatusColor => Status switch
        {
            "Done ✓"      => _fgSuccess,
            "Error"       => _fgDanger,
            "Cancelled"   => _fgWarn,
            "Compressing" => _fgAccent,
            _             => _fgTertiary,
        };

        public Brush StatusBackground => Status switch
        {
            "Done ✓"      => _bgSuccess,
            "Error"       => _bgDanger,
            "Cancelled"   => _bgWarn,
            "Compressing" => _bgAccent,
            _             => _bgPending,
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class MainWindow : Window
    {
        // ── Queue ─────────────────────────────────────────────────────────────
        public ObservableCollection<QueueItem> Queue { get; } = new();

        // ── State ────────────────────────────────────────────────────────────
        private bool                     _isCompressing;
        private CancellationTokenSource? _cts;
        private CancellationTokenSource? _estimateCts;
        private string?                  _outputFolder;
        private string                   _outputSuffix = "_compressed";

        private static readonly string FfmpegDir =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg");

        private static readonly string[] VideoExtensions =
            { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv", ".webm", ".m4v", ".ts", ".mts" };

        // ── Shell thumbnail P/Invoke ──────────────────────────────────────────
        [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig] int GetImage(System.Drawing.Size size, uint flags, out IntPtr phbm);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHCreateItemFromParsingName(
            string pszPath, IntPtr pbc, ref Guid riid,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppv);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private static BitmapSource? GetShellThumbnail(string filePath, int pixels)
        {
            try
            {
                var iid = typeof(IShellItemImageFactory).GUID;
                int hr  = SHCreateItemFromParsingName(filePath, IntPtr.Zero, ref iid, out object ppv);
                if (hr != 0 || ppv is not IShellItemImageFactory factory) return null;

                hr = factory.GetImage(new System.Drawing.Size(pixels, pixels), 0, out IntPtr hBitmap);
                if (hr != 0 || hBitmap == IntPtr.Zero) return null;

                try
                {
                    var src = Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap, IntPtr.Zero, Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    src.Freeze();
                    return src;
                }
                finally { DeleteObject(hBitmap); }
            }
            catch { return null; }
        }

        // ── Init ─────────────────────────────────────────────────────────────
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            FFmpeg.SetExecutablesPath(FfmpegDir);
            CheckRegistrationStatus();
            UpdateCrfDescription((int)CrfSlider.Value);

            var args = Environment.GetCommandLineArgs();
            if (args.Length >= 2 && File.Exists(args[1]))
                AddToQueue(new[] { args[1] });
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
                Title       = "Select video file(s)",
                Filter      = "Video files|*.mp4;*.mov;*.avi;*.mkv;*.wmv;*.flv;*.webm;*.m4v;*.ts;*.mts"
                            + "|All files|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog(this) == true)
                AddToQueue(dlg.FileNames);
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
            if (files == null || files.Length == 0) return;

            var videos = files.Where(IsVideoFile).ToArray();
            if (videos.Length > 0)
                AddToQueue(videos);
            else
                SetStatus("⚠  Unsupported file type — please drop video files.", warn: true);
        }

        private void AddToQueue(string[] paths)
        {
            foreach (var path in paths)
            {
                if (!File.Exists(path) || !IsVideoFile(path)) continue;
                if (Queue.Any(q => q.FilePath == path)) continue;

                var fi   = new FileInfo(path);
                var item = new QueueItem
                {
                    FilePath = path,
                    FileName = fi.Name,
                    SizeMb   = (fi.Length / 1_048_576.0).ToString("F1") + " MB",
                };
                Queue.Add(item);
                _ = LoadItemExtrasAsync(item);
            }

            RefreshQueueUI();
            _ = UpdateEstimate();
        }

        private async Task LoadItemExtrasAsync(QueueItem item)
        {
            // Shell thumbnail — no FFmpeg needed
            var thumb = await Task.Run(() => GetShellThumbnail(item.FilePath, 56));
            if (thumb != null) item.Thumbnail = thumb;

            // Source resolution — needs ffprobe
            if (!File.Exists(Path.Combine(FfmpegDir, "ffprobe.exe"))) return;
            try
            {
                var info = await FFmpeg.GetMediaInfo(item.FilePath);
                var vid  = info.VideoStreams.FirstOrDefault();
                if (vid != null)
                {
                    item.SourceResolution = $"{vid.Width}×{vid.Height}";
                    Dispatcher.Invoke(UpdateSourceResLabel);
                }
            }
            catch { }
        }

        private void RefreshQueueUI()
        {
            int  count    = Queue.Count;
            bool hasItems = count > 0;

            DropZoneLarge.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
            QueueArea.Visibility     = hasItems ? Visibility.Visible   : Visibility.Collapsed;
            QueueHeader.Text         = $"QUEUE  ({count})";
            ClearQueueBtn.Visibility = hasItems ? Visibility.Visible   : Visibility.Collapsed;
            CompressBtn.IsEnabled    = Queue.Any(q => q.Status == "Pending") && !_isCompressing;

            if (!_isCompressing)
                SetStatus(hasItems ? $"{count} file(s) in queue."
                                   : "Ready — drop videos here or click Browse");

            UpdateSourceResLabel();
        }

        private void UpdateSourceResLabel()
        {
            var first = Queue.FirstOrDefault(q => q.Status == "Pending");
            SourceResLabel.Text = first?.SourceResolution != null
                ? $"Source: {first.SourceResolution}"
                : "";
        }

        private void RemoveFromQueue_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is QueueItem item)
            {
                if (item.IsProcessing) return;
                Queue.Remove(item);
                RefreshQueueUI();
                _ = UpdateEstimate();
            }
        }

        private void ClearQueue_Click(object sender, RoutedEventArgs e)
        {
            if (_isCompressing) return;
            Queue.Clear();
            RefreshQueueUI();
            Dispatcher.Invoke(() =>
            {
                EstimateCard.Visibility      = Visibility.Collapsed;
                EstimateArrowLabel.Visibility = Visibility.Collapsed;
                EstimateOutLabel.Visibility   = Visibility.Collapsed;
                EstimatePctLabel.Visibility   = Visibility.Collapsed;
            });
        }

        // ── Output settings ──────────────────────────────────────────────────
        private void OutputFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description            = "Select output folder",
                UseDescriptionForTitle = true,
                SelectedPath           = _outputFolder
                                         ?? (Queue.Count > 0
                                             ? Path.GetDirectoryName(Queue[0].FilePath) ?? ""
                                             : "")
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _outputFolder          = dlg.SelectedPath;
                OutputFolderLabel.Text = _outputFolder;
            }
        }

        private void ResetOutputFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            _outputFolder          = null;
            OutputFolderLabel.Text = "(same as source)";
        }

        private void OutputSuffixBox_TextChanged(object sender,
            System.Windows.Controls.TextChangedEventArgs e)
        {
            string raw    = OutputSuffixBox.Text;
            _outputSuffix = string.IsNullOrWhiteSpace(raw) ? "_compressed" : raw.Trim();
        }

        // ── CRF description ───────────────────────────────────────────────────
        private static string GetCrfDescription(int crf) => crf switch
        {
            <= 22 => "Visually lossless",
            <= 28 => "Balanced",
            <= 35 => "Small file",
            _     => "Aggressive compression",
        };

        private void UpdateCrfDescription(int crf)
        {
            if (CrfDescLabel == null) return;
            CrfDescLabel.Text = GetCrfDescription(crf);
        }

        // ── Compression ──────────────────────────────────────────────────────
        private void CompressBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isCompressing) { _cts?.Cancel(); return; }
            _ = StartBatchCompression();
        }

        private async Task StartBatchCompression()
        {
            var pending = Queue.Where(q => q.Status == "Pending").ToList();
            if (pending.Count == 0) return;

            _isCompressing          = true;
            CompressBtn.Content     = "■  Cancel";
            BrowseBtn.IsEnabled     = false;
            ClearQueueBtn.IsEnabled = false;

            _cts = new CancellationTokenSource();

            try
            {
                Directory.CreateDirectory(FfmpegDir);
                if (!File.Exists(Path.Combine(FfmpegDir, "ffmpeg.exe")))
                {
                    SetStatus("Downloading FFmpeg (~70 MB)… please wait.");
                    BottomProgress.IsIndeterminate = true;
                    BottomProgress.Visibility      = Visibility.Visible;
                    await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, FfmpegDir);
                    FFmpeg.SetExecutablesPath(FfmpegDir);
                    BottomProgress.IsIndeterminate = false;
                    BottomProgress.Visibility      = Visibility.Collapsed;
                    _ = UpdateEstimate();
                    // Load source resolutions now that ffprobe is available
                    foreach (var q in Queue)
                        if (q.SourceResolution == null)
                            _ = LoadItemExtrasAsync(q);
                }

                if (!_cts.IsCancellationRequested)
                {
                    foreach (var item in pending)
                    {
                        if (_cts.IsCancellationRequested) break;
                        await CompressItem(item, _cts.Token);
                        if (item.Status == "Cancelled") break;
                    }
                }
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

            _isCompressing          = false;
            CompressBtn.Content     = "▶  Compress";
            BrowseBtn.IsEnabled     = true;
            ClearQueueBtn.IsEnabled = true;
            CompressBtn.IsEnabled   = Queue.Any(q => q.Status == "Pending");

            ShowBatchSummary();
        }

        private async Task CompressItem(QueueItem item, CancellationToken token)
        {
            int    crf    = (int)CrfSlider.Value;
            string preset = GetSelectedPreset();
            string res    = GetSelectedResolution();
            string output = GetOutputPath(item.FilePath);

            item.Status   = "Compressing";
            item.Progress = 0;

            BottomProgress.IsIndeterminate = false;
            BottomProgress.Value           = 0;
            BottomProgress.Visibility      = Visibility.Visible;
            SetStatus($"{item.FileName}  —  CRF {crf} · {preset}"
                + (string.IsNullOrEmpty(res) ? "" : $" · {res}p"));

            try
            {
                IMediaInfo info = await FFmpeg.GetMediaInfo(item.FilePath, token);

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
                    .AddParameter($"-crf {crf}",          ParameterPosition.PostInput)
                    .AddParameter($"-preset {preset}",    ParameterPosition.PostInput)
                    .AddParameter("-b:a 128k",            ParameterPosition.PostInput)
                    .AddParameter("-movflags +faststart", ParameterPosition.PostInput);

                if (!string.IsNullOrEmpty(res) && video != null)
                    conversion.AddParameter($"-vf scale=-2:{res}", ParameterPosition.PostInput);

                conversion.OnProgress += (_, args) =>
                    Dispatcher.Invoke(() =>
                    {
                        int pct = Math.Clamp(args.Percent, 0, 100);
                        BottomProgress.Value = pct;
                        item.Progress        = pct;
                    });

                await conversion.Start(token);

                if (File.Exists(output))
                {
                    item.Status     = "Done ✓";
                    item.Progress   = 100;
                    item.OutputPath = output;
                    BottomProgress.Value = 100;
                }
                else
                {
                    item.Status = "Error";
                }
            }
            catch (OperationCanceledException)
            {
                item.Status = "Cancelled";
            }
            catch (Exception ex)
            {
                item.Status = "Error";
                ShowError($"{item.FileName}:\n{ex.Message}");
            }
            finally
            {
                BottomProgress.Visibility = Visibility.Collapsed;
                BottomProgress.Value      = 0;
            }
        }

        private void ShowBatchSummary()
        {
            // Update bottom status bar
            var done    = Queue.Where(q => q.Status == "Done ✓" && q.OutputPath != null).ToList();
            int errors  = Queue.Count(q => q.Status == "Error");
            int pending = Queue.Count(q => q.Status == "Pending");

            string summary = done.Count > 0 ? $"✓ {done.Count} done" : "";
            if (errors  > 0) summary += (summary.Length > 0 ? "  ·  " : "") + $"✕ {errors} failed";
            if (pending > 0) summary += (summary.Length > 0 ? "  ·  " : "") + $"{pending} skipped";
            if (summary.Length > 0) SetStatus(summary);

            // Don't show the window if nothing happened at all
            if (done.Count == 0 && errors == 0) return;

            new BatchSummaryWindow(Queue) { Owner = this }.ShowDialog();
        }

        // ── Estimate ─────────────────────────────────────────────────────────
        private async Task UpdateEstimate()
        {
            _estimateCts?.Cancel();
            _estimateCts = new CancellationTokenSource();
            var token = _estimateCts.Token;

            try
            {
                await Task.Delay(400, token);
                if (token.IsCancellationRequested) return;

                var pending = Queue.Where(q => q.Status == "Pending").ToList();
                if (pending.Count == 0)
                {
                    Dispatcher.Invoke(() => EstimateCard.Visibility = Visibility.Collapsed);
                    return;
                }

                long totalSrcBytes = pending.Sum(item => new FileInfo(item.FilePath).Length);
                long totalEstBytes = 0;

                if (File.Exists(Path.Combine(FfmpegDir, "ffprobe.exe")))
                {
                    int    crf = 0;
                    string res = "";
                    Dispatcher.Invoke(() => { crf = (int)CrfSlider.Value; res = GetSelectedResolution(); });

                    foreach (var item in pending)
                    {
                        if (token.IsCancellationRequested) return;
                        IMediaInfo info;
                        try { info = await FFmpeg.GetMediaInfo(item.FilePath, token); }
                        catch { continue; }

                        var vid = info.VideoStreams.FirstOrDefault();
                        if (vid == null) continue;

                        double srcBps    = vid.Bitrate > 0 ? vid.Bitrate
                                           : new FileInfo(item.FilePath).Length * 8.0
                                             / Math.Max(info.Duration.TotalSeconds, 1);
                        double crfFactor = 0.45 * Math.Pow(2.0, (23.0 - crf) / 6.0);
                        int    targetH   = int.TryParse(res, out int h) ? h : 0;
                        double resFactor = targetH > 0 && targetH < vid.Height
                                           ? Math.Pow((double)targetH / vid.Height, 2) : 1.0;
                        double estBps    = srcBps * crfFactor * resFactor + 128_000;
                        totalEstBytes   += (long)(estBps * info.Duration.TotalSeconds / 8.0);
                    }
                }

                Dispatcher.Invoke(() =>
                {
                    EstimateCard.Visibility  = Visibility.Visible;
                    EstimateSrcLabel.Text    = $"{totalSrcBytes / 1_048_576.0:F1} MB";

                    if (totalEstBytes > 0)
                    {
                        double saved = totalSrcBytes > 0
                            ? (1.0 - (double)totalEstBytes / totalSrcBytes) * 100.0 : 0;
                        EstimateArrowLabel.Visibility = Visibility.Visible;
                        EstimateOutLabel.Visibility   = Visibility.Visible;
                        EstimatePctLabel.Visibility   = Visibility.Visible;
                        EstimateOutLabel.Text         = $"~{totalEstBytes / 1_048_576.0:F1} MB";
                        EstimatePctLabel.Text         = $"(↓ {saved:F0}%)";
                    }
                    else
                    {
                        EstimateArrowLabel.Visibility = Visibility.Collapsed;
                        EstimateOutLabel.Visibility   = Visibility.Collapsed;
                        EstimatePctLabel.Visibility   = Visibility.Collapsed;
                    }
                });
            }
            catch (OperationCanceledException) { }
            catch
            {
                Dispatcher.Invoke(() => EstimateCard.Visibility = Visibility.Collapsed);
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private void CrfSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CrfLabel == null) return;
            int crf = (int)CrfSlider.Value;
            CrfLabel.Text = crf.ToString();
            UpdateCrfDescription(crf);
            _ = UpdateEstimate();
        }

        private void ResolutionCombo_SelectionChanged(object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
            => _ = UpdateEstimate();

        private string GetSelectedPreset()
        {
            if (PresetCombo.SelectedItem is System.Windows.Controls.ComboBoxItem item)
                return item.Tag?.ToString() ?? "medium";
            return "medium";
        }

        private string GetSelectedResolution()
        {
            if (ResolutionCombo.SelectedItem is System.Windows.Controls.ComboBoxItem item)
                return item.Tag?.ToString() ?? "";
            return "";
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

        private string GetOutputPath(string inputFile)
        {
            string dir    = _outputFolder ?? Path.GetDirectoryName(inputFile) ?? ".";
            string name   = Path.GetFileNameWithoutExtension(inputFile);
            string suffix = string.IsNullOrWhiteSpace(_outputSuffix) ? "_compressed" : _outputSuffix;
            return Path.Combine(dir, name + suffix + ".mp4");
        }

        private static bool IsVideoFile(string path)
            => Array.IndexOf(VideoExtensions,
               Path.GetExtension(path).ToLowerInvariant()) >= 0;

        // ── Explorer Integration ──────────────────────────────────────────────
        private void CheckRegistrationStatus()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(
                    @"SystemFileAssociations\.mp4\shell\VideoCompressor");
                bool registered = key != null;
                CtxMenuStatusLabel.Text       = registered
                    ? "✓  Registered — right-click any video in Explorer"
                    : "Add right-click \"Compress video\" to Explorer";
                CtxMenuStatusLabel.Foreground = registered
                    ? FindResource("SuccessBrush")  as Brush
                    : FindResource("TextSecondaryBrush") as Brush;
                RegisterCtxBtn.Visibility     = registered
                    ? Visibility.Collapsed : Visibility.Visible;
            }
            catch { }
        }

        private async void RegisterCtxBtn_Click(object sender, RoutedEventArgs e)
        {
            RegisterCtxBtn.IsEnabled      = false;
            CtxMenuStatusLabel.Text       = "Registering…";
            CtxMenuStatusLabel.Foreground = FindResource("TextSecondaryBrush") as Brush;

            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName
                                 ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VideoCompressorUI.exe");

                string regFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                              "install_context_menu.reg");

                await Task.Run(() =>
                    File.WriteAllText(regFile, BuildRegContent(exePath), Encoding.Unicode));

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

                if (exitCode == 0)
                    CheckRegistrationStatus();
                else
                {
                    CtxMenuStatusLabel.Text       = "Registration failed — try running as administrator.";
                    CtxMenuStatusLabel.Foreground = FindResource("DangerBrush") as Brush;
                    RegisterCtxBtn.IsEnabled      = true;
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                CtxMenuStatusLabel.Text       = "Cancelled — administrator access required.";
                CtxMenuStatusLabel.Foreground = FindResource("DangerBrush") as Brush;
                RegisterCtxBtn.IsEnabled      = true;
            }
            catch (Exception ex)
            {
                ShowError($"Failed to register context menu:\n{ex.Message}");
                RegisterCtxBtn.IsEnabled = true;
            }
        }

        private static string BuildRegContent(string exePath)
        {
            string ep     = exePath.Replace(@"\", @"\\");
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
