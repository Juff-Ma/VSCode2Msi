using CommandLine;
using System.Diagnostics;
using System.Drawing;
using System.IO.Compression;
using VSCode2Msi;
using WixSharp;
using WixSharp.CommonTasks;
using File = System.IO.File;

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
        options.IfVerbose(() => Console.WriteLine($"No archive specified, using \"{Constants.DefaultVsCodeArchiveUrl}\""));
        options.ArchivePath = Constants.DefaultVsCodeArchiveUrl;
    }

    // check if archive is a URL, and if so, download it
    options.IfVerbose(() => Console.WriteLine("Checking if archive should be downloaded..."));
    if (Uri.TryCreate(options.ArchivePath, UriKind.Absolute, out var uri))
    {
        var filename = Constants.ArchiveDownloadPath;
        options.IfVerbose(() => Console.WriteLine($"Downloading archive from \"{options.ArchivePath}\" to \"{filename}\"... "));

        using HttpClient client = new();
        await using var file = File.OpenWrite(filename);

        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);

        if (response.Content.Headers.ContentLength is {} contentLength)
        {
            await using var stream = await response.Content.ReadAsStreamAsync();


            var buffer = new byte[1024 * 8];
            long totalRead = 0;
            int read;

            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await file.WriteAsync(buffer, 0, read);
                totalRead += read;
                var progress = (int)((totalRead * 100) / contentLength);
                Console.Write($"\rDownloading... {progress}% ");
                options.IfVerbose(() => Console.Write($"({totalRead}/{contentLength}) "));
            }
        }
        else
        {
            await response.Content.CopyToAsync(file);
        }

        Console.WriteLine("Done downloading.");

        options.ArchivePath = filename;
    }

    options.IfVerbose(() => Console.WriteLine("Checking if archive exists..."));
    if (!File.Exists(options.ArchivePath))
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
        File.Delete(options.ArchivePath);
    }

    // locating vscode executable
    var codeExe = Path.Combine(Constants.ArchiveExtractPath, "Code.exe");
    options.IfVerbose(() => Console.WriteLine("Checking for vscode executable..."));
    if (!File.Exists(codeExe))
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

    // Fix WixUI dependency
    msi.UI = WUI.WixUI_ProgressOnly;

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
    var icon = Icon.ExtractIcon(path, 0);

    if (icon is null)
    {
        Console.WriteLine("Warning: high quality icon extraction failed, falling pack to low quality");
        // use lower quality icon extraction if ExtractIcon fails
        icon = Icon.ExtractAssociatedIcon(path);

        // if this also fails we just skip the icon extraction
        if (icon is null)
            return null;
    }
    using var stream = System.IO.File.OpenWrite(Constants.VsCodeIconPath);
    icon.Save(stream);
    icon.Dispose();

    return Constants.VsCodeIconPath;
}