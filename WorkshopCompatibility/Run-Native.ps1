param([string]$Fixture,[switch]$Reload,[switch]$Resave)
$ErrorActionPreference='Stop'
$fixturePath=[IO.Path]::GetFullPath($Fixture)
if(!(Test-Path -LiteralPath (Join-Path $fixturePath 'isolated-test.marker'))){throw 'Isolation marker missing'}
if(Get-Process Cities -ErrorAction SilentlyContinue){throw 'An existing game is running'}
$si=New-Object Diagnostics.ProcessStartInfo
$si.FileName=Join-Path $fixturePath 'Cities.exe'
$si.WorkingDirectory=$fixturePath
$si.UseShellExecute=$false
$si.WindowStyle='Hidden'
$si.EnvironmentVariables['HUNDREDTILES_SEVEN_ROOT']=$fixturePath
$si.EnvironmentVariables['SteamAppId']='255710'
if($Reload){$si.EnvironmentVariables['HUNDREDTILES_SEVEN_RELOAD']='1'}
if($Resave){$si.EnvironmentVariables['HUNDREDTILES_SEVEN_RESAVE']='1'}
$si.Arguments='-noWorkshop -windowed -screen-width 960 -screen-height 540 -logFile "'+(Join-Path $fixturePath 'unity.log')+'"'
$p=[Diagnostics.Process]::Start($si)
Write-Output ('Isolated PID '+$p.Id)
