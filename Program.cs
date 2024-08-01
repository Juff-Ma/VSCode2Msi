using CommandLine;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.IconLib;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using VSCode2Msi;
using WixSharp;

Console.WriteLine($"VSCode2Msi v{typeof(Program).Assembly.GetName().Version.ToNoRevisionString()}");
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

        // this is stupid but it works
        object locker = new();
        webClient.DownloadProgressChanged += (_, e) =>
        {
            lock (locker)
            {
                Console.Write($"\rDownloading... {e.ProgressPercentage}%");
                options.IfVerbose(() => Console.Write($" ({e.BytesReceived}/{e.TotalBytesToReceive})"));
            }
        };
        webClient.DownloadFileCompleted += (_, e) => { lock (locker) { Console.WriteLine(" Done."); } };

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

    var codeExe = Path.Combine(Constants.ArchiveExtractPath, "Code.exe");
    options.IfVerbose(() => Console.WriteLine("Checking for vscode executable..."));
    if (!System.IO.File.Exists(codeExe))
    {
        Console.WriteLine("Error: vscode executable not found");
        Environment.Exit(-1);
    }
    options.IfVerbose(() => Console.WriteLine($"VSCode executable is at \"{codeExe}\""));

    options.IfVerbose(() => Console.WriteLine("Extracting icon from vscode executable..."));
    var iconFile = ExtractIconFile(codeExe);
    if (iconFile is null)
    {
        Console.WriteLine("Warning: failed to extract icon from vs code executable");
    }
    options.IfVerbose(() => Console.WriteLine($"Successfully extracted icon to \"{iconFile}\""));

    options.IfVerbose(() => Console.WriteLine("Extracting attributes from vscode executable..."));
    var attributes = FileVersionInfo.GetVersionInfo(codeExe);
}

static string? ExtractIconFile(string path)
{
    try
    {
        MultiIcon multiIcon = [];
        multiIcon.Load(path);
        multiIcon.Save(Constants.VSCodeIconPath, MultiIconFormat.ICO);
    }
    catch
    {
        Console.WriteLine("Warning: failed to extract icon via IconLib, falling back to System.Drawing");
        using var icon = Icon.ExtractAssociatedIcon(path);

        if (icon is null)
        {
            return null;
        }
        using var stream = System.IO.File.OpenWrite(Constants.VSCodeIconPath);
        icon.Save(stream);
    }
    return Constants.VSCodeIconPath;
}