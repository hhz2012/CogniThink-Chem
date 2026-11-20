# Measure Task 23 routing impact: taskType flips 3f -> 6 into {reaction_check, atomic_structure, equilibrium}
# Run via UTF-8 iex
$utf8 = [Text.Encoding]::UTF8
function Rows($d){ $o=@(); foreach($b in 'bothCorrectCases','gatewayOnlyCorrectCases','pureLlmOnlyCorrectCases','bothWrongCases'){ if($d.$b){$o+=$d.$b} }; $o }
function B($v){ ($null -ne $v) -and ($v -is [bool]) -and $v }
function ScopeStats($rows, $tt){
  $rs=@($rows | Where-Object { $_.taskType -eq $tt })
  [pscustomobject]@{ n=$rs.Count; engUsed=@($rs|Where-Object{B $_.gatewayUsedEngine}).Count; adopted_correct=@($rs|Where-Object{(B $_.gatewayTruthAdopted)-and(B $_.gatewayCorrect)}).Count; harm=@($rs|Where-Object{(B $_.gatewayTruthAdopted)-and(-not(B $_.gatewayCorrect))-and(B $_.pureLlmCorrect)}).Count }
}

$b3 = [IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage3f.json",$utf8)|ConvertFrom-Json
$n6 = [IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage6_classify.json",$utf8)|ConvertFrom-Json
$rb = Rows $b3; $rn = Rows $n6
$bh=@{}; foreach($r in $rb){ $bh[$r.id]=$r }
$nh=@{}; foreach($r in $rn){ $nh[$r.id]=$r }

$targets = 'reaction_check','atomic_structure','equilibrium'
$lines = New-Object System.Collections.Generic.List[string]

$flipped = @()
foreach($id in $bh.Keys){
  if(-not $nh.ContainsKey($id)){ continue }
  $b=$bh[$id]; $n=$nh[$id]
  if($b.taskType -ne $n.taskType -and $targets -contains $n.taskType){
    $flipped += [pscustomobject]@{ id=$id; from=$b.taskType; to=$n.taskType; engUsed=(B $n.gatewayUsedEngine); adopted=(B $n.gatewayTruthAdopted); corr=(B $n.gatewayCorrect); plCorr=(B $n.pureLlmCorrect); bCorr=(B $b.gatewayCorrect) }
  }
}
$lines.Add("=== taskType flips INTO {reaction_check, atomic_structure, equilibrium} (3f -> 6), common cases ===")
$lines.Add(("total flipped in: {0}" -f $flipped.Count))
foreach($tt in $targets){
  $g = @($flipped | Where-Object { $_.to -eq $tt })
  $reached = @($g | Where-Object { $_.engUsed }).Count
  $adoptedCorr = @($g | Where-Object { $_.adopted -and $_.corr }).Count
  $gained = @($g | Where-Object { -not $_.bCorr -and $_.corr }).Count
  $lines.Add(("  {0,-16}: flipped={1}  reached(engUsed)={2}  adopted&corr={3}  gained(wrong->corr)={4}" -f $tt,$g.Count,$reached,$adoptedCorr,$gained))
}
$lines.Add("")
$lines.Add("--- detail of flipped-in cases that reached engine (engUsed=true) ---")
foreach($f in ($flipped | Where-Object { $_.engUsed } | Sort-Object to,id)){
  $lines.Add(("  {0}: {1} -> {2}  adopted={3} corr={4} plCorr={5}" -f $f.id,$f.from,$f.to,$f.adopted,$f.corr,$f.plCorr))
}
$lines.Add("")
$lines.Add("=== absolute per-type: stage3f -> stage6 ===")
foreach($tt in $targets){
  $sb = ScopeStats $rb $tt
  $sn = ScopeStats $rn $tt
  $lines.Add(("  {0,-16}: n {1}->{2} | engUsed {3}->{4} | adopted&corr {5}->{6} | harm {7}->{8}" -f $tt,$sb.n,$sn.n,$sb.engUsed,$sn.engUsed,$sb.adopted_correct,$sn.adopted_correct,$sb.harm,$sn.harm))
}

$txt = $lines -join "`r`n"
[IO.File]::WriteAllText("D:\github\CogniThink-Chem\_evalscratch\probe_stage6_routing_out.txt", $txt, (New-Object Text.UTF8Encoding($false)))
Write-Output $txt
