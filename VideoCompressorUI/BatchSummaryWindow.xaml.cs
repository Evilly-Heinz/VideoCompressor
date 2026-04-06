using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Brush            = System.Windows.Media.Brush;
using Color            = System.Windows.Media.Color;
using Freezable        = System.Windows.Freezable;
using SolidColorBrush  = System.Windows.Media.SolidColorBrush;

namespace VideoCompressorUI
{
    // ── Per-file row data ─────────────────────────────────────────────────────
    public sealed class SummaryRow
    {
        // Frozen status brushes (shared across instances)
        private static readonly SolidColorBrush _fgSuccess  = Freeze(new SolidColorBrush(Color.FromRgb  (0x6C, 0xCB, 0x5F)));
        private static readonly SolidColorBrush _fgDanger   = Freeze(new SolidColorBrush(Color.FromRgb  (0xFF, 0x45, 0x3A)));
        private static readonly SolidColorBrush _fgWarn     = Freeze(new SolidColorBrush(Color.FromRgb  (0xFF, 0xD6, 0x0A)));
        private static readonly SolidColorBrush _fgTertiary = Freeze(new SolidColorBrush(Color.FromArgb (0x8C, 0xFF, 0xFF, 0xFF)));
        private static readonly SolidColorBrush _bgSuccess  = Freeze(new SolidColorBrush(Color.FromArgb (0x28, 0x6C, 0xCB, 0x5F)));
        private static readonly SolidColorBrush _bgDanger   = Freeze(new SolidColorBrush(Color.FromArgb (0x28, 0xFF, 0x45, 0x3A)));
        private static readonly SolidColorBrush _bgWarn     = Freeze(new SolidColorBrush(Color.FromArgb (0x28, 0xFF, 0xD6, 0x0A)));
        private static readonly SolidColorBrush _bgPending  = Freeze(new SolidColorBrush(Color.FromArgb (0x18, 0xFF, 0xFF, 0xFF)));

        private static T Freeze<T>(T o) where T : Freezable { o.Freeze(); return o; }

        public string FileName   { get; init; } = "";
        public string InputSize  { get; init; } = "";
        public string OutputSize { get; init; } = "—";
        public string Savings    { get; init; } = "—";
        public string Status     { get; init; } = "";

        public Brush StatusFg => Status switch
        {
            "Done ✓"    => _fgSuccess,
            "Error"     => _fgDanger,
            "Cancelled" => _fgWarn,
            _           => _fgTertiary,
        };

        public Brush StatusBg => Status switch
        {
            "Done ✓"    => _bgSuccess,
            "Error"     => _bgDanger,
            "Cancelled" => _bgWarn,
            _           => _bgPending,
        };

        // Green when savings exist, tertiary for "—"
        public Brush SavingsForeground => Savings == "—"
            ? _fgTertiary
            : _fgSuccess;
    }

    // ── Window ────────────────────────────────────────────────────────────────
    public partial class BatchSummaryWindow : Window
    {
        private readonly string? _lastOutputFolder;

