# Re-apply the EvaluateCaseAsync extraction to GatewayVsLlmEvaluationService.cs
# ASCII-only: Chinese C# content lives in _eval_methods.txt (read as UTF-8).
$csPath     = "d:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\Application\Evaluation\GatewayVsLlmEvaluationService.cs"
$methodPath = "d:\github\CogniThink-Chem\_evalscratch\_eval_methods.txt"

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$utf8Bom   = New-Object System.Text.UTF8Encoding($true)

# Preserve original BOM state
$bytes = [IO.File]::ReadAllBytes($csPath)
$hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
$enc = if ($hasBom) { $utf8Bom } else { $utf8NoBom }

$lines   = [IO.File]::ReadAllLines($csPath, $utf8NoBom)   # StreamReader auto-detects BOM
$methods = [IO.File]::ReadAllLines($methodPath, $utf8NoBom)

$startIdx = -1
for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match 'EvaluateAsync\(CancellationToken ct = default, bool assetsOnly = false\)') { $startIdx = $i; break }
}
if ($startIdx -lt 0) { Write-Output "ERROR: EvaluateAsync signature not found"; exit 1 }

$retIdx = -1
for ($i = $startIdx; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match 'return BuildSummary\(results, LlmAvailable\);') { $retIdx = $i; break }
}
if ($retIdx -lt 0) { Write-Output "ERROR: return BuildSummary line not found"; exit 1 }

$endIdx = $retIdx + 1
while ($endIdx -lt $lines.Length -and $lines[$endIdx].Trim() -eq '') { $endIdx++ }

Write-Output ("start={0} ret={1} end={2} endLine=[{3}] hasBom={4}" -f $startIdx, $retIdx, $endIdx, $lines[$endIdx], $hasBom)

$prefix = if ($startIdx -gt 0) { $lines[0..($startIdx - 1)] } else { @() }
$suffix = if ($endIdx -lt $lines.Length - 1) { $lines[($endIdx + 1)..($lines.Length - 1)] } else { @() }

$result = New-Object System.Collections.Generic.List[string]
foreach ($l in $prefix) { $result.Add([string]$l) }
foreach ($l in $methods) { $result.Add([string]$l) }
foreach ($l in $suffix) { $result.Add([string]$l) }

[IO.File]::WriteAllLines($csPath, $result, $enc)
Write-Output ("WROTE OK; total lines={0}" -f $result.Count)
