# run_eval_resume.ps1
# ----------------------------------------------------------------------------
# Drives CogniThink.TutorBrain.EvalRunner in a loop so a 90s process kill does
# not lose progress. The runner checkpoints every completed case to a JSONL
# file and skips already-done case ids on restart. This script keeps launching
# the runner until the `checkpoint_<tag>.done` marker appears.
#
# Run this in the BACKGROUND so it outlives foreground timeouts:
#   (start it via run_in_background, or: Start-Process powershell -Arg ...)
#
# Usage:
#   .\_evalscratch\run_eval_resume.ps1 [-Tag current] [-OutDir _evalscratch] [-MaxIterations 50]
# ----------------------------------------------------------------------------
param(
    [string]$Tag = "current",
    [string]$OutDir = "_evalscratch",
    [int]$MaxIterations = 50
)

$ErrorActionPreference = "Continue"

# repoRoot = parent of this script's folder (_evalscratch lives at repo root)
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
Set-Location $repoRoot

# Enable the real LLM (Development appsettings has Enabled=true + real key).
# Remove/override this to run the offline (LLM-disabled) engine dump.
$env:ASPNETCORE_ENVIRONMENT = "Development"

$outDirFull = if ([System.IO.Path]::IsPathRooted($OutDir)) { $OutDir } else { Join-Path $repoRoot $OutDir }
$doneMarker = Join-Path $outDirFull "checkpoint_$Tag.done"
$checkpoint = Join-Path $outDirFull "checkpoint_$Tag.jsonl"

function Get-DoneCount {
    if (Test-Path $checkpoint) {
        return (Get-Content $checkpoint | Measure-Object -Line).Lines
    }
    return 0
}

Write-Host "=== Eval resume driver ==="
Write-Host "repoRoot=$repoRoot"
Write-Host "tag=$Tag  outDir=$outDirFull"
Write-Host "ASPNETCORE_ENVIRONMENT=$env:ASPNETCORE_ENVIRONMENT"
Write-Host "doneMarker=$doneMarker"

# Build once so each iteration is fast and never recompiles mid-eval.
Write-Host "[driver] building runner (Debug)..."
dotnet build "src\CogniThink.TutorBrain.EvalRunner" -c Debug --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Host "[driver] BUILD FAILED (exit $LASTEXITCODE). Aborting."
    exit 2
}

for ($i = 1; $i -le $MaxIterations; $i++) {
    if (Test-Path $doneMarker) {
        Write-Host "[driver] done marker present before iteration $i. Finished."
        break
    }
    $done = Get-DoneCount
    Write-Host "[driver] --- iteration $i/$MaxIterations starting; checkpoint done ~$done ---"

    dotnet run --project "src\CogniThink.TutorBrain.EvalRunner" -c Debug --no-build -- --tag $Tag --out-dir $OutDir
    $rc = $LASTEXITCODE
    Write-Host "[driver] iteration $i finished (exit=$rc)"

    if (Test-Path $doneMarker) {
        Write-Host "[driver] done marker appeared after iteration $i. Finished."
        break
    }
    Write-Host "[driver] no done marker yet; sleeping 3s then resuming from checkpoint..."
    Start-Sleep -Seconds 3
}

if (Test-Path $doneMarker) {
    Write-Host "[driver] SUCCESS: eval completed."
    Write-Host "[driver] snapshot: $(Join-Path $outDirFull "snapshot_$Tag.json")"
    Write-Host "[driver] summary : $(Join-Path $outDirFull "checkpoint_$Tag`_summary.txt")"
    exit 0
}
else {
    Write-Host "[driver] STOPPED after $MaxIterations iterations without done marker."
    Write-Host "[driver] progress so far: $(Get-DoneCount) cases checkpointed. Re-run to continue."
    exit 1
}
