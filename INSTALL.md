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

---

## 1. DEPENDENCIES

To build Threadle from source, you need:

- **.NET 8.0 SDK or later**  
  Download from: https://dotnet.microsoft.com/download

Optional (Windows only):

- **Visual Studio 2022** (Community Edition or higher), which includes the .NET SDK.

---

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













