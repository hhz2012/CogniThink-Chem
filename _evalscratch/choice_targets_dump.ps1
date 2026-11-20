$ErrorActionPreference = 'Stop'
$root = 'd:\github\CogniThink-Chem'
$ckpt = Join-Path $root '_evalscratch\checkpoint_choice_after1.jsonl'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$tsv = Join-Path $root '_evalscratch\choice_unsolved_v2.tsv'
# read primary bucket assignments from the tsv (id, primary, ...)
$prim = @{}
$first = $true
foreach ($l in [System.IO.File]::ReadAllLines($tsv, $utf8)) {
  if ($first) { $first = $false; continue }
  $p = $l -split "`t"
  if ($p.Count -lt 2) { continue }
  $prim[$p[0]] = $p[1]
}
$targets = @('FACT','NODATA','MULTI','PARSEFAIL','NUCLIDE','CRYSTAL','ELECTROCHEM')
$out = Join-Path $root '_evalscratch\choice_targets_dump.md'
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('# choice targets: FACT/NODATA/MULTI/PARSEFAIL/NUCLIDE/CRYSTAL/ELECTROCHEM (post-fix unsolved)')
$lines.Add('')
$rows = New-Object System.Collections.Generic.List[object]
foreach ($line in [System.IO.File]::ReadLines($ckpt, $utf8)) {
  if ([string]::IsNullOrWhiteSpace($line)) { continue }
  $rec = $null
  try { $rec = $line | ConvertFrom-Json } catch { continue }
  if ($null -eq $rec) { continue }
  if ([string]$rec.taskType -ne 'choice') { continue }
  if ($rec.gatewayCorrect -eq $true) { continue }
  $id = [string]$rec.case.id
  $b = ''
  if ($prim.ContainsKey($id)) { $b = $prim[$id] } else { $b = '?(pdf-412?)' }
  if ($targets -notcontains $b) { continue }
  $rows.Add([pscustomobject]@{ bucket=$b; id=$id; rec=$rec })
}
foreach ($r in ($rows | Sort-Object bucket, id)) {
  $c = $r.rec.case
  $q = [string]$c.question
  $q = $q -replace "`r?`n", ' | '
  if ($q.Length -gt 500) { $q = $q.Substring(0,500) }
  $conc = [string]$r.rec.gatewayConclusion
  if ($conc.Length -gt 120) { $conc = $conc.Substring(0,120) }
  $lines.Add('## [' + $r.bucket + '] ' + $r.id + '  answer=' + $c.answer + '  usedEngine=' + $r.rec.gatewayUsedEngine)
  $lines.Add('- Q: ' + $q)
  $lines.Add('- engineConclusion: ' + $conc)
  $lines.Add('')
}
[System.IO.File]::WriteAllLines($out, $lines, $utf8)
Write-Output ('WROTE ' + $out + '  rows=' + $rows.Count)
