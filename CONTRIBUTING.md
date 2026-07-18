# Contributing

Contributions are welcome for noncommercial use under this repository's license.

## Development setup

1. Install the .NET 10 SDK on Windows 10 or Windows 11 x64.
2. Clone the repository.
3. Run `dotnet restore ExpeditionsMacro.slnx`.
4. Run `dotnet build ExpeditionsMacro.slnx -c Debug`.
5. Run `dotnet test tests/ExpeditionsMacro.Tests/ExpeditionsMacro.Tests.csproj -c Debug --filter "Category!=Golden"`.

The optional golden regression tests use private raw Roblox captures that are not distributed in the repository. See [datasets/README.md](datasets/README.md) to prepare a local dataset.

Keep changes scoped, preserve Roblox-relative coordinates, restore window and input state in `finally` blocks, and add a regression test for behavior changes. Never commit webhook URLs, logs, local models, raw personal screenshots, or build artifacts.
