param([Parameter(Mandatory=$true)][string]$ModDll,[Parameter(Mandatory=$true)][string]$GameManaged)
$ErrorActionPreference='Stop'
if(Get-Process Cities -ErrorAction SilentlyContinue){throw 'Close Cities: Skylines before applying the repair.'}
$versions=@{
 '92E819A07CFD18C6E76D8A78F32BEDF54A0B091D0E49F2BA2F593F9444F17BBC'='6889FC97C243473E11C982BE0FFE0CC937FD9F47FD78156BB53AAD1C4BB290A1'
 '0519E418B0DDB6A7AB8C717C12B3521B9A38DF7784D9C0E8B49BBB61D02502C0'='2EFE23119D2805F1CFF2DD793B867E8722CC71C1BDD749A4F84707A3A31C29B1'
}
$hash=(Get-FileHash -LiteralPath $ModDll).Hash
if($hash -in $versions.Values){Write-Output 'Reviewed IPT cache repair already installed.';return}
if(-not $versions.ContainsKey($hash)){throw 'Unreviewed IPT version; no files changed.'}
$backupDirectory=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'HundredTiles\CompatibilityBackups'
New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null
$backup=Join-Path $backupDirectory ($hash+'.dll')
if(Test-Path -LiteralPath $backup){if((Get-FileHash -LiteralPath $backup).Hash -ne $hash){throw 'Backup mismatch'}}
else{Copy-Item -LiteralPath $ModDll -Destination $backup}
$output=Join-Path ([IO.Path]::GetTempPath()) ('ipt-node-cache-'+[Guid]::NewGuid().ToString('N')+'.dll')
dotnet run --project (Join-Path $PSScriptRoot 'NetworkRenewPatcher.csproj') -c Release -- --ipt-node-cache $backup $output $GameManaged
if($LASTEXITCODE){throw 'Repair failed; original DLL was not changed.'}
if((Get-FileHash -LiteralPath $output).Hash -ne $versions[$hash]){throw 'Derived DLL mismatch; original DLL was not changed.'}
Copy-Item -LiteralPath $output -Destination $ModDll -Force
Remove-Item -LiteralPath $output
Write-Output ('IPT node cache repair installed. Original backup: '+$backup)
