# SteamLobbyLib

Narratable multiplayer protocol testbed built on Steamworks.NET. Includes:

- `SteamLobbyLib`: Doctrine-aligned domain library for lobby lifecycle and trace narration
- `SteamLobbyTester`: Executable harness for simulating lobby discovery, joins, and metadata updates
- `steamworks/`: Native bindings organized by platform

## Getting Started

1. Install .NET 9 SDK and Runtime
2. Launch Steam client
3. Run: `dotnet run --project SteamLobbyTester`

## Architecture

- Registry-driven trace actions
- Typed payloads and semantic wrappers
- Cross-platform native layout (Windows-x64, x86, OSX/Linux)

## License

MIT (or your preferred license)
