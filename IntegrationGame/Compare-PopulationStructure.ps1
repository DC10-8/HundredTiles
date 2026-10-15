param([Parameter(Mandatory=$true)][string]$Expected,[Parameter(Mandatory=$true)][string]$Actual)
$ErrorActionPreference='Stop'
function ReadStructure([string]$path){
    $records=[Collections.Generic.Dictionary[string,string]]::new()
    $stream=[IO.File]::OpenRead([IO.Path]::GetFullPath($path));$reader=[IO.BinaryReader]::new($stream)
    try{
        while($stream.Position-lt $stream.Length){
            $tag=$reader.ReadByte();$id=$reader.ReadInt32();$values=[Collections.Generic.List[string]]::new()
            switch($tag){
                1 { $values.Add($reader.ReadString());for($i=0;$i-lt 4;$i++){$values.Add($reader.ReadSingle().ToString('R',[Globalization.CultureInfo]::InvariantCulture))} }
                2 { $values.Add($reader.ReadString());for($i=0;$i-lt 3;$i++){$values.Add($reader.ReadSingle().ToString('R',[Globalization.CultureInfo]::InvariantCulture))};for($i=0;$i-lt 8;$i++){$values.Add($reader.ReadUInt16().ToString())} }
                3 { $values.Add($reader.ReadString());$values.Add($reader.ReadUInt16().ToString());$values.Add($reader.ReadUInt16().ToString()) }
                4 { $values.Add($reader.ReadUInt16().ToString()) }
                default {throw "Unknown fixture structure tag: $tag"}
            }
            $records.Add("${tag}:$id",[string]::Join(' | ',$values))
        }
    }finally{$reader.Dispose()}
    return ,$records
}
$expectedRecords=ReadStructure $Expected;$actualRecords=ReadStructure $Actual
$differences=0
foreach($key in $expectedRecords.Keys){
    if(-not $actualRecords.ContainsKey($key) -or $actualRecords[$key]-cne $expectedRecords[$key]){
        $differences++;if($differences-le 20){Write-Output "DIFF $key`n  expected: $($expectedRecords[$key])`n  actual:   $($actualRecords[$key])"}
    }
}
foreach($key in $actualRecords.Keys){if(-not $expectedRecords.ContainsKey($key)){$differences++;if($differences-le 20){Write-Output "ADDED $key : $($actualRecords[$key])"}}}
Write-Output "Records expected/actual: $($expectedRecords.Count)/$($actualRecords.Count); differing records: $differences"
if($differences){exit 1}
