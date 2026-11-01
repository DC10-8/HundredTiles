# Mods that changed since review (2026-11-02)

Runtime's per-mod compatibility code threw when a mod's code differed from the reviewed build, so a Workshop update of one mod stopped every city load ("Compatibility code changed: RightOfWay"). Patcher mode `changed-mods` routes `ReviewedCompatibility.Verify` and `Add`, and each per-mod `AddHooks` in `Compatibility.AddHooks`, through `ChangedMods.cs`. A mod that fails loses its reviewed hooks, any hooks it had queued are removed, a warning is logged and startup continues. `NetworkRecalculationCompatibility` and `NetworkCapacityCompatibility.Configure` stay strict because they decide capacities that must match the save.

The same patcher mode covers the other update stops:
- `NetworkCapacityCompatibility.Configure` falls back to the mod's own `ExpansionEnabled`, or to the live segment buffer.
- `NetworkRecalculationCompatibility` keeps its deferral while the two `Invoke` calls exist.
- The inline AutoHistoricalPlop check is replaced by `ChangedMods.AddHistorical`.
- `Harmony.Patch` in `Installer.Install` goes through `ChangedMods.Patch`, so a reviewed adapter that fails on a mod's code is skipped. Game targets stay strict. Failed targets are removed from the hook list in `SaveSafety.Validate`; otherwise saving would be blocked with "HundredTiles patch was removed".
- `ReviewedOverlays.Evaluate` allows, with a warning, a mod HundredTiles already knows that replaces save code. This carries some save risk, so the warning asks for a backup.

Still stops: a manager that cannot be rebuilt was created early, a game target fails, or a never-reviewed owner replaces save code.

Simulated offline on both builds with fake updated mods; every path above continues, and a game target and a never-reviewed owner stay strict.

`AutoAdapter` now also adapts small static helpers in the same class as a grid reader that return int and contain the n-1 clamp constant, such as `YieldScanner.CellIndex`. Without this the walk stays inside the vanilla-sized cells.

Checked offline against RightOfWay 0.3.0.0 on both builds: the reviewed `Verify` reproduces the error, the guard skips the hooks, and `CellIndex` clamps at 599 / 1079 with its origin unchanged. Not confirmed in game.

# Surface line patch arrays (2026-11-02)

`PatchArrays.cs` widens `new TransportLine.TempUpdateMeshData[81]` to GridSize squared in `PathVisualizer.RefreshPath`, `NetAdjust.CalculatePath` and any enabled mod with the same allocation. The expanded `TerrainManager.GetPatchIndex` returns up to GridSize squared minus 1. Selecting a ship past patch 80 therefore raised "Array index is out of range" on the simulation thread every step, with `TransportLine.CalculatePathSegmentCount` under `PathVisualizer.RefreshPath`. It runs once from `AutoAdapter.ApplyOnce`; a failure is logged and does not stop the game. Allocation search and the rewrite were checked offline on both builds. Not confirmed in game.

# Seven-mod compatibility update — 2026-11-01 JST

Exact official Workshop builds were reviewed and tested. The repaired DLL bundle was installed locally with an automatic backup; the user's 100-tile selection was retained. Player saves were not modified.

Supported patch owners: Tree Control, Prop Control, Visibility Control, Ploppable RICO Revisited, More CitizenUnits, Hide It and Network Skins (original item 1758376843). Manifests pin the reviewed binaries and explicitly permitted target/phase combinations. This is not an owner-wide conflict bypass.

Implemented adaptations:
- Tree Control: variable tree-capacity snapshots (262144–2097152), legacy migration validation and expanded render grids.
- More CitizenUnits: reviewed native capacity-transpiler composition.
- Hide It: expanded resource indexing/textures and the instance argument for replacement texture methods.
- Prop Control: first-priority expanded-coordinate handling, precision preservation inside the native map and prop-grid rebuilding after precision deserialization.
- Visibility Control and Ploppable RICO Revisited: reviewed rendering postfix and zoning-prefix coexistence.
- Network Skins: reviewed manager initialization and both CreateNode overloads.
- Move It dependency: null-safe cleanup of destroyed Unity controller, button and UI references; other cleanup remains intact and live exceptions are not swallowed.

