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

FPS Booster hotfix: the verifier also recognizes its reviewed runtime lifecycle renames/prefixes, preserving the complete original-body comparison and pinned BehaviourUpdater check. This combination now has a native 100-tile reload test; see VERIFICATION.md. FpsFixture and FpsBodyChecks cover positive and negative verifier cases; FpsEntry is an isolated preloader bootstrap.
