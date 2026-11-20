# Verify stage 6-F / AC-25 (calc coverage extension): zero regression + 9 calc gains + no harm.
# Baselines: after_stage3f.json (long-term anchor), after_stage6_concept.json, after_stage6_choice.json
#            (immediate pre-task). New: after_stage6_calc.json.
$utf8 = [Text.Encoding]::UTF8
function Rows($d){ $o=@(); foreach($b in 'bothCorrectCases','gatewayOnlyCorrectCases','pureLlmOnlyCorrectCases','bothWrongCases'){ if($d.$b){$o+=$d.$b} }; $o }
function B($v){ ($null -ne $v) -and ($v -is [bool]) -and $v }
function Flip($br,$nr){ $nh=@{}; foreach($r in $nr){$nh[$r.id]=$r}; $lost=@(); $gained=@(); $common=0; foreach($b in $br){ if($nh.ContainsKey($b.id)){ $common++; $cb=B $b.gatewayCorrect; $cn=B $nh[$b.id].gatewayCorrect; if($cb -and -not $cn){$lost+=$b.id}; if(-not $cb -and $cn){$gained+=$b.id} } }; [pscustomobject]@{common=$common;lost=$lost;gained=$gained} }

$b3   = [IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage3f.json",$utf8) | ConvertFrom-Json
$base = [IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage6_concept.json",$utf8) | ConvertFrom-Json
$ch   = [IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage6_choice.json",$utf8) | ConvertFrom-Json
$nc   = [IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage6_calc.json",$utf8) | ConvertFrom-Json
$rb = Rows $b3; $rbase = Rows $base; $rch = Rows $ch; $rn = Rows $nc
$nh=@{}; foreach($r in $rn){$nh[$r.id]=$r}

$fcc = Flip $rch $rn     # choice -> calc (immediate regression scan)
$fbc = Flip $rbase $rn   # concept -> calc
$f3c = Flip $rb $rn      # 3f -> calc (long-term anchor)
$globalHarm = @($rn | Where-Object { (B $_.gatewayTruthAdopted) -and (-not (B $_.gatewayCorrect)) -and (B $_.pureLlmCorrect) })

# calc scope
$calcScopeNew = @($rn | Where-Object { $_.taskType -eq 'calculation' })
$calcAdoptedCorrNew = @($calcScopeNew | Where-Object { (B $_.gatewayTruthAdopted) -and (B $_.gatewayCorrect) })
$calcScopeBase = @($rch | Where-Object { $_.taskType -eq 'calculation' })
$calcAdoptedCorrBase = @($calcScopeBase | Where-Object { (B $_.gatewayTruthAdopted) -and (B $_.gatewayCorrect) })

# the 9 calc gains (4 Hess + 5 narrow) - must all be calculation + engUsed + adopted + correct
$gainIds = @('d5-3','d5-4','d5-5','d10-1','d10-2','d10-3','d10-4','pdf-242','pdf-260')
# 12 dump-artifact keeps (must STAY correct - not real regressions)
$artifactKeepCorrect = @('test-1','test-2','test-3','test-4','test-5','concept-1','concept-2','concept-3','property-1','property-2','pdf-208','pdf-510')
# 11 pre-existing calc solves (must stay correct)
$calcBaselineKeep = @('calc-1','calc-2','calc-3','calc-4','calc-5','calc-6','pdf-022','pdf-023','pdf-243','pdf-273','pdf-357')
# 59 choice gains (must stay correct)
$choiceGains = @('pdf-005','pdf-007','pdf-008','pdf-011','pdf-033','pdf-035','pdf-036','pdf-046','pdf-053','pdf-054','pdf-055','pdf-057','pdf-058','pdf-061','pdf-062','pdf-069','pdf-091','pdf-096','pdf-099','pdf-124','pdf-131','pdf-154','pdf-181','pdf-183','pdf-186','pdf-192','pdf-200','pdf-209','pdf-210','pdf-264','pdf-266','pdf-268','pdf-270','pdf-276','pdf-277','pdf-320','pdf-326','pdf-335','pdf-366','pdf-369','pdf-373','pdf-374','pdf-375','pdf-379','pdf-385','pdf-392','pdf-430','pdf-441','pdf-443','pdf-444','pdf-447','pdf-448','pdf-449','pdf-452','pdf-499','pdf-500','pdf-501','pdf-502','pdf-513')

$L = New-Object System.Collections.Generic.List[string]
$L.Add("=== stage6-F / AC-25 verify (calc coverage extension) ===")
$L.Add("")
$L.Add("--- top metrics (3f -> concept -> choice -> calc) ---")
foreach($k in 'totalCases','gatewayCorrect','gatewayEngineUsed','engineSolved','enginePrecision','harmCount','pureLlmCorrect'){
  $L.Add(("  {0,-18}: {1} -> {2} -> {3} -> {4}" -f $k,$b3.$k,$base.$k,$ch.$k,$nc.$k))
}
$L.Add("")
$L.Add("--- LOST / GAINED (common cases) ---")
$L.Add(("  choice->calc : common={0}  LOST={1}  GAINED={2}" -f $fcc.common,$fcc.lost.Count,$fcc.gained.Count))
$L.Add(("  concept->calc: common={0}  LOST={1}  GAINED={2}" -f $fbc.common,$fbc.lost.Count,$fbc.gained.Count))
$L.Add(("  3f->calc     : common={0}  LOST={1}  GAINED={2}" -f $f3c.common,$f3c.lost.Count,$f3c.gained.Count))
$L.Add(("  LOST(choice->calc): [{0}]" -f ($fcc.lost -join ', ')))
$L.Add(("  LOST(concept->calc): [{0}]" -f ($fbc.lost -join ', ')))
$L.Add(("  LOST(3f->calc): [{0}]" -f ($f3c.lost -join ', ')))
$L.Add(("  GAINED(choice->calc) count: {0}" -f $fcc.gained.Count))
$L.Add(("  global harm: {0}" -f $globalHarm.Count))
$L.Add("")
$L.Add("--- calc scope (engineSolved) ---")
$L.Add(("  baseline(choice): calc total={0}  adopted&corr={1}" -f $calcScopeBase.Count,$calcAdoptedCorrBase.Count))
$L.Add(("  calc            : calc total={0}  adopted&corr={1}  (net +{2})" -f $calcScopeNew.Count,$calcAdoptedCorrNew.Count,($calcAdoptedCorrNew.Count-$calcAdoptedCorrBase.Count)))
$L.Add("")
$L.Add("--- 9 calc gains (must all be calculation + engUsed + adopted + correct) ---")
$gainOk = 0; $gainBad = @()
foreach($id in $gainIds){
  $r = $nh[$id]; if($null -eq $r){ $L.Add("  $id : MISSING"); $gainBad += $id; continue }
  $ok = ($r.taskType -eq 'calculation') -and (B $r.gatewayUsedEngine) -and (B $r.gatewayTruthAdopted) -and (B $r.gatewayCorrect)
  if($ok){ $gainOk++ } else { $gainBad += $id }
  $L.Add(("  {0,-9} tt={1,-11} engUsed={2} adopted={3} corr={4}  {5}" -f $id,$r.taskType,(B $r.gatewayUsedEngine),(B $r.gatewayTruthAdopted),(B $r.gatewayCorrect),$(if($ok){'OK'}else{'CHECK'})))
}
$L.Add(("  gains OK: {0}/9" -f $gainOk))
if($gainBad.Count -gt 0){ $L.Add(("  GAIN BAD: [{0}]" -f ($gainBad -join ', '))) }
$L.Add("")
$L.Add("--- regression guards: 11 baseline calc solves + 59 choice gains + 12 artifacts stay correct ---")
$keepAll = @($calcBaselineKeep + $choiceGains + $artifactKeepCorrect)
$keepOk = (@($keepAll | Where-Object { $nh[$_] -and (B $nh[$_].gatewayCorrect) })).Count
$keepBad = @($keepAll | Where-Object { -not ($nh[$_] -and (B $nh[$_].gatewayCorrect)) })
$L.Add(("  keep-correct OK: {0}/{1}" -f $keepOk,$keepAll.Count))
if($keepBad.Count -gt 0){ $L.Add(("  KEEP BAD: [{0}]" -f ($keepBad -join ', '))) }
$L.Add("")
$pass = $true
function Ck($n,$cond){ $script:pass = $script:pass -and $cond; $script:L.Add(("  {0,-46}: {1}" -f $n,$(if($cond){'PASS'}else{'FAIL'}))) }
$L.Add("--- red lines ---")
Ck 'harmCount==0' ($nc.harmCount -eq 0)
Ck 'global harm empty' ($globalHarm.Count -eq 0)
Ck 'enginePrecision==1.0' ([double]$nc.enginePrecision -ge 1.0 - 1e-9)
Ck 'LOST(choice->calc) empty' ($fcc.lost.Count -eq 0)
Ck 'LOST(concept->calc) empty' ($fbc.lost.Count -eq 0)
Ck 'LOST(3f->calc) empty' ($f3c.lost.Count -eq 0)
Ck '9/9 gains adopted&correct' ($gainOk -eq 9)
Ck 'calc engineSolved net +>=2' (($calcAdoptedCorrNew.Count - $calcAdoptedCorrBase.Count) -ge 2)
Ck 'calc engineSolved net ==9 (exact)' (($calcAdoptedCorrNew.Count - $calcAdoptedCorrBase.Count) -eq 9)
Ck 'calc scope solved ==20' ($calcAdoptedCorrNew.Count -eq 20)
Ck 'pureLlmCorrect unchanged (551)' ($nc.pureLlmCorrect -eq 551)
Ck 'engineSolved increased (264 vs 255)' ($nc.engineSolved -gt $ch.engineSolved)
Ck 'gatewayCorrect 264' ($nc.gatewayCorrect -eq 264)
Ck 'engineSolved 264' ($nc.engineSolved -eq 264)
Ck 'all keep-correct guards pass' ($keepOk -eq $keepAll.Count)
Ck 'GAINED(choice->calc)==9' ($fcc.gained.Count -eq 9)
$L.Add("")
$L.Add("OVERALL: $(if($pass){'PASS'}else{'FAIL'})")
$txt = $L -join "`r`n"
[IO.File]::WriteAllText("D:\github\CogniThink-Chem\_evalscratch\verify_stage6_calc_out.txt",$txt,(New-Object Text.UTF8Encoding($false)))
Write-Output $txt
