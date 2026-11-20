$path = 'D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\Application\Gateway\LlmGatewayService.cs'
$c = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
"contains 'return \"equilibrium\";': $($c.Contains('return "equilibrium";'))"
"contains '物质的量': $($c.Contains('物质的量'))"
"contains '滴定': $($c.Contains('滴定'))"
"contains '什么是': $($c.Contains('什么是'))"
$eol = if ($c.Contains("`r`n")) {'CRLF'} else {'LF'}
"eol: $eol"
# Try the exact oldA with detected eol
$nl = if ($eol -eq 'CRLF') {"`r`n"} else {"`n"}
$oldA = "            return `"equilibrium`";" + $nl + "        if (lower.Contains(`"物质的量`")"
"oldA constructed length: $($oldA.Length)"
"file contains oldA: $($c.Contains($oldA))"
# Show context around equilibrium
$idx = $c.IndexOf('return "equilibrium";')
if ($idx -ge 0) {
  $seg = $c.Substring($idx, [Math]::Min(120, $c.Length - $idx))
  "context bytes: " + (($seg.ToCharArray() | ForEach-Object { [int]$_ }) -join ',')
}
