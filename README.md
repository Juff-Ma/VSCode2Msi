# VSCode2Msi
### What?
VSCode2Msi is a tool for creating a Windows Installer (MSI) package from official microsoft VSCode releases.
### Why?
Microsoft doesn't publish MSIs for VSCode but some people prefer or require MSI installers (e.g. GPO deployment).
### How?
VSCode2Msi repackages a zip release of VSCode using WiX.

## Usage
### Requirements
- A recent WiX version (version 6 recommended, version 5 should work too)
- If you build from source, you need the .NET SDK
### Installation
- Install the WiX command line: https://github.com/wixtoolset/wix/releases/latest (you can also use the [dotnet tool](https://wixtoolset.org/docs/intro/))
- Download VSCode2Msi from the [releases page](https://github.com/Juff-Ma/VSCode2Msi/releases/latest) or build it from source
### Building the MSI
Just run `VSCode2Msi.exe`, this should download VSCode and build the msi in the current directory, for advanced options run `VSCode2Msi.exe --help`

### Limitations
- Arm64 is currently untested and the current configuration is propably incompatible, though it could work with slight modifications. (e.g. change platform attribute and use a different zip file)
- Automatic updates don't work, you can however create a MSI from a newer VSCode release and it will update your existing installation.
