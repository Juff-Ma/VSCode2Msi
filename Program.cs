using CommandLine;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.IconLib;
using System.IO.Compression;
using System.Net;
using VSCode2Msi;
using WixSharp;
using WixSharp.CommonTasks;

await Parser.Default.ParseArguments<Options>(args)
    .WithNotParsed(errors =>
    {
        if (errors.Any())
        {
            Console.WriteLine("Error: invalid arguments");
            Environment.Exit(-1);
        }
    })
    .WithParsedAsync(Run);

static async Task Run(Options options)
{
    if (!options.NoLogo)
    {
        Console.WriteLine($"VSCode2Msi v{typeof(Program).Assembly.GetName().Version.ToNoRevisionString()}");
        Console.WriteLine($"Copyright (C) {DateTime.Now.Year} VSCode2Msi, licensed under LGPL 2.1");
        Console.WriteLine();
    }

    // check if archive is specified
    if (string.IsNullOrWhiteSpace(options.ArchivePath))
    {
        options.IfVerbose(() => Console.WriteLine($"No archive specified, using \"{Constants.DefaultVSCodeArchiveUrl}\""));
        options.ArchivePath = Constants.DefaultVSCodeArchiveUrl;
    }

    // check if archive is a URL, and if so, download it
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

        // download file
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
    // if archive was extracted before, remove old extracted archive
    if (Directory.Exists(Constants.ArchiveExtractPath))
    {
        Console.WriteLine("Removing old extracted archive...");
        Directory.Delete(Constants.ArchiveExtractPath, true);
    }

    Console.Write("Extracting archive...");
    ZipFile.ExtractToDirectory(options.ArchivePath, Constants.ArchiveExtractPath);
    Console.WriteLine(" Done.");
    options.IfVerbose(() => Console.WriteLine($"Archive extracted to \"{Constants.ArchiveExtractPath}\""));

    // remove unneeded archive if it was not specified
    if (options.ArchivePath == Constants.ArchiveDownloadPath)
    {
        options.IfVerbose(() => Console.WriteLine("Removing unneeded archive..."));
        System.IO.File.Delete(options.ArchivePath);
    }

    // locating vscode executable
    var codeExe = Path.Combine(Constants.ArchiveExtractPath, "Code.exe");
    options.IfVerbose(() => Console.WriteLine("Checking for vscode executable..."));
    if (!System.IO.File.Exists(codeExe))
    {
        Console.WriteLine("Error: vscode executable not found");
        Environment.Exit(-1);
    }
    options.IfVerbose(() => Console.WriteLine($"VSCode executable is at \"{codeExe}\""));

    // extract icon of vscode executable
    options.IfVerbose(() => Console.WriteLine("Extracting icon from vscode executable..."));
    var iconFile = ExtractIconFile(codeExe);
    if (iconFile is null)
    {
        Console.WriteLine("Warning: failed to extract icon from vs code executable");
    }
    options.IfVerbose(() => Console.WriteLine($"Successfully extracted icon to \"{iconFile}\""));

    // get vscode attributes (version, name, etc.)
    options.IfVerbose(() => Console.WriteLine("Extracting attributes from vscode executable..."));
    var attributes = FileVersionInfo.GetVersionInfo(codeExe);

    // computing correct output path as expected by WiX
    if (string.IsNullOrWhiteSpace(options.OutputPath))
    {
        options.OutputPath = Environment.CurrentDirectory + Path.DirectorySeparatorChar + $"VSCode-{attributes.ProductVersion}-x64";
        options.IfVerbose(() => Console.WriteLine($"No output path specified, using \"{options.OutputPath}\""));
    }
    if (!Directory.Exists(Path.GetDirectoryName(options.OutputPath)))
    {
        Console.WriteLine($"Error: directory \"{Path.GetDirectoryName(options.OutputPath)}\" does not exist");
        Environment.Exit(-1);
    }
    if (options.OutputPath!.EndsWith(".msi"))
    {
        options.OutputPath = options.OutputPath[..^4];
    }

    options.IfVerbose(() => Console.WriteLine("Configuring msi..."));
    // base structure
    Project msi = new(attributes.ProductName,
        new Dir(@"%ProgramFiles%\Microsoft VS Code",
                new Files(Constants.ArchiveExtractPath + Path.DirectorySeparatorChar + "*.*")));

    msi.ResolveWildCards();
    // add desktop and app menu shortcuts
    msi.FindFile(f => f.Name.EndsWith("Code.exe"))[0]
        .AddShortcuts(
            new FileShortcut(attributes.ProductName, "ProgramMenuFolder"),
            new FileShortcut(attributes.ProductName, "%Desktop%"));
    msi.MajorUpgradeStrategy = MajorUpgradeStrategy.Default;
    // add vscode binaries to PATH
    msi.Add(new EnvironmentVariable("PATH", @"[INSTALLDIR]\bin") { Part = EnvVarPart.last });

    // basic attributes
    msi.Version = new(attributes.ProductMajorPart, attributes.ProductMinorPart, attributes.ProductBuildPart);
    msi.Platform = Platform.x64;
    msi.OutFileName = options.OutputPath;
    msi.LicenceFile = Constants.ArchiveExtractPath + Path.DirectorySeparatorChar + @"resources\app\LICENSE.rtf";

    // set GUID of installer
    msi.GUID = new Guid("fcd5a47f-9d70-4c32-8c3a-ae65c9b17a64");

    // advanced attributes
    msi.ControlPanelInfo.NoModify = true;
    msi.ControlPanelInfo.Comments = attributes.FileDescription;
    msi.ControlPanelInfo.Readme = "https://code.visualstudio.com/learn";
    msi.ControlPanelInfo.HelpLink = "https://code.visualstudio.com/docs";
    msi.ControlPanelInfo.UrlInfoAbout = "https://code.visualstudio.com";
    msi.ControlPanelInfo.Manufacturer = attributes.CompanyName;
    msi.ControlPanelInfo.InstallLocation = "[INSTALLDIR]";

    // if icon file was extracted use it as product icon
    if (iconFile is not null)
    {
        msi.ControlPanelInfo.ProductIcon = iconFile;
    }

    Console.Write("Building msi...");
    Compiler.BuildMsi(msi);
    Console.WriteLine("Successfully built MSI installer.");
}

static string? ExtractIconFile(string path)
{
    try
    {
        // we try to use IconLib first
        MultiIcon multiIcon = [];
        multiIcon.Load(path);
        multiIcon.Save(Constants.VSCodeIconPath, MultiIconFormat.ICO);
    }
    catch
    {
        Console.WriteLine("Warning: failed to extract icon via IconLib, falling back to System.Drawing");
        // if this fails we try to use System.Drawing, this icon won't be as good but it's better than nothing
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