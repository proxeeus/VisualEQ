# VisualEQ - EQEmu Visual Zone Editor

A visual editor for managing EQEmu zone spawn data. This tool allows server administrators to visually manipulate NPC spawn points, paths, and properties in a 3D environment. This is a continuation (in a different form) of the OpenEQ client by Daeken.

## Features

- Visualize all NPC spawn locations in a zone
- Drag and drop NPCs to position them precisely within the game world
- Edit spawn properties, groups, and paths
- Save changes directly to the EQEmu database

## Prerequisites

- .NET 6.0 or higher
- Access to an EQEmu database (MySQL/MariaDB)
- EverQuest game client files (for model and zone data)

## Setup

1. Clone the repository:
```bash
git clone https://github.com/yourusername/VisualEQ.git
cd VisualEQ
```

2. Configure database connection:
   - Copy `database.config.template.json` to `database.config.json`
   - Edit `database.config.json` with your EQEmu database credentials

3. Build the project:
```bash
dotnet build
```

4. Run the application:
```bash
dotnet run
```

## Configuration

### Database Settings
The application requires connection details for your EQEmu database. Create a `database.config.json` file based on the template:

```json
{
    "DatabaseSettings": {
        "Server": "your_server",
        "Database": "your_database",
        "Username": "your_username",
        "Password": "your_password",
        "Port": 3306
    }
}
```

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

- Never commit database credentials or sensitive information
- Keep `database.config.json` in your `.gitignore`
- Use environment variables or secure secrets management in production

## License

Copyright (c) VisualEQ. All rights reserved.

## Acknowledgments

- EQEmu Project (https://www.eqemulator.org/)
- Contributors to the project 