        public BatchSummaryWindow(IReadOnlyList<QueueItem> items)
        {
            InitializeComponent();

            var done      = items.Where(i => i.Status == "Done ✓" && i.OutputPath != null).ToList();
            var failed    = items.Where(i => i.Status == "Error").ToList();
            var cancelled = items.Where(i => i.Status == "Cancelled").ToList();
            var pending   = items.Where(i => i.Status == "Pending").ToList();

            // ── Header icon & title ──────────────────────────────────────────
            bool allGood = done.Count > 0 && failed.Count == 0 && cancelled.Count == 0;
            bool mixed   = done.Count > 0 && (failed.Count > 0 || cancelled.Count > 0);
            bool allBad  = done.Count == 0;

            if (allGood)
            {
                HeaderIcon.Text       = "✓";
                HeaderIcon.Foreground = (Brush)FindResource("SuccessBrush");
                SummaryTitle.Text     = done.Count == 1
                    ? "1 file compressed"
                    : $"{done.Count} files compressed";
            }
            else if (mixed)
            {
                HeaderIcon.Text       = "⚠";
                HeaderIcon.Foreground = (Brush)FindResource("WarnAmberBrush");
                SummaryTitle.Text     = $"{done.Count} done";
            }
            else
            {
                HeaderIcon.Text       = "✕";
                HeaderIcon.Foreground = (Brush)FindResource("DangerBrush");
                SummaryTitle.Text     = cancelled.Count > 0 && failed.Count == 0
                    ? "Cancelled"
                    : "Compression failed";
            }

            // ── Subtitle (secondary info + size line) ────────────────────────
            var notes = new List<string>();
            if (failed.Count    > 0) notes.Add($"{failed.Count} failed");
            if (cancelled.Count > 0) notes.Add($"{cancelled.Count} cancelled");
            if (pending.Count   > 0) notes.Add($"{pending.Count} skipped");

            if (done.Count > 0)
            {
                long totalIn  = done.Sum(i => new FileInfo(i.FilePath).Length);
                long totalOut = done.Sum(i => new FileInfo(i.OutputPath!).Length);

                string sizeNote = $"{FormatBytes(totalIn)}  →  {FormatBytes(totalOut)}";
                if (notes.Count > 0)
                    sizeNote = string.Join("  ·  ", notes) + "  ·  " + sizeNote;
                SummarySubtitle.Text       = sizeNote;
                SummarySubtitle.Visibility = Visibility.Visible;
            }
            else if (notes.Count > 0)
            {
                SummarySubtitle.Text       = string.Join("  ·  ", notes);
                SummarySubtitle.Visibility = Visibility.Visible;
            }

            // ── Stat cards ───────────────────────────────────────────────────
            StatDoneValue.Text = done.Count.ToString();

            if (done.Count > 0)
            {
                long totalIn  = done.Sum(i => new FileInfo(i.FilePath).Length);
                long totalOut = done.Sum(i => new FileInfo(i.OutputPath!).Length);
                long saved    = Math.Max(0L, totalIn - totalOut);
                double pct    = totalIn > 0 ? (double)saved / totalIn * 100.0 : 0;

                StatSavedValue.Text = FormatBytes(saved);
                StatPctValue.Text   = $"↓ {pct:F0}%";
            }
            else
            {
                // Dim cards when nothing was compressed
                StatSavedValue.Text             = "—";
                StatPctValue.Text               = "—";
                StatSavedCard.Opacity           = 0.4;
                StatPctCard.Opacity             = 0.4;
                StatDoneValue.Foreground        = (Brush)FindResource("DangerBrush");
            }

            // ── File rows ────────────────────────────────────────────────────
            var rows = items.Select(item =>
            {
                long inputBytes = new FileInfo(item.FilePath).Length;

                bool hasOutput = item.OutputPath != null && File.Exists(item.OutputPath);
                long outBytes  = hasOutput ? new FileInfo(item.OutputPath!).Length : -1;
                double pct     = outBytes >= 0 && inputBytes > 0
                    ? (1.0 - (double)outBytes / inputBytes) * 100.0 : -1;

                return new SummaryRow
                {
                    FileName   = item.FileName,
                    InputSize  = FormatBytes(inputBytes),
                    OutputSize = hasOutput ? FormatBytes(outBytes) : "—",
                    Savings    = pct >= 0 ? $"↓ {pct:F0}%" : "—",
                    Status     = item.Status,
                };
            }).ToList();

            FileList.ItemsSource = rows;

            // ── Open folder ──────────────────────────────────────────────────
            string? lastOut = done.LastOrDefault()?.OutputPath;
            _lastOutputFolder           = lastOut != null ? Path.GetDirectoryName(lastOut) : null;
            OpenFolderBtn.IsEnabled     = _lastOutputFolder != null;
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1_073_741_824L) return $"{bytes / 1_073_741_824.0:F1} GB";
            if (bytes >= 1_048_576L)     return $"{bytes / 1_048_576.0:F0} MB";
            return $"{bytes / 1024.0:F0} KB";
        }

        // ── Event handlers ────────────────────────────────────────────────────
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

        private void OpenFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_lastOutputFolder != null)
                Process.Start("explorer.exe", $"\"{_lastOutputFolder}\"");
        }
    }
}
