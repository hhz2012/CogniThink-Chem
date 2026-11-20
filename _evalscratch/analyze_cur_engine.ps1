# Analyze checkpoint_cur_engine.jsonl: categorize engine gaps.
# ASCII-ONLY SCRIPT: all Chinese comes from JSON data read at runtime.
$cp  = "D:\github\CogniThink-Chem\_evalscratch\checkpoint_cur_engine.jsonl"
$out = "D:\github\CogniThink-Chem\_evalscratch\cur_engine_analysis.md"

$utf8 = New-Object System.Text.UTF8Encoding($false)
$lines = [IO.File]::ReadAllLines($cp, $utf8)

$rows = New-Object System.Collections.Generic.List[object]
foreach ($line in $lines) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $rows.Add(($line | ConvertFrom-Json))
}
Write-Output ("loaded rows = " + $rows.Count)

function Short($s, $n) {
    if ($null -eq $s) { return "" }
    $s = ($s -replace "`r?`n", " ").Trim()
    if ($s.Length -gt $n) { return $s.Substring(0, $n) + "..." }
    return $s
}
function Expected($c) {
    if ($null -ne $c.expectedAffirmative) {
        return $(if ($c.expectedAffirmative) { "AFFIRM" } else { "NEGATE" })
    }
    if ($c.answerKeywords) { return ("KW:" + (($c.answerKeywords -join "|"))) }
    return ""
}

$A = New-Object System.Collections.Generic.List[object]
$B = New-Object System.Collections.Generic.List[object]
$C = New-Object System.Collections.Generic.List[object]
foreach ($r in $rows) {
    if ($r.gatewayTruthAdopted -and -not $r.gatewayCorrect) { $A.Add($r) }
    elseif ($r.gatewayUsedEngine -and -not $r.gatewayTruthAdopted) { $B.Add($r) }
    elseif (-not $r.gatewayUsedEngine) { $C.Add($r) }
}

$tt = @{}
foreach ($r in $rows) {
    $t = if ($r.taskType) { $r.taskType } else { "(null)" }
    if (-not $tt.ContainsKey($t)) { $tt[$t] = @{ total=0; correct=0; A=0; B=0; C=0; pdf=0 } }
    $tt[$t].total++
    if ($r.gatewayCorrect) { $tt[$t].correct++ }
    if ($r.gatewayTruthAdopted -and -not $r.gatewayCorrect) { $tt[$t].A++ }
    elseif ($r.gatewayUsedEngine -and -not $r.gatewayTruthAdopted) { $tt[$t].B++ }
    elseif (-not $r.gatewayUsedEngine) { $tt[$t].C++ }
    if ($r.case.isPdf) { $tt[$t].pdf++ }
}

$okCount = ($rows | Where-Object { $_.gatewayTruthAdopted -and $_.gatewayCorrect }).Count

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("# cur_engine offline analysis (LLM disabled, 741 cases)")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("- total: " + $rows.Count)
[void]$sb.AppendLine("- adopted & correct: " + $okCount)
[void]$sb.AppendLine("- A adopted-but-wrong: " + $A.Count)
[void]$sb.AppendLine("- B used-but-not-adopted: " + $B.Count)
[void]$sb.AppendLine("- C not-engaged: " + $C.Count)
[void]$sb.AppendLine("")
[void]$sb.AppendLine("## by taskType")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("| taskType | total | correct | A | B | C | pdf |")
[void]$sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|")
foreach ($k in ($tt.Keys | Sort-Object)) {
    $v = $tt[$k]
    [void]$sb.AppendLine(("| {0} | {1} | {2} | {3} | {4} | {5} | {6} |" -f $k, $v.total, $v.correct, $v.A, $v.B, $v.C, $v.pdf))
}

function DumpList($title, $list) {
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("## " + $title + " (" + $list.Count + ")")
    [void]$sb.AppendLine("")
    $g = @{}
    foreach ($r in $list) {
        $t = if ($r.taskType) { $r.taskType } else { "(null)" }
        if (-not $g.ContainsKey($t)) { $g[$t] = New-Object System.Collections.Generic.List[object] }
        $g[$t].Add($r)
    }
    foreach ($t in ($g.Keys | Sort-Object)) {
        [void]$sb.AppendLine("### " + $t + " (" + $g[$t].Count + ")")
        [void]$sb.AppendLine("")
        foreach ($r in $g[$t]) {
            $c = $r.case
            $tag = if ($c.isPdf) { "[pdf]" } else { "[hand]" }
            [void]$sb.AppendLine("- **" + $c.id + "** " + $tag)
            [void]$sb.AppendLine("  - Q: " + (Short $c.question 140))
            $exp = Expected $c
            $noteStr = if ($c.note) { "  (note: " + (Short $c.note 60) + ")" } else { "" }
            [void]$sb.AppendLine("  - expected: " + $exp + $noteStr)
            [void]$sb.AppendLine("  - engineAnswer: " + (Short $r.gatewayAnswer 120))
            [void]$sb.AppendLine("  - conclusion: " + (Short $r.gatewayConclusion 120))
        }
        [void]$sb.AppendLine("")
    }
}

DumpList "A - adopted but WRONG (precision leak, fix first)" $A
DumpList "B - engine engaged but not adopted (Ok set, no usable answer)" $B
DumpList "C - engine not engaged (coverage gap, biggest opportunity)" $C

[IO.File]::WriteAllText($out, $sb.ToString(), $utf8)
Write-Output ("report written: " + $out)

Write-Output ""
Write-Output "=== taskType breakdown ==="
Write-Output ("{0,-22} {1,6} {2,8} {3,6} {4,6} {5,6} {6,5}" -f "taskType","total","correct","A","B","C","pdf")
foreach ($k in ($tt.Keys | Sort-Object)) {
    $v = $tt[$k]
    Write-Output ("{0,-22} {1,6} {2,8} {3,6} {4,6} {5,6} {6,5}" -f $k, $v.total, $v.correct, $v.A, $v.B, $v.C, $v.pdf)
}
Write-Output ""
Write-Output ("A (adopted-but-wrong) = " + $A.Count)
foreach ($r in $A) {
    $q = Short $r.case.question 70
    Write-Output ("  " + $r.case.id + " [" + $r.taskType + "] " + $q)
}
