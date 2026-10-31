param([string]$DataRoot=(Join-Path $env:LOCALAPPDATA 'Colossal Order\Cities_Skylines'))
$ErrorActionPreference='Stop'
if(Get-Process Cities -ErrorAction SilentlyContinue){throw 'Close Cities: Skylines before installing.'}
$package=$PSScriptRoot
$profiles=Join-Path $DataRoot 'HundredTiles-profiles'
$local=Join-Path $DataRoot 'Addons\Mods\HundredTiles'
$workshop='C:\Program Files (x86)\Steam\steamapps\workshop\content\255710\3818239060'
foreach($profile in @('100','324')){
 $bundle=Join-Path $package ('profiles\'+$profile)
 & (Join-Path $package 'HundredTiles.ProfileSwitch.exe') validate $bundle
 if($LASTEXITCODE -ne 0){throw "Invalid profile bundle $profile"}
}
$initial='324'
if(Test-Path (Join-Path $local 'HundredTiles.Core.dll')){
 $hash=(Get-FileHash (Join-Path $local 'HundredTiles.Core.dll')).Hash
 if($hash -eq (Get-FileHash (Join-Path $package 'profiles\100\HundredTiles.Core.dll')).Hash){$initial='100'}
 elseif($hash -ne (Get-FileHash (Join-Path $package 'profiles\324\HundredTiles.Core.dll')).Hash){throw 'Existing core does not match either preserved profile. Installation stopped.'}
}
$targets=@($local)
if(Test-Path (Join-Path $workshop 'HundredTiles.Runtime.dll')){$targets+=@($workshop)}
$backup=Join-Path $DataRoot ('HundredTiles-profile-backups\selector-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $profiles,$backup | Out-Null
for($i=0;$i -lt $targets.Count;$i++){
 New-Item -ItemType Directory -Force (Join-Path $backup $i) | Out-Null
 if(Test-Path $targets[$i]){Get-ChildItem $targets[$i] -Filter '*.dll' | Copy-Item -Destination (Join-Path $backup $i)}
}
foreach($profile in @('100','324')){New-Item -ItemType Directory -Force (Join-Path $profiles $profile) | Out-Null;Copy-Item (Join-Path $package ('profiles\'+$profile+'\*')) -Destination (Join-Path $profiles $profile) -Force}
Copy-Item (Join-Path $package 'HundredTiles.ProfileSwitch.exe') $profiles -Force
$selected=$initial
if((Test-Path (Join-Path $DataRoot 'HundredTiles.startup-profile.txt')) -and (Get-Content (Join-Path $DataRoot 'HundredTiles.startup-profile.txt') -Raw).Trim() -eq 'editor'){$selected='editor'}
[IO.File]::WriteAllText((Join-Path $profiles 'selected.txt'),$selected)
[IO.File]::WriteAllText((Join-Path $profiles 'request.txt'),[Guid]::NewGuid().ToString('N')+"`n"+$selected)
[IO.File]::WriteAllLines((Join-Path $profiles 'targets.txt'),[string[]]$targets)
try{
 foreach($target in $targets){New-Item -ItemType Directory -Force $target | Out-Null;Copy-Item (Join-Path $package ('profiles\'+$initial+'\*.dll')) $target -Force}
}catch{
 for($i=0;$i -lt $targets.Count;$i++){Get-ChildItem (Join-Path $backup $i) -Filter '*.dll' | Copy-Item -Destination $targets[$i] -Force}
 throw
}
[IO.File]::WriteAllText((Join-Path $profiles 'switch-status.txt'),'Installed. Select 324 / 100 / Editor in options, fully exit the game, then wait for Complete before restarting.')
Write-Output "Installed profile selector; active city profile=$initial; backup=$backup"
