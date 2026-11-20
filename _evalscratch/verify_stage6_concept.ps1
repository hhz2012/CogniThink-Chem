# Verify stage 6-B / AC-24 (ConceptFactSolver): zero regression + 11 concept gains + no harm.
# Baselines: after_stage3f.json (long-term anchor), after_stage6_classify_v2.json (immediate pre-task).
# New:      after_stage6_concept.json
# Run via UTF-8 iex:  $c=[IO.File]::ReadAllText($p,[Text.Encoding]::UTF8); iex $c
$utf8 = [Text.Encoding]::UTF8
function Rows($d){ $o=@(); foreach($b in 'bothCorrectCases','gatewayOnlyCorrectCases','pureLlmOnlyCorrectCases','bothWrongCases'){ if($d.$b){$o+=$d.$b} }; $o }
function B($v){ ($null -ne $v) -and ($v -is [bool]) -and $v }
function Flip($br,$nr){ $nh=@{}; foreach($r in $nr){$nh[$r.id]=$r}; $lost=@(); $gained=@(); $common=0; foreach($b in $br){ if($nh.ContainsKey($b.id)){ $common++; $cb=B $b.gatewayCorrect; $cn=B $nh[$b.id].gatewayCorrect; if($cb -and -not $cn){$lost+=$b.id}; if(-not $cb -and $cn){$gained+=$b.id} } }; [pscustomobject]@{common=$common;lost=$lost;gained=$gained} }

