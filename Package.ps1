param([switch]$SkipBuild)
$ErrorActionPreference='Stop'
$projectRoot=$PSScriptRoot
if(-not $SkipBuild){dotnet build (Join-Path $projectRoot 'Runtime') -c Release -m:1;if($LASTEXITCODE){throw 'Release build failed.'}}
$packageRoot=Join-Path $projectRoot 'artifacts\HundredTiles-1.0'
$modRoot=Join-Path $packageRoot 'HundredTiles'
New-Item -ItemType Directory -Force -Path $modRoot | Out-Null
$files=@('HundredTiles.Core.dll','HundredTiles.ExperimentalPatch.dll','HundredTiles.SaveBridge.dll','HundredTiles.Utilities.dll','HundredTiles.Runtime.dll','CitiesHarmony.API.dll')
foreach($name in $files){Copy-Item -LiteralPath (Join-Path $projectRoot ('Runtime\bin\Release\net35\'+$name)) -Destination (Join-Path $modRoot $name)}
Copy-Item -LiteralPath (Join-Path $projectRoot 'Utilities\UPSTREAM-LICENSE.txt') -Destination (Join-Path $modRoot 'UPSTREAM-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'Utilities\CitiesHarmony-LICENSE.txt') -Destination (Join-Path $modRoot 'CitiesHarmony-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.ja.md') -Destination (Join-Path $packageRoot 'README.ja.md')
Copy-Item -LiteralPath (Join-Path $projectRoot 'Install.ps1') -Destination (Join-Path $packageRoot 'Install.ps1')
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\Release-1.0.ja.md') -Destination (Join-Path $packageRoot '検証記録.ja.md')
$manifest=foreach($name in $files){$item=Get-Item -LiteralPath (Join-Path $modRoot $name);[pscustomobject]@{Name=$name;Length=$item.Length;SHA256=(Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash}}
$manifest | Export-Csv -LiteralPath (Join-Path $packageRoot 'files.csv') -NoTypeInformation -Encoding UTF8
$expected=@('README.ja.md','Install.ps1','検証記録.ja.md','files.csv','HundredTiles\UPSTREAM-LICENSE.txt','HundredTiles\CitiesHarmony-LICENSE.txt')+@($files | ForEach-Object {'HundredTiles\'+$_})
foreach($item in Get-ChildItem -LiteralPath $packageRoot -File -Recurse){$relative=$item.FullName.Substring($packageRoot.Length+1);if($relative -notin $expected){throw "Unexpected package file: $relative"}}
$zip=Join-Path $projectRoot 'artifacts\HundredTiles-1.0.zip'
Compress-Archive -LiteralPath $packageRoot -DestinationPath $zip -Force
Get-Item -LiteralPath $zip | Select-Object FullName,Length
