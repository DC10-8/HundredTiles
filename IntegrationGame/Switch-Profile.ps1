param([Parameter(Mandatory=$true)][ValidateSet('100','324','324Trial')][string]$Profile,[switch]$SkipBuild)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot
$fixture=[IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts\isolated-game'))
if(-not(Test-Path -LiteralPath (Join-Path $fixture 'isolated-test.marker'))){throw 'Prepared isolated fixture marker missing.'}
function RequireStopped {
    if(@(Get-CimInstance Win32_Process -Filter "Name = 'Cities.exe'" -ErrorAction Stop).Count-ne 0){throw 'A Cities process is running; fixture files were not changed.'}
}
RequireStopped
if(-not $SkipBuild){
    if($Profile-eq '324Trial'){dotnet build $PSScriptRoot -c Release -m:1 -p:HundredTilesMapProfile=324 -p:HundredTilesTrialRuntime=true}
    elseif($Profile-eq '324'){dotnet build $PSScriptRoot -c Release -m:1 -p:HundredTilesMapProfile=324 -p:HundredTilesTrialRuntime=false}
    else{dotnet build $PSScriptRoot -c Release -m:1 -p:HundredTilesMapProfile=100 -p:HundredTilesTrialRuntime=false}
    if($LASTEXITCODE){throw 'Profile build failed; fixture unchanged.'}
}
$suffix=if($Profile-eq '324Trial'){'324-trial\Release\net35'}elseif($Profile-eq '324'){'324\Release\net35'}else{'Release\net35'}
$source=Join-Path $projectRoot ('Runtime\bin\'+$suffix)
$harness=Join-Path $projectRoot ('IntegrationGame\bin\'+$suffix+'\HundredTiles.IntegrationGame.dll')
$target=Join-Path $fixture 'UserData\Addons\Mods\HundredTiles'
$names=@('HundredTiles.Core.dll','HundredTiles.ExperimentalPatch.dll','HundredTiles.Runtime.dll','HundredTiles.SaveBridge.dll','HundredTiles.Utilities.dll','CitiesHarmony.API.dll')
if(-not(Test-Path -LiteralPath $target -PathType Container)){throw 'Existing isolated HundredTiles mod folder missing.'}
foreach($file in @($harness)+@($names | ForEach-Object {Join-Path $source $_})){if(-not(Test-Path -LiteralPath $file -PathType Leaf)){throw "Build output missing: $file"}}
RequireStopped
$backup=Join-Path $fixture ('profile-backups\'+$Profile+'-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $backup -Force | Out-Null
foreach($name in $names){if(Test-Path -LiteralPath (Join-Path $target $name)){Copy-Item -LiteralPath (Join-Path $target $name) -Destination $backup}}
Copy-Item -LiteralPath (Join-Path $fixture 'doorstop_config.ini') -Destination $backup
foreach($name in $names){Copy-Item -LiteralPath (Join-Path $source $name) -Destination (Join-Path $target $name)}
[IO.File]::WriteAllText((Join-Path $fixture 'doorstop_config.ini'),"[UnityDoorstop]`r`nenabled=true`r`ntargetAssembly=$harness`r`n")
Write-Output "Isolated profile switched to $Profile; previous DLLs/config retained at $backup. Other fixture mods and user installation were not changed."
