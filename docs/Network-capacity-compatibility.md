# More Network Capacity Renew compatibility

The reviewed Workshop item is 3818403598. Exact input hashes and the local patch command are listed in [the Japanese guide](Network-capacity-compatibility.ja.md).

HundredTiles now supports 65,024 utility nodes and pulse queues. Its original v1 utility snapshots can migrate while preserving wrapped queue order. Unknown capacities, corrupt snapshots, and capacity reduction are rejected. The service-resource snapshot format remains unchanged.

The reviewed Renew wrapper references Harmony 1.2 APIs and produced a TypeLoadException with the installed modern Cities Harmony. The included source patcher converts one Harmony constructor site and 16 Patch call sites. It preserves the original outside the game plugin directory, checks the input and derived DLL hashes, and refuses unknown versions. It does not redistribute the third-party DLL or replace the Harmony engine. Run NetworkRenewPatcher/Apply.ps1 only with the game closed, using .NET SDK 10.0.401.

Steam updates may replace the locally patched wrapper. An updated, unreviewed wrapper is not automatically accepted. Removing the network capacity mod from a city saved with expanded capacity is outside the supported migration scope.

Use separately named test saves and external backups. Large-city, long-running, and arbitrary mod configuration safety is not established by the isolated tests. Native test results are reported separately in the validation record.
