namespace VSCode2Msi;

internal static class Constants
{
    public const string DefaultVsCodeArchiveUrl = "https://code.visualstudio.com/sha/download?build=stable&os=win32-x64-archive";

    public static readonly string ArchiveDownloadPath = Path.GetTempPath() + Path.DirectorySeparatorChar + "VSCode2Msi-archive-" + Guid.NewGuid() + ".zip";
    public static readonly string ArchiveExtractPath = Path.GetTempPath() + Path.DirectorySeparatorChar + "VSCode2Msi-extracted";
    public static readonly string VsCodeIconPath = Path.GetTempPath() + Path.DirectorySeparatorChar + "VSCode2Msi-vscode.ico";
}
