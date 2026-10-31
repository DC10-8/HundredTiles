# Profile selector and fog fix (2026-11-01)

This is the source of the recovery-era profile selector, not a reconstruction of the deleted complete October 31 runtime source. The normal `Build.ps1` still builds the October 20 public implementation. Do not describe that output as identical to the currently distributed recovery DLLs.

## Behavior

Options show City (324 tiles), City (100 tiles), and Editor in Japanese/English. The current process keeps its existing dimensions. Selecting a different city profile starts a hidden worker that waits until Cities: Skylines has completely exited, verifies the selected bundle, backs up all destination DLLs, and replaces the complete set. A fast restart leaves the request waiting until the next complete exit. Pending choices show a waiting label and block unlock operations and city loads with mismatched DLL dimensions.

This does not shrink or convert a 324-tile save to 100 tiles. Use the corresponding profile for each save. The 100 profile is the retained October 31 100-tile binary, with selector wiring and reviewed RealTimeSync/AreaPOBuilder data updated; it does not contain all 324-specific changes.

`ProfileFogLoading.cs` supplies the missing city fog controller for the retained 100-tile runtime. It skips runtimes that already provide that controller, removes camera/global shader fog contributions, and restores captured display state on unload. It does not edit saved weather or pollution.

## Build and inputs

Run `Build-Helpers.ps1` on Windows, supplying your own game managed DLLs and .NET 3.5 reference assemblies. No game DLLs are included in Git. This builds the shared helper DLL and the standalone .NET Framework switch worker.

`BuildProfiles.cs` is a targeted Mono.Cecil adapter for the preserved binary versions. Put the original matching six-DLL sets in `bundles/100` and `bundles/324`, and build the helper first. Run:

```powershell
dotnet build ProfileSwitcher/BuildProfiles.csproj -c Release
ProfileSwitcher/bin/Release/net10.0/BuildProfiles.exe <ProfileSwitcher-directory> <game-managed-directory> <CitiesHarmony-directory>
```

Keep untouched input copies: the adapter modifies staged runtime DLLs in place and deliberately fails if the expected settings structure differs. It changes the settings methods and city-load gate; 100-side compatibility metadata is updated from the two committed TSVs. The input binaries are not replaced by the October 20 source build. `Install.ps1` expects a distribution containing `profiles/100`, `profiles/324`, their `files.tsv` manifests, and the worker EXE. A helper DLL alone is insufficient.

Inputs for the retained runtime copies before selector adaptation:

- 100: retained October 31 100-tile profile backup.
- 324: RealTimeSync-repaired runtime SHA256 `AE82661453439DA796718952ACF50C08EB7C14AF50E495AEDBA914684DD72189`.

## Verification scope

Verified locally: both Core profile constants, three choices in both languages, RealTimeSync compatibility for both profiles, bundle checksums, stale-request cancellation, injected mid-transaction failure rollback, installed DLL switching 100 -> 324, and waiting for a game-named test process before applying 100. No user saves were modified.

The public worker self-test uses temporary mock bundles and does not need game DLLs:

```sh
dotnet run --project ProfileSwitcher/Checks/SwitchWorker.Checks.csproj -c Release -- selftest
```

Not verified for this update: the actual game options interaction, city save/reload, or visual confirmation that haze is gone. Do not treat the mock worker test as a live city test.
