# Detached eval client: fetches the eval JSON and writes it + a DONE marker.
# Run with: powershell -NoProfile -File <this>
$ErrorActionPreference = 'Stop'
$out  = "D:\github\CogniThink-Chem\_evalscratch\after_stage6_concept.json"
$done = "D:\github\CogniThink-Chem\_evalscratch\after_stage6_concept.done"
$log  = "D:\github\CogniThink-Chem\_evalscratch\eval_client.log"
try {
    "client start $(Get-Date -Format o)" | Out-File -FilePath $log -Encoding utf8
    $r = Invoke-WebRequest -Uri "http://localhost:5080/api/evaluation/gateway-vs-llm" -UseBasicParsing -TimeoutSec 800
    [IO.File]::WriteAllText($out, $r.Content, (New-Object Text.UTF8Encoding($false)))
    "client OK bytes=$($r.Content.Length) $(Get-Date -Format o)" | Out-File -FilePath $log -Append -Encoding utf8
    "DONE" | Out-File -FilePath $done -Encoding ascii
} catch {
    "client FAIL: $($_.Exception.Message) $(Get-Date -Format o)" | Out-File -FilePath $log -Append -Encoding utf8
    "FAIL" | Out-File -FilePath $done -Encoding ascii
}
