param(
 [Parameter(Mandatory=$true)][string]$ModDll,
 [Parameter(Mandatory=$true)][string]$HelperDll,
 [Parameter(Mandatory=$true)][string]$GameManaged,
 [string]$BackupRoot=(Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'HundredTiles\CompatibilityBackups')
)
$ErrorActionPreference='Stop'
if(Get-Process Cities -ErrorAction SilentlyContinue){throw 'Close Cities: Skylines before replacing a DLL.'}
$original=(Get-FileHash -LiteralPath $ModDll -Algorithm SHA256).Hash
$versions=@{
 'D65C0618D23D398DB27D98B750AA66F552E8EE10E2982DF3316B843B27F46474'='9262776B14E34D2F8E23B3F59D4499E042139FDE8B06997E38031D627D0D5AC5'
 '4C37C9B79102ABC257A56E46C33AF01131855134936378A2C32AD0A767EFA93F'='700700FA3C71413F4A695594599F7D7C0CC2A2B71CB5A54FE8D5DE2F5222071C'
}
if($versions.Values -contains $original){Write-Output 'Reviewed startup repair already installed.';return}
if(-not $versions.ContainsKey($original)){throw 'Unknown input version; no changes made.'}
$stage=Join-Path ([IO.Path]::GetTempPath()) ('HundredTiles-startup-repair-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
$output=Join-Path $stage ([IO.Path]::GetFileName($ModDll))
dotnet (Join-Path $PSScriptRoot 'bin\Release\net10.0\NetworkRenewPatcher.dll') --startup-repair $ModDll $output $HelperDll $GameManaged
if($LASTEXITCODE){throw 'Startup repair tool failed; installed DLL unchanged.'}
if((Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash -ne $versions[$original]){throw 'Output differs from the reviewed native-test version; installed DLL unchanged.'}
New-Item -ItemType Directory -Force -Path $BackupRoot | Out-Null
$backup=Join-Path $BackupRoot ($original+'.dll')
if(Test-Path -LiteralPath $backup){if((Get-FileHash -LiteralPath $backup).Hash -ne $original){throw 'Existing backup hash differs.'}}
else{Copy-Item -LiteralPath $ModDll -Destination $backup}
Copy-Item -LiteralPath $output -Destination $ModDll -Force
if((Get-FileHash -LiteralPath $ModDll).Hash -ne $versions[$original]){throw 'Installed DLL verification failed; original is backed up.'}
Write-Output ('Installed reviewed startup repair. Original backup: '+$backup)