Evidence and scope are in VERIFICATION.md. Both profiles passed 35 official-build composition checks. Native 100/324 save and separate-process reload passed; 324 also passed native 524288-capacity migration, expanded tree IDs and above-ground fixed-height persistence. No arbitrary future MOD version or complete visual/large-city coverage is implied.

Build notes:
1. Build.ps1 compiles the net35 adapter against the game's assemblies and preserved matching runtime references.
2. Patcher applies explicit incremental changes to copies of preserved, matching Core/SaveBridge/Runtime DLLs. It must not be applied twice to the same copy.
3. Build-Checks.ps1 builds offline and native checks. HUNDREDTILES_TEST_WORKSHOP=1 selects the actual Workshop binaries for SevenChecks; native fixtures use a separate data root and an isolation marker.
4. Package-Staged.ps1 validates and packages both eight-file profiles. The installer makes a backup before changing player DLLs.

This is incremental repair source for the preserved runtime. The older Runtime project in this recovered checkout is not asserted to reproduce the later preserved runtime. Do not replace the installed runtime by rebuilding that older project without reconciling its later changes. Binary inputs and native test saves are excluded from Git.

Official reference sources: https://github.com/algernon-A/TreeControl, https://github.com/algernon-A/MoreCitizenUnits, https://github.com/keallu/CSL-HideIt, https://github.com/boformer/NetworkSkins. Exact revisions are in upstream-commits.tsv. These reference builds are not asserted to reproduce the Workshop binaries; manifests now identify the downloaded official builds.

Serializer runtime check: SerializerRuntime.Verify replaces SaveBridge's whole-file SHA256 of ColossalManaged.dll with a fingerprint of the ColossalFramework.IO namespace only (types, fields, method signatures and IL with metadata tokens resolved to names). HundredTiles uses nothing else from that file, so loaders that rewrite unrelated types (StartupAccelerator rewrites PluginManager) no longer stop startup. The original and StartupAccelerator-patched game DLLs both give the reviewed fingerprint; a one-instruction change in DataSerializer is rejected and one in PluginManager is accepted. The Assembly-CSharp check is unchanged. Apply to an already-integrated copy with `Patcher <dir> serializer-only`, which rewrites SaveBridge only. After a game update, recompute the fingerprint from the new ColossalManaged.dll and review it before changing ReviewedFingerprint.

Early managers: HundredTiles enlarges manager arrays through Awake edits, so a manager another MOD created before HundredTiles installed keeps vanilla sizes; loading a save then failed with "Array index is out of range" in NetManager.InitializeNode. EarlyManagers.Prepare (called before Installer.Install resolves connections) reallocates empty grid-only arrays of Net/Building/Prop/Zone/Tree/Vehicle/Citizen/Render/Notification managers at 100-tile size (RenderManager groups created in the main menu are released with ReleaseGroup, as RenderManager.OnDestroy does, because they hold no city data and use 45x45 positions), and stops activation with the manager names when District/Electricity/Water/ImmaterialResource/NaturalResource/GameArea/Terrain managers already exist or a grid already holds data. EarlyManagers.VerifyBeforeLoad runs inside SessionGate.BeforeLoad's try block and refuses the city through the existing dialog if any live manager array is not at 100-tile size. Apply to an integrated copy with `Patcher <dir> early-managers` (Runtime only; repeated runs do not add the calls twice).

Early-manager sizes come from each profile's embedded plan (ReviewedPlan.tsv for 10x10, ReviewedPlan324.tsv for 18x18) and are chosen from HundredTiles.Core ExpandedMap.GridSize at run time; other grid sizes are refused.

