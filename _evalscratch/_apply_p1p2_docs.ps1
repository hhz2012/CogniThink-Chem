$ErrorActionPreference = 'Stop'
$root = 'D:\github\CogniThink-Chem'
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$utf8Bom = New-Object System.Text.UTF8Encoding($true)

function Get-Enc([string]$path) {
  $b = [IO.File]::ReadAllBytes($path)
  if ($b.Length -ge 3 -and $b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF) { return $utf8Bom }
  return $utf8NoBom
}
function Read-Utf8([string]$path) {
  return [IO.File]::ReadAllLines($path, [Text.Encoding]::UTF8)
}

# ---- tasks.md ----
$tasks = Join-Path $root '.trae\specs\chem-engine-question-solving\tasks.md'
$encTasks = Get-Enc $tasks
$lines = Read-Utf8 $tasks

# guard: line 1157 (index 1156) is the P1.3 data-mismatch line
if (-not ($lines[1156].StartsWith('    3. ') -and $lines[1156].Contains('pdf-108/173/507'))) {
  throw ('tasks.md[1156] guard failed: ' + $lines[1156])
}
$lines[1156] = (Read-Utf8 (Join-Path $root '_evalscratch\_p1p2_new1157.txt'))[0]

# guard: line 1170 (index 1169) is the P4 data-quality line mentioning pdf-412
if (-not ($lines[1169].StartsWith('    - ') -and $lines[1169].Contains('pdf-412'))) {
  throw ('tasks.md[1169] guard failed: ' + $lines[1169])
}
$lines[1169] = (Read-Utf8 (Join-Path $root '_evalscratch\_p1p2_new1170.txt'))[0]

# append Task 43 block if not already present
$joined = $lines -join "`n"
if ($joined -notlike '*## Task 43*') {
  $block = Read-Utf8 (Join-Path $root '_evalscratch\_p1p2_task43.txt')
  $lines = $lines + '' + $block
}
[IO.File]::WriteAllLines($tasks, $lines, $encTasks)
Write-Output 'tasks.md updated'

# ---- compare.txt ----
$cmp = Join-Path $root '_evalscratch\compare.txt'
$encCmp = Get-Enc $cmp
$clines = Read-Utf8 $cmp
$cjoined = $clines -join "`n"
if ($cjoined -notlike '*verify_stage6_p1_out*') {
  $cblock = Read-Utf8 (Join-Path $root '_evalscratch\_p1p2_compare.txt')
  $clines = $clines + '' + $cblock
}
[IO.File]::WriteAllLines($cmp, $clines, $encCmp)
Write-Output 'compare.txt updated'
