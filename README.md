# VisualEQ - EQEmu Visual Zone Editor

A visual editor for managing EQEmu zone spawn data. This tool allows server administrators to visually manipulate NPC spawn points, paths, and properties in a 3D environment. This is a continuation (in a different form) of the VisualEQ client by Daeken.

## Features

- Visualize all NPC spawn locations in a zone
- Drag and drop NPCs to position them precisely within the game world
- Edit spawn properties, groups, and paths
- Save changes directly to the EQEmu database

## For end users

1. Grab the latest zip from the [Releases page](https://github.com/proxeeus/VisualEQ/releases). It's a self-contained Windows x64 build — no .NET install required.
2. Unzip anywhere and run `VisualEQ.exe`.
3. In the main menu, open **Settings**:
   - Set your **EverQuest install path** (used by the in-app decoder to read the game's S3D/WLD assets).
   - Optionally set **Database Connection** for spawn editing. Without a DB, the app is a viewer only.
4. Click **Decode New Zone** to convert a zone from your EQ install. Converted zips land in `%APPDATA%\VisualEQ\zones\`.
5. Load a zone from the main menu list.

Press **F10** while a zone is loaded to return to the main menu and swap zones.

## For contributors

### Prerequisites

- .NET 8.0 SDK
- Access to an EQEmu database (MySQL/MariaDB) — optional if you only want to render zones without spawn data
- EverQuest game client files (for model and zone data)

Windows-on-ARM (Parallels / Apple Silicon) needs both the ARM64 SDK and the x64 runtime installed side-by-side, because `cimgui.dll` is x64-native. See [dev/README.md](dev/README.md) and `CLAUDE.md` §4.

### Build & run

```bash
git clone https://github.com/proxeeus/VisualEQ.git
cd VisualEQ
dotnet build
```

Then:

```bat
dev\visualeq.bat
```

This opens the in-app main menu. Legacy CLI-style launchers (`dev\load_zone.bat`, `dev\list_models.bat`) are kept for reference but reference the pre-`%APPDATA%\VisualEQ\zones\` layout — see [dev/README.md](dev/README.md) for caveats.

### Build a release zip locally

```powershell
./publish-release.ps1 -Version 0.1.0-dev
```

Produces `release/VisualEQ-<version>-win-x64.zip`. Same script CI runs on a `v*` tag push (see [.github/workflows/release.yml](.github/workflows/release.yml)).

## Configuration

Database credentials and the EverQuest install path are managed inside the app (Settings + Database Connection views) and persisted to `%APPDATA%\VisualEQ\settings.json`. There is no committed config file — the first-run flow prompts for connection details.

## Development

This project follows C# coding standards defined in `stylecop.json`. Make sure to:

- Use 4 spaces for indentation
- Place using directives outside namespace
- Follow the ordering rules for using statements
- Add a newline at the end of files

## Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## Security

- Never commit database credentials or sensitive information. All credentials live in `%APPDATA%\VisualEQ\settings.json`, which is outside the repo.
- Use environment variables or secure secrets management in production.

## License

Copyright (c) VisualEQ. All rights reserved.

## Acknowledgments

- EQEmu Project (https://www.eqemulator.org/)
- Contributors to the project
