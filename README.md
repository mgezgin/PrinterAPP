# Rumi Restaurant Printer App

A specialized Windows application built with **.NET MAUI** that handles automated receipt printing for the Rumi Restaurant system. It connects to the backend API, polls for new orders, and routes print jobs to specific thermal printers (Cashier, Front Kitchen, Back Kitchen) with custom formatting.

![Printer App UI](https://raw.githubusercontent.com/mgezgin/PrinterAPP/master/docs/screenshot.png) *(Placeholder if you have one)*

## 🚀 Key Features

- **Auto-Printing:** Automatically polls for "Pending" orders and prints them immediately.
- **Intelligent Routing:**
  - **Cashier Printer:** Prints full bill with prices and payment info.
  - **Kitchen Printers:** Prints simple order tickets.
  - **Fallback Logic:** If "Front Kitchen" printer is missing, routes traffic to the Cashier printer automatically.
- **Smart Formatting:**
  - **Kitchen Tickets:** Large, bold text for easy reading in the kitchen.
  - **Ingredient Customizations:** Clearly marks added/removed ingredients (e.g., `✘ NO Onion`, `+ EXTRA Cheese`).
  - **Turish Character Support:** Correctly handles Turkish characters (e.g., `İ`, `Ş`, `Ğ`) on thermal printers.
- **Auto-Update System:** 
  - Checks GitHub Releases for updates on startup and via manual check.
  - Automatically downloads and installs new versions.
- **Background Operation:** Runs reliably on Windows to ensure no orders are missed.

## 📥 Installation

1. Go to the [Releases Page](https://github.com/mgezgin/PrinterAPP/releases).
2. Download the latest `PrinterApp-Setup-x64.exe` (or `x86` for older systems).
3. Run the installer.
4. Launch "PrinterAPP" from the Start Menu.

## ⚙️ Configuration

On first launch, you must configure the application:

1. **API URL:** `https://rumi-restaurant.fly.dev` (or your local backend URL).
2. **Printers:** Select the Windows printer queues for:
   - **Cashier Printer** (Main receipt printer)
   - **Kitchen Printer** (Back of house)
   - **Front Kitchen Printer** (optional - Bar/Front prep area)
3. **Auto-Print:** Enable the toggle to start polling for orders.

## 🛠️ Development

### Prerequisites
- **Visual Studio 2022** (17.8+)
- **.NET 9 SDK**
- **MAUI Workload** (`dotnet workload install maui`)

### Building Locally

```powershell
# Restore dependencies
dotnet restore

# Run in Debug mode
dotnet build -c Debug -t:Run -f net9.0-windows10.0.19041.0
```

### Publishing

To build the standalone `.exe`:

```powershell
dotnet publish PrinterAPP/PrinterAPP.csproj -c Release -f net9.0-windows10.0.19041.0 -r win-x64 --self-contained -p:PublishSingleFile=true
```

## 📦 Release Workflow

We use automated GitHub Actions for releases.

👉 **See [RELEASE_WORKFLOW.md](RELEASE_WORKFLOW.md) for detailed release instructions.**

### Quick Summary
1. Update `<ApplicationDisplayVersion>` and `<Version>` in `PrinterAPP.csproj`.
2. Commit changes.
3. Tag the commit (e.g., `v1.0.8`).
4. Push tag to GitHub.
5. GitHub Actions will auto-build and publish the release.

## 📄 License

Proprietary software for Rumi Restaurant.
