$ErrorActionPreference = 'Stop'
$evalPath = "D:\github\CogniThink-Chem\_evalscratch\after_stage6_classify_v2.json"
$outPath  = "D:\github\CogniThink-Chem\_evalscratch\probe_concept_candidates.txt"

$raw = [IO.File]::ReadAllText($evalPath, [Text.Encoding]::UTF8)
$json = $raw | ConvertFrom-Json

# gather all rows: either .rows or the 4 buckets concatenated
$rows = @()
if ($json.rows) { $rows = @($json.rows) }
else {
    foreach ($b in @('bothCorrectCases','gatewayOnlyCorrectCases','pureLlmOnlyCorrectCases','bothWrongCases')) {
        if ($json.$b) { $rows += @($json.$b) }
    }
}

$candIds = @('test-1','test-2','test-3','test-4','test-5','property-1','property-2','concept-1','concept-2','concept-3','pdf-205','pdf-215','pdf-510')

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("=== total rows: $($rows.Count) ===")
[void]$sb.AppendLine("")

# 1) All concept/unknown rows overview
[void]$sb.AppendLine("=== ALL concept/unknown rows (id | tt | gOk | gUsed | adopted | gCorr | pCorr | expected) ===")
$cu = $rows | Where-Object { $_.taskType -in @('concept','unknown') } | Sort-Object id
foreach ($r in $cu) {
    $exp = "$($r.expected)"
    if ($exp.Length -gt 60) { $exp = $exp.Substring(0,60) + '...' }
    [void]$sb.AppendLine(("{0,-14} {1,-10} ok={2,-5} used={3,-5} adopt={4,-5} gCorr={5,-5} pCorr={6,-5} exp='{7}'" -f $r.id, $r.taskType, $r.gatewayOk, $r.gatewayUsedEngine, $r.gatewayTruthAdopted, $r.gatewayCorrect, $r.pureLlmCorrect, $exp))
}
[void]$sb.AppendLine("")
[void]$sb.AppendLine("concept/unknown count: $($cu.Count);  adopted=false count: $(($cu | Where-Object { -not $_.gatewayTruthAdopted }).Count)")
[void]$sb.AppendLine("")

# 2) Detail for the 13 candidates
[void]$sb.AppendLine("=== DETAIL: 13 candidates ===")
foreach ($id in $candIds) {
    $r = $rows | Where-Object { $_.id -eq $id } | Select-Object -First 1
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("----- $id -----")
    if (-not $r) { [void]$sb.AppendLine("  <<NOT FOUND in eval>>"); continue }
    [void]$sb.AppendLine("  taskType      : $($r.taskType)")
    [void]$sb.AppendLine("  gatewayOk     : $($r.gatewayOk)")
    [void]$sb.AppendLine("  gatewayUsed   : $($r.gatewayUsedEngine)")
    [void]$sb.AppendLine("  gatewayAdopt  : $($r.gatewayTruthAdopted)")
    [void]$sb.AppendLine("  gatewayCorrect: $($r.gatewayCorrect)")
    [void]$sb.AppendLine("  pureLlmCorrect: $($r.pureLlmCorrect)")
    [void]$sb.AppendLine("  expected/key  : $($r.expected)")
    [void]$sb.AppendLine("  note          : $($r.note)")
    [void]$sb.AppendLine("  gatewayAnswer : $($r.gatewayAnswer)")
    $q = "$($r.question)"
    [void]$sb.AppendLine("  question      : $q")
}

[IO.File]::WriteAllText($outPath, $sb.ToString(), (New-Object Text.UTF8Encoding($false)))
Write-Output "Wrote $outPath ($($sb.Length) chars)"
