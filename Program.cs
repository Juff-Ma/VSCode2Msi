using CommandLine;
using System.Net;
using VSCode2Msi;
using WixSharp;

Console.WriteLine($"VSCode2Msi v{typeof(Program).Assembly.GetName().Version.ToNoRevisionString()}.");
Console.WriteLine($"Copyright (C) Juff-Ma {DateTime.Now.Year}, licensed under LGPL 2.1");
Console.WriteLine();

await Parser.Default.ParseArguments<Options>(args)
    .WithNotParsed(_ =>
    {
        Console.WriteLine("Error: failed to parse command line arguments");

        Environment.Exit(-1);
    })
    .WithParsedAsync(Run);

static async Task Run(Options options)
{
    if (string.IsNullOrWhiteSpace(options.ArchivePath))
    {
        options.IfVerbose(() => Console.WriteLine($"No archive specified, using \"{Constants.DefaultVSCodeArchiveUrl}\""));
        options.ArchivePath = Constants.DefaultVSCodeArchiveUrl;
    }

    if (Uri.TryCreate(options.ArchivePath, UriKind.Absolute, out var uri))
    {
        options.IfVerbose(() => Console.Write($"Downloading archive from \"{options.ArchivePath}\" "));
        var filename = Path.GetTempPath() + Path.DirectorySeparatorChar + "VSCode2Msi-archive-" + Guid.NewGuid() + ".zip";
        options.IfVerbose(() => Console.WriteLine($"to \"{filename}\""));

        using WebClient webClient = new();

        object locker = new();
        webClient.DownloadProgressChanged += (_, e) =>
        {
            // this is stupid but it works
            lock (locker)
            {
                Console.Write($"\rDownloading... {e.ProgressPercentage}%");
                options.IfVerbose(() => Console.Write($" ({e.BytesReceived}/{e.TotalBytesToReceive})"));
            }
        };
        webClient.DownloadFileCompleted += (_, e) => Console.WriteLine(" Done.");

        await webClient.DownloadFileTaskAsync(uri, filename);
    }
}