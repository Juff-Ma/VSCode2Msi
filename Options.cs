using CommandLine;

namespace VSCode2Msi;

internal class Options
{
    [Option('a', "archive", HelpText = "Path to VSCode archive, can be url or local path")]
    public string? ArchivePath { get; set; }

    [Option('o', "output", HelpText = "Path to output MSI file")]
    public string? OutputPath { get; set; }

    [Option("nologo", Default = false, HelpText = "Don't display copyright")]
    public bool NoLogo { get; set; }
#if DEBUG
    [Option('v', "verbose", Default = true, HelpText = "Verbose output")]
#else
    [Option('v', "verbose", Default = false, HelpText = "Verbose output")]
#endif
    public bool Verbose { get; set; }

    public void IfVerbose(Action action)
    {
        if (Verbose)
        {
            action();
        }
    }
}
