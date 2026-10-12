param([Parameter(Mandatory=$true)][string]$ModDirectory,[Parameter(Mandatory=$true)][string]$HarmonyDll)
$ErrorActionPreference='Stop'
if(Get-Process Cities -ErrorAction SilentlyContinue){throw 'Close Cities: Skylines before applying the patch.'}
$original=Join-Path ([IO.Path]::GetFullPath($ModDirectory)) 'MoreNetworkCapacityRenew.dll'
$expected='C4334AD302F232644E147EC6FC2397C0C328EF26A02BCA8E1CE71A5F0183338B'
$patched='33977CE2A2C2D83AA14BFB4432244EBEBA2F9ACF29FDB8B9CAD7E53A4A0C5A07'
$hash=(Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash
if($hash -eq $patched){Write-Output 'Reviewed compatibility patch is already installed.';return}
if($hash -ne $expected){throw 'Unknown Renew version; original DLL was not changed.'}
$backupRoot=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'HundredTiles\CompatibilityBackups'
New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null
$backup=Join-Path $backupRoot ($expected+'.dll')
if(Test-Path -LiteralPath $backup){if((Get-FileHash -LiteralPath $backup).Hash -ne $expected){throw 'Backup hash differs.'}}
else{Copy-Item -LiteralPath $original -Destination $backup}
$output=Join-Path ([IO.Path]::GetTempPath()) ('renew-'+[Guid]::NewGuid().ToString('N')+'.dll')
dotnet run --project (Join-Path $PSScriptRoot 'NetworkRenewPatcher.csproj') -c Release -- $original $output $HarmonyDll
if($LASTEXITCODE -ne 0){throw 'Patcher failed; original DLL was not changed.'}
if((Get-FileHash -LiteralPath $output).Hash -ne $patched){throw 'Derived DLL hash differs; original DLL was not changed.'}
Copy-Item -LiteralPath $output -Destination $original -Force
Remove-Item -LiteralPath $output
Write-Output ('Compatibility patch installed. Original backup: '+$backup)
