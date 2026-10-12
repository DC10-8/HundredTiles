param([string]$GameRoot='C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot
$testRoot=Join-Path $projectRoot 'artifacts\isolated-game'
if (Get-Process Cities -ErrorAction SilentlyContinue) { throw 'Close the game before preparing a test instance.' }
dotnet build $PSScriptRoot -c Release -m:1
if ($LASTEXITCODE) { throw 'Integration harness build failed.' }
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
foreach ($name in @('Cities.exe','steam_api64.dll','winhttp.dll','colossal.cgs')) {
    Copy-Item -LiteralPath (Join-Path $GameRoot $name) -Destination (Join-Path $testRoot $name)
}
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
