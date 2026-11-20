# Verify stage 6-G / AC-26 (equilibrium coverage extension): zero regression + 2 eq gains + no harm.
# Baselines: after_stage3f.json (long-term anchor), after_stage6_concept.json,
#            after_stage6_choice.json, after_stage6_calc.json (immediate pre-task).
# New: after_stage6_eq.json.
$utf8 = [Text.Encoding]::UTF8
function Rows($d){ $o=@(); foreach($b in 'bothCorrectCases','gatewayOnlyCorrectCases','pureLlmOnlyCorrectCases','bothWrongCases'){ if($d.$b){$o+=$d.$b} }; $o }
function B($v){ ($null -ne $v) -and ($v -is [bool]) -and $v }
function Flip($br,$nr){ $nh=@{}; foreach($r in $nr){$nh[$r.id]=$r}; $lost=@(); $gained=@(); $common=0; foreach($b in $br){ if($nh.ContainsKey($b.id)){ $common++; $cb=B $b.gatewayCorrect; $cn=B $nh[$b.id].gatewayCorrect; if($cb -and -not $cn){$lost+=$b.id}; if(-not $cb -and $cn){$gained+=$b.id} } }; [pscustomobject]@{common=$common;lost=$lost;gained=$gained} }

$b3   = [IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage3f.json",$utf8) | ConvertFrom-Json
$base = [IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage6_concept.json",$utf8) | ConvertFrom-Json
$ch   = [IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage6_choice.json",$utf8) | ConvertFrom-Json
$nc   = [IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage6_calc.json",$utf8) | ConvertFrom-Json
$ne   = [IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage6_eq.json",$utf8) | ConvertFrom-Json
$rb = Rows $b3; $rbase = Rows $base; $rch = Rows $ch; $rnc = Rows $nc; $rne = Rows $ne
$nh=@{}; foreach($r in $rne){$nh[$r.id]=$r}

$fce = Flip $rnc $rne     # calc -> eq (immediate regression scan)
$fbc = Flip $rbase $rne   # concept -> eq
$f3c = Flip $rb $rne      # 3f -> eq (long-term anchor)
$globalHarm = @($rne | Where-Object { (B $_.gatewayTruthAdopted) -and (-not (B $_.gatewayCorrect)) -and (B $_.pureLlmCorrect) })

# equilibrium scope
$eqScopeNew = @($rne | Where-Object { $_.taskType -eq 'equilibrium' })
$eqAdoptedCorrNew = @($eqScopeNew | Where-Object { (B $_.gatewayTruthAdopted) -and (B $_.gatewayCorrect) })
$eqScopeBase = @($rnc | Where-Object { $_.taskType -eq 'equilibrium' })
$eqAdoptedCorrBase = @($eqScopeBase | Where-Object { (B $_.gatewayTruthAdopted) -and (B $_.gatewayCorrect) })

# the 2 equilibrium gains (conservation + acid-order) - must all be equilibrium + engUsed + adopted + correct
$gainIds = @('d4-3','pdf-514')
# 12 dump-artifact keeps (must STAY correct - not real regressions)
$artifactKeepCorrect = @('test-1','test-2','test-3','test-4','test-5','concept-1','concept-2','concept-3','property-1','property-2','pdf-208','pdf-510')
# 11 pre-existing equilibrium solves (must stay correct - the baseline eq scope)
$eqBaselineKeep = @('d4-1','d4-2','d4-4','eq-1','eq-2','eq-3','eq-4','eq-5','eq-6','pdf-257','pdf-259')
# 9 calc gains (must stay correct)
$calcGains = @('d5-3','d5-4','d5-5','d10-1','d10-2','d10-3','d10-4','pdf-242','pdf-260')
# 59 choice gains (must stay correct)
$choiceGains = @('pdf-005','pdf-007','pdf-008','pdf-011','pdf-033','pdf-035','pdf-036','pdf-046','pdf-053','pdf-054','pdf-055','pdf-057','pdf-058','pdf-061','pdf-062','pdf-069','pdf-091','pdf-096','pdf-099','pdf-124','pdf-131','pdf-154','pdf-181','pdf-183','pdf-186','pdf-192','pdf-200','pdf-209','pdf-210','pdf-264','pdf-266','pdf-268','pdf-270','pdf-276','pdf-277','pdf-320','pdf-326','pdf-335','pdf-366','pdf-369','pdf-373','pdf-374','pdf-375','pdf-379','pdf-385','pdf-392','pdf-430','pdf-441','pdf-443','pdf-444','pdf-447','pdf-448','pdf-449','pdf-452','pdf-499','pdf-500','pdf-501','pdf-502','pdf-513')

$L = New-Object System.Collections.Generic.List[string]
$L.Add("=== stage6-G / AC-26 verify (equilibrium coverage extension) ===")
$L.Add("")
$L.Add("--- top metrics (3f -> concept -> choice -> calc -> eq) ---")
foreach($k in 'totalCases','gatewayCorrect','gatewayEngineUsed','engineSolved','enginePrecision','harmCount','pureLlmCorrect'){
  $L.Add(("  {0,-18}: {1} -> {2} -> {3} -> {4} -> {5}" -f $k,$b3.$k,$base.$k,$ch.$k,$nc.$k,$ne.$k))
}
$L.Add("")
$L.Add("--- LOST / GAINED (common cases) ---")
$L.Add(("  calc->eq    : common={0}  LOST={1}  GAINED={2}" -f $fce.common,$fce.lost.Count,$fce.gained.Count))
$L.Add(("  concept->eq : common={0}  LOST={1}  GAINED={2}" -f $fbc.common,$fbc.lost.Count,$fbc.gained.Count))
$L.Add(("  3f->eq      : common={0}  LOST={1}  GAINED={2}" -f $f3c.common,$f3c.lost.Count,$f3c.gained.Count))
$L.Add(("  LOST(calc->eq): [{0}]" -f ($fce.lost -join ', ')))
$L.Add(("  LOST(concept->eq): [{0}]" -f ($fbc.lost -join ', ')))
$L.Add(("  LOST(3f->eq): [{0}]" -f ($f3c.lost -join ', ')))
$L.Add(("  GAINED(calc->eq) count: {0}" -f $fce.gained.Count))
$L.Add(("  global harm: {0}" -f $globalHarm.Count))
$L.Add("")
$L.Add("--- equilibrium scope (engineSolved) ---")
$L.Add(("  baseline(calc): eq total={0}  adopted&corr={1}" -f $eqScopeBase.Count,$eqAdoptedCorrBase.Count))
$L.Add(("  eq            : eq total={0}  adopted&corr={1}  (net +{2})" -f $eqScopeNew.Count,$eqAdoptedCorrNew.Count,($eqAdoptedCorrNew.Count-$eqAdoptedCorrBase.Count)))
$L.Add("")
$L.Add("--- 2 eq gains (must all be equilibrium + engUsed + adopted + correct) ---")
$gainOk = 0; $gainBad = @()
foreach($id in $gainIds){
  $r = $nh[$id]; if($null -eq $r){ $L.Add("  $id : MISSING"); $gainBad += $id; continue }
  $ok = ($r.taskType -eq 'equilibrium') -and (B $r.gatewayUsedEngine) -and (B $r.gatewayTruthAdopted) -and (B $r.gatewayCorrect)
  if($ok){ $gainOk++ } else { $gainBad += $id }
  $L.Add(("  {0,-9} tt={1,-11} engUsed={2} adopted={3} corr={4}  {5}" -f $id,$r.taskType,(B $r.gatewayUsedEngine),(B $r.gatewayTruthAdopted),(B $r.gatewayCorrect),$(if($ok){'OK'}else{'CHECK'})))
}
$L.Add(("  gains OK: {0}/2" -f $gainOk))
if($gainBad.Count -gt 0){ $L.Add(("  GAIN BAD: [{0}]" -f ($gainBad -join ', '))) }
$L.Add("")
$L.Add("--- regression guards: 11 baseline eq solves + 9 calc gains + 59 choice gains + 12 artifacts stay correct ---")
$keepAll = @($eqBaselineKeep + $calcGains + $choiceGains + $artifactKeepCorrect)
$keepOk = (@($keepAll | Where-Object { $nh[$_] -and (B $nh[$_].gatewayCorrect) })).Count
$keepBad = @($keepAll | Where-Object { -not ($nh[$_] -and (B $nh[$_].gatewayCorrect)) })
$L.Add(("  keep-correct OK: {0}/{1}" -f $keepOk,$keepAll.Count))
if($keepBad.Count -gt 0){ $L.Add(("  KEEP BAD: [{0}]" -f ($keepBad -join ', '))) }
$L.Add("")
$pass = $true
function Ck($n,$cond){ $script:pass = $script:pass -and $cond; $script:L.Add(("  {0,-46}: {1}" -f $n,$(if($cond){'PASS'}else{'FAIL'}))) }
$L.Add("--- red lines ---")
Ck 'harmCount==0' ($ne.harmCount -eq 0)
Ck 'global harm empty' ($globalHarm.Count -eq 0)
Ck 'enginePrecision==1.0' ([double]$ne.enginePrecision -ge 1.0 - 1e-9)
Ck 'LOST(calc->eq) empty' ($fce.lost.Count -eq 0)
Ck 'LOST(concept->eq) empty' ($fbc.lost.Count -eq 0)
Ck 'LOST(3f->eq) empty' ($f3c.lost.Count -eq 0)
Ck '2/2 gains adopted&correct' ($gainOk -eq 2)
Ck 'eq engineSolved net +>=2' (($eqAdoptedCorrNew.Count - $eqAdoptedCorrBase.Count) -ge 2)
Ck 'eq engineSolved net ==2 (exact)' (($eqAdoptedCorrNew.Count - $eqAdoptedCorrBase.Count) -eq 2)
Ck 'eq scope solved ==13' ($eqAdoptedCorrNew.Count -eq 13)
Ck 'pureLlmCorrect unchanged (551)' ($ne.pureLlmCorrect -eq 551)
Ck 'engineSolved increased (266 vs 264)' ($ne.engineSolved -gt $nc.engineSolved)
Ck 'gatewayCorrect 266' ($ne.gatewayCorrect -eq 266)
Ck 'engineSolved 266' ($ne.engineSolved -eq 266)
Ck 'all keep-correct guards pass' ($keepOk -eq $keepAll.Count)
Ck 'GAINED(calc->eq)==2' ($fce.gained.Count -eq 2)
$L.Add("")
$L.Add("OVERALL: $(if($pass){'PASS'}else{'FAIL'})")
$txt = $L -join "`r`n"
[IO.File]::WriteAllText("D:\github\CogniThink-Chem\_evalscratch\verify_stage6_eq_out.txt",$txt,(New-Object Text.UTF8Encoding($false)))
Write-Output $txt
