using CommandLine;

namespace VSCode2Msi;

internal class Options
{
#if DEBUG
    private const bool Debug = true;
#else
    private const bool Debug = false;
#endif

    [Option('a', "archive", HelpText = "Path to VSCode archive, can be url or local path")]
    public string? ArchivePath { get; set; }

    [Option('o', "output", HelpText = "Path to output MSI file")]
    public string? OutputPath { get; set; }

    [Option("nologo", Default = false, HelpText = "Don't display copyright")]
    public bool NoLogo { get; set; }

    [Option('v', "verbose", Default = Debug, HelpText = "Verbose output")]

    public bool Verbose { get; set; }

    public void IfVerbose(Action action)
    {
        if (Verbose)
        {
            action();
        }
    }
}
