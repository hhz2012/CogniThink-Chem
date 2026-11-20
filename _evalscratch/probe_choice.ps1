# Targeted end-to-end probe for Task 27: 5 eval-key fixes + 3 representative gains.
# Verifies the choice validators integrate through the REAL pipeline (LLM parse -> engine -> adopt).
# Reuses apibuild (Release, Development env loads LLM). Saves raw responses to probe_raw/.
$ErrorActionPreference = 'Continue'
$utf8 = New-Object Text.UTF8Encoding($false)
$repo = "D:\github\CogniThink-Chem"
$basepath = "$repo\_evalscratch\after_stage6_concept.json"
$outFile = "$repo\_evalscratch\probe_choice_out.txt"
$rawDir = "$repo\_evalscratch\probe_raw"
if(-not (Test-Path $rawDir)){ New-Item -ItemType Directory -Force -Path $rawDir | Out-Null }
$dotnet = (Get-Command dotnet).Source
$dll = "$repo\_evalscratch\apibuild\CogniThink.TutorBrain.Api.dll"
$srvOut = "$repo\_evalscratch\srv_probe_out.log"
$srvErr = "$repo\_evalscratch\srv_probe_err.log"

# 5 key fixes + 3 gains spanning different validators
$ids = @('pdf-008','pdf-154','pdf-186','pdf-326','pdf-502','pdf-005','pdf-053','pdf-513')

$base = [IO.File]::ReadAllText($basepath, $utf8) | ConvertFrom-Json
$rows = @(); foreach($b in 'bothCorrectCases','gatewayOnlyCorrectCases','pureLlmOnlyCorrectCases','bothWrongCases'){ if($base.$b){ $rows += @($base.$b) } }
$caseMap = @{}; foreach($r in $rows){ $caseMap[$r.id] = $r }
# expected answer letters (from the corrected eval keys + chemically-verified truth)
$expected = @{ 'pdf-008'='C'; 'pdf-154'='B'; 'pdf-186'='A'; 'pdf-326'='D'; 'pdf-502'='B'; 'pdf-005'='C'; 'pdf-053'='B'; 'pdf-513'='B' }

function Norm($s){ if($null -eq $s){return ''}; $t="$s".ToLowerInvariant(); $t=$t -replace '\^',''; return $t }
function LetterHit($text, $letter){ if(-not $letter){return $false}; return [regex]::IsMatch("$text", "(?<![a-z])$letter(?![a-z])") }

$env:ASPNETCORE_URLS = "http://localhost:5080"
$env:ASPNETCORE_ENVIRONMENT = "Development"
$srv = Start-Process -FilePath $dotnet -ArgumentList $dll `
  -WorkingDirectory $repo `
  -RedirectStandardOutput $srvOut -RedirectStandardError $srvErr -NoNewWindow -PassThru
Write-Output "server PID=$($srv.Id)"
$ready=$false
for($i=0;$i -lt 45;$i++){ try { $null=Invoke-WebRequest -Uri "http://localhost:5080/" -UseBasicParsing -TimeoutSec 3; $ready=$true; break } catch { if($srv.HasExited){Write-Output "server exited early (code $($srv.ExitCode))"; break}; Start-Sleep -Seconds 2 } }

$L = New-Object System.Collections.Generic.List[string]
$L.Add("=== gateway /process probe: Task 27 choice (5 key fixes + 3 gains) ===")
$L.Add("")
$okCount=0; $ttChoice=0; $letterHit=0
if($ready){
  Start-Sleep -Seconds 2
  foreach($id in $ids){
    $c = $caseMap[$id]
    if($null -eq $c){ $L.Add("$id : MISSING"); continue }
    $q = "$($c.question)"
    $body = @{ text = $q } | ConvertTo-Json -Compress
    $bodyBytes = [Text.Encoding]::UTF8.GetBytes($body)
    try {
      $resp = Invoke-WebRequest -Uri "http://localhost:5080/api/gateway/process" -Method Post -Body $bodyBytes -ContentType "application/json; charset=utf-8" -UseBasicParsing -TimeoutSec 50
      $raw = $resp.Content
      [IO.File]::WriteAllText("$rawDir\$id.json", $raw, $utf8)
      $j = $raw | ConvertFrom-Json
      $tt = "$($j.task_type)"
      $engOk = "$($j.engine_response.ok)"
      $final = "$($j.engine_response.final_result)"
      $reply = "$($j.reply)"
      $want = $expected[$id]
      $lh = LetterHit $final $want
      if($tt -eq 'choice'){ $ttChoice++ }
      if($engOk -eq 'True'){ $okCount++ }
      if($lh){ $letterHit++ }
      $shortFinal = $final; if($shortFinal.Length -gt 50){ $shortFinal=$shortFinal.Substring(0,50)+'...' }
      $shortReply = $reply; if($shortReply.Length -gt 40){ $shortReply=$shortReply.Substring(0,40)+'...' }
      $L.Add(("{0,-9} want={1} tt={2,-7} engOk={3,-6} letterHit={4}  final='{5}'" -f $id,$want,$tt,$engOk,$lh,$shortFinal))
    } catch {
      $L.Add(("$id : HTTP ERROR " + $_.Exception.Message))
    }
  }
} else { $L.Add("server never ready") }

if(-not $srv.HasExited){ Stop-Process -Id $srv.Id -Force -ErrorAction SilentlyContinue; $L.Add("server stopped") } else { $L.Add("server already exited") }

$L.Add("")
$L.Add("--- summary (n=8) ---")
$L.Add(("taskType=choice  = {0}/8" -f $ttChoice))
$L.Add(("engineOk=True   = {0}/8" -f $okCount))
$L.Add(("final letter hit= {0}/8" -f $letterHit))
$txt = $L -join "`r`n"
[IO.File]::WriteAllText($outFile, $txt, $utf8)
Write-Output $txt
