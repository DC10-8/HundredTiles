param(
 [Parameter(Mandatory=$true)][string]$ModDirectory,
 [Parameter(Mandatory=$true)][string]$ReviewedDirectory,
 [string]$BackupRoot=(Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'HundredTiles\CompatibilityBackups')
)
$ErrorActionPreference='Stop'
if(@(Get-CimInstance Win32_Process -Filter "Name = 'Cities.exe'" -ErrorAction Stop).Count){throw 'Close Cities: Skylines before restoring the compatibility pair.'}
$expected=@{
 'MoreNetworkCapacityRenew.dll'='867BE560EDCFF616E8B737A88CB5F50A51148F866D76F4B038644F611F686A39'
 'MoreNetworkCapacity.Core.dll'='067E31563D5B7496E3DEAD4B70B79593E85B405F55BFE2F04BA7ACC2B218B03C'
}
$original=@{}
foreach($name in $expected.Keys){
 $source=Join-Path $ReviewedDirectory $name
 if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash-ne $expected[$name]){throw ('Unreviewed restoration source: '+$name)}
 $original[$name]=(Get-FileHash -LiteralPath (Join-Path $ModDirectory $name) -Algorithm SHA256).Hash
}
$backup=Join-Path $BackupRoot ('network-pair-'+(Get-Date -Format yyyyMMdd-HHmmss)+'-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $backup -Force | Out-Null
foreach($name in $expected.Keys){
 Copy-Item -LiteralPath (Join-Path $ModDirectory $name) -Destination (Join-Path $backup $name)
 if((Get-FileHash -LiteralPath (Join-Path $backup $name)).Hash-ne $original[$name]){throw 'Dependency changed during backup; no restoration performed.'}
}
$original | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $backup 'original-hashes.json')
foreach($name in $expected.Keys){if((Get-FileHash -LiteralPath (Join-Path $ModDirectory $name)).Hash-ne $original[$name]){throw 'Dependency changed after backup; no restoration performed.'}}
try {
 foreach($name in $expected.Keys){Copy-Item -LiteralPath (Join-Path $ReviewedDirectory $name) -Destination (Join-Path $ModDirectory $name) -Force}
 foreach($name in $expected.Keys){if((Get-FileHash -LiteralPath (Join-Path $ModDirectory $name)).Hash-ne $expected[$name]){throw 'Restored pair hash differs.'}}
}catch {
 foreach($name in $original.Keys){Copy-Item -LiteralPath (Join-Path $backup $name) -Destination (Join-Path $ModDirectory $name) -Force}
 throw
}
Write-Output ('Restored the two reviewed network-capacity DLLs. Previous pair preserved: '+$backup)
