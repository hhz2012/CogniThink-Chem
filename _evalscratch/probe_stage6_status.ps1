# Probe: extract status of all AC-23 probe candidates from after_stage6_classify.json
# Run: $c=[IO.File]::ReadAllText('...probe_stage6_status.ps1',[Text.Encoding]::UTF8); iex $c
$utf8 = [Text.Encoding]::UTF8
$j = [IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage6_classify.json", $utf8) | ConvertFrom-Json
$rows = @(); foreach ($b in 'bothCorrectCases','gatewayOnlyCorrectCases','pureLlmOnlyCorrectCases','bothWrongCases') { if ($j.$b) { $rows += $j.$b } }
$h = @{}; foreach ($r in $rows) { $h[$r.id] = $r }

$cands = @(
 'pdf-173','pdf-486','pdf-488','pdf-198','pdf-485','pdf-117','pdf-120','pdf-121','pdf-122',
 'pdf-134','pdf-163','pdf-466','pdf-469','pdf-470','pdf-473','d4-3','pdf-514','pdf-516',
 'pdf-190','pdf-188','pdf-489','pdf-533','pdf-013','pdf-016',
 'pdf-108','pdf-166','pdf-482','pdf-487','pdf-490','pdf-538'
)

function B($v){ return ($null -ne $v) -and ($v -is [bool]) -and $v }

$reached = 0; $adoptedCorr = 0; $gainedVs3f = 0
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("id               taskType          engUsed adopted correct plCorr  reached? adopted&corr?")
$lines.Add("------------------------------------------------------------------------------------------")
foreach ($id in $cands) {
    $r = $h[$id]
    if ($null -eq $r) { $lines.Add(("{0,-16} MISSING" -f $id)); continue }
    $eu = B $r.gatewayUsedEngine; $ad = B $r.gatewayTruthAdopted; $gc = B $r.gatewayCorrect; $pc = B $r.pureLlmCorrect
    if ($eu) { $reached++ }
    if ($ad -and $gc) { $adoptedCorr++ }
    $lines.Add(("{0,-16} {1,-17} {2,-7} {3,-7} {4,-7} {5,-7} {6,-8} {7}" -f $id,$r.taskType,$eu,$ad,$gc,$pc,$(if($eu){'YES'}else{'-'}),$(if($ad -and $gc){'YES'}else{'-'})))
}
$lines.Add("")
$lines.Add(("Candidates reaching correct solver (engUsed=true): {0}  (need >=12)" -f $reached))
$lines.Add(("Candidates adopted && correct:                  {0}  (need >=8)" -f $adoptedCorr))
$txt = $lines -join "`r`n"
[IO.File]::WriteAllText("D:\github\CogniThink-Chem\_evalscratch\probe_stage6_status_out.txt", $txt, (New-Object Text.UTF8Encoding($false)))
Write-Output $txt
