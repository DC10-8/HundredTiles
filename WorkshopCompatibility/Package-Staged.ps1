param([string]$Destination=(Join-Path $PSScriptRoot '..\artifacts\seven-mod-candidate'))
$ErrorActionPreference='Stop'
$destinationPath=[IO.Path]::GetFullPath($Destination)
New-Item -ItemType Directory -Force $destinationPath | Out-Null
foreach($profile in @('100','324')){
 $source=Join-Path $PSScriptRoot ('staged\'+$profile)
 $bundle=Join-Path $destinationPath ('profiles\'+$profile)
 New-Item -ItemType Directory -Force $bundle | Out-Null
 $names=@('CitiesHarmony.API.dll','HundredTiles.Core.dll','HundredTiles.ExperimentalPatch.dll','HundredTiles.Runtime.dll','HundredTiles.SaveBridge.dll','HundredTiles.Utilities.dll','HundredTiles.Profiles.dll','HundredTiles.WorkshopCompatibility.dll')
 foreach($name in $names){Copy-Item -LiteralPath (Join-Path $source $name) -Destination $bundle -Force}
 $lines=$names | Sort-Object | ForEach-Object {$_+"`t"+(Get-FileHash -LiteralPath (Join-Path $bundle $_)).Hash}
 [IO.File]::WriteAllLines((Join-Path $bundle 'files.tsv'),[string[]]$lines)
 & (Join-Path $PSScriptRoot 'HundredTiles.ProfileSwitch.exe') validate $bundle
 if($LASTEXITCODE -ne 0){throw 'Invalid candidate bundle'}
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'HundredTiles.ProfileSwitch.exe') -Destination $destinationPath -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\ProfileSwitcher\Install.ps1') -Destination $destinationPath -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'VERIFICATION.md') -Destination $destinationPath -Force
$map=@("926B7CDF7AFDDC5A81F2A5FCC25F3A69351EB17D614C0BF436B7AECEB9419320`t100","A522CF18EC5B23BA07256AA14A9B1848A9BC322FE973F687857B3BFE0929B732`t324")
foreach($profile in @('100','324')){$map+=((Get-FileHash -LiteralPath (Join-Path $destinationPath ('profiles\'+$profile+'\HundredTiles.Core.dll'))).Hash+"`t"+$profile)}
[IO.File]::WriteAllLines((Join-Path $destinationPath 'core-profiles.tsv'),[string[]]$map)
Write-Output $destinationPath
