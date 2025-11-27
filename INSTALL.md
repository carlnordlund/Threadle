# THREADLE INSTALLATION INSTRUCTIONS
Threadle is a cross-platform software system for handling large-scale complex network data.
See README.txt for more information or visit:
https://threadle.dev

Threadle is developed under the so-called MIT License. Please read [LICENSE.md](LICENSE.md)
before use and/or compiling the source code.

This document explains how to compile the Threadle client from source code
that is available on its github repository:
https://github.com/carlnordlund/threadle

Threadle is structured as two separate projects:
* Threadle.Core: This contains all data models, methods, and interfaces that represent base functionality.
* Threadle.CLIconsole: This is a CLI-based console/terminal application that acts as a frontend to Threadle.Core

Whereas Threadle.Core can be used separately from Threadle CLIconsole, e.g. for integration with other applications
and frontends, it is most likely that most users want the CLIconsole frontend as well. The following instructions
are for compiling Threadle.CLIconsole, with Threadle.Core included, as a single-file executable binary.


### 1. DEPENDENCIES
To build Threadle from source, you need:
- .NET 8.0 SDK or later
  Download this from: https://dotnet.microsoft.com/download

Optional (Windows only):
- Visual Studio 2022 (Community Edition or higher), which
  includes the .NET SDK.


### 2. GETTING THE SOURCE CODE
Clone this repository from GitHub:
  `git clone https://github.com/carlnordlund/threadle.git`

Navigate to the folder where your copy of the source code is:
  `cd threadle`

Optional (Visual Studio only):
- It is also possible to clone the repository from Visual Studio,
  i.e. using the github.com URL provided above.


### 3. BUILDING FROM SOURCE (CROSS-PLATFORM)
Threadle project can be built using the .NET CLI, which works
the same on Windows, Linux and MacOS.

To build in Release mode:
  `dotnet build -c Release`

Binaries will be in:
  `bin/Release/net8.0/`

To build in Debug mode:
  `dotnet build`

This will produce binaries in:
  `bin/Debug/net8.0/`

### 4. RUNNING THE APPLICATION
You can run Socnet.se directly from the .NET CLI:
  `dotnet run --project Threadle.CLIconsole`

Or by executing the compiled binary (Release mode):

- Windows:
  `Threadle.CLIconsole.exe`

- Linux / MacOS:
  `./Threadle.CLIconsole`

### 5. PUBLISHING SELF-CONTAINED BINARIES
To compile the source code into stand-alone executables for
different platforms, use the 'dotnet publish' command in .NET CLI:

- Windows (x64, i.e. 64-bit):
`dotnet publish Threadle.CLIconsole -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`

- Linux (x64):
    `dotnet publish Threadle.CLIconsole -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true`

- MacOS (x64):
    `dotnet publish Threadle.CLIconsole -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true`

The output will be placed in:
  `bin/Release/net8.0/<RID>/publish/`

...where `<RID>` is the runtime identifier (e.g. win-x64, linux-x64, osx-x64).

These published files can be distributed and run on the respective platforms without
requiring the .NET runtime. (These are the files that are available as pre-compiled
and code-signed binaries on https://threadle.dev website).

Supported Runtime Identifiers (RIDs) are as follows:

Windows:
  win-x86     32-bit Intel/AMD
  win-x64     64-bit Intel/AMD
  win-arm     32-bit ARM (e.g. Surface RT, somewhat rare)
  win-arm64   64-bit ARM (e.g. Surface pro X, some modern tablets/laptops)

MacOS:
  osx-x64     64-bit Intel (older Macs)
  osx-arm64   64-bit ARM (Apple Silicon: M1, M2, M3...)

Linux:
  linux-x64   64-bit Intel/AMD
  linux-arm   32-bit ARM (e.g. Raspberry Pi OS 32-bit, embedded systems)
  linux-arm64 64-bit ARM (Raspberry Pi 4/5 with 64-bit OS, ARM servers etc)

### 6. PUBLISH WITH VISUAL STUDIO (Windows only)
Windows users can alternatively open the solution file in Visual Studio 2022:
  `Threadle.sln`

This can then be built, run and published directly from the Visual Studio IDE.
Make sure that Threadle.CLIconsole then is the selected Startup project. Also
make sure that Threadle.Core is in Threadle.CLIconsole/Dependencies/Projects/.

### 7. NOTES
- Threadle is cross-platform: it compiles and runs on Windows, Linux and MacOS.
- 
- Ensure that the .NET SDK is correctly installed and available in your system PATH
before attempting to build or run.
