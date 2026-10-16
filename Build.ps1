param([string]$GameManaged='C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed',[string]$HarmonyDirectory=(Join-Path $PSScriptRoot 'Libs'),[ValidateSet('100','324')][string]$MapProfile='100')
$ErrorActionPreference='Stop'
foreach($name in @('Assembly-CSharp.dll','ColossalManaged.dll','ICities.dll','UnityEngine.dll')){if(-not (Test-Path -LiteralPath (Join-Path $GameManaged $name))){throw "Game assembly missing: $name"}}
foreach($name in @('CitiesHarmony.API.dll','CitiesHarmony.Harmony.dll')){if(-not (Test-Path -LiteralPath (Join-Path $HarmonyDirectory $name))){throw "Harmony assembly missing: $name. Supply -HarmonyDirectory."}}
$project=Join-Path $PSScriptRoot 'Runtime/HundredTiles.Runtime.csproj'
dotnet restore $project --locked-mode "-p:GameManaged=$GameManaged" "-p:HarmonyDirectory=$HarmonyDirectory" "-p:HundredTilesMapProfile=$MapProfile" -p:HundredTilesTrialRuntime=false
if($LASTEXITCODE){throw 'Locked dependency restore failed.'}
dotnet build $project -c Release -m:1 --no-restore "-p:GameManaged=$GameManaged" "-p:HarmonyDirectory=$HarmonyDirectory" "-p:HundredTilesMapProfile=$MapProfile" -p:HundredTilesTrialRuntime=false
if($LASTEXITCODE){throw 'Release build failed.'}
$output=if($MapProfile-eq '324'){'Runtime/bin/324/Release/net35'}else{'Runtime/bin/Release/net35'}
Write-Output ('Built mod: '+(Join-Path $PSScriptRoot $output))
