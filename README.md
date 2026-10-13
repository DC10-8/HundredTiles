# HundredTiles — Cities: Skylines 1

**Experimental release.** Long-term large-city operation and arbitrary mod combinations are unverified. Use copied saves and keep an external backup. See [safety and build verification](docs/Safety-and-build.md) for confirmed checks and limits.

Standard-sized 10×10 city area (100 tiles). Keeps original coordinates and adds 19 tiles to the east and north. Windows Steam game build 1.21.1-f9 is the reviewed build.

日本語の導入・操作説明: [README.ja.md](README.ja.md)

## Features

- Standard tile purchase and a free unlock-all options button.
- Added terrain, districts, zoning, networks, object grids, utilities, service resources, rendering and save data.
- Immediate below-sea-level water initialization on unlock.
- Optional hills, river connections and forests in added tiles.
- Reviewed adapters for installed versions of Move It!, Extra Landscaping Tools, No Utility Networks, ForceRoadConnect, TerrainLink and RightOfWay. Reviewed patch composition also supports the installed Building Control, Custom Zone Mixer, FreeUtilities, QuayTools, SimulationGuard, First Person Camera, HyperLoad and PassengerLineBuilder versions; unknown versions remain blocked.
- Taxi eligibility across purchased original and added tiles; vanilla probabilities preserved, with a 40m outer-edge guard.
- Japanese / English options dropdown: switches labels and current results immediately; preference persists.

## Use

Enable Cities Harmony and HundredTiles, then restart the game. Disable other tile expansion mods. Load a city and open Options → HundredTiles 1.0. Select 日本語 or English. Unlock all tiles and optionally generate/repair added nature, then save the city under a new name. Keep the mod enabled for expanded saves. Editors and scenarios are not supported. DLC features require the corresponding DLC.

## Build from source

Install .NET SDK 10.0.401 (pinned by global.json) and your own Windows Steam copy of the reviewed game. You need `Assembly-CSharp.dll`, `ColossalManaged.dll`, `ICities.dll`, and `UnityEngine.dll` from `Cities_Data/Managed`, plus `CitiesHarmony.API.dll` and `CitiesHarmony.Harmony.dll` from Cities Harmony. These DLLs are not part of this repository.

```powershell
./Build.ps1 -GameManaged 'C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed' -HarmonyDirectory 'C:\path\to\CitiesHarmony'
./Package.ps1 -SkipBuild
```

The runtime targets .NET Framework 3.5, using the NuGet reference-assemblies package. Optionally create ignored `Directory.Build.user.props` to set `GameManaged` / `HarmonyDirectory` locally. `Libs/` is ignored. Copying or publishing the game's assemblies is not required.

Pure logic checks: `dotnet run --project Checks -c Release`, `SaveChecks`, `BridgeChecks`. Reflection/native checks under RuntimeChecks and IntegrationGame require the reviewed game and mod dependencies. IntegrationGame is a development harness; it is not included in the mod release.

## Publication materials

[Workshop/description-ja-en.txt](Workshop/description-ja-en.txt) contains a combined Japanese/English description and instructions under 4,000 characters. [Workshop/thumbnail.jpg](Workshop/thumbnail.jpg) is the upload-ready artwork. The PNG master and generation prompt are retained beside it.

## Validation limits

Construction, service grids, save reloads, terrain tools, and deterministic taxi dispatch/path/pickup/drop-off callbacks were tested in an isolated real game. Taxi arrival callbacks were invoked for repeatable testing; autonomous trip times and prolonged large-city operation were not measured. The mod checks reviewed code and rejects unknown changes rather than applying unsafe patches. Compatibility with every other mod is not guaranteed.

## License

Original code: MIT. Preserve third-party notices. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Source excludes game DLLs, decompiled game code, saved cities and private logs.

Reviewed More Network Capacity Renew support requires a local Harmony API patch for the pinned wrapper. See [compatibility instructions](docs/Network-capacity-compatibility.md).

Nature action buttons retain their operation names while displaying pending or completed results. Language changes update both lines immediately.

See [startup repair scope and verification](docs/Startup-repairs.ja.md) for camera, rail, asset and optional-integration fixes. Third-party DLLs are not included in this repository.
