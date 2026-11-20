# Apply doc-comment edit from _docedit.txt (marker|||newline, UTF-8). ASCII-only .ps1.
$ErrorActionPreference = 'Stop'
$manifest = 'D:\github\CogniThink-Chem\_evalscratch\_docedit.txt'
$f = 'D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\Application\Evaluation\GatewayVsLlmEvaluationService.cs'
$enc = New-Object System.Text.UTF8Encoding($false)
$edits = [System.IO.File]::ReadAllLines($manifest, [System.Text.Encoding]::UTF8)
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
      Write-Output ("REPLACED L{0}: {1}" -f ($i + 1), $marker)
      break
    }
  }
}
if ($changed) {
  [System.IO.File]::WriteAllLines($f, $lines, $enc)
  Write-Output ("WROTE {0}" -f $f)
} else {
  Write-Output "NO CHANGES"
}
