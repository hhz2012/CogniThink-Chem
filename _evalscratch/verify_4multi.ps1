$ErrorActionPreference = 'Stop'
$root = 'd:\github\CogniThink-Chem'
$ckpt = Join-Path $root '_evalscratch\checkpoint_choice_after1.jsonl'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$ids = @('pdf-225','pdf-329','pdf-360','pdf-495')
foreach ($line in [System.IO.File]::ReadLines($ckpt, $utf8)) {
  if ([string]::IsNullOrWhiteSpace($line)) { continue }
  $rec = $null
  try { $rec = $line | ConvertFrom-Json } catch { continue }
  if ($null -eq $rec) { continue }
  $id = [string]$rec.case.id
  if ($ids -notcontains $id) { continue }
  Write-Output ("{0}  answer={1}  gatewayCorrect={2}  adopted={3}  usedEngine={4}  ans=[{5}]" -f $id, $rec.case.answer, $rec.gatewayCorrect, $rec.gatewayTruthAdopted, $rec.gatewayUsedEngine, $rec.gatewayAnswer)
}
