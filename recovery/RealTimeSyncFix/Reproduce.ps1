param([Parameter(Mandatory=$true)][string]$InputRuntime,[Parameter(Mandatory=$true)][string]$OutputRuntime)
$ErrorActionPreference='Stop'
dotnet build "$PSScriptRoot\PatchResource.csproj" -c Release --locked-mode
if($LASTEXITCODE -ne 0){throw 'Build failed'}
& "$PSScriptRoot\bin\Release\net10.0\PatchResource.exe" $InputRuntime "$PSScriptRoot\RealTimeSync-vNext.tsv" $OutputRuntime
if($LASTEXITCODE -ne 0){throw 'Patch failed'}
