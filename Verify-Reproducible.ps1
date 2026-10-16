param(
    [Parameter(Mandatory=$true)][string]$GameManaged,
    [Parameter(Mandatory=$true)][string]$HarmonyDirectory,
    [ValidateSet('100','324')][string]$MapProfile='100'
)
$ErrorActionPreference='Stop'
$resultRoot=Join-Path $PSScriptRoot ('artifacts/reproducible-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $resultRoot | Out-Null
$copies=@('source-a','different-path/source-b')
$records=@()
foreach($copy in $copies){
    $target=Join-Path $resultRoot $copy
    & (Join-Path $PSScriptRoot 'Export-Source.ps1') -Destination $target
    & (Join-Path $target 'Build.ps1') -GameManaged $GameManaged -HarmonyDirectory $HarmonyDirectory -MapProfile $MapProfile
    $output=if($MapProfile-eq '324'){'Runtime/bin/324/Release/net35'}else{'Runtime/bin/Release/net35'}
    $records+=,@(Get-ChildItem (Join-Path $target $output) -Filter '*.dll' | Where-Object {$_.Name -in @('HundredTiles.Core.dll','HundredTiles.ExperimentalPatch.dll','HundredTiles.Runtime.dll','HundredTiles.SaveBridge.dll','HundredTiles.Utilities.dll','CitiesHarmony.API.dll')} | Sort-Object Name | ForEach-Object {[pscustomobject]@{Name=$_.Name;SHA256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash}})
}
if($records[0].Count -ne 6 -or $records[1].Count -ne 6){throw 'Missing production DLLs.'}
for($i=0;$i -lt 6;$i++){if($records[0][$i].Name -ne $records[1][$i].Name -or $records[0][$i].SHA256 -ne $records[1][$i].SHA256){throw ('Non-reproducible DLL: '+$records[0][$i].Name)}}
$records[0] | ConvertTo-Json | Set-Content (Join-Path $resultRoot 'dll-hashes.json') -Encoding utf8
Write-Output ('PASS: two clean source exports in different directories produced identical six DLLs. Evidence: '+$resultRoot)
