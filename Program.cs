using CommandLine;
using System.IO.Compression;
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

    options.IfVerbose(() => Console.WriteLine("Checking if archive should be downloaded..."));
    if (Uri.TryCreate(options.ArchivePath, UriKind.Absolute, out var uri))
    {
        options.IfVerbose(() => Console.Write($"Downloading archive from \"{options.ArchivePath}\" "));
        var filename = Constants.ArchiveDownloadPath;
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

        options.ArchivePath = filename;
    }

    options.IfVerbose(() => Console.WriteLine("Checking if archive exists..."));
    if (!System.IO.File.Exists(options.ArchivePath))
    {
        Console.WriteLine($"Error: archive not found at \"{options.ArchivePath}\"");
        Environment.Exit(-1);
    }

    options.IfVerbose(() => Console.WriteLine("Checking for existing extracted archive..."));
    if (Directory.Exists(Constants.ArchiveExtractPath))
    {
        Console.WriteLine("Removing old extracted archive...");
        Directory.Delete(Constants.ArchiveExtractPath, true);
    }

    Console.Write("Extracting archive...");
    ZipFile.ExtractToDirectory(options.ArchivePath, Constants.ArchiveExtractPath);
    Console.WriteLine(" Done.");
    options.IfVerbose(() => Console.WriteLine($"Archive extracted to \"{Constants.ArchiveExtractPath}\""));

    options.IfVerbose(() => Console.WriteLine("Removing unneeded archive..."));
    System.IO.File.Delete(options.ArchivePath);
}