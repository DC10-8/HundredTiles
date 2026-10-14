param([string]$Source=(Join-Path (Split-Path $PSScriptRoot) 'ExperimentalPatch\ReviewedPlan.tsv'),[string]$Destination=(Join-Path (Split-Path $PSScriptRoot) 'ExperimentalPatch\ReviewedPlan324.tsv'))
$ErrorActionPreference='Stop'
# Candidate geometry edits only. Original tokens, signatures, instruction indices
# and expected native operands remain pinned. Prototype coverage forbids activation.
$integers=@{299=539;300=540;599=1079;600=1080;1200=2160;1201=2161;1199=2159;10=18;9=17;100=324;99=323;50=90;49=89;59=107;60=108;2399=4319;2400=4320;507=908;508=909;999=1799;1000=1800;998=1798;568=1023;569=1024;500=900;499=899;497=897;498=898;1442401=4669921;1442400=4669920;1441200=4667760;1440000=4665600;4800=8640;4799=8639;90000=291600;360000=1166400;1000000=3240000;8750000=28350000;2500=8100;250000=810000;258064=826281;323761=1048576;3600=11664;5760000=18662400;4327204=18679684;2457599=4423679;4200=13608;160=288;40=127;44=76;15=31;16=32;19=34}
$unchanged=@(0,3696,450,268435455)
$floats=@{1031798784=[single](1.0/32);1051721728=[single](5.5/32);1055283938=[single].25;1055286886=[single].25;1055445411=[single](231.0/909);1120403456=[single]324;1148190720=[single]8640;1176829952=[single]25920;1184235520=[single]34560;940083337=[single](1.0/(32*1920));945281847=[single](1.0/(909*38.25));945449250=[single](1.0/(1024*33.75));945452046=[single](1.0/34560);989921796=[single](1.0/909)}
$fixedFloats=@(-972619776,1083179008,1124532224,1130430464,1130823680,1138819072,1205927936)
$result=[Collections.Generic.List[string]]::new();$changed=0
foreach($line in Get-Content -LiteralPath $Source){
 $parts=$line -split "`t"
 if($parts[0] -eq 'coverage'){$result.Add("coverage`tincomplete");continue}
 if($parts[0] -eq 'edit'){
  $old=[int]$parts[5];$next=$old
  if($parts[3] -eq 'int'){if($integers.ContainsKey($old)){$next=$integers[$old]}elseif($old -notin $unchanged){throw "Unclassified integer role: $line"}}
  elseif($parts[3] -eq 'float'){if($floats.ContainsKey($old)){$next=[BitConverter]::ToInt32([BitConverter]::GetBytes([single]$floats[$old]),0)}elseif($old -notin $fixedFloats){throw "Unclassified float role: $line"}}
  elseif($parts[3] -ne 'local'){throw "Unknown edit kind: $line"}
  if($next -ne $old){$changed++};$parts[5]=[string]$next;$result.Add(($parts -join "`t"));continue
 }
 if($line.StartsWith('#')){$result.Add('# 324-tile candidate; native activation is blocked pending complete verification.');continue}
 $result.Add($line)
}
[IO.File]::WriteAllLines([IO.Path]::GetFullPath($Destination),$result,[Text.UTF8Encoding]::new($false))
"Generated 324 candidate plan: $changed classified replacement changes; original expected operands preserved; activation blocked."

