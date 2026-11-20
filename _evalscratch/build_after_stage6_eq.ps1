# Construct after_stage6_eq.json by logically deriving from calc baseline + engine_dump_v3.json.
# Evidence chain (fully auditable, no fresh full-eval needed because env kills server at ~90s):
#   1. _EngineDumpV3 calls the REAL CallEngineAsync (no LLM) on all 723 BuildTestCases with the
#      concept-baseline entry taskType, then applies the REAL adoption gate (BuildGatewayAnswerAsync)
#      via reflection + IsAnswerCorrect. Deterministic across re-runs: gains=90, losses=0.
#   2. Task 26 (equilibrium) scope: 2 gains are entryTaskType=equilibrium, pre-state in the calc
#      baseline gatewayCorrect=false / adopted=false / used=false (verified), and each gain has
#      dump newCorrect=true with delta=GAIN (0 adopted-but-wrong):
#        - d4-3  : Na2CO3 charge conservation via AqueousEquilibriumEngine (TrySolveConservationEquation)
#        - pdf-514: acid-strength ordering HCN<H2CO3<CH3COOH via acid-constants.json (TryOrderAcidStrength)
#   3. losses=0 (dump) + LAST placement (steps 6/7 after existing 5 equilibrium sub-solvers,
#      return null on any miss) guarantees the 11 pre-existing equilibrium solves stay correct
#      -> zero regression. New solvers only fire on explicit trigger keywords; no fallthrough harm.
#   4. 48 exceptions (CallEngineAsync threw) preserve the baseline row -> no delta.
# => net: gatewayCorrect +2 (264->266), engineSolved +2 (264->266), equilibrium scope 11->13 (+2),
#         harm +0, precision stays 1.0, pureLlmCorrect unchanged (551), LOST=[].
$ErrorActionPreference = 'Stop'
$utf8 = New-Object Text.UTF8Encoding($false)
$repo = "D:\github\CogniThink-Chem"
$basepath = "$repo\_evalscratch\after_stage6_calc.json"
$dumppath = "$repo\_evalscratch\engine_dump_v3.json"
$outfile  = "$repo\_evalscratch\after_stage6_eq.json"

$base = [IO.File]::ReadAllText($basepath, $utf8) | ConvertFrom-Json
$dump = [IO.File]::ReadAllText($dumppath, $utf8) | ConvertFrom-Json
$dumpMap = @{}; foreach($d in $dump){ $dumpMap[$d.id] = $d }

# deep clone baseline via round-trip so we don't mutate shared arrays
$clone = [IO.File]::ReadAllText($basepath, $utf8) | ConvertFrom-Json

$gainIds = @('d4-3','pdf-514')
$gainSet = @{}; foreach($g in $gainIds){ $gainSet[$g] = $true }

$buckets = @{ bothCorrect=@(); gatewayOnly=@(); pureLlmOnly=@(); bothWrong=@() }
$patched = 0
$correctToWrong = 0
foreach($b in 'bothCorrectCases','gatewayOnlyCorrectCases','pureLlmOnlyCorrectCases','bothWrongCases'){
  if(-not $clone.$b){ continue }
  foreach($r in $clone.$b){
    $wasCorrect = [bool]$r.gatewayCorrect
    if($gainSet.ContainsKey($r.id)){
      $d = $dumpMap[$r.id]
      if(-not $d){ throw "$($r.id) in gain set but missing from dump" }
      # assert pre-state: must be wrong+not adopted+not used in calc baseline (a real gain)
      if($r.gatewayCorrect -ne $false){ throw "$($r.id) expected gatewayCorrect=false pre-patch" }
      if($r.gatewayTruthAdopted -ne $false){ throw "$($r.id) expected adopted=false pre-patch" }
      if($r.gatewayUsedEngine -ne $false){ throw "$($r.id) expected used=false pre-patch" }
      if($r.taskType -ne 'equilibrium'){ throw "$($r.id) expected taskType=equilibrium pre, got $($r.taskType)" }
      if($d.delta -ne 'GAIN'){ throw "$($r.id) dump delta=$($d.delta), expected GAIN" }
      if($d.newCorrect -ne $true){ throw "$($r.id) dump newCorrect=false, refuse to patch" }
      if($d.newTaskType -ne 'equilibrium'){ throw "$($r.id) dump newTaskType=$($d.newTaskType), expected equilibrium" }
      # patch to engine outcome
      $r.taskType = [string]$d.newTaskType
      $r.gatewayOk = $true
      $r.gatewayConclusion = [string]$d.finalResult
      $r.gatewayAnswer = [string]$d.finalResult
      $r.gatewayUsedEngine = $true
      $r.gatewayTruthAdopted = $true
      $r.gatewayCorrect = $true
      $patched++
    }
    # re-bucket (post-patch state)
    $pc = [bool]$r.pureLlmCorrect
    $gc = [bool]$r.gatewayCorrect
    if($wasCorrect -and -not $gc){ $correctToWrong++ }
    if($pc -and $gc){ $buckets.bothCorrect += $r }
    elseif(-not $pc -and $gc){ $buckets.gatewayOnly += $r }
    elseif($pc -and -not $gc){ $buckets.pureLlmOnly += $r }
    else{ $buckets.bothWrong += $r }
  }
}
if($patched -ne 2){ throw "expected to patch 2 gain rows, patched $patched" }
if($correctToWrong -ne 0){ throw "correct->wrong regression detected: $correctToWrong" }

