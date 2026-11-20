$ErrorActionPreference = 'Stop'
$root = 'd:\github\CogniThink-Chem'
$ckpt = Join-Path $root '_evalscratch\checkpoint_choice_base.jsonl'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$out = Join-Path $root '_evalscratch\choice_B_dump.md'
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('# choice B cases (engaged, not adopted)')
$lines.Add('')
foreach ($line in [System.IO.File]::ReadLines($ckpt, $utf8)) {
  if ([string]::IsNullOrWhiteSpace($line)) { continue }
  $rec = $null
  try { $rec = $line | ConvertFrom-Json } catch { continue }
  if ($null -eq $rec) { continue }
  if ([string]$rec.taskType -ne 'choice') { continue }
  if ($rec.gatewayCorrect -eq $true) { continue }
  if ($rec.gatewayUsedEngine -ne $true) { continue }
  $c = $rec.case
  $conc = [string]$rec.gatewayConclusion
  if ($conc.Length -gt 300) { $conc = $conc.Substring(0,300) }
  $gwa = [string]$rec.gatewayAnswer
  $q = [string]$c.question
  if ($q.Length -gt 160) { $q = $q.Substring(0,160) }
  $lines.Add('## ' + $c.id + '  answer=' + $c.answer + '  ansShape=' + $(if($c.answer -match '^[A-D]{2,4}$'){'multi'}elseif($c.answer -match '^[A-D]$'){'single'}else{'other'}))
  $lines.Add('- Q: ' + ($q -replace "`r?`n",' '))
  $lines.Add('- engineConclusion: ' + ($conc -replace "`r?`n",' '))
  $lines.Add('- gatewayAnswer(adopted): [' + $gwa + ']')
  $lines.Add('- gatewayOk: ' + $rec.gatewayOk + '   structuredHit: ' + $rec.structuredHit)
  $lines.Add('')
}
[System.IO.File]::WriteAllLines($out, $lines, $utf8)
Write-Output ('WROTE ' + $out)
