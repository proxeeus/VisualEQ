# Contributor launcher scripts

These `.bat` files are dev-workflow helpers, kept in `dev/` so they don't
compete for attention with the packaged `VisualEQ.exe` that end users get from
a Release zip.

**End users don't need these.** Grab the latest [release
zip](https://github.com/proxeeus/VisualEQ/releases), unzip, run `VisualEQ.exe`.

## Why they exist

VisualEQ links to `cimgui.dll`, which is **x64 native** (bundled by the
`ImGui.NET` NuGet package). On a Windows-on-ARM host (Apple Silicon Mac running
Parallels + Windows 11 ARM64) the default `dotnet` is ARM64 and will crash
trying to load an x64 DLL.

The bat files hardcode
`C:\Program Files\dotnet\x64\dotnet.exe exec bin\Debug\net8.0\VisualEQ.dll`
so launches route through the x64 runtime side-installed alongside the ARM64
SDK. On a normal x64 Windows dev box, plain `dotnet build && dotnet run
--project VisualEQ` also works fine.

Each script does `cd /d "%~dp0.."` first so their internal relative paths
(`ConverterApp/`, `VisualEQ/bin/...`) keep resolving even though the script
itself lives in `dev/`.

## Scripts

- **`visualeq.bat`** — Debug-build launcher into the in-app main menu. This is
  the everyday "run what I just built" script.
- **`load_zone.bat [eq_path] [zone_name]`** — legacy CLI-style launcher that
  skips the menu and loads a zone directly. **Note:** still references the
  pre-Slice-1 `ConverterApp/` and `VisualEQ/` output layout for
  `*_oes.zip` lookup and writes/reads `eq_config.txt`. The current app writes
  to `%APPDATA%\VisualEQ\zones\` and reads settings from
  `%APPDATA%\VisualEQ\settings.json`. If you actually want to use this script,
  either copy zips into `ConverterApp/` manually or use the in-app menu
  instead.
- **`list_models.bat`** — interactive character-model dumper backed by the
  `ModelLister` project. Same caveat as `load_zone.bat` — reads from the
  pre-Slice-1 layout. `VisualEQ.exe --list-models` is the current supported
  path.

## Building a release zip

The release-zip pipeline lives at the repo root, not here:

```powershell
./publish-release.ps1 -Version 0.1.0-dev
```

Produces `release/VisualEQ-<version>-win-x64.zip`. Same script CI runs on a
`v*` tag push.
