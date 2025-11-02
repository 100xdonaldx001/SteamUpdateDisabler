# SteamManifestToggler.WPF (.NET 8, Windows)

A ready-to-run **WPF** GUI that:
- Prompts for your **Steam root** (must contain `config` and a `libraryfolders.vdf` under `steamapps/` or `config/`).
- Parses `libraryfolders.vdf` to find all library paths.
- Scans each library's `steamapps/appmanifest_*.acf` and extracts **AppID** and **Game name**.
- Shows a grid with **Name / AppID / Manifest Path / ReadOnly** and a search box.
- Lets you **double‑click** to toggle the manifest file's **ReadOnly ⇄ Read/Write**.
- Provides buttons: **Select Root…**, **Refresh**, **Set ReadOnly**, **Set Read/Write**, **Open Folder**.
- Creates a `*.acf.bak` once before the first change to a manifest.

> ⚠️ **Note:** Modifying Steam files can cause the client to re‑verify or repair. Use at your own risk.

## Build & Run
1. Install **.NET 8 SDK** on Windows.
2. Unzip this folder and open a terminal here.
3. Run:
   ```bash
   dotnet build
   dotnet run --project SteamManifestToggler.WPF.csproj
   ```
   Or open `SteamManifestToggler.WPF.csproj` in **Visual Studio 2022+** or **Rider** and press **Run**.

## Tech details
- Project: `net8.0-windows`, `<UseWPF>true</UseWPF>`, `<UseWindowsForms>true</UseWindowsForms>` (for FolderBrowserDialog).
- Regex-based VDF parsing supports both classic and newer `libraryfolders.vdf` shapes.
