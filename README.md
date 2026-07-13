# VisualEQ - EQEmu Visual Zone Editor

A visual editor for managing EQEmu zone spawn data. This tool allows server administrators to visually manipulate NPC spawn points, paths, and properties in a 3D environment. This is a continuation (in a different form) of the VisualEQ client by Daeken.

## Features

- Visualize all NPC spawn locations in a zone
- Drag and drop NPCs to position them precisely within the game world
- Edit spawn properties, groups, and paths
- Save changes directly to the EQEmu database

## Prerequisites

- .NET 8.0 SDK
- Access to an EQEmu database (MySQL/MariaDB)
- EverQuest game client files (for model and zone data)

Windows-on-ARM (Parallels / Apple Silicon) needs both the ARM64 SDK and the x64 runtime installed side-by-side. See `CLAUDE.md` §4 for details.

## Setup

1. Clone the repository:

   ```bash
   git clone https://github.com/proxeeus/VisualEQ.git
   cd VisualEQ
   ```

2. Build the solution:

   ```bash
   dotnet build
   ```

3. Launch the app:

   ```bat
   visualeq.bat
   ```

   This opens the in-app main menu. From there you can list zones, decode a new zone from your EverQuest install, or edit settings. `load_zone.bat [eq_path] [zone_name]` skips the menu and loads a zone directly.

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
