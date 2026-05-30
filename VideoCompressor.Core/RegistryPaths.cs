namespace VideoCompressor.Core;

/// <summary>
/// Registry key paths for Explorer context-menu integration.
/// Only <see cref="ShellName"/> is supported; no legacy shell names or alternate hives.
/// </summary>
public static class RegistryPaths
{
    public const string ShellName = "VideoCompressor";
    public const string RegFileHeader = "Windows Registry Editor Version 5.00";
    public const string ProbeExtension = ".mp4";

    public static readonly string[] SupportedExtensions =
    {
        ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".flv", ".webm", ".m4v"
    };

    /// <summary>Relative path under <c>HKCR</c> for the shell key (deleting this removes command too).</summary>
    public static string ShellSubKeyPath(string extension)
        => $@"SystemFileAssociations\{extension}\shell\{ShellName}";

    /// <summary>Relative path under <c>HKCR</c> for the command subkey.</summary>
    public static string CommandSubKeyPath(string extension)
        => $@"SystemFileAssociations\{extension}\shell\{ShellName}\command";

    /// <summary>Full .reg path for delete/import of the shell key.</summary>
    public static string ShellRegKeyPath(string extension)
        => $@"HKEY_CLASSES_ROOT\SystemFileAssociations\{extension}\shell\{ShellName}";
}
