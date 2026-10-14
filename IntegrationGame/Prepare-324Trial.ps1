param([switch]$Restore)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot
$fixture=[IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts\isolated-game'))
if(-not(Test-Path -LiteralPath (Join-Path $fixture 'isolated-test.marker'))){throw 'Prepare the isolated game first.'}
$citiesProcesses=@(Get-CimInstance -ClassName Win32_Process -Filter "Name = 'Cities.exe'" -ErrorAction Stop)
if($citiesProcesses.Count-ne 0){throw 'A Cities process is running; fixture files were not changed.'}
function WithinFixture([string]$path){$full=[IO.Path]::GetFullPath($path);if(-not $full.StartsWith($fixture+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Path is outside the fixture: $full"};return $full}
$mods=WithinFixture (Join-Path $fixture 'UserData\Addons\Mods')
$record=Join-Path $fixture 'trial324-backup-path.txt'
$config=Join-Path $fixture 'doorstop_config.ini'
if($Restore){
    if(-not(Test-Path -LiteralPath $record)){throw 'Original fixture mod backup record is missing.'}
    $backup=WithinFixture ((Get-Content -LiteralPath $record -Raw).Trim())
    if(-not(Test-Path -LiteralPath $backup -PathType Container)){throw 'Original fixture mod backup is missing.'}
    $archive=WithinFixture (Join-Path $fixture ('trial324-mods-minimal-'+[Guid]::NewGuid().ToString('N')))
    Move-Item -LiteralPath $mods -Destination $archive
    Move-Item -LiteralPath $backup -Destination $mods
    $harness=Join-Path $projectRoot 'IntegrationGame\bin\Release\net35\HundredTiles.IntegrationGame.dll'
    [IO.File]::WriteAllText($config,"[UnityDoorstop]`r`nenabled=true`r`ntargetAssembly=$harness`r`n")
    Write-Output "Restored original fixture mods; trial mods retained at $archive"
    return
}
if(Test-Path -LiteralPath $record){$existing=WithinFixture ((Get-Content -LiteralPath $record -Raw).Trim());if(Test-Path -LiteralPath $existing){throw 'A trial backup already exists; restore it first.'}}
dotnet build $PSScriptRoot -c Release -m:1 -p:HundredTilesMapProfile=324 -p:HundredTilesTrialRuntime=true
if($LASTEXITCODE){throw '324 trial build failed; fixture unchanged.'}
$harmony=Get-ChildItem -LiteralPath $mods -Directory | Where-Object {Test-Path -LiteralPath (Join-Path $_.FullName 'CitiesHarmony.dll')}
if(@($harmony).Count-ne 1){throw 'Exactly one CitiesHarmony mod is required.'}
$backup=WithinFixture (Join-Path $fixture ('trial324-mods-full-'+[Guid]::NewGuid().ToString('N')))
Move-Item -LiteralPath $mods -Destination $backup
Set-Content -LiteralPath $record -Value $backup
New-Item -ItemType Directory -Force -Path $mods | Out-Null
Copy-Item -LiteralPath (Join-Path $backup $harmony.Name) -Destination $mods -Recurse
$target=Join-Path $mods 'HundredTiles';New-Item -ItemType Directory -Path $target | Out-Null
foreach($name in @('HundredTiles.Core.dll','HundredTiles.ExperimentalPatch.dll','HundredTiles.Runtime.dll','HundredTiles.SaveBridge.dll','HundredTiles.Utilities.dll','CitiesHarmony.API.dll')){Copy-Item -LiteralPath (Join-Path $projectRoot ('Runtime\bin\324-trial\Release\net35\'+$name)) -Destination $target}
$harness=Join-Path $projectRoot 'IntegrationGame\bin\324-trial\Release\net35\HundredTiles.IntegrationGame.dll'
[IO.File]::WriteAllText($config,"[UnityDoorstop]`r`nenabled=true`r`ntargetAssembly=$harness`r`n")
Write-Output 'Prepared isolated 324 trial. User mod directories and saves were not changed.'
