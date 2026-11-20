# Stage 6 (Task 23) verification: classification fix + atomic structure guard widening
# Compares after_stage3f.json (baseline, harm=0 engPrec=1) -> after_stage6_classify.json (new)
# Also checks immediate predecessor after_stage5.json -> new for regression safety.
# Red lines:
#   - global LOST (correct->wrong) empty vs BOTH baseline and immediate predecessor
#   - new harmCount == 0  (no new harm)
#   - enginePrecision not dropped (== 1.0)
#   - gained includes pdf-486 and pdf-533 (clean atomic-structure gains)
#   - broken-key cases pdf-173 / pdf-190 NOT adopted (zero harm, chemistry right but key mismatch)
#   - original 4 atomic cases (pdf-482/487/490/538) stay correct
# Run (PS5 UTF-8 safe):
#   $c=[IO.File]::ReadAllText('D:\github\CogniThink-Chem\_evalscratch\verify_stage6_classify.ps1',[Text.Encoding]::UTF8); iex $c

function B($v) { return ($null -ne $v) -and ($v -is [bool]) -and $v }

function Rows($d) {
    $o = @()
    foreach ($b in 'bothCorrectCases','gatewayOnlyCorrectCases','pureLlmOnlyCorrectCases','bothWrongCases') {
        if ($null -ne $d.$b) { foreach ($c in $d.$b) { $o += $c } }
    }
    return $o
}

function ScopeStats($rows, $tt) {
    $rs = @($rows | Where-Object { $_.taskType -eq $tt })
    $n = $rs.Count
    $g = @($rs | Where-Object { B $_.gatewayCorrect }).Count
    $a = @($rs | Where-Object { B $_.gatewayTruthAdopted }).Count
    $ac = @($rs | Where-Object { (B $_.gatewayTruthAdopted) -and (B $_.gatewayCorrect) }).Count
    $h = @($rs | Where-Object { (B $_.gatewayTruthAdopted) -and (-not (B $_.gatewayCorrect)) -and (B $_.pureLlmCorrect) }).Count
    $u = @($rs | Where-Object { B $_.gatewayUsedEngine }).Count
    $p = if ($a -gt 0) { $ac / $a } else { 0.0 }
    return [pscustomobject]@{ n = $n; gcorr = $g; adopted = $a; adopted_correct = $ac; harm = $h; engUsed = $u; prec = $p }
}

function Flip($baseRows, $newRows) {
    $nh = @{}
    foreach ($r in $newRows) { $nh[$r.id] = $r }
    $lost = @(); $gained = @(); $common = 0
    foreach ($b in $baseRows) {
        if ($nh.ContainsKey($b.id)) {
            $common++
            $cB = B $b.gatewayCorrect
            $cN = B $nh[$b.id].gatewayCorrect
            if ($cB -and -not $cN) { $lost += $b.id }
            if (-not $cB -and $cN) { $gained += $b.id }
        }
    }
    return [pscustomobject]@{ common = $common; lost = $lost; gained = $gained }
}

