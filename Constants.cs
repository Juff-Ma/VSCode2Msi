namespace VSCode2Msi;

internal static class Constants
{
#pragma warning disable S1075 // URIs should not be hard coded
    public const string DefaultVSCodeArchiveUrl = "https://code.visualstudio.com/sha/download?build=stable&os=win32-x64-archive";
#pragma warning restore S1075 // URIs should not be hard coded

    public static readonly string ArchiveDownloadPath = Path.GetTempPath() + Path.DirectorySeparatorChar + "VSCode2Msi-archive-" + Guid.NewGuid() + ".zip";
    public static readonly string ArchiveExtractPath = Path.GetTempPath() + Path.DirectorySeparatorChar + "VSCode2Msi-extracted";
}
