using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;

namespace VideoCompressor.Core;

/// <summary>
/// Reads Explorer context-menu registration state and builds .reg file content.
/// Does not launch processes or perform elevated imports — UI layer owns UAC/regedit.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ContextMenuRegistry
{
    private static readonly string ProbeCommandSubKey =
        RegistryPaths.CommandSubKeyPath(RegistryPaths.ProbeExtension);

    public static bool TryGetRegisteredExePath(out string? exePath)
    {
        exePath = null;
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(ProbeCommandSubKey);
            if (key?.GetValue(null) is not string command)
                return false;

            return TryParseCommandExePath(command, out exePath);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsRegisteredForCurrentExe(string currentExePath)
    {
        if (!TryGetRegisteredExePath(out var registered) || string.IsNullOrWhiteSpace(registered))
            return false;

        return PathsEqual(registered, currentExePath);
    }

    public static string BuildCleanupRegContent()
    {
        var sb = new StringBuilder();
        sb.AppendLine(RegistryPaths.RegFileHeader);

        foreach (string ext in RegistryPaths.SupportedExtensions)
        {
            sb.AppendLine();
            sb.AppendLine($"[-{RegistryPaths.ShellRegKeyPath(ext)}]");
        }

        return sb.ToString();
    }

    public static string BuildInstallRegContent(string exePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine(RegistryPaths.RegFileHeader);
        AppendInstallBlocks(sb, exePath);
        return sb.ToString();
    }

    public static string BuildCombinedRegContent(string exePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine(RegistryPaths.RegFileHeader);

        foreach (string ext in RegistryPaths.SupportedExtensions)
        {
            sb.AppendLine();
            sb.AppendLine($"[-{RegistryPaths.ShellRegKeyPath(ext)}]");
        }

        AppendInstallBlocks(sb, exePath);
        return sb.ToString();
    }

    public static bool PathsEqual(string pathA, string pathB)
    {
        return string.Equals(
            NormalizePath(pathA),
            NormalizePath(pathB),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendInstallBlocks(StringBuilder sb, string exePath)
    {
        string ep = exePath.Replace(@"\", @"\\");

        foreach (string ext in RegistryPaths.SupportedExtensions)
        {
            sb.AppendLine();
            sb.AppendLine($@"[HKEY_CLASSES_ROOT\SystemFileAssociations\{ext}\shell\{RegistryPaths.ShellName}]");
            sb.AppendLine(@"@=""Compress this video""");
            sb.AppendLine($"\"Icon\"=\"{ep},0\"");
            sb.AppendLine($@"[HKEY_CLASSES_ROOT\SystemFileAssociations\{ext}\shell\{RegistryPaths.ShellName}\command]");
            sb.AppendLine($"@=\"\\\"{ep}\\\" \\\"%1\\\"\"");
        }
    }

    private static bool TryParseCommandExePath(string command, out string? exePath)
    {
        exePath = null;
        if (string.IsNullOrWhiteSpace(command))
            return false;

        command = command.Trim();
        if (command.Length < 2 || command[0] != '"')
            return false;

        int endQuote = command.IndexOf('"', 1);
        if (endQuote < 1)
            return false;

        exePath = command[1..endQuote];
        return !string.IsNullOrWhiteSpace(exePath);
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path.Trim();
        }
    }
}
