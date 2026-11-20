# Exact-marker regression pre-check (matches code L279: 电子排布/轨道表示式/未成对电子)
# Scenario: raise atomic_structure branch to BEFORE calculation (still after equilibrium & vsepr & choice).
# Find non-choice, non-atomic cases that match exact markers -> would flip. Check correctness (regression risk).
$utf8=[Text.Encoding]::UTF8
$j=[IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage6_classify.json",$utf8)|ConvertFrom-Json
$rows=@(); foreach($b in 'bothCorrectCases','gatewayOnlyCorrectCases','pureLlmOnlyCorrectCases','bothWrongCases'){ if($j.$b){$rows+=$j.$b} }
function B($v){ ($null -ne $v) -and ($v -is [bool]) -and $v }
$markers = '电子排布','轨道表示式','未成对电子'   # exact code markers (L279)
$lines=New-Object System.Collections.Generic.List[string]
$lines.Add("=== exact markers (电子排布/轨道表示式/未成对电子); non-choice; current tt != atomic_structure ===")
$lines.Add("These would flip to atomic_structure if atomic branch is raised before calculation.")
$risk=0
foreach($r in $rows){
  if($r.taskType -eq 'choice' -or $r.taskType -eq 'atomic_structure'){ continue }
  $q=$r.question; if(-not $q){ continue }
  $hit=$false; foreach($m in $markers){ if($q.Contains($m)){ $hit=$true; break } }
  if(-not $hit){ continue }
  $corr=B $r.gatewayCorrect; $ad=B $r.gatewayTruthAdopted; $eu=B $r.gatewayUsedEngine
  if($corr){ $risk++ }
  $qs="$q"; if($qs.Length -gt 50){ $qs=$qs.Substring(0,50)+'...' }
  $lines.Add(("  {0}: tt={1,-16} corr={2} adopted={3} engUsed={4} | {5}" -f $r.id,$r.taskType,$corr,$ad,$eu,$qs))
}
$lines.Add("")
$lines.Add(("currently-correct that would flip (REGRESSION RISK): {0}" -f $risk))
$txt=$lines -join "`r`n"
[IO.File]::WriteAllText("D:\github\CogniThink-Chem\_evalscratch\probe_atomic_flip_risk_out.txt",$txt,(New-Object Text.UTF8Encoding($false)))
Write-Output $txt
