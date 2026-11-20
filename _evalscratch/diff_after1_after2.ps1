$ErrorActionPreference = 'Stop'
$root = 'd:\github\CogniThink-Chem'
$utf8 = New-Object System.Text.UTF8Encoding($false)
function Load($path) {
  $h = @{}
  foreach ($line in [System.IO.File]::ReadLines($path, $utf8)) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $rec = $null
    try { $rec = $line | ConvertFrom-Json } catch { continue }
    if ($null -eq $rec) { continue }
    $h[[string]$rec.case.id] = [pscustomobject]@{ correct=[bool]$rec.gatewayCorrect; adopted=[bool]$rec.gatewayTruthAdopted; used=[bool]$rec.gatewayUsedEngine; task=[string]$rec.taskType; answer=[string]$rec.case.answer; concl=[string]$rec.gatewayConclusion; gwa=[string]$rec.gatewayAnswer }
  }
  return $h
}
$before = Load (Join-Path $root '_evalscratch\checkpoint_choice_after1.jsonl')
$after  = Load (Join-Path $root '_evalscratch\checkpoint_choice_after2.jsonl')
$gained = New-Object System.Collections.Generic.List[string]
$lost   = New-Object System.Collections.Generic.List[string]
$newWrong = New-Object System.Collections.Generic.List[string]
foreach ($id in $after.Keys) {
  $a = $after[$id]; $b = $before[$id]
  if ($null -eq $b) { continue }
  if ($a.correct -and -not $b.correct) {
    $gained.Add(("{0} [{1}] ans={2}" -f $id, $a.task, $a.answer))
  }
  if ($b.correct -and -not $a.correct) {
    $lost.Add(("{0} [{1}] ans={2}" -f $id, $a.task, $a.answer))
  }
}
Write-Output ("GAIN (wrong->correct): " + $gained.Count)
$gained | ForEach-Object { Write-Output ("  + " + $_) }
Write-Output ("LOST (correct->wrong): " + $lost.Count)
$lost | ForEach-Object { Write-Output ("  - " + $_) }
# check ion-6 specifically
Write-Output "--- ion-6 in after2 ---"
foreach ($line in [System.IO.File]::ReadLines((Join-Path $root '_evalscratch\checkpoint_choice_after2.jsonl'), $utf8)) {
  if ($line -match '"id":"ion-6"') {
    $rec = $line | ConvertFrom-Json
    Write-Output ("ion-6 correct=" + $rec.gatewayCorrect + " adopted=" + $rec.gatewayTruthAdopted + " gwa=[" + $rec.gatewayAnswer + "] concl=[" + $rec.gatewayConclusion + "]")
    break
  }
}
