param([string]$GameRoot='C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines',[ValidateSet('Auto','Create','Reload','BaseEntryCreate','BaseEntryReload','Graphics','OuterCreate','OuterReload','OuterGraphics','SupplyCreate','SupplyReload','SupplyEntry','SupplyEntrySave','SupplyEntryReload','SupplyEntryGraphics','LandscapeEntryCreate','LandscapeEntryReload','LandscapeEntryFresh')][string]$Mode='Auto',[switch]$Options,[switch]$WaterTextures)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot
$testRoot=Join-Path $projectRoot 'artifacts\isolated-game'
if (-not (Test-Path -LiteralPath (Join-Path $testRoot 'isolated-test.marker'))) { throw 'Prepared isolated test marker missing.' }
if (Get-Process Cities -ErrorAction SilentlyContinue) { throw 'An existing Cities process is running. No second game will be started.' }
if (-not (Test-Path -LiteralPath (Join-Path $testRoot 'Cities_Data\Managed\Assembly-CSharp.dll'))) { throw 'Isolated game data is not prepared.' }
if (-not (Get-Process steam -ErrorAction SilentlyContinue)) { Start-Process -FilePath (Join-Path (Split-Path (Split-Path (Split-Path $GameRoot))) 'steam.exe') -ArgumentList '-silent' -WindowStyle Hidden }
$startInfo=New-Object System.Diagnostics.ProcessStartInfo
$startInfo.FileName=Join-Path $testRoot 'Cities.exe'
$startInfo.WorkingDirectory=$testRoot
$startInfo.UseShellExecute=$false
$startInfo.EnvironmentVariables['HUNDREDTILES_TEST_ROOT']=$testRoot
$startInfo.EnvironmentVariables['SteamAppId']='255710'
$startInfo.EnvironmentVariables['HUNDREDTILES_TEST_MODE']=$Mode
if($env:HUNDREDTILES_TEST_COMPAT -eq '1'){$startInfo.EnvironmentVariables['HUNDREDTILES_TEST_COMPAT']='1'}
if($env:HUNDREDTILES_TEST_TERRAIN -eq '1'){$startInfo.EnvironmentVariables['HUNDREDTILES_TEST_TERRAIN']='1'}
if($env:HUNDREDTILES_TEST_CONSTRUCTION -eq '1'){$startInfo.EnvironmentVariables['HUNDREDTILES_TEST_CONSTRUCTION']='1'}
if($env:HUNDREDTILES_TEST_TERRAIN_REWRITE -eq '1'){$startInfo.EnvironmentVariables['HUNDREDTILES_TEST_TERRAIN_REWRITE']='1'}
if($env:HUNDREDTILES_TEST_SEA -eq '1'){$startInfo.EnvironmentVariables['HUNDREDTILES_TEST_SEA']='1'}
if($env:HUNDREDTILES_TEST_TAXI -eq '1'){$startInfo.EnvironmentVariables['HUNDREDTILES_TEST_TAXI']='1'}
if($env:HUNDREDTILES_TEST_REVIEWED -eq '1'){$startInfo.EnvironmentVariables['HUNDREDTILES_TEST_REVIEWED']='1'}
if($env:HUNDREDTILES_TEST_SAVE_SAFETY -eq '1'){$startInfo.EnvironmentVariables['HUNDREDTILES_TEST_SAVE_SAFETY']='1'}
if($env:HUNDREDTILES_TEST_NETWORK_CAPACITY -eq '1'){$startInfo.EnvironmentVariables['HUNDREDTILES_TEST_NETWORK_CAPACITY']='1'}
if($env:HUNDREDTILES_TEST_SIMULATION_REPAIRS -eq '1'){$startInfo.EnvironmentVariables['HUNDREDTILES_TEST_SIMULATION_REPAIRS']='1'}
if($Options){$startInfo.EnvironmentVariables['HUNDREDTILES_TEST_OPTIONS']='1'}
if($env:HUNDREDTILES_TEST_STARTUP_COPY -eq '1'){$startInfo.EnvironmentVariables['HUNDREDTILES_TEST_STARTUP_COPY']='1'}
if($env:HUNDREDTILES_TEST_STARTUP_REPAIRS -eq '1'){$startInfo.EnvironmentVariables['HUNDREDTILES_TEST_STARTUP_REPAIRS']='1'}
if($WaterTextures){$startInfo.EnvironmentVariables['HUNDREDTILES_TEST_WATER']='1'}
$startInfo.Arguments='-noWorkshop -windowed -screen-width 960 -screen-height 540 -logFile "'+(Join-Path $testRoot 'unity.log')+'"'
$startInfo.WindowStyle=[System.Diagnostics.ProcessWindowStyle]::Hidden
$gameProcess=[System.Diagnostics.Process]::Start($startInfo)
Set-Content -LiteralPath (Join-Path $testRoot 'process.id') -Value $gameProcess.Id
Write-Output "Isolated Unity verification started: PID $($gameProcess.Id)."

