param([string]$Zip=(Join-Path (Split-Path $PSScriptRoot) 'artifacts\HundredTiles-1.0.zip'))
$ErrorActionPreference='Stop'
if(Get-Process Cities -ErrorAction SilentlyContinue){throw 'Close the verification game first.'}
$testRoot=Join-Path (Split-Path $PSScriptRoot) ('artifacts\package-check-'+[Guid]::NewGuid().ToString('N'))
Expand-Archive -LiteralPath $Zip -DestinationPath $testRoot
$package=Join-Path $testRoot 'HundredTiles-1.0'
$mods=Join-Path $testRoot 'Mods'
& (Join-Path $package 'Install.ps1') -ModsDirectory $mods
$manifest=Import-Csv -LiteralPath (Join-Path $package 'files.csv')
foreach($record in $manifest){$installed=Join-Path $mods ('HundredTiles\'+$record.Name);if((Get-FileHash -LiteralPath $installed -Algorithm SHA256).Hash -ne $record.SHA256){throw 'Installed DLL differs from package.'}}
Set-Content -LiteralPath (Join-Path $mods 'HundredTiles\existing-note.txt') -Value 'Preserve this existing installation.'
& (Join-Path $package 'Install.ps1') -ModsDirectory $mods
$backups=@(Get-ChildItem -LiteralPath (Join-Path $testRoot 'HundredTiles-backups') -Directory)
if($backups.Count -ne 1 -or -not (Test-Path -LiteralPath (Join-Path $backups[0].FullName 'existing-note.txt'))){throw 'Previous installation backup missing.'}
if(@(Get-ChildItem -LiteralPath $mods -Directory).Count -ne 1){throw 'Backup could be loaded as a duplicate mod.'}
$manifest[0].SHA256='0'*64
$manifest | Export-Csv -LiteralPath (Join-Path $package 'files.csv') -NoTypeInformation -Encoding UTF8
$rejected=$false
try{& (Join-Path $package 'Install.ps1') -ModsDirectory (Join-Path $testRoot 'RejectedMods')}catch{$rejected=$true}
if(-not $rejected -or (Test-Path -LiteralPath (Join-Path $testRoot 'RejectedMods'))){throw 'Damaged package was not rejected before writing.'}
Write-Output 'PASS: extracted release ZIP installed; all six DLL hashes matched; existing installation backed up outside the mod scan; damaged manifest refused before writing.'
