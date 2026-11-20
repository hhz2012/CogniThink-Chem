# Verify v2 (after pdf-533 routing fix): red lines + pdf-533 gain + LOST scan vs 3f and 5
# Run via UTF-8 iex
$utf8=[Text.Encoding]::UTF8
function Rows($d){ $o=@(); foreach($b in 'bothCorrectCases','gatewayOnlyCorrectCases','pureLlmOnlyCorrectCases','bothWrongCases'){ if($d.$b){$o+=$d.$b} }; $o }
function B($v){ ($null -ne $v) -and ($v -is [bool]) -and $v }
function Flip($br,$nr){ $nh=@{}; foreach($r in $nr){$nh[$r.id]=$r}; $lost=@(); $gained=@(); $common=0; foreach($b in $br){ if($nh.ContainsKey($b.id)){ $common++; $cb=B $b.gatewayCorrect; $cn=B $nh[$b.id].gatewayCorrect; if($cb -and -not $cn){$lost+=$b.id}; if(-not $cb -and $cn){$gained+=$b.id} } }; [pscustomobject]@{common=$common;lost=$lost;gained=$gained} }

$b3=[IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage3f.json",$utf8)|ConvertFrom-Json
$m5=[IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage5.json",$utf8)|ConvertFrom-Json
$v2=[IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage6_classify_v2.json",$utf8)|ConvertFrom-Json
$rb=Rows $b3; $rm=Rows $m5; $rn=Rows $v2
$nh=@{}; foreach($r in $rn){$nh[$r.id]=$r}

$fb=Flip $rb $rn; $fm=Flip $rm $rn
$globalHarm=@($rn | Where-Object { (B $_.gatewayTruthAdopted) -and (-not (B $_.gatewayCorrect)) -and (B $_.pureLlmCorrect) })

$L=New-Object System.Collections.Generic.List[string]
$L.Add("=== v2 verify (after pdf-533 routing fix) ===")
$L.Add("")
$L.Add("--- top metrics (3f -> 5 -> v2) ---")
foreach($k in 'totalCases','gatewayCorrect','gatewayEngineUsed','engineSolved','enginePrecision','harmCount'){
  $L.Add(("  {0,-18}: {1} -> {2} -> {3}" -f $k,$b3.$k,$m5.$k,$v2.$k))
}
$L.Add("")
$L.Add("--- LOST / GAINED (common cases) ---")
$L.Add(("  3f->v2: common={0}  LOST={1}  GAINED={2}" -f $fb.common,$fb.lost.Count,$fb.gained.Count))
$L.Add(("  5->v2 : common={0}  LOST={1}  GAINED={2}" -f $fm.common,$fm.lost.Count,$fm.gained.Count))
$L.Add(("  LOST(3f->v2): [{0}]" -f ($fb.lost -join ', ')))
$L.Add(("  LOST(5->v2) : [{0}]" -f ($fm.lost -join ', ')))
$L.Add(("  GAINED(3f->v2): [{0}]" -f (($fb.gained|Sort-Object) -join ', ')))
$L.Add(("  global harm: {0}" -f $globalHarm.Count))
$L.Add("")
$L.Add("--- pdf-533 + the 4 reclassified cases ---")
foreach($id in 'pdf-533','pdf-108','pdf-481','pdf-190','pdf-486'){
  $r=$nh[$id]; if($null -eq $r){$L.Add("  $id : MISSING"); continue}
  $ans="$($r.gatewayAnswer)"; if($ans.Length -gt 35){$ans=$ans.Substring(0,35)+'...'}
  $L.Add(("  {0}: tt={1,-16} engUsed={2} adopted={3} corr={4} ans='{5}'" -f $id,$r.taskType,(B $r.gatewayUsedEngine),(B $r.gatewayTruthAdopted),(B $r.gatewayCorrect),$ans))
}
$L.Add("")
# atomic scope
$at=@($rn | Where-Object { $_.taskType -eq 'atomic_structure' })
$L.Add(("--- atomic_structure scope: n={0}  adopted&corr={1}  harm={2}  engUsed={3} ---" -f $at.Count,(@($at|Where-Object{(B $_.gatewayTruthAdopted)-and(B $_.gatewayCorrect)}).Count),(@($at|Where-Object{(B $_.gatewayTruthAdopted)-and(-not(B $_.gatewayCorrect))-and(B $_.pureLlmCorrect)}).Count),(@($at|Where-Object{B $_.gatewayUsedEngine}).Count)))
$L.Add("")
$pass=$true
function Ck($n,$cond){ $script:pass=$script:pass -and $cond; $script:L.Add(("  {0,-36}: {1}" -f $n,$(if($cond){'PASS'}else{'FAIL'}))) }
$L.Add("--- red lines ---")
Ck 'harmCount==0' ($v2.harmCount -eq 0)
Ck 'global harm empty' ($globalHarm.Count -eq 0)
Ck 'enginePrecision==1.0' ([double]$v2.enginePrecision -ge 1.0 - 1e-9)
Ck 'LOST(3f->v2) empty' ($fb.lost.Count -eq 0)
Ck 'LOST(5->v2) empty' ($fm.lost.Count -eq 0)
$p=$nh['pdf-533']
Ck 'pdf-533 -> atomic_structure' ($p.taskType -eq 'atomic_structure')
Ck 'pdf-533 engUsed' (B $p.gatewayUsedEngine)
Ck 'pdf-533 adopted & correct' ((B $p.gatewayTruthAdopted) -and (B $p.gatewayCorrect))
Ck 'GAINED(3f->v2) contains pdf-533' ($fb.gained -contains 'pdf-533')
Ck 'pdf-482 stays correct' (B $nh['pdf-482'].gatewayCorrect)
Ck 'pdf-487 stays correct' (B $nh['pdf-487'].gatewayCorrect)
Ck 'pdf-490 stays correct' (B $nh['pdf-490'].gatewayCorrect)
Ck 'pdf-538 stays correct' (B $nh['pdf-538'].gatewayCorrect)
$L.Add("")
$L.Add("OVERALL: $(if($pass){'PASS'}else{'FAIL'})")
$txt=$L -join "`r`n"
[IO.File]::WriteAllText("D:\github\CogniThink-Chem\_evalscratch\verify_stage6_v2_out.txt",$txt,(New-Object Text.UTF8Encoding($false)))
Write-Output $txt