Improved Public Transport 3.0.1 (Workshop 3690061052, pinned by ImprovedPublicTransport3.tsv): its sub-modules each run PatchAll on the whole assembly, so the same attribute patches appear under eight Harmony IDs. The patches on HundredTiles targets (BetterBoarding and Express Bus Services on LoadPassengers, the LoadPassengers counters, the VehicleAI.SimulationStep path-flag prefix) contain no grid code, but BetterBoarding replaces LoadPassengers and, with three other IPT3 methods, walks the citizen or node grid with vanilla constants. Ipt3Adapter transpiles those four methods to the active grid size and only then allows the 16 reviewed patch methods, each on its own target and phase. HundredTiles keeps each grid's vanilla origin (WorldMin -8640) and grows the map toward +x/+z, so GridRemap moves only the side constants (2159/2160, 269/270, 53/54) and leaves origin offsets (1080, 135, 27) unchanged, exactly like the plan edits.

Transfer Manager CE 3.1.39, Intersection Marking Tool 1.15, Campus Parks r2.0.0 and Lifecycle Rebalance Revisited 1.6.9 (ModAdapters, each pinned by its own manifest): Transfer Manager CE's nine grid helpers are remapped (its ShipAI.CanSpawnAt prefix replaces the original and uses one of them), and its spawn prefixes, CheckPassengers postfixes and UpdateNotifications postfix are allowed. Lifecycle Rebalance's GetTaxiProbability replacement has its PointOutOfArea call routed through TaxiHooks.Unavailable, the bounds check HundredTiles adds to the original. Intersection Marking Tool's renderer transpilers and Campus Parks' ParkBuildingAI transpiler hold no grid code; because HundredTiles' plan transpiler needs the original IL, they are allowed only when ordered after it. Build verification for these five mods (ReviewedBuild): the exact pinned file passes directly. A different build of the same version (a reporter's IPT3 3.0.1 differed byte-wise from the reviewed copy) passes only when every manifest method keeps its semantic hash and an IL scan of the whole assembly finds no method reading a manager grid outside the adapted or reviewed list; otherwise the log names the changed methods or new grid readers. Not adapted: Intersection Marking Tool's own selection ray cast (ModsCommon.BaseSelectToolMode`1.RayCast) uses the vanilla segment grid inside a generic type, so selecting in the added tiles may miss; it does not touch save data. A different IPT3 build leaves the adapter off without stopping HundredTiles, so saving stays blocked.

Unreviewed mods (policy since 2026-11-01): HundredTiles no longer stops for other mods' patches. ReviewedOverlays.Evaluate applies the reviewed rules first; any other patch on a HundredTiles target is allowed at startup and at save time and logged once ("Unreviewed patch allowed"), except a transpiler, finalizer or skip-capable (bool) prefix on a serialization method (Serialize, Deserialize, AfterDeserialize, Managers_UpdateData, DataSerializer), which still goes to the strict rules because it can corrupt a save. Per-mod reviewed adapters in SpatialAdapters that fail verification are skipped with a warning instead of failing startup. AutoAdapter then runs once before the first city load (only after a successful install, so arrays are already expanded): it scans every enabled mod and remaps the side constants of the simple grids each method reads (node, segment, building, prop: 270; tree, vehicle, parked: 540; citizen: 2160; vehicle grid2: 54; building grid2: 45; scaled by GridSize/9, origins unchanged), patching each method separately and logging failures. Zone, district, utility, resource, area and terrain grids also change origin or cell size and are only reported; methods in generic types are reported, not patched; other map-expansion mods (EightyOne*) are skipped. Mono 2.0 crashes the process when reading the body of a method whose locals use a type from a missing assembly, so a mod with an unresolvable reference is skipped entirely. Offline over 165 locally subscribed mod assemblies: 70 methods adapted in about 2.4 s, no crash.

Profiles: the profile switcher copies the stored bundles in %LOCALAPPDATA%\Colossal Order\Cities_Skylines\HundredTiles-profiles\{100,324} (checked against files.tsv) over the installed DLLs. Fixes must be applied to both bundles, with files.tsv regenerated, or the next switch reverts them.

FPS Booster hotfix: the verifier also recognizes its reviewed runtime lifecycle renames/prefixes, preserving the complete original-body comparison and pinned BehaviourUpdater check. This combination now has a native 100-tile reload test; see VERIFICATION.md. FpsFixture and FpsBodyChecks cover positive and negative verifier cases; FpsEntry is an isolated preloader bootstrap.
