# Task 29 / AC-29 final gate: after_stage3f.json (long-term anchor) -> after_stage6.json (stage 6 final).
# Asserts TR-29.1 (engineSolved >= 157) and TR-29.2 (harm=0, precision=1.0, LOST=[], correct->wrong=[]).
$utf8 = [Text.Encoding]::UTF8
function Rows($d){ $o=@(); foreach($b in 'bothCorrectCases','gatewayOnlyCorrectCases','pureLlmOnlyCorrectCases','bothWrongCases'){ if($d.$b){$o+=$d.$b} }; $o }
function B($v){ ($null -ne $v) -and ($v -is [bool]) -and $v }
function Flip($br,$nr){ $nh=@{}; foreach($r in $nr){$nh[$r.id]=$r}; $lost=@(); $gained=@(); $common=0; foreach($b in $br){ if($nh.ContainsKey($b.id)){ $common++; if((B $b.gatewayCorrect) -and -not (B $nh[$b.id].gatewayCorrect)){$lost+=$b.id}; if(-not (B $b.gatewayCorrect) -and (B $nh[$b.id].gatewayCorrect)){$gained+=$b.id} } }; [pscustomobject]@{common=$common;lost=$lost;gained=$gained} }

$f  = [IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage3f.json",$utf8) | ConvertFrom-Json
$c  = [IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage6_concept.json",$utf8) | ConvertFrom-Json
$ch = [IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage6_choice.json",$utf8) | ConvertFrom-Json
$nc = [IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage6_calc.json",$utf8) | ConvertFrom-Json
$ne = [IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage6.json",$utf8) | ConvertFrom-Json
$rf=Rows $f; $rc=Rows $c; $rch=Rows $ch; $rnc=Rows $nc; $rne=Rows $ne
$nh=@{}; foreach($r in $rne){$nh[$r.id]=$r}

$f36 = Flip $rf $rne      # 3f -> stage6 (long-term anchor, zero-regression proof)
$c6  = Flip $rc $rne      # concept -> stage6
$ch6 = Flip $rch $rne     # choice -> stage6
$nc6 = Flip $rnc $rne     # calc -> stage6
$globalHarm = @($rne | Where-Object { (B $_.gatewayTruthAdopted) -and (-not (B $_.gatewayCorrect)) -and (B $_.pureLlmCorrect) })

# per-taskType solved (adopted & correct) 3f -> stage6
$tts = @('ion_check','equilibrium','calculation','reaction_check','balance','choice','concept','atomic_structure','vsepr','structure_verify','unknown')
$rows = @(); foreach($t in $tts){ $fc=@($rf|?{$_.taskType -eq $t -and $_.gatewayTruthAdopted -and $_.gatewayCorrect}).Count; $ec=@($rne|?{$_.taskType -eq $t -and $_.gatewayTruthAdopted -and $_.gatewayCorrect}).Count; $rows += [pscustomobject]@{tt=$t;f3=$fc;s6=$ec;delta=($ec-$fc)} }

# keep-correct guards: high-value stage6 gains must stay correct in the final snapshot
$keepCalc   = @('d5-3','d5-4','d5-5','d10-1','d10-2','d10-3','d10-4','pdf-242','pdf-260')
$keepEq     = @('d4-1','d4-2','d4-4','eq-1','eq-2','eq-3','eq-4','eq-5','eq-6','pdf-257','pdf-259','d4-3','pdf-514')
$keepChoice = @('pdf-005','pdf-007','pdf-008','pdf-011','pdf-033','pdf-035','pdf-036','pdf-046','pdf-053','pdf-054','pdf-055','pdf-057','pdf-058','pdf-061','pdf-062','pdf-069','pdf-091','pdf-096','pdf-099','pdf-124','pdf-131','pdf-154','pdf-181','pdf-183','pdf-186','pdf-192','pdf-200','pdf-209','pdf-210','pdf-264','pdf-266','pdf-268','pdf-270','pdf-276','pdf-277','pdf-320','pdf-326','pdf-335','pdf-366','pdf-369','pdf-373','pdf-374','pdf-375','pdf-379','pdf-385','pdf-392','pdf-430','pdf-441','pdf-443','pdf-444','pdf-447','pdf-448','pdf-449','pdf-452','pdf-499','pdf-500','pdf-501','pdf-502','pdf-513')
$keepArtifacts = @('test-1','test-2','test-3','test-4','test-5','concept-1','concept-2','concept-3','property-1','property-2','pdf-208','pdf-510')
$keepAll = @($keepCalc + $keepEq + $keepChoice + $keepArtifacts)
$keepOk = (@($keepAll | Where-Object { $nh[$_] -and (B $nh[$_].gatewayCorrect) })).Count
$keepBad = @($keepAll | Where-Object { -not ($nh[$_] -and (B $nh[$_].gatewayCorrect)) })

$L = New-Object System.Collections.Generic.List[string]
$L.Add("=== Task 29 / AC-29 final gate (after_stage3f -> after_stage6) ===")
$L.Add("")
$L.Add("--- top metrics (3f anchor -> stage6 final) ---")
foreach($k in 'totalCases','gatewayCorrect','gatewayEngineUsed','engineSolved','enginePrecision','harmCount','pureLlmCorrect'){
  $L.Add(("  {0,-18}: {1} -> {2}  (delta {3:+0;-0})" -f $k,$f.$k,$ne.$k,([int]$ne.$k-[int]$f.$k)))
}
$L.Add("")
$L.Add("--- LOST / GAINED (common cases) ---")
$L.Add(("  3f->stage6 : common={0}  LOST={1}  GAINED={2}" -f $f36.common,$f36.lost.Count,$f36.gained.Count))
$L.Add(("  concept->s6: common={0}  LOST={1}  GAINED={2}" -f $c6.common,$c6.lost.Count,$c6.gained.Count))
$L.Add(("  choice->s6 : common={0}  LOST={1}  GAINED={2}" -f $ch6.common,$ch6.lost.Count,$ch6.gained.Count))
$L.Add(("  calc->s6   : common={0}  LOST={1}  GAINED={2}" -f $nc6.common,$nc6.lost.Count,$nc6.gained.Count))
$L.Add(("  LOST(3f->s6): [{0}]" -f ($f36.lost -join ', ')))
$L.Add(("  global harm: {0}" -f $globalHarm.Count))
$L.Add("")
$L.Add("--- per-taskType solved (adopted & correct) 3f -> stage6 ---")
foreach($r in $rows){ $L.Add(("  {0,-18} {1,3} -> {2,3}  ({3})" -f $r.tt,$r.f3,$r.s6,$(if($r.delta -gt 0){"+"+$r.delta}else{$r.delta}))) }
$L.Add("")
$L.Add(("--- keep-correct guards (calc+eq+choice+artifacts): " + $keepOk + "/" + $keepAll.Count))
if($keepBad.Count -gt 0){ $L.Add(("  KEEP BAD: [{0}]" -f ($keepBad -join ', '))) }
$L.Add("")
$pass = $true
function Ck($n,$cond){ $script:pass = $script:pass -and $cond; $script:L.Add(("  {0,-46}: {1}" -f $n,$(if($cond){'PASS'}else{'FAIL'}))) }
$L.Add("--- red lines ---")
Ck 'TR-29.1 engineSolved >= 157' ($ne.engineSolved -ge 157)
Ck 'TR-29.1 gatewayCorrect >= 157' ($ne.gatewayCorrect -ge 157)
Ck 'TR-29.2 harmCount==0' ($ne.harmCount -eq 0)
Ck 'TR-29.2 global harm empty' ($globalHarm.Count -eq 0)
Ck 'TR-29.2 enginePrecision==1.0' ([double]$ne.enginePrecision -ge 1.0 - 1e-9)
Ck 'TR-29.2 LOST(3f->s6) empty' ($f36.lost.Count -eq 0)
Ck 'TR-29.2 LOST(concept->s6) empty' ($c6.lost.Count -eq 0)
Ck 'TR-29.2 LOST(choice->s6) empty' ($ch6.lost.Count -eq 0)
Ck 'TR-29.2 LOST(calc->s6) empty' ($nc6.lost.Count -eq 0)
Ck 'keep-correct guards pass' ($keepOk -eq $keepAll.Count)
Ck 'pureLlmCorrect final==551' ($ne.pureLlmCorrect -eq 551)
Ck 'engineSolved==266' ($ne.engineSolved -eq 266)
$L.Add("")
$L.Add("OVERALL: $(if($pass){'PASS'}else{'FAIL'})")
$txt = $L -join "`r`n"
[IO.File]::WriteAllText("D:\github\CogniThink-Chem\_evalscratch\verify_stage6_out.txt",$txt,(New-Object Text.UTF8Encoding($false)))
Write-Output $txt
