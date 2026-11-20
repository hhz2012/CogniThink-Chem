# Verify stage 6-E / AC-27 (choice coverage extension): zero regression + 59 choice gains + no harm.
# Baselines: after_stage3f.json (long-term anchor), after_stage6_concept.json (immediate pre-task).
# New:      after_stage6_choice.json
$utf8 = [Text.Encoding]::UTF8
function Rows($d){ $o=@(); foreach($b in 'bothCorrectCases','gatewayOnlyCorrectCases','pureLlmOnlyCorrectCases','bothWrongCases'){ if($d.$b){$o+=$d.$b} }; $o }
function B($v){ ($null -ne $v) -and ($v -is [bool]) -and $v }
function Flip($br,$nr){ $nh=@{}; foreach($r in $nr){$nh[$r.id]=$r}; $lost=@(); $gained=@(); $common=0; foreach($b in $br){ if($nh.ContainsKey($b.id)){ $common++; $cb=B $b.gatewayCorrect; $cn=B $nh[$b.id].gatewayCorrect; if($cb -and -not $cn){$lost+=$b.id}; if(-not $cb -and $cn){$gained+=$b.id} } }; [pscustomobject]@{common=$common;lost=$lost;gained=$gained} }

$b3   = [IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage3f.json",$utf8) | ConvertFrom-Json
$base = [IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage6_concept.json",$utf8) | ConvertFrom-Json
$nc   = [IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage6_choice.json",$utf8) | ConvertFrom-Json
$rb = Rows $b3; $rbase = Rows $base; $rn = Rows $nc
$nh=@{}; foreach($r in $rn){$nh[$r.id]=$r}

$fbc = Flip $rbase $rn   # concept -> choice (immediate regression scan)
$f3c = Flip $rb $rn      # 3f -> choice (long-term anchor)
$globalHarm = @($rn | Where-Object { (B $_.gatewayTruthAdopted) -and (-not (B $_.gatewayCorrect)) -and (B $_.pureLlmCorrect) })

# choice scope
$choiceScope = @($rn | Where-Object { $_.taskType -eq 'choice' })
$choiceAdoptedCorr = @($choiceScope | Where-Object { (B $_.gatewayTruthAdopted) -and (B $_.gatewayCorrect) })
$choiceHarm = @($choiceScope | Where-Object { (B $_.gatewayTruthAdopted) -and (-not (B $_.gatewayCorrect)) -and (B $_.pureLlmCorrect) })

# the 59 gains (must all be choice + engUsed + adopted + correct)
$gainIds = @('pdf-005','pdf-007','pdf-008','pdf-011','pdf-033','pdf-035','pdf-036','pdf-046','pdf-053','pdf-054','pdf-055','pdf-057','pdf-058','pdf-061','pdf-062','pdf-069','pdf-091','pdf-096','pdf-099','pdf-124','pdf-131','pdf-154','pdf-181','pdf-183','pdf-186','pdf-192','pdf-200','pdf-209','pdf-210','pdf-264','pdf-266','pdf-268','pdf-270','pdf-276','pdf-277','pdf-320','pdf-326','pdf-335','pdf-366','pdf-369','pdf-373','pdf-374','pdf-375','pdf-379','pdf-385','pdf-392','pdf-430','pdf-441','pdf-443','pdf-444','pdf-447','pdf-448','pdf-449','pdf-452','pdf-499','pdf-500','pdf-501','pdf-502','pdf-513')
# the 5 eval-key pollution fixes (pdf-008/154/186/326/502) - must now be correct
$keyFix = @('pdf-008','pdf-154','pdf-186','pdf-326','pdf-502')
# 12 dump-artifact "losses" - must stay CORRECT in derived JSON (not real regressions)
$artifactKeepCorrect = @('test-1','test-2','test-3','test-4','test-5','concept-1','concept-2','concept-3','property-1','property-2','pdf-208','pdf-510')
# regression guards: choice questions correct before Task 27 must stay correct
$regressionGuards = @('pdf-041','pdf-042','pdf-049','pdf-059','pdf-110','pdf-089','pdf-533','pdf-486')

$L = New-Object System.Collections.Generic.List[string]
$L.Add("=== stage6-E / AC-27 verify (choice coverage extension) ===")
$L.Add("")
$L.Add("--- top metrics (3f -> concept -> choice) ---")
foreach($k in 'totalCases','gatewayCorrect','gatewayEngineUsed','engineSolved','enginePrecision','harmCount','pureLlmCorrect'){
  $L.Add(("  {0,-18}: {1} -> {2} -> {3}" -f $k,$b3.$k,$base.$k,$nc.$k))
}
$L.Add("")
$L.Add("--- LOST / GAINED (common cases) ---")
$L.Add(("  concept->choice: common={0}  LOST={1}  GAINED={2}" -f $fbc.common,$fbc.lost.Count,$fbc.gained.Count))
$L.Add(("  3f->choice     : common={0}  LOST={1}  GAINED={2}" -f $f3c.common,$f3c.lost.Count,$f3c.gained.Count))
$L.Add(("  LOST(concept->choice): [{0}]" -f ($fbc.lost -join ', ')))
$L.Add(("  LOST(3f->choice): [{0}]" -f ($f3c.lost -join ', ')))
$L.Add(("  GAINED(concept->choice) count: {0}" -f $fbc.gained.Count))
$L.Add(("  global harm: {0}" -f $globalHarm.Count))
$L.Add("")
$L.Add("--- choice scope ---")
$L.Add(("  total n={0}  adopted&corr={1}  harm={2}" -f $choiceScope.Count,$choiceAdoptedCorr.Count,$choiceHarm.Count))
$L.Add("")
$L.Add("--- 59 gains (must all be choice + engUsed + adopted + correct) ---")
$gainOk = 0; $gainBad = @()
foreach($id in $gainIds){
  $r = $nh[$id]; if($null -eq $r){ $L.Add("  $id : MISSING"); $gainBad += $id; continue }
  $ok = ($r.taskType -eq 'choice') -and (B $r.gatewayUsedEngine) -and (B $r.gatewayTruthAdopted) -and (B $r.gatewayCorrect)
  if($ok){ $gainOk++ } else { $gainBad += $id }
  $L.Add(("  {0,-10} tt={1,-7} engUsed={2} adopted={3} corr={4}  {5}" -f $id,$r.taskType,(B $r.gatewayUsedEngine),(B $r.gatewayTruthAdopted),(B $r.gatewayCorrect),$(if($ok){'OK'}else{'CHECK'})))
}
$L.Add(("  gains OK: {0}/59" -f $gainOk))
if($gainBad.Count -gt 0){ $L.Add(("  GAIN BAD: [{0}]" -f ($gainBad -join ', '))) }
$L.Add("")
$L.Add("--- 5 eval-key pollution fixes (must be correct now) ---")
foreach($id in $keyFix){
  $r = $nh[$id]; if($null -eq $r){ $L.Add("  $id : MISSING"); continue }
  $L.Add(("  {0,-10} adopted={1} corr={2} concl={3}" -f $id,(B $r.gatewayTruthAdopted),(B $r.gatewayCorrect),$r.gatewayConclusion))
}
$L.Add("")
$L.Add("--- 12 dump-artifact 'losses' (must STAY correct in derived JSON - not real regressions) ---")
foreach($id in $artifactKeepCorrect){
  $r = $nh[$id]; if($null -eq $r){ $L.Add("  $id : MISSING"); continue }
  $L.Add(("  {0,-12} tt={1,-13} adopted={2} corr={3}" -f $id,$r.taskType,(B $r.gatewayTruthAdopted),(B $r.gatewayCorrect)))
}
$L.Add("")
$L.Add("--- regression guards (previously correct must stay correct) ---")
foreach($id in $regressionGuards){
  $r = $nh[$id]; if($null -eq $r){ $L.Add("  $id : MISSING"); continue }
  $L.Add(("  {0,-10} tt={1,-10} adopted={2} corr={3}" -f $id,$r.taskType,(B $r.gatewayTruthAdopted),(B $r.gatewayCorrect)))
}
$L.Add("")
$pass = $true
function Ck($n,$cond){ $script:pass = $script:pass -and $cond; $script:L.Add(("  {0,-46}: {1}" -f $n,$(if($cond){'PASS'}else{'FAIL'}))) }
$L.Add("--- red lines ---")
Ck 'harmCount==0' ($nc.harmCount -eq 0)
Ck 'global harm empty' ($globalHarm.Count -eq 0)
Ck 'enginePrecision==1.0' ([double]$nc.enginePrecision -ge 1.0 - 1e-9)
Ck 'LOST(concept->choice) empty' ($fbc.lost.Count -eq 0)
Ck 'LOST(3f->choice) empty' ($f3c.lost.Count -eq 0)
Ck 'choice scope harm==0' ($choiceHarm.Count -eq 0)
Ck '59/59 gains adopted&correct' ($gainOk -eq 59)
Ck 'choice net gained >=20 (concept->choice)' ($fbc.gained.Count -ge 20)
Ck 'choice net gained == 59 (exact)' ($fbc.gained.Count -eq 59)
Ck 'pureLlmCorrect unchanged (551)' ($nc.pureLlmCorrect -eq 551)
Ck 'engineSolved increased (255 vs 196)' ($nc.engineSolved -gt $base.engineSolved)
Ck '5 key fixes all correct' ((@($keyFix | Where-Object { $nh[$_] -and (B $nh[$_].gatewayCorrect) })).Count -eq 5)
Ck '12 artifacts stay correct' ((@($artifactKeepCorrect | Where-Object { $nh[$_] -and (B $nh[$_].gatewayCorrect) })).Count -eq 12)
Ck 'regression guards all correct' ((@($regressionGuards | Where-Object { $nh[$_] -and (B $nh[$_].gatewayCorrect) })).Count -eq $regressionGuards.Count)
Ck 'gatewayCorrect 255' ($nc.gatewayCorrect -eq 255)
Ck 'engineSolved 255' ($nc.engineSolved -eq 255)
$L.Add("")
$L.Add("OVERALL: $(if($pass){'PASS'}else{'FAIL'})")
$txt = $L -join "`r`n"
[IO.File]::WriteAllText("D:\github\CogniThink-Chem\_evalscratch\verify_stage6_choice_out.txt",$txt,(New-Object Text.UTF8Encoding($false)))
Write-Output $txt
