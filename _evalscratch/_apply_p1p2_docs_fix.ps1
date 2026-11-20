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
function FirstLine([string]$path) {
  # @() forces array even for single-line file so [0] is the line, not the first char
  return @(Read-Utf8 $path)[0]
}

$tasks = Join-Path $root '.trae\specs\chem-engine-question-solving\tasks.md'
$encTasks = Get-Enc $tasks
$lines = Read-Utf8 $tasks

# Guard index 1156 (line 1157) via neighbors: prev line is P1.2 (pdf-495), next-but-one is P2 header.
if (-not ($lines[1155].Contains('pdf-495') -and $lines[1158].Contains('**P2'))) {
  throw ('tasks.md neighbor guard failed around index 1156; prev=' + $lines[1155] + ' p2=' + $lines[1158])
}
$new1157 = FirstLine (Join-Path $root '_evalscratch\_p1p2_new1157b.txt')
if (-not ($new1157.Contains('Task 46') -and $new1157.Length -gt 60)) { throw ('new1157 sanity failed: ' + $new1157) }
$lines[1156] = $new1157

# Guard index 1169 (line 1170) via neighbor: prev line is the P4 header.
if (-not $lines[1168].StartsWith('  - **P4')) {
  throw ('tasks.md neighbor guard failed at index 1168; got=' + $lines[1168])
}
$new1170 = FirstLine (Join-Path $root '_evalscratch\_p1p2_new1170b.txt')
if (-not ($new1170.Contains('Task 46') -and $new1170.Length -gt 60)) { throw ('new1170 sanity failed: ' + $new1170) }
$lines[1169] = $new1170

# Append Task 46 block if not already present
$joined = $lines -join "`n"
if ($joined -notlike '*## Task 46*') {
  $block = Read-Utf8 (Join-Path $root '_evalscratch\_p1p2_task46.txt')
  $lines = $lines + '' + $block
}
[IO.File]::WriteAllLines($tasks, $lines, $encTasks)
Write-Output 'tasks.md fixed OK'
