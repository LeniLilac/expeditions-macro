# Contributing

Contributions are welcome for noncommercial use under this repository's license.

## Development setup

1. Install the .NET 10 SDK on Windows 10 or Windows 11 x64.
2. Clone the repository.
3. Read [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) and the subsystem document relevant to your change.
4. Run `./scripts/Test-RepositoryPolicy.ps1`.
5. Run `dotnet restore ExpeditionsMacro.slnx --locked-mode`.
6. Run `dotnet build ExpeditionsMacro.slnx -c Debug --no-restore`.
7. Run `dotnet test tests/ExpeditionsMacro.Tests/ExpeditionsMacro.Tests.csproj -c Debug --no-build`.

Golden regression tests use the image dataset in this repository. See [datasets/README.md](datasets/README.md) before adding or replacing captures.

The complete validation matrix is in [docs/TESTING.md](docs/TESTING.md), and release requirements are in [docs/RELEASING.md](docs/RELEASING.md).

Keep changes scoped, preserve Roblox-relative coordinates, leave Roblox at the canonical client size after automation resizes it, always restore input state in guaranteed cleanup paths, and add a regression test for behavior changes. New production files are limited to 500 lines; existing oversized files may not grow. Never commit webhook URLs, logs, local models, raw personal screenshots, or build artifacts.
