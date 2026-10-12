param([string]$GameRoot='C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines',
      [Parameter(Mandatory=$true)][string]$HarmonyModDirectory,
      [string]$HarmonyDirectory=(Join-Path (Split-Path $PSScriptRoot) 'Libs'),
      [string]$DoorstopLibrary=(Join-Path $GameRoot 'winhttp.dll'))
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot
$testRoot=Join-Path $projectRoot 'artifacts\isolated-game'
if (Get-Process Cities -ErrorAction SilentlyContinue) { throw 'Close the game before preparing a test instance.' }
if(-not (Test-Path $DoorstopLibrary)){throw 'Native verification requires the separately installed UnityDoorstop winhttp.dll. Supply -DoorstopLibrary.'}
if(-not (Test-Path (Join-Path $HarmonyModDirectory 'CitiesHarmony.dll'))){throw 'Supply the installed Cities Harmony mod folder with -HarmonyModDirectory.'}
dotnet build $PSScriptRoot -c Release -m:1 "-p:GameManaged=$(Join-Path $GameRoot 'Cities_Data/Managed')" "-p:HarmonyDirectory=$HarmonyDirectory"
if ($LASTEXITCODE) { throw 'Integration harness build failed.' }
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
foreach ($name in @('Cities.exe','steam_api64.dll','colossal.cgs')) {
    Copy-Item -LiteralPath (Join-Path $GameRoot $name) -Destination (Join-Path $testRoot $name)
}
Copy-Item -LiteralPath $DoorstopLibrary -Destination (Join-Path $testRoot 'winhttp.dll')
$modRoot=Join-Path $testRoot 'UserData/Addons/Mods'
New-Item -ItemType Directory -Force -Path (Join-Path $modRoot 'HundredTiles'),(Join-Path $modRoot 'Harmony') | Out-Null
foreach($name in @('HundredTiles.Core.dll','HundredTiles.ExperimentalPatch.dll','HundredTiles.Runtime.dll','HundredTiles.SaveBridge.dll','HundredTiles.Utilities.dll','CitiesHarmony.API.dll')){Copy-Item -LiteralPath (Join-Path $projectRoot ('Runtime/bin/Release/net35/'+$name)) -Destination (Join-Path $modRoot 'HundredTiles')}
Get-ChildItem -LiteralPath $HarmonyModDirectory | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $modRoot 'Harmony') -Recurse -Force}
foreach ($name in @('Cities_Data','Files','Mono')) {
    $linkPath=Join-Path $testRoot $name
    $target=Join-Path $GameRoot $name
    if (Test-Path -LiteralPath $linkPath) {
        $existing=Get-Item -LiteralPath $linkPath
        if ($existing.LinkType -ne 'Junction' -or $existing.Target -ne $target) { throw "Unexpected game data link: $linkPath" }
    } else { New-Item -ItemType Junction -Path $linkPath -Target $target | Out-Null }
}
Set-Content -LiteralPath (Join-Path $testRoot 'isolated-test.marker') -Value 'HundredTiles development verification only'
Set-Content -LiteralPath (Join-Path $testRoot 'steam_appid.txt') -Value '255710'
$entryPath=Join-Path $PSScriptRoot 'bin\Release\net35\HundredTiles.IntegrationGame.dll'
Set-Content -LiteralPath (Join-Path $testRoot 'doorstop_config.ini') -Value "[UnityDoorstop]`nenabled=true`ntargetAssembly=$entryPath"
Write-Output "Prepared isolated Unity verification: $testRoot"
