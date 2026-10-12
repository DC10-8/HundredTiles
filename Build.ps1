param([string]$GameManaged='C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed',[string]$HarmonyDirectory=(Join-Path $PSScriptRoot 'Libs'))
$ErrorActionPreference='Stop'
foreach($name in @('Assembly-CSharp.dll','ColossalManaged.dll','ICities.dll','UnityEngine.dll')){if(-not (Test-Path -LiteralPath (Join-Path $GameManaged $name))){throw "Game assembly missing: $name"}}
foreach($name in @('CitiesHarmony.API.dll','CitiesHarmony.Harmony.dll')){if(-not (Test-Path -LiteralPath (Join-Path $HarmonyDirectory $name))){throw "Harmony assembly missing: $name. Supply -HarmonyDirectory."}}
dotnet build (Join-Path $PSScriptRoot 'Runtime/HundredTiles.Runtime.csproj') -c Release -m:1 "-p:GameManaged=$GameManaged" "-p:HarmonyDirectory=$HarmonyDirectory"
if($LASTEXITCODE){throw 'Release build failed.'}
Write-Output ('Built mod: '+(Join-Path $PSScriptRoot 'Runtime/bin/Release/net35'))
