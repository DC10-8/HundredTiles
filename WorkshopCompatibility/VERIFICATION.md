# Official Workshop verification record (2026-11-01 JST)

The four previously absent binaries were downloaded using the signed-in Steam client. Their Workshop IDs, assembly versions and SHA-256 hashes are recorded in official-builds.csv. Prop Control, Visibility Control and Ploppable RICO Revisited use the previously available official Workshop binaries pinned in their manifests.

Completed:
- 100 and 324: 35 exact owner/target/phase compositions using all seven official binaries; wrong phases rejected.
- 100 and 324: resource/render grid helper transformations and native-before-MOD transpiler composition.
- Regression checks forbid detouring Prop Control's private position helpers and require the expanded-coordinate prefixes to run first.
- 100: isolated new-map load, all 100 areas unlocked, native SavePanel save and separate-process reload completed. The saved file remained SHA-256 245636158F9B7DBAAF01974D50FD710A35F0F45F907EDAFA52BB466D40393D35.

Native issues found during verification:
- Detouring Prop Control.GetPositionPrefix itself crashed Mono. Replaced with first-priority native Position prefixes; the native crash no longer occurred.
- The isolated test runner required .NET 3.5, Application.runInBackground=true, a one-time creation guard and normalized fixture path matching. These are test-harness fixes, not player settings changes.
- Move It 2.10.9.41557 raises NullReferenceException in MoveItLoader.UninstallMod during application exit, after successful saving. Added exact-build cleanup guards for destroyed ToolController, button and tool UI references while preserving all remaining cleanup. The exception did not recur on subsequent 324 default-capacity reload or 524288-capacity save exits.

Additional verification / scope:
- 324 default-capacity native save/reload and far-object assertions passed. Source SHA-256: 4C43492AC9D5627EEE9C99ABFE62193BE4A4B34C917972AE564D2F511135CC93.
- Tree Control native 524288 capacity migration and save passed; ID 410430 was exercised. Final 524288-capacity save/reload passed, including far tree ID 410431 at recorded fixed height 340.0469 and its far Prop. Source SHA-256 remained 4C1D2F75C7965D92E73F40EF64E3CD27A7C5AE52A11B124F99B6B7283AA83172. The final process exited without an observed managed exception.
- These tests do not establish every visual LOD, transit, consumer or arbitrary multi-MOD configuration.

The bundle is ready for backed-up local installation for the exact reviewed versions. Unknown MOD builds remain rejected; these results do not establish compatibility with arbitrary updates or combinations.

The initial far-tree checks placed trees at height 80 below terrain height 320. Tree Control correctly raised them under its default KeepAboveGround policy; horizontal coordinates remained intact. The revised fixed-height test places the tree 20m above terrain and records its height. This was a fixture expectation error, not evidence of a horizontal-coordinate persistence defect.



Deployment: both local and Workshop-local DLL copies were verified against all eight package hashes after installation. Active profile remains 100. Automatic backup was created outside the Mods directory. Steam Workshop itself has not been uploaded or published by this operation.