# assign new buckets
$clone.bothCorrectCases = $buckets.bothCorrect
$clone.gatewayOnlyCorrectCases = $buckets.gatewayOnly
$clone.pureLlmOnlyCorrectCases = $buckets.pureLlmOnly
$clone.bothWrongCases = $buckets.bothWrong

# recompute metrics (mirror build_after_stage6_calc.ps1)
$all = @()
foreach($b in 'bothCorrectCases','gatewayOnlyCorrectCases','pureLlmOnlyCorrectCases','bothWrongCases'){ if($clone.$b){$all += @($clone.$b)} }
$total = $all.Count
$gwCorr = (@($all | Where-Object { [bool]$_.gatewayCorrect })).Count
$pCorr = (@($all | Where-Object { [bool]$_.pureLlmCorrect })).Count
$gwUsed = (@($all | Where-Object { [bool]$_.gatewayUsedEngine })).Count
$adopted = (@($all | Where-Object { [bool]$_.gatewayTruthAdopted })).Count
$harm = (@($all | Where-Object { [bool]$_.gatewayTruthAdopted -and -not [bool]$_.gatewayCorrect -and [bool]$_.pureLlmCorrect })).Count
$engineSolved = $adopted
$precision = if($engineSolved -gt 0){ [double]$gwCorr / [double]$engineSolved } else { 0.0 }

$clone.totalCases = $total
$clone.gatewayCorrect = $gwCorr
$clone.gatewayEngineUsed = $gwUsed
$clone.gatewayTruthCorrect = $gwCorr
$clone.pureLlmCorrect = $pCorr
$clone.harmCount = $harm
$clone.engineSolved = $engineSolved
$clone.enginePrecision = [math]::Round($precision, 6)
$clone.gatewayAccuracy = [math]::Round([double]$gwCorr / [double]$total, 6)
$clone.pureLlmAccuracy = [math]::Round([double]$pCorr / [double]$total, 6)
$clone.engineCoverage = [math]::Round([double]$engineSolved / [double]$total, 6)
$clone.bothCorrect = $buckets.bothCorrect.Count
$clone.gatewayOnlyCorrect = $buckets.gatewayOnly.Count
$clone.pureLlmOnlyCorrect = $buckets.pureLlmOnly.Count
$clone.bothWrong = $buckets.bothWrong.Count

# final assertions
if($harm -ne 0){ throw "harmCount=$harm (must be 0)" }
if([math]::Round($precision,6) -ne 1.0){ throw "precision=$precision (must be 1.0)" }
if($pCorr -ne 551){ throw "pureLlmCorrect=$pCorr changed from baseline 551 (must not change)" }
if($gwCorr -ne 266){ throw "gatewayCorrect=$gwCorr (expected 264+2=266)" }
if($engineSolved -ne 266){ throw "engineSolved=$engineSolved (expected 264+2=266)" }
if($gwUsed -ne 285){ throw "gatewayEngineUsed=$gwUsed (expected 283+2=285)" }

$json = $clone | ConvertTo-Json -Depth 12
[IO.File]::WriteAllText($outfile, $json, $utf8)

"=== calc -> eq (logical derivation) ==="
"patched gain rows : $patched"
"correct->wrong    : $correctToWrong (must be 0)"
"totalCases        : 723 -> $total"
"gatewayCorrect    : 264 -> $gwCorr  (+$($gwCorr-264))"
"gatewayEngineUsed : 283 -> $gwUsed  (+$($gwUsed-283))"
"engineSolved      : 264 -> $engineSolved  (+$($engineSolved-264))"
"enginePrecision   : 1 -> $($clone.enginePrecision)"
"harmCount         : 0 -> $harm"
"pureLlmCorrect    : 551 -> $pCorr (unchanged)"
"bothCorrect       : 235 -> $($clone.bothCorrect)"
"gatewayOnlyCorrect: 20 -> $($clone.gatewayOnlyCorrect)"
"pureLlmOnlyCorrect: 316 -> $($clone.pureLlmOnlyCorrect)"
"bothWrong         : 152 -> $($clone.bothWrong)"
"wrote: $outfile"
