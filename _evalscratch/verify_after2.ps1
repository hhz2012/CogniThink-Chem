$ErrorActionPreference = 'Stop'
$root = 'd:\github\CogniThink-Chem'
$utf8 = New-Object System.Text.UTF8Encoding($false)
$ckpt = Join-Path $root '_evalscratch\checkpoint_choice_after2.jsonl'
$ids = @('pdf-225','pdf-329','pdf-360','pdf-495','ion-6')
foreach ($line in [System.IO.File]::ReadLines($ckpt, $utf8)) {
  if ([string]::IsNullOrWhiteSpace($line)) { continue }
  $rec = $null
  try { $rec = $line | ConvertFrom-Json } catch { continue }
  if ($null -eq $rec) { continue }
  $id = [string]$rec.case.id
  if ($ids -notcontains $id) { continue }
  Write-Output ("{0,-10} correct={1,-5} adopted={2,-5} task={3,-8} ans=[{4}]" -f $id, $rec.gatewayCorrect, $rec.gatewayTruthAdopted, $rec.taskType, $rec.gatewayAnswer)
}
