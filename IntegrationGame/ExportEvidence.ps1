param([ValidateSet('save','reload')][string]$Phase)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot
$testRoot=Join-Path $projectRoot 'artifacts\isolated-game'
$lines=[IO.File]::ReadAllLines((Join-Path $testRoot 'integration.log'))
$start=-1
for($index=$lines.Length-1;$index -ge 0;$index--){if($lines[$index].Contains('Waiting for first Unity main-thread managed log')){$start=$index;break}}
if($start -lt 0){throw 'Verification run boundary missing.'}
$run=$lines[$start..($lines.Length-1)]
if($run | Where-Object {$_ -match 'FAILURE|BOOT FAILURE|FAIL:'}){throw 'Current verification run failed.'}
$expected=if($Phase -eq 'save'){'Real .crp saved;'}else{'PASS: actual detailed GPU height texture'}
if(-not ($run | Where-Object { $_.Contains($expected) })){throw 'Verification completion marker missing.'}
[IO.File]::WriteAllLines((Join-Path $projectRoot ('reports\release-1.0-unity-'+$Phase+'.log')),$run,[Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath (Join-Path $testRoot 'unity.log') -Destination (Join-Path $projectRoot ('reports\release-1.0-unity-'+$Phase+'-engine.log'))
if($Phase -eq 'reload'){Copy-Item -LiteralPath (Join-Path $testRoot 'added-area.png') -Destination (Join-Path $projectRoot 'reports\release-1.0-added-area.png');Copy-Item -LiteralPath (Join-Path $testRoot 'added-area-close.png') -Destination (Join-Path $projectRoot 'reports\release-1.0-added-area-close.png');Copy-Item -LiteralPath (Join-Path $testRoot 'service-texture.png') -Destination (Join-Path $projectRoot 'reports\release-1.0-service-texture.png')}
Write-Output ('Exported successful current native '+$Phase+' run.')
