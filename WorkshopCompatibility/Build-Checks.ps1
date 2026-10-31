param([string]$GameManaged=(Join-Path ${env:ProgramFiles(x86)} 'Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed'),[string]$ModReferences=(Join-Path $PSScriptRoot '..\recovery\installed-dlls'),[string]$Harmony=(Join-Path ${env:ProgramFiles(x86)} 'Steam\steamapps\workshop\content\255710\2040656402\CitiesHarmony.Harmony.dll'))
$ErrorActionPreference='Stop'
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$references=@('Assembly-CSharp.dll','ColossalManaged.dll','UnityEngine.dll') | ForEach-Object {'/reference:'+(Join-Path $GameManaged $_)}
$references+=@('HundredTiles.Runtime.dll','HundredTiles.ExperimentalPatch.dll','HundredTiles.SaveBridge.dll') | ForEach-Object {'/reference:'+(Join-Path $ModReferences $_)}
$references+=('/reference:'+$Harmony)
& $csc /nologo ('/out:'+(Join-Path $PSScriptRoot 'SevenChecks.exe')) $references ('/reference:'+(Join-Path $PSScriptRoot 'HundredTiles.WorkshopCompatibility.dll')) (Join-Path $PSScriptRoot 'SevenChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Composition check build failed'}
$framework=Join-Path $env:USERPROFILE '.nuget\packages\microsoft.netframework.referenceassemblies.net35\1.0.3\build\.NETFramework\v3.5'
$nativeReferences=@('mscorlib.dll','System.dll','System.Core.dll')|ForEach-Object {'/reference:'+(Join-Path $framework $_)}
& $csc /nologo /nostdlib /noconfig /target:exe ('/out:'+(Join-Path $PSScriptRoot 'NativeChecks.dll')) $nativeReferences $references (Join-Path $PSScriptRoot 'NativeChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Native harness build failed'}