$b3 = [IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage3f.json",$utf8) | ConvertFrom-Json
$v2 = [IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage6_classify_v2.json",$utf8) | ConvertFrom-Json
$nc = [IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage6_concept.json",$utf8) | ConvertFrom-Json
$rb = Rows $b3; $rv = Rows $v2; $rn = Rows $nc
$nh=@{}; foreach($r in $rn){$nh[$r.id]=$r}

$f3n = Flip $rb $rn   # 3f -> concept
$fvn = Flip $rv $rn   # v2 -> concept (immediate regression scan)
$globalHarm = @($rn | Where-Object { (B $_.gatewayTruthAdopted) -and (-not (B $_.gatewayCorrect)) -and (B $_.pureLlmCorrect) })

# concept engine介入 scope: concept_fact (solved by new solver) + concept/unknown historically
$conceptScope = @($rn | Where-Object { $_.taskType -in @('concept','unknown','concept_fact') })
$adoptedCorr = @($conceptScope | Where-Object { (B $_.gatewayTruthAdopted) -and (B $_.gatewayCorrect) })
$scopeHarm = @($conceptScope | Where-Object { (B $_.gatewayTruthAdopted) -and (-not (B $_.gatewayCorrect)) -and (B $_.pureLlmCorrect) })
$scopeEngUsed = @($conceptScope | Where-Object { B $_.gatewayUsedEngine })
$cfOnly = @($rn | Where-Object { $_.taskType -eq 'concept_fact' })

$cands = @('test-1','test-2','test-3','test-4','test-5','property-1','property-2','concept-1','concept-2','concept-3','pdf-510')
$ceilings = @('pdf-205','pdf-215')

$L = New-Object System.Collections.Generic.List[string]
$L.Add("=== stage6-B / AC-24 verify (ConceptFactSolver) ===")
$L.Add("")
$L.Add("--- top metrics (3f -> v2 -> concept) ---")
foreach($k in 'totalCases','gatewayCorrect','gatewayEngineUsed','engineSolved','enginePrecision','harmCount'){
  $L.Add(("  {0,-18}: {1} -> {2} -> {3}" -f $k,$b3.$k,$v2.$k,$nc.$k))
}
$L.Add("")
$L.Add("--- LOST / GAINED (common cases) ---")
$L.Add(("  3f->concept: common={0}  LOST={1}  GAINED={2}" -f $f3n.common,$f3n.lost.Count,$f3n.gained.Count))
$L.Add(("  v2->concept: common={0}  LOST={1}  GAINED={2}" -f $fvn.common,$fvn.lost.Count,$fvn.gained.Count))
$L.Add(("  LOST(3f->concept): [{0}]" -f ($f3n.lost -join ', ')))
$L.Add(("  LOST(v2->concept): [{0}]" -f ($fvn.lost -join ', ')))
$L.Add(("  GAINED(v2->concept): [{0}]" -f (($fvn.gained|Sort-Object) -join ', ')))
$L.Add(("  GAINED(3f->concept): [{0}]" -f (($f3n.gained|Sort-Object) -join ', ')))
$L.Add(("  global harm: {0}" -f $globalHarm.Count))
$L.Add("")
$L.Add("--- concept/unknown/concept_fact scope ---")
$L.Add(("  total n={0}  concept_fact n={1}  engUsed={2}  adopted&corr={3}  harm={4}" -f $conceptScope.Count,$cfOnly.Count,$scopeEngUsed.Count,$adoptedCorr.Count,$scopeHarm.Count))
$L.Add("")
$L.Add("--- 11 candidates (must all be concept_fact + engUsed + adopted + correct) ---")
$candOk = 0
foreach($id in $cands){
  $r = $nh[$id]; if($null -eq $r){ $L.Add("  $id : MISSING"); continue }
  $ok = ($r.taskType -eq 'concept_fact') -and (B $r.gatewayUsedEngine) -and (B $r.gatewayTruthAdopted) -and (B $r.gatewayCorrect)
  if($ok){ $candOk++ }
  $L.Add(("  {0,-12} tt={1,-13} engUsed={2} adopted={3} corr={4}  {5}" -f $id,$r.taskType,(B $r.gatewayUsedEngine),(B $r.gatewayTruthAdopted),(B $r.gatewayCorrect),$(if($ok){'OK'}else{'CHECK'})))
}
$L.Add(("  candidates OK: {0}/11" -f $candOk))
$L.Add("")
$L.Add("--- 2 ceiling cases (must stay NOT adopted; no harm; pure LLM unaffected) ---")
foreach($id in $ceilings){
  $r = $nh[$id]; if($null -eq $r){ $L.Add("  $id : MISSING"); continue }
  $L.Add(("  {0,-10} tt={1,-10} engUsed={2} adopted={3} corr={4} pCorr={5}" -f $id,$r.taskType,(B $r.gatewayUsedEngine),(B $r.gatewayTruthAdopted),(B $r.gatewayCorrect),(B $r.pureLlmCorrect)))
}
$L.Add("")
$pass = $true
function Ck($n,$cond){ $script:pass = $script:pass -and $cond; $script:L.Add(("  {0,-42}: {1}" -f $n,$(if($cond){'PASS'}else{'FAIL'}))) }
$L.Add("--- red lines ---")
Ck 'harmCount==0' ($nc.harmCount -eq 0)
Ck 'global harm empty' ($globalHarm.Count -eq 0)
Ck 'enginePrecision==1.0' ([double]$nc.enginePrecision -ge 1.0 - 1e-9)
Ck 'LOST(3f->concept) empty' ($f3n.lost.Count -eq 0)
Ck 'LOST(v2->concept) empty' ($fvn.lost.Count -eq 0)
Ck 'scope harm==0' ($scopeHarm.Count -eq 0)
Ck '11/11 candidates adopted&correct' ($candOk -eq 11)
Ck 'concept net gained >=8 (v2->concept)' ($fvn.gained.Count -ge 8)
Ck 'pdf-205 not adopted (ceiling)' (-not (B $nh['pdf-205'].gatewayTruthAdopted))
Ck 'pdf-215 not adopted (ceiling)' (-not (B $nh['pdf-215'].gatewayTruthAdopted))
Ck 'pdf-533 stays correct (regression guard)' (B $nh['pdf-533'].gatewayCorrect)
Ck 'pdf-486 stays correct (regression guard)' (B $nh['pdf-486'].gatewayCorrect)
$L.Add("")
$L.Add("OVERALL: $(if($pass){'PASS'}else{'FAIL'})")
$txt = $L -join "`r`n"
[IO.File]::WriteAllText("D:\github\CogniThink-Chem\_evalscratch\verify_stage6_concept_out.txt",$txt,(New-Object Text.UTF8Encoding($false)))
Write-Output $txt
