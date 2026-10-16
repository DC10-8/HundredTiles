param([string]$Destination=(Join-Path $PSScriptRoot ('artifacts\source-'+[Guid]::NewGuid().ToString('N'))))
$ErrorActionPreference='Stop'
$destinationPath=[IO.Path]::GetFullPath($Destination)
if(Test-Path -LiteralPath $destinationPath){throw 'Use a new export directory to avoid mixing private or generated files.'}
New-Item -ItemType Directory -Path $destinationPath | Out-Null
$directories=@('Core','ExperimentalPatch','SaveBridge','Utilities','Runtime','Checks','SaveChecks','BridgeChecks','UtilityChecks','ServiceChecks','PatchChecks','RuntimeChecks','IntegrationGame','Audit','AuditFixture','Diagnostics','PatchBuilder','MetadataFixture','NetworkRenewPatcher','StartupRepairHelper','TextureChecks','docs','Workshop','.github')
foreach($directory in $directories){$source=Join-Path $PSScriptRoot $directory;foreach($file in Get-ChildItem -LiteralPath $source -Recurse -File){$relative=$file.FullName.Substring($source.Length+1);if($relative -match '(^|[\\/])(bin|obj|results)([\\/]|$)'){continue};if($file.Extension -notin @('.cs','.csproj','.tsv','.txt','.md','.ps1','.png','.jpg','.json','.yml','.csv')){continue};$target=Join-Path $destinationPath (Join-Path $directory $relative);New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null;Copy-Item -LiteralPath $file.FullName -Destination $target}}
foreach($name in @('.gitignore','NuGet.Config','HundredTiles.sln','global.json','Directory.Build.props','Build.ps1','Package.ps1','Install.ps1','Restore-ReviewedNetworkCapacity.ps1','Export-Source.ps1','README.md','README.ja.md','LICENSE','THIRD-PARTY-NOTICES.md','Verify.ps1','Verify-Reproducible.ps1')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $destinationPath}
if(Get-ChildItem -LiteralPath $destinationPath -Recurse -File | Where-Object {$_.Extension -in @('.dll','.exe','.crp','.log') -or $_.Name -eq 'Directory.Build.user.props'}){throw 'Source export contains forbidden private/generated files.'}
Write-Output $destinationPath

