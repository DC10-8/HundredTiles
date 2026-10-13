param([Parameter(Mandatory=$true)][string]$ModDirectory,[Parameter(Mandatory=$true)][string]$HarmonyDll)
$ErrorActionPreference='Stop'
if(Get-Process Cities -ErrorAction SilentlyContinue){throw 'Close Cities: Skylines before applying the patch.'}
$original=Join-Path ([IO.Path]::GetFullPath($ModDirectory)) 'MoreNetworkCapacityRenew.dll'
$expected='C4334AD302F232644E147EC6FC2397C0C328EF26A02BCA8E1CE71A5F0183338B'
$legacy='33977CE2A2C2D83AA14BFB4432244EBEBA2F9ACF29FDB8B9CAD7E53A4A0C5A07'
$patched='83E318E1ED01D4C000EC268A31767EF295A2357814C025DAD6A8E8E631D5C832'
$hash=(Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash
if($hash -eq '867BE560EDCFF616E8B737A88CB5F50A51148F866D76F4B038644F611F686A39'){Write-Output 'Reviewed current Renew compatibility patch is already installed.';return}
if($hash -eq 'BB664EAC91039216A1BA65C70EC5C5AAD06E50BBA46CFF2F5CCEAD3C742B0E18'){
    $expected=$hash
    $patched='867BE560EDCFF616E8B737A88CB5F50A51148F866D76F4B038644F611F686A39'
}
if($hash -eq $patched){Write-Output 'Reviewed compatibility patch is already installed.';return}
if($hash -ne $expected -and $hash -ne $legacy){throw 'Unknown Renew version; original DLL was not changed.'}
$backupRoot=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'HundredTiles\CompatibilityBackups'
New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null
$backup=Join-Path $backupRoot ($expected+'.dll')
if(Test-Path -LiteralPath $backup){if((Get-FileHash -LiteralPath $backup).Hash -ne $expected){throw 'Backup hash differs.'}}
elseif($hash -eq $expected){Copy-Item -LiteralPath $original -Destination $backup}
else{throw 'The original Renew backup is required to update the previous compatibility patch.'}
if($hash -eq $legacy){$previous=Join-Path $backupRoot ($legacy+'.dll');if(!(Test-Path -LiteralPath $previous)){Copy-Item -LiteralPath $original -Destination $previous}}
$output=Join-Path ([IO.Path]::GetTempPath()) ('renew-'+[Guid]::NewGuid().ToString('N')+'.dll')
dotnet run --project (Join-Path $PSScriptRoot 'NetworkRenewPatcher.csproj') -c Release -- $backup $output $HarmonyDll
if($LASTEXITCODE -ne 0){throw 'Patcher failed; original DLL was not changed.'}
if((Get-FileHash -LiteralPath $output).Hash -ne $patched){throw 'Derived DLL hash differs; original DLL was not changed.'}
Copy-Item -LiteralPath $output -Destination $original -Force
Remove-Item -LiteralPath $output
Write-Output ('Compatibility patch installed. Original backup: '+$backup)
