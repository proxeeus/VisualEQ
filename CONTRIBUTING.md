# Contributing to VisualEQ

Thank you for your interest in contributing to VisualEQ! This document provides guidelines and instructions for contributing to the project.

## Code Style

This project uses StyleCop for maintaining consistent code style. The rules are defined in `stylecop.json`:

- Use 4 spaces for indentation (no tabs)
- Place using directives outside namespace
- System using directives first, with blank lines between groups
- No Hungarian notation
- Add newline at end of files

## Development Setup

1. Install Prerequisites:
   - .NET 6.0 SDK or higher
   - Visual Studio 2022 or VS Code with C# extensions
   - MySQL/MariaDB for local development

2. Configure Development Environment:
   - Copy `database.config.template.json` to `database.config.json`
   - Configure your local database settings

## Pull Request Process

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Make your changes
4. Run tests and ensure they pass
5. Commit your changes (`git commit -m 'Add amazing feature'`)
6. Push to your branch (`git push origin feature/amazing-feature`)
7. Create a Pull Request

## Commit Guidelines

- Use clear, descriptive commit messages
- Reference issue numbers when applicable
- Keep commits focused and atomic

## Security

- Never commit sensitive information (passwords, API keys, etc.)
- Don't commit the `database.config.json` file
- Report security vulnerabilities privately to the maintainers

## Testing

- Add tests for new features
- Ensure existing tests pass
- Test your changes with different zone configurations

## Documentation

- Update README.md if adding new features
- Document public APIs and important classes
- Include comments for complex logic

## Code Review

All submissions require review. We use GitHub pull requests for this purpose:

1. Fork the repo and create your branch
2. Make your changes
3. Ensure the code style matches the project
4. Submit a pull request

## Questions?

If you have questions, please:
1. Check existing issues
2. Create a new issue for discussion
3. Ask in the pull request

Thank you for contributing to VisualEQ! 