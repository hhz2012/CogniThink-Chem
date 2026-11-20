$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$path = 'd:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json'
$json = Get-Content -Raw -Encoding UTF8 $path | ConvertFrom-Json
$targets = @('pdf-029','pdf-065','pdf-067','pdf-079','pdf-254','pdf-283','pdf-305','pdf-316','pdf-394')

# Schema: union of top-level keys across all records, with counts
$keyCount = @{}
foreach ($r in $json) {
  foreach ($p in $r.PSObject.Properties.Name) {
    if (-not $keyCount.ContainsKey($p)) { $keyCount[$p] = 0 }
    $keyCount[$p]++
  }
}

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("=== SCHEMA KEYS (camelCase) across all $($json.Count) records ===")
foreach ($k in ($keyCount.Keys | Sort-Object)) {
  [void]$sb.AppendLine(("{0,-18} count={1}" -f $k, $keyCount[$k]))
}

[void]$sb.AppendLine("")
[void]$sb.AppendLine("=== TARGET RECORDS (verbatim) ===")
$byId = @{}
foreach ($r in $json) { $byId[$r.id] = $r }
foreach ($tid in $targets) {
  [void]$sb.AppendLine("")
  [void]$sb.AppendLine("########## $tid ##########")
  if ($byId.ContainsKey($tid)) {
    $rec = $byId[$tid]
    [void]$sb.AppendLine("id: $($rec.id)")
    [void]$sb.AppendLine("type: $($rec.type)")
    [void]$sb.AppendLine("answer: $($rec.answer)")
    [void]$sb.AppendLine("question: $($rec.question)")
    [void]$sb.AppendLine("answerKeywords:")
    if ($null -eq $rec.answerKeywords) { [void]$sb.AppendLine("  (null)") }
    else { foreach ($kw in $rec.answerKeywords) { [void]$sb.AppendLine("  - $kw") } }
    [void]$sb.AppendLine("options:")
    if ($null -eq $rec.options) { [void]$sb.AppendLine("  (null)") }
    else { foreach ($op in $rec.options.PSObject.Properties) { [void]$sb.AppendLine("  $($op.Name): $($op.Value)") } }
    [void]$sb.AppendLine("note: $($rec.note)")
  } else {
    [void]$sb.AppendLine("  (NOT FOUND)")
  }
}

$out = 'd:\github\CogniThink-Chem\_evalscratch\dump_targets9_out.txt'
[System.IO.File]::WriteAllText($out, $sb.ToString(), [System.Text.Encoding]::UTF8)
Write-Output "WROTE $out"
