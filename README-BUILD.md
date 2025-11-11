# Building PrinterAPP for Windows

This guide explains how to build PrinterAPP as a standalone Windows .exe file.

## Prerequisites

- .NET 9.0 SDK or later
- Windows 10/11 (for building and running)

## Quick Start

### Using PowerShell (Recommended for Windows)

```powershell
.\build-windows.ps1
```

### Using Bash (Linux/macOS/WSL)

```bash
chmod +x build-windows.sh
./build-windows.sh
```

## Build Options

### PowerShell Script Parameters

```powershell
# Build in Debug mode
.\build-windows.ps1 -Configuration Debug

# Build for 32-bit Windows
.\build-windows.ps1 -Runtime win-x86

# Build for ARM64 Windows
.\build-windows.ps1 -Runtime win-arm64

# Build framework-dependent (requires .NET runtime on target machine)
.\build-windows.ps1 -SelfContained:$false
```

## Manual Build

If you prefer to build manually using the command line:

```bash
dotnet publish PrinterAPP/PrinterAPP.csproj \
    -c Release \
    -r win-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=false \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "./publish/win-x64"
```

## Output Location

After building, the executable will be located at:
```
./publish/win-x64/PrinterAPP.exe
```

## Build Configuration Explained

- **Configuration**: `Release` - Optimized build for distribution
- **Runtime**: `win-x64` - Target Windows 64-bit systems
- **Self-Contained**: `true` - Includes .NET runtime (no installation required)
- **PublishSingleFile**: `true` - Creates a single .exe file
- **PublishTrimmed**: `false` - Includes all dependencies (prevents runtime issues)
- **IncludeNativeLibrariesForSelfExtract**: `true` - Embeds native libraries

## Distribution

The generated `.exe` file is self-contained and can be distributed to users without requiring them to install .NET separately. Simply share the `PrinterAPP.exe` file from the publish folder.

### System Requirements for End Users

- Windows 10 version 1809 or later (build 17763+)
- Windows 11 (all versions)
- No additional software installation required

## Troubleshooting

### Build Fails

1. Ensure .NET 9.0 SDK is installed:
   ```bash
   dotnet --version
   ```

2. Restore NuGet packages:
   ```bash
   dotnet restore PrinterAPP/PrinterAPP.csproj
   ```

3. Clean the build:
   ```bash
   dotnet clean PrinterAPP/PrinterAPP.csproj
   ```

### Runtime Issues

If the application fails to start on the target machine:
- Ensure Windows is up to date
- Check that the target system meets the minimum OS version requirements
- Try building without trimming (already disabled in the default configuration)

## Advanced Options

### Creating an Installer

For a professional installer, consider using:
- **WiX Toolset**: Create MSI installers
- **Inno Setup**: Create setup executables
- **MSIX**: Create MSIX packages for Microsoft Store

### Code Signing

To sign your executable (removes Windows SmartScreen warnings):
```powershell
signtool sign /f certificate.pfx /p password /t http://timestamp.digicert.com ./publish/win-x64/PrinterAPP.exe
```
