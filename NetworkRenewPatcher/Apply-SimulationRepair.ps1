param([Parameter(Mandatory=$true)][string]$Dll)
$ErrorActionPreference='Stop'
if(Get-Process Cities -ErrorAction SilentlyContinue){throw 'Close Cities: Skylines before applying the patch.'}
$inputHash=(Get-FileHash -LiteralPath $Dll).Hash
$pairs=@{
 'E84ED6D3900EB948DFF0B1D8E9E16153DDE712764E42D9FC8D7EA88165030907'='54CD43552683DADD35893158AE14BC07A29F8A0FB4D55F7E04110B2D11FD2D55'
 '4D70FA1E6684E4095F4E6944AC3003F7BBAE6DDEF123BDE72991F96A86D91217'='2CCD18CDBCDEE06313CB2F68102AAA01D008D015F785696162961F26294BEFEA'
}
if($pairs.Values -contains $inputHash){Write-Output 'Reviewed simulation repair is already installed.';return}
if(!$pairs.ContainsKey($inputHash)){throw 'Unknown version; original DLL was not changed.'}
$backupRoot=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'HundredTiles\CompatibilityBackups'
New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null
$backup=Join-Path $backupRoot ($inputHash+'.dll')
if(Test-Path -LiteralPath $backup){if((Get-FileHash -LiteralPath $backup).Hash -ne $inputHash){throw 'Backup hash differs.'}}
else{Copy-Item -LiteralPath $Dll -Destination $backup}
$output=Join-Path ([IO.Path]::GetTempPath()) ('simulation-'+[Guid]::NewGuid().ToString('N')+'.dll')
dotnet run --project (Join-Path $PSScriptRoot 'NetworkRenewPatcher.csproj') -c Release -- --simulation-repair $backup $output
if($LASTEXITCODE -ne 0){throw 'Patcher failed; original DLL was not changed.'}
if((Get-FileHash -LiteralPath $output).Hash -ne $pairs[$inputHash]){throw 'Derived DLL hash differs; original DLL was not changed.'}
Copy-Item -LiteralPath $output -Destination $Dll -Force
Remove-Item -LiteralPath $output
Write-Output ('Simulation repair installed. Original backup: '+$backup)
