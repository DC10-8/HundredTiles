param(
 [Parameter(Mandatory=$true)][string]$GameManaged,
 [Parameter(Mandatory=$true)][string]$HarmonyDirectory,
 [Parameter(Mandatory=$true)][string]$TrafficManagerDll,
 [Parameter(Mandatory=$true)][string]$BehaviourUpdaterDll
)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$scratch=Join-Path $root ('artifacts\fps-lifecycle-'+[Guid]::NewGuid().ToString('N'))
$managed=Join-Path $scratch 'Managed'
New-Item -ItemType Directory -Force -Path $managed | Out-Null
Copy-Item -Path (Join-Path $GameManaged '*.dll') -Destination $managed
dotnet build (Join-Path $root 'RuntimeChecks') -c Release -m:1 -p:HundredTilesMapProfile=100 -p:HundredTilesTrialRuntime=false "-p:GameManaged=$GameManaged" "-p:HarmonyDirectory=$HarmonyDirectory"
if($LASTEXITCODE){throw 'Runtime check build failed.'}
dotnet run --project (Join-Path $root 'MetadataFixture') -c Release -- (Join-Path $GameManaged 'ColossalManaged.dll') (Join-Path $scratch 'ColossalManaged.dll') fps-ui-base
if($LASTEXITCODE){throw 'UI base fixture failed.'}
Copy-Item (Join-Path $scratch 'ColossalManaged.dll') (Join-Path $managed 'ColossalManaged.dll') -Force
$keys=@('HUNDREDTILES_FIXTURE_SEARCH_DIRS','HUNDREDTILES_REVIEW_EXPORT','HUNDREDTILES_REVIEW_VALIDATE','HUNDREDTILES_ELT_UPDATER','HUNDREDTILES_REVIEW_LIVE')
$before=@{};foreach($key in $keys){$before[$key]=[Environment]::GetEnvironmentVariable($key)}
try {
 $env:HUNDREDTILES_FIXTURE_SEARCH_DIRS=$managed
 $env:HUNDREDTILES_REVIEW_EXPORT=$TrafficManagerDll
 $env:HUNDREDTILES_REVIEW_VALIDATE='1'
 $env:HUNDREDTILES_ELT_UPDATER=$BehaviourUpdaterDll
 foreach($mode in @('fps-ui-start','fps-ui-start-wrongfield','fps-ui-start-false','fps-ui-start-mutated')){
  $fixture=Join-Path $scratch ($mode+'.dll')
  dotnet run --project (Join-Path $root 'MetadataFixture') -c Release -- $TrafficManagerDll $fixture $mode (Join-Path $scratch 'ColossalManaged.dll')
  if($LASTEXITCODE){throw 'Lifecycle fixture failed.'}
  $env:HUNDREDTILES_REVIEW_LIVE=$fixture
  $output=& (Join-Path $root 'RuntimeChecks\bin\Release\net35\HundredTiles.RuntimeChecks.exe') $managed $HarmonyDirectory
  $accepted=$LASTEXITCODE-eq 0
  $expected=$mode-eq 'fps-ui-start'
  $output | Set-Content -LiteralPath (Join-Path $scratch ($mode+'.log'))
  if($accepted-ne $expected){throw ('Unexpected lifecycle verification result: '+$mode)}
  Write-Output ('PASS: '+$mode+' '+$(if($accepted){'accepted'}else{'rejected'}))
 }
 $global:LASTEXITCODE=0
}finally{foreach($key in $keys){[Environment]::SetEnvironmentVariable($key,$before[$key])}}
