param([string]$GameManaged=(Join-Path ${env:ProgramFiles(x86)} 'Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed'),[string]$ModReferences=(Join-Path $PSScriptRoot '..\recovery\installed-dlls'),[string]$Harmony=(Join-Path ${env:ProgramFiles(x86)} 'Steam\steamapps\workshop\content\255710\2040656402\CitiesHarmony.Harmony.dll'))
$ErrorActionPreference='Stop'
$ref=Join-Path $env:USERPROFILE '.nuget\packages\microsoft.netframework.referenceassemblies.net35\1.0.3\build\.NETFramework\v3.5'
$references=@('mscorlib.dll','System.dll','System.Core.dll') | ForEach-Object {'/reference:'+(Join-Path $ref $_)}
$references+=@('ColossalManaged.dll','UnityEngine.dll','Assembly-CSharp.dll') | ForEach-Object {'/reference:'+(Join-Path $GameManaged $_)}
$references+=@('HundredTiles.Core.dll','HundredTiles.SaveBridge.dll','HundredTiles.ExperimentalPatch.dll') | ForEach-Object {'/reference:'+(Join-Path $ModReferences $_)}
$references+=('/reference:'+$Harmony)
$resources=Get-ChildItem $PSScriptRoot -Filter '*.tsv' | Where-Object {$_.Name -ne 'upstream-commits.tsv'} | ForEach-Object {'/resource:'+$_.FullName+','+$_.Name}
& (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe') /nologo /unsafe /target:library /nostdlib /noconfig $references $resources ('/out:'+(Join-Path $PSScriptRoot 'HundredTiles.WorkshopCompatibility.dll')) (Join-Path $PSScriptRoot 'ReviewedOverlays.cs') (Join-Path $PSScriptRoot 'ExpandedObjects.cs') (Join-Path $PSScriptRoot 'SpatialAdapters.cs') (Join-Path $PSScriptRoot 'CleanupAdapters.cs') (Join-Path $PSScriptRoot 'SerializerRuntime.cs') (Join-Path $PSScriptRoot 'EarlyManagers.cs') (Join-Path $PSScriptRoot 'Ipt3Adapter.cs') (Join-Path $PSScriptRoot 'GridRemap.cs') (Join-Path $PSScriptRoot 'ModAdapters.cs') (Join-Path $PSScriptRoot 'ReviewedBuild.cs')
if($LASTEXITCODE -ne 0){throw 'Workshop compatibility build failed.'}

