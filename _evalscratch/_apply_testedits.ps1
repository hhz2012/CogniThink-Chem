# Apply test-file sync edits from _p1_testedits.txt (marker|||newline, UTF-8).
# Keeps this .ps1 pure ASCII to avoid PS5 GBK misreading; Chinese lives in the .txt.
$ErrorActionPreference = 'Stop'
$manifest = 'D:\github\CogniThink-Chem\_evalscratch\_p1_testedits.txt'
$files = @(
  'D:\github\CogniThink-Chem\tests\CogniThink.TutorBrain.Tests\AtomicStructureSolverTests.cs',
  'D:\github\CogniThink-Chem\tests\CogniThink.TutorBrain.Tests\VseprSolverTests.cs'
)
$enc = New-Object System.Text.UTF8Encoding($false)
$edits = [System.IO.File]::ReadAllLines($manifest, [System.Text.Encoding]::UTF8)
foreach ($f in $files) {
  $lines = [System.IO.File]::ReadAllLines($f, [System.Text.Encoding]::UTF8)
  $changed = $false
  for ($i = 0; $i -lt $lines.Length; $i++) {
    foreach ($e in $edits) {
      if ([string]::IsNullOrWhiteSpace($e)) { continue }
      $idx = $e.IndexOf('|||')
      if ($idx -lt 0) { continue }
      $marker = $e.Substring(0, $idx)
      $newline = $e.Substring($idx + 3)
      if ($lines[$i].Contains($marker)) {
        $lines[$i] = $newline
        $changed = $true
        Write-Output ("REPLACED {0} L{1}: {2}" -f (Split-Path $f -Leaf), ($i + 1), $marker)
        break
      }
    }
  }
  if ($changed) {
    [System.IO.File]::WriteAllLines($f, $lines, $enc)
    Write-Output ("WROTE {0}" -f $f)
  } else {
    Write-Output ("NO CHANGES {0}" -f $f)
  }
}
Write-Output "DONE"
