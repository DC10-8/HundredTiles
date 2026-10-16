param([switch]$SkipBuild,[ValidateSet('100','324')][string]$MapProfile='100')
$ErrorActionPreference='Stop'
$projectRoot=$PSScriptRoot
$files=@('HundredTiles.Core.dll','HundredTiles.ExperimentalPatch.dll','HundredTiles.SaveBridge.dll','HundredTiles.Utilities.dll','HundredTiles.Runtime.dll','CitiesHarmony.API.dll')
if(-not $SkipBuild){& (Join-Path $projectRoot 'Build.ps1') -MapProfile $MapProfile;if($LASTEXITCODE){throw 'Release build failed.'}}
if($MapProfile-eq '324'){
    $validation=Get-Content -LiteralPath (Join-Path $projectRoot 'docs\324-release-validation.json') -Raw | ConvertFrom-Json
    if($validation.profile-ne 324 -or $validation.nativeSaveReload-ne 'passed' -or @($validation.dlls).Count-ne 6){throw 'Completed 324 release validation is required.'}
    foreach($name in $files){
        $record=@($validation.dlls | Where-Object Name -EQ $name)
        if($record.Count-ne 1){throw ('324 validation entry missing or duplicated: '+$name)}
        $path=Join-Path $projectRoot ('Runtime\bin\324\Release\net35\'+$name)
        if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash-ne $record[0].SHA256){throw ('324 DLL differs from tested release: '+$name)}
    }
}
$staging=Join-Path $projectRoot ('artifacts\package-staging-'+[Guid]::NewGuid().ToString('N'))
$packageName=if($MapProfile-eq '324'){'HundredTiles-324-Beta'}else{'HundredTiles-1.0'}
$packageRoot=Join-Path $staging $packageName
$modRoot=Join-Path $packageRoot 'HundredTiles'
New-Item -ItemType Directory -Force -Path $modRoot | Out-Null
$output=if($MapProfile-eq '324'){'Runtime\bin\324\Release\net35\'}else{'Runtime\bin\Release\net35\'}
foreach($name in $files){Copy-Item -LiteralPath (Join-Path $projectRoot ($output+$name)) -Destination (Join-Path $modRoot $name)}
Copy-Item -LiteralPath (Join-Path $projectRoot 'Utilities\UPSTREAM-LICENSE.txt') -Destination (Join-Path $modRoot 'UPSTREAM-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'Utilities\CitiesHarmony-LICENSE.txt') -Destination (Join-Path $modRoot 'CitiesHarmony-LICENSE.txt')
$readme=if($MapProfile-eq '324'){'docs\Release-324-Beta.ja.md'}else{'README.ja.md'}
Copy-Item -LiteralPath (Join-Path $projectRoot $readme) -Destination (Join-Path $packageRoot 'README.ja.md')
Copy-Item -LiteralPath (Join-Path $projectRoot 'Install.ps1') -Destination (Join-Path $packageRoot 'Install.ps1')
$report=if($MapProfile-eq '324'){'docs\324-development.ja.md'}else{'docs\Release-1.0.ja.md'}
Copy-Item -LiteralPath (Join-Path $projectRoot $report) -Destination (Join-Path $packageRoot '検証記録.ja.md')
if($MapProfile-eq '324'){Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\324-release-validation.json') -Destination (Join-Path $packageRoot 'validation.json')}
$manifest=foreach($name in $files){$item=Get-Item -LiteralPath (Join-Path $modRoot $name);[pscustomobject]@{Name=$name;Length=$item.Length;SHA256=(Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash}}
$manifest | Export-Csv -LiteralPath (Join-Path $packageRoot 'files.csv') -NoTypeInformation -Encoding UTF8
$expected=@('README.ja.md','Install.ps1','検証記録.ja.md','files.csv','HundredTiles\UPSTREAM-LICENSE.txt','HundredTiles\CitiesHarmony-LICENSE.txt')+@($files | ForEach-Object {'HundredTiles\'+$_})
if($MapProfile-eq '324'){$expected+=@('validation.json')}
foreach($item in Get-ChildItem -LiteralPath $packageRoot -File -Recurse){$relative=$item.FullName.Substring($packageRoot.Length+1);if($relative -notin $expected){throw "Unexpected package file: $relative"}}
$zip=Join-Path $projectRoot ('artifacts\'+$packageName+'.zip')
Compress-Archive -LiteralPath $packageRoot -DestinationPath $zip -Force
Get-Item -LiteralPath $zip | Select-Object FullName,Length
