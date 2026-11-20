$ErrorActionPreference = 'Stop'
$path = 'd:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\Application\Evaluation\GatewayVsLlmEvaluationService.cs'
$methodPath = 'd:\github\CogniThink-Chem\_evalscratch\_method.txt'
$utf8 = New-Object System.Text.UTF8Encoding($false)

$lines = [System.IO.File]::ReadAllLines($path, $utf8)
$methodLines = [System.IO.File]::ReadAllLines($methodPath, $utf8)

$bodyStart = -1
$callIdx = -1
for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($bodyStart -lt 0 -and $lines[$i].Contains('var result = new QaCaseResult { Case = c };')) { $bodyStart = $i }
    if ($callIdx -lt 0 -and $lines[$i].Contains('results[i] = await EvaluateCaseAsync(c, ct);')) { $callIdx = $i }
}
if ($bodyStart -lt 0 -or $callIdx -lt 0) { throw "markers not found bodyStart=$bodyStart callIdx=$callIdx" }

$combined = $lines[0..($bodyStart - 1)] + $lines[$callIdx..($lines.Length - 1)]

$docIdx = -1
$buildIdx = -1
for ($i = 0; $i -lt $combined.Length; $i++) {
    if ($docIdx -lt 0 -and $combined[$i].Contains('BuildSummary')) { $docIdx = $i }
    if ($buildIdx -lt 0 -and $combined[$i].Contains('public static GatewayVsLlmSummary BuildSummary')) { $buildIdx = $i }
}
if ($buildIdx -lt 0) { throw "build marker not found" }
# docIdx points at the doc-comment line containing "BuildSummary"; insert before its opening <summary> (docIdx - 1)
$insertAt = $docIdx - 1

$result = $combined[0..($insertAt - 1)] + $methodLines + $combined[$insertAt..($combined.Length - 1)]

$bytes = [System.IO.File]::ReadAllBytes($path)
$hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
$enc = New-Object System.Text.UTF8Encoding($hasBom)
[System.IO.File]::WriteAllLines($path, $result, $enc)
Write-Output "OK bodyStart=$bodyStart callIdx=$callIdx insertAt=$insertAt buildIdx=$buildIdx before=$($lines.Length) after=$($result.Length)"
