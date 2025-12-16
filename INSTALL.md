# THREADLE INSTALLATION INSTRUCTIONS

Threadle is a cross-platform software system for handling large-scale complex network data.  
See the [README](README.md) for more information or visit:  
https://threadle.dev

Threadle is developed under the MIT License. Please read the  
[LICENSE.md](LICENSE.md) file before using and/or compiling the source code.

This document explains how to compile the Threadle client from source code  
available on the GitHub repository:  
https://github.com/carlnordlund/threadle

Threadle consists of two separate projects:

- **Threadle.Core** — Contains all data models, methods, and interfaces representing base functionality.  
- **Threadle.CLIconsole** — A CLI-based console/terminal frontend to Threadle.Core.

Although Threadle.Core can be used independently (e.g., for integration with other applications), most users will want the CLIconsole frontend. These instructions describe how to build Threadle.CLIconsole (including Threadle.Core) as a single-file executable binary.

## 1. DEPENDENCIES
To build Threadle from source, you need:
- **.NET 8.0 SDK or later**  
  Download from: https://dotnet.microsoft.com/download

Optional (Windows only):
- **Visual Studio 2022** (Community Edition or higher), which includes the .NET SDK.

## 2. GETTING THE SOURCE CODE
Clone the repository:
```bash
git clone https://github.com/carlnordlund/threadle.git
```
Navigate into the project folder:
```bash
cd threadle
```
Optional (Visual Studio users):
- You can also clone the repository directly via Visual Studio using the GitHub URL above.

## 3. BUILDING FROM SOURCE (CROSS-PLATFORM)
Threadle can be built using the .NET CLI, which works the same on Windows, Linux, and macOS.
### Build in Release mode:
```bash
dotnet build -c Release
```
Binaries will be in:
```bash
bin/Release/net8.0/
```
### Build in Debug mode:
```bash
dotnet build -c Debug
```
Binaries will be in:
```bash
bin/Debug/net8.0/
```
## 4. RUNNING THE APPLICATION
You can run Threadle directly using the .NET CLI:
```bash
dotnet run --project Threadle.CLIconsole
```
Or run the compiled binary (Release mode):
- **Windows:**
```bash
/[path to executable binary]/Threadle.CLIconsole.exe
```
- **Linux / macOS:**
```bash
/[path to executable binary]/.Threadle.CLIconsole
```
## 5. PUBLISHING SELF-CONTAINED BINARIES
To generate stand-alone executables for different platforms, use `dotnet publish:`
### Windows (x64)
```bash
dotnet publish Threadle.CLIconsole -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
### Linux (x64)
```bash
dotnet publish Threadle.CLIconsole -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
```

### macOS (x64)
```bash
dotnet publish Threadle.CLIconsole -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true
```
Output will be located in:
```bash
bin/Release/net8.0/<RID>/publish/
```
...where <RID> is the runtime identifier (e.g., `win-x64`, `linux-x64`, `osx-x64`).

These published files can be distributed and run on their respective platforms *without requiring the .NET runtime*.
(These are the files available as pre-compiled and code-signed binaries on [https://threadle.dev/](https://threadle.dev))
### Supported Runtime Identifiers (RIDs)
#### Windows
- `win-x86` - 32-bit Intel/AMD
- `win-x64` - 64-bit Intel/AMD
- `win-arm64` 64-bit ARM
#### Linux
- `linux-x64` - 64-bit Intel/AMD
- `linux-arm` - 32-bit ARM (e.g. Raspberry Pi OS 32-bit)
- `linux-arm64` - 64-bit ARM (e.g. Raspberry Pi 4/5, ARM servers)
#### macOS
- `osx-x64` - 64-bit Intel
- `osx-arm64` - Apple Silicon (M1, M2, M3 ...)

## 6. PUBLISHING WITH VISUAL STUDIO (Windows only)
Windows users may alternatively open the solution file:
```bash
Threadle.sln
```
From Visual Studio 2022, you can build, run, and publish directly from the IDE.
Ensure that:
- **Threadle.CLIconsole** is set as the Startup Project in the Solution explorer.
- **Threadle.Core** is listed under `Threadle.CLIconsole -> Dependencies -> Projects`.

## 7. NOTES
- Threadle is fully cross-platform: it compiles and runs on Windows, Linux, and macOS.
- Ensure that the .NET SDK is correctly installed and available in your system PATH before building or running.









