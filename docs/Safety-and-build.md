# Experimental release: safety and reproducibility

HundredTiles is an experimental release. Successful compilation and bounded integration tests do not establish safety for an important large city. Keep an external backup and use a separately named copy. Long-running large cities, arbitrary mod combinations, and complete migration of older 81 Tiles saves are unverified. The `fakeGAM` ownership importer is not a complete importer for that mod's custom utility data.

The review correctly identified a stale `TrainDepartureSequencer/Libs` path in the verification script. It was a path to Harmony binaries, not a runtime dependency on TrainDepartureSequencer. The path has been removed. All external game/Harmony references are configurable. Developer-specific ILSpy installation paths were replaced with a pinned Mono.Cecil 0.11.6 NuGet dependency. The SDK is pinned to 10.0.401, dependencies have committed lock files, and the repository has a solution containing all 18 projects.

`Verify-Reproducible.ps1` exports two clean source trees in different directories, builds with the same separately supplied game/Harmony assemblies, and compares all six production DLL hashes. This has passed on the developer's PC; independent reproduction on another PC is still needed. Game DLLs and other mods are not redistributed with the source. `Build.ps1` uses locked restore, deterministic compilation and source path mapping.

```powershell
./Build.ps1 -GameManaged 'D:\your-game\Cities_Data\Managed' -HarmonyDirectory 'D:\your-harmony'
./Verify.ps1 -GameAssembly 'D:\your-game\Cities_Data\Managed\Assembly-CSharp.dll' -HarmonyDirectory 'D:\your-harmony'
./Verify-Reproducible.ps1 -GameManaged 'D:\your-game\Cities_Data\Managed' -HarmonyDirectory 'D:\your-harmony'
```

The reviewed game assembly is Windows Steam 1.21.1-f9, SHA-256 `DF557994A03C93D8BB4A14F80B1FBCC0FB29D547658442445D078D069BCDD9FE`. `Reviewed-compatibility.csv` records reviewed third-party assembly hashes and method counts. This is a code identity matrix, not a guarantee for every setting or gameplay interaction. Even reviewed mods can interact outside the intercepted methods.

The save bridge now refuses saving after a load exception until another load begins. It also checks patched targets immediately before save capture: new unreviewed owners and removal of HundredTiles patches block the save. Earlier owner-name-only compatibility exceptions were replaced with reviewed binary/method identities. These checks do not monitor every game method and cannot promise that no other mod has already damaged state. Keep backups independently of the mod.

Pure tests cover corrupted/missing/mixed extension data, rollback, coordinates and mock manager lifecycles. Game-DLL tests inspect/execute reviewed IL and serializer code outside the game; they are not Unity playtests. Public CI runs only Checks, SaveChecks, BridgeChecks and ServiceChecks without proprietary game DLLs. Its success must not be presented as full gameplay validation.

Native integration uses a separate save/settings directory, an installed game, Cities Harmony and a separately supplied UnityDoorstop loader. `IntegrationGame/Prepare.ps1` documents the required paths. `BaseEntryCreate` and `BaseEntryReload` provide a basic production-entry save/reload test. Additional mod tests require their exact reviewed binaries and associated resource files. Native test evidence includes construction, terrain/undo, utilities, service resources and save reloads in a small fixture city. Taxi arrival callbacks are controlled; prolonged autonomous driving is untested. An external UnifiedUI exception remains during shutdown in the reviewed-mod fixture.

Stability requires large-city copy tests over multiple save generations, object preservation checks, longer operation with specific mod settings, independent clean builds and independent code review. This release is not being declared stable based on the present tests. See the Japanese [detailed assessment](Safety-and-build.ja.md) and [validation record](Validation-2026-10-13.ja.md).
