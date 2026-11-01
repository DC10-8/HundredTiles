param(
 [string]$GameManaged=(Join-Path ${env:ProgramFiles(x86)} 'Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed'),
 [string]$ReferenceAssemblies=(Join-Path $env:USERPROFILE '.nuget\packages\microsoft.netframework.referenceassemblies.net35\1.0.3\build\.NETFramework\v3.5')
)
$ErrorActionPreference='Stop'
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$references=@('mscorlib.dll','System.dll','System.Core.dll') | ForEach-Object {'/reference:'+(Join-Path $ReferenceAssemblies $_)}
$references+=@('ColossalManaged.dll','UnityEngine.dll','Assembly-CSharp.dll','ICities.dll') | ForEach-Object {'/reference:'+(Join-Path $GameManaged $_)}
& $csc /nologo /target:library /nostdlib /noconfig $references ('/out:'+(Join-Path $PSScriptRoot 'HundredTiles.Profiles.dll')) (Join-Path $PSScriptRoot 'Selector.cs') (Join-Path $PSScriptRoot 'QuitSwitch.cs') (Join-Path $PSScriptRoot 'ProfileFogLoading.cs')
if($LASTEXITCODE -ne 0){throw 'Profile helper build failed.'}
& $csc /nologo ('/out:'+(Join-Path $PSScriptRoot 'HundredTiles.ProfileSwitch.exe')) (Join-Path $PSScriptRoot 'SwitchWorker.cs')
if($LASTEXITCODE -ne 0){throw 'Switch worker build failed.'}
