param([string]$ModsDirectory=(Join-Path $env:LOCALAPPDATA 'Colossal Order\Cities_Skylines\Addons\Mods'))
$ErrorActionPreference='Stop'
if(@(Get-CimInstance Win32_Process -Filter "Name = 'Cities.exe'" -ErrorAction Stop).Count-ne 0){throw 'Cities: Skylinesを終了してから導入してください。'}
$manifest=Import-Csv -LiteralPath (Join-Path $PSScriptRoot 'files.csv')
$allowed=@('HundredTiles.Core.dll','HundredTiles.ExperimentalPatch.dll','HundredTiles.SaveBridge.dll','HundredTiles.Utilities.dll','HundredTiles.Runtime.dll','CitiesHarmony.API.dll')
if($manifest.Count -ne $allowed.Count){throw '配布ファイル一覧が一致しません。'}
foreach($name in $allowed){$record=@($manifest | Where-Object Name -EQ $name);if($record.Count -ne 1){throw '配布ファイル一覧が重複または欠落しています。'};$source=Join-Path $PSScriptRoot ('HundredTiles\'+$name);if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $record[0].SHA256 -or (Get-Item -LiteralPath $source).Length -ne [long]$record[0].Length){throw "配布ファイルの照合に失敗しました: $name"}}
$target=Join-Path ([IO.Path]::GetFullPath($ModsDirectory)) 'HundredTiles'
if(Test-Path -LiteralPath $target){$backupRoot=Join-Path (Split-Path $target -Parent | Split-Path -Parent) 'HundredTiles-backups';New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null;$backup=Join-Path $backupRoot ((Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N'));Copy-Item -LiteralPath $target -Destination $backup -Recurse;Write-Output "既存版のバックアップ: $backup"}
New-Item -ItemType Directory -Force -Path $target | Out-Null
foreach($name in $allowed){Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('HundredTiles\'+$name)) -Destination (Join-Path $target $name)}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'HundredTiles\UPSTREAM-LICENSE.txt') -Destination $target
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'HundredTiles\CitiesHarmony-LICENSE.txt') -Destination $target
Write-Output "導入先: $target"
Write-Output 'Harmonyを導入し、コンテンツマネージャーでHundredTilesを有効化した後、ゲームを再起動してください。'
