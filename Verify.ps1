param(
    [string]$GameAssembly = 'C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed\Assembly-CSharp.dll',
    [string]$HarmonyDirectory = (Join-Path $PSScriptRoot 'Libs')
)
$ErrorActionPreference = 'Stop'
$env:GameManaged = Split-Path $GameAssembly
$env:HarmonyDirectory = $HarmonyDirectory
Push-Location $PSScriptRoot
try {
    dotnet build Core -c Release
    if ($LASTEXITCODE) { throw 'Core build failed' }
    dotnet build Diagnostics -c Release
    if ($LASTEXITCODE) { throw 'Diagnostics build failed' }
    dotnet run --project Checks -c Release
    if ($LASTEXITCODE) { throw 'Core checks failed' }
    dotnet run --project SaveChecks -c Release
    if ($LASTEXITCODE) { throw 'Save/restore checks failed' }
    dotnet run --project BridgeChecks -c Release
    if ($LASTEXITCODE) { throw 'Runtime bridge mock checks failed' }
    dotnet build AuditFixture -c Release
    if ($LASTEXITCODE) { throw 'Audit fixture build failed' }
    dotnet run --project Audit -c Release -- AuditFixture/bin/Release/net8.0/HundredTiles.AuditFixture.dll AuditFixture/results
    if ($LASTEXITCODE) { throw 'Audit fixture scan failed' }
    $auditCandidates = Import-Csv AuditFixture/results/constants.csv
    foreach ($auditExpected in @(@('Resolution','1080'),@('WorldWidth','17280'),@('PatchSide','9'),@('NewBuffer','1168561'))) {
        if (-not ($auditCandidates | Where-Object { $_.method -eq $auditExpected[0] -and $_.value -eq $auditExpected[1] })) {
            throw "Missing audit constant: $auditExpected"
        }
    }
    $auditReferences = Import-Csv AuditFixture/results/references.csv
    if (-not ($auditReferences | Where-Object { $_.method -eq 'Call' -and $_.target -eq 'TerrainAuditFixture.Resolution' })) { throw 'Missing call edge' }
    if (($auditCandidates | Where-Object method -eq 'Branch').Count -ne 5) { throw 'IL switch decoding failed' }
    dotnet run --project Audit -c Release -- $GameAssembly reports
    if ($LASTEXITCODE) { throw 'Game audit failed' }
    dotnet run --project PatchBuilder -c Release -- $GameAssembly reports/regenerated-plan.tsv
    if ($LASTEXITCODE) { throw 'Reviewed patch plan generation failed' }
    $trusted=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ExperimentalPatch/ReviewedPlan.tsv')).Replace("`r`n","`n").TrimEnd()
    $generated=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'reports/regenerated-plan.tsv')).Replace("`r`n","`n").TrimEnd()
    if($trusted -ne $generated){throw 'Regenerated plan differs from trusted source; verification will not modify the trusted plan.'}
    dotnet run --project PatchBuilder -c Release -- $GameAssembly ExperimentalPatch/ReviewedPlan.tsv net-audit reports/net-spatial-dependencies.csv
    if ($LASTEXITCODE) { throw 'Net spatial dependency audit failed' }
    dotnet run --project PatchBuilder -c Release -- $GameAssembly ExperimentalPatch/ReviewedPlan.tsv building-audit reports/building-spatial-dependencies.csv
    if ($LASTEXITCODE) { throw 'Building spatial dependency audit failed' }
    dotnet run --project PatchBuilder -c Release -- $GameAssembly ExperimentalPatch/ReviewedPlan.tsv rendering-audit reports/rendering-spatial-candidates.csv
    if ($LASTEXITCODE) { throw 'Rendering dependency audit failed' }
    dotnet run --project PatchBuilder -c Release -- $GameAssembly ExperimentalPatch/ReviewedPlan.tsv spatial-audit reports/remaining-spatial.csv
    if ($LASTEXITCODE) { throw 'Remaining spatial dependency audit failed' }
    dotnet run --project PatchBuilder -c Release -- $GameAssembly ExperimentalPatch/ReviewedPlan.tsv packed-audit reports/packed-axis-reads.csv
    if ($LASTEXITCODE) { throw 'Packed coordinate dependency audit failed' }
    dotnet build ExperimentalPatch -c Release
    if ($LASTEXITCODE) { throw 'Experimental patch build failed' }
    dotnet build SaveBridge -c Release -m:1
    if ($LASTEXITCODE) { throw 'Save bridge build failed' }
    dotnet build Utilities -c Release -m:1
    if ($LASTEXITCODE) { throw 'Utility implementation build failed' }
    dotnet build Runtime -c Release -m:1
    if ($LASTEXITCODE) { throw 'Runtime entry build failed' }
    dotnet build RuntimeChecks -c Release -m:1
    if ($LASTEXITCODE) { throw 'Runtime connection check build failed' }
    dotnet build IntegrationGame -c Release -m:1
    if ($LASTEXITCODE) { throw 'Isolated Unity verification harness build failed' }
    & "$PSScriptRoot\RuntimeChecks\bin\Release\net35\HundredTiles.RuntimeChecks.exe" (Split-Path $GameAssembly) $HarmonyDirectory
    if ($LASTEXITCODE) { throw 'Real-game runtime connection checks failed' }
    dotnet run --project UtilityChecks -c Release -- $GameAssembly
    if ($LASTEXITCODE) { throw 'Utility save and serializer checks failed' }
    dotnet run --project ServiceChecks -c Release
    if ($LASTEXITCODE) { throw 'Expanded service simulation checks failed' }
    dotnet run --project PatchChecks -c Release -- $GameAssembly
    if ($LASTEXITCODE) { throw 'Harmony transformation checks failed' }
    Write-Output 'PASS: builds, migration/save/transport checks, bridge mock lifecycle, game IL scan and reviewed Harmony transformations.'
}
finally { Pop-Location }
