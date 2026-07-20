# Contributing

Contributions are welcome for noncommercial use under this repository's license.

## Development setup

1. Install the .NET 10 SDK on Windows 10 or Windows 11 x64.
2. Clone the repository.
3. Run `dotnet restore ExpeditionsMacro.slnx`.
4. Run `dotnet build ExpeditionsMacro.slnx -c Debug`.
5. Run `dotnet test tests/ExpeditionsMacro.Tests/ExpeditionsMacro.Tests.csproj -c Debug`.

Golden regression tests use the image dataset in this repository. See [datasets/README.md](datasets/README.md) before adding or replacing captures.

Keep changes scoped, preserve Roblox-relative coordinates, leave Roblox at the canonical client size after automation resizes it, always restore input state in `finally` blocks, and add a regression test for behavior changes. Never commit webhook URLs, logs, local models, raw personal screenshots, or build artifacts.