$utf8 = [System.Text.Encoding]::UTF8
$base = [IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage3f.json", $utf8) | ConvertFrom-Json
$mid  = [IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage5.json",  $utf8) | ConvertFrom-Json
$new  = [IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage6_classify.json", $utf8) | ConvertFrom-Json

$rb = Rows $base
$rm = Rows $mid
$rn = Rows $new

$fb = Flip $rb $rn   # baseline (3f) -> new
$fm = Flip $rm $rn   # immediate predecessor (5) -> new

$globalHarm = @($rn | Where-Object { (B $_.gatewayTruthAdopted) -and (-not (B $_.gatewayCorrect)) -and (B $_.pureLlmCorrect) })

$sa = ScopeStats $rn 'atomic_structure'

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("=== Stage 6 / Task 23 verification: classification + atomic-structure guard ===")
$lines.Add("")
$lines.Add("--- top-level metrics (stage3f -> stage5 -> stage6) ---")
foreach ($k in 'totalCases','gatewayCorrect','gatewayTruthCorrect','gatewayEngineUsed','engineSolved','enginePrecision','harmCount') {
    $lines.Add(("  {0,-20}: {1} -> {2} -> {3}" -f $k, $base.$k, $mid.$k, $new.$k))
}
$lines.Add("")
$lines.Add("--- case-id intersection flips ---")
$lines.Add(("  baseline(3f)<->new : common={0}  LOST={1}  GAINED={2}" -f $fb.common, $fb.lost.Count, $fb.gained.Count))
$lines.Add(("  stage5<->new       : common={0}  LOST={1}  GAINED={2}" -f $fm.common, $fm.lost.Count, $fm.gained.Count))
$lines.Add(("  LOST(3f->6) : [{0}]   (must be empty)" -f ($fb.lost -join ', ')))
$lines.Add(("  LOST(5->6)  : [{0}]   (must be empty)" -f ($fm.lost -join ', ')))
$lines.Add(("  GAINED(3f->6): [{0}]" -f (($fb.gained | Sort-Object) -join ', ')))
$lines.Add(("  GAINED(5->6) : [{0}]" -f (($fm.gained | Sort-Object) -join ', ')))
$lines.Add("")
$lines.Add("--- atomic_structure scope (stage6) ---")
foreach ($k in 'n','gcorr','adopted','adopted_correct','harm','engUsed') {
    $lines.Add(("  {0,-16}: {1}" -f $k, $sa.$k))
}
$lines.Add(("  {0,-16}: {1:F4}" -f 'precision', $sa.prec))
$lines.Add("")
$lines.Add("--- global harm scan (adopted && !correct && pureLlmCorrect) ---")
$lines.Add("  stage6 global harm count = $($globalHarm.Count)")
foreach ($h in $globalHarm) { $lines.Add("    - $($h.id) [$($h.taskType)] concl=$($h.gatewayConclusion)") }
$lines.Add("")
$lines.Add("--- trigger case check (10 atomic-structure triggers) ---")
$nh = @{}; foreach ($r in $rn) { $nh[$r.id] = $r }
$triggers = 'pdf-108','pdf-166','pdf-173','pdf-190','pdf-482','pdf-486','pdf-487','pdf-490','pdf-533','pdf-538'
foreach ($id in $triggers) {
    $r = $nh[$id]
    if ($null -eq $r) { $lines.Add("  $id : MISSING"); continue }
    $ans = "$($r.gatewayAnswer)"; if ($ans.Length -gt 40) { $ans = $ans.Substring(0,40) + '...' }
    $lines.Add(("  {0}: tt={1} gwCorr={2} adopted={3} engUsed={4} ans='{5}'" -f $id,$r.taskType,(B $r.gatewayCorrect),(B $r.gatewayTruthAdopted),(B $r.gatewayUsedEngine),$ans))
}
$lines.Add("")

$pass = $true
function Ck($name, $cond) {
    $script:pass = $script:pass -and $cond
    $script:lines.Add(("  {0,-40}: {1}" -f $name, $(if ($cond) { 'PASS' } else { 'FAIL' })))
}
$lines.Add("--- red lines ---")
Ck 'new harmCount == 0'                        ($new.harmCount -eq 0)
Ck 'global harm scan empty'                    ($globalHarm.Count -eq 0)
Ck 'enginePrecision == 1.0 (not dropped)'      ([double]$new.enginePrecision -ge 1.0 - 1e-9)
Ck 'LOST(3f->6) empty'                         ($fb.lost.Count -eq 0)
Ck 'LOST(5->6) empty'                          ($fm.lost.Count -eq 0)
Ck 'GAINED contains pdf-486'                   ($fb.gained -contains 'pdf-486')
Ck 'GAINED contains pdf-533'                   ($fb.gained -contains 'pdf-533')
$p486 = $nh['pdf-486']; $p533 = $nh['pdf-533']
Ck 'pdf-486 adopted & correct'                 ((B $p486.gatewayTruthAdopted) -and (B $p486.gatewayCorrect))
Ck 'pdf-533 adopted & correct'                 ((B $p533.gatewayTruthAdopted) -and (B $p533.gatewayCorrect))
$p173 = $nh['pdf-173']; $p190 = $nh['pdf-190']
Ck 'pdf-173 NOT adopted (broken key, non-harm)'(-not (B $p173.gatewayTruthAdopted))
Ck 'pdf-190 NOT adopted (broken key, non-harm)'(-not (B $p190.gatewayTruthAdopted))
foreach ($id in 'pdf-482','pdf-487','pdf-490','pdf-538') {
    $r = $nh[$id]
    Ck "$id stays correct"                     ((B $r.gatewayCorrect))
}
$lines.Add("")
$lines.Add("OVERALL: $(if ($pass) { 'PASS' } else { 'FAIL' })")

$txt = $lines -join "`r`n"
$outPath = "D:\github\CogniThink-Chem\_evalscratch\verify_stage6_classify_out.txt"
[System.IO.File]::WriteAllText($outPath, $txt, [System.Text.UTF8Encoding]::new($false))
Write-Output $txt
Write-Output "`n(saved to $outPath)"
