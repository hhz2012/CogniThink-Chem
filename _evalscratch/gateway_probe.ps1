# Targeted end-to-end probe: POST each of the 11 concept candidates to the live gateway /process,
# verifying ConceptFactSolver integrates through the real pipeline (LLM parse -> engine -> adopt).
# v2 fix: GatewayResult serializes snake_case (task_type / engine_response.final_result / reply), not camelCase.
# Also saves raw response content for structure verification.
$ErrorActionPreference = 'Continue'
$utf8 = New-Object Text.UTF8Encoding($false)
$repo = "D:\github\CogniThink-Chem"
$v2path = "$repo\_evalscratch\after_stage6_classify_v2.json"
$outFile = "$repo\_evalscratch\gateway_probe_out.txt"
$respFile = "$repo\_evalscratch\gateway_probe_responses.json"
$rawDir = "$repo\_evalscratch\probe_raw"
if(-not (Test-Path $rawDir)){ New-Item -ItemType Directory -Force -Path $rawDir | Out-Null }
$dotnet = (Get-Command dotnet).Source
$dll = "$repo\_evalscratch\apibuild\CogniThink.TutorBrain.Api.dll"
$srvOut = "$repo\_evalscratch\srv_probe_out.log"
$srvErr = "$repo\_evalscratch\srv_probe_err.log"

$ids = @('test-1','test-2','test-3','test-4','test-5','property-1','property-2','concept-1','concept-2','concept-3','pdf-510')

$v2 = [IO.File]::ReadAllText($v2path, $utf8) | ConvertFrom-Json
$rows = @(); foreach($b in 'bothCorrectCases','gatewayOnlyCorrectCases','pureLlmOnlyCorrectCases','bothWrongCases'){ if($v2.$b){ $rows += @($v2.$b) } }
$caseMap = @{}; foreach($r in $rows){ $caseMap[$r.id] = $r }

function Get-Keywords($exp){
  if($null -eq $exp){ return @() }
  $s = "$exp"
  if($s -match '^关键词[:：]\s*(.*)$'){ $s = $matches[1] }
  return ($s -split ',') | ForEach-Object { $_.Trim() } | Where-Object { $_ }
}
function Norm($s){ if($null -eq $s){return ''}; $t="$s".ToLowerInvariant(); $t=$t -replace '\^',''; return $t }
function AnyKeyword($text, $kws){ $n = Norm $text; foreach($k in $kws){ if($k -and $n.Contains((Norm $k))){ return $true } }; return $false }

# start server detached from repo root so DataRoot.Resolve finds data/ marker
$env:ASPNETCORE_URLS = "http://localhost:5080"
$env:ASPNETCORE_ENVIRONMENT = "Development"
$srv = Start-Process -FilePath $dotnet -ArgumentList $dll `
  -WorkingDirectory $repo `
  -RedirectStandardOutput $srvOut -RedirectStandardError $srvErr -NoNewWindow -PassThru
Write-Output "server PID=$($srv.Id)"
$ready=$false
for($i=0;$i -lt 60;$i++){ try { $null=Invoke-WebRequest -Uri "http://localhost:5080/" -UseBasicParsing -TimeoutSec 3; Write-Output "ready attempt $i"; $ready=$true; break } catch { if($srv.HasExited){Write-Output "server exited early"; break}; Start-Sleep -Seconds 2 } }

$results = New-Object System.Collections.Generic.List[object]
$L = New-Object System.Collections.Generic.List[string]
$L.Add("=== gateway /process probe: 11 concept candidates (live pipeline) ===")
$L.Add("")

if($ready){
  Start-Sleep -Seconds 3
  foreach($id in $ids){
    $c = $caseMap[$id]
    if($null -eq $c){ $L.Add("$id : MISSING in v2 eval"); continue }
    $q = "$($c.question)"
    $kws = Get-Keywords $c.expected
    $body = @{ text = $q } | ConvertTo-Json -Compress
    $bodyBytes = [Text.Encoding]::UTF8.GetBytes($body)
    try {
      $resp = Invoke-WebRequest -Uri "http://localhost:5080/api/gateway/process" -Method Post -Body $bodyBytes -ContentType "application/json; charset=utf-8" -UseBasicParsing -TimeoutSec 60
      $raw = $resp.Content
      # save raw content for structure verification
      [IO.File]::WriteAllText("$rawDir\$id.json", $raw, $utf8)
      $j = $raw | ConvertFrom-Json
      # snake_case field names (GatewayResult contract)
      $tt = "$($j.task_type)"
      $engOk = "$($j.engine_response.ok)"
      $final = "$($j.engine_response.final_result)"
      $reply = "$($j.reply)"
      $hit = AnyKeyword $final $kws
      $replyHit = AnyKeyword $reply $kws
      $shortFinal = $final; if($shortFinal.Length -gt 60){ $shortFinal=$shortFinal.Substring(0,60)+'...' }
      $L.Add(("{0,-12} tt={1,-14} engOk={2,-6} finalHit={3} replyHit={4}  final='{5}'" -f $id,$tt,$engOk,$hit,$replyHit,$shortFinal))
      $results.Add([pscustomobject]@{ id=$id; taskType=$tt; engineOk=$engOk; finalResult=$final; reply=$reply; finalHit=$hit; replyHit=$replyHit; keywords=($kws -join '|') })
    } catch {
      $L.Add(("$id : HTTP ERROR " + $_.Exception.Message))
    }
  }
} else { $L.Add("server never ready") }

# stop server
if(-not $srv.HasExited){ Stop-Process -Id $srv.Id -Force -ErrorAction SilentlyContinue; $L.Add("server stopped") } else { $L.Add("server already exited") }

$L.Add("")
$L.Add("--- summary ---")
$arr = $results.ToArray()
$hitCount = (@($arr | Where-Object { $_.finalHit })).Count
$cfCount = (@($arr | Where-Object { $_.taskType -eq 'concept_fact' })).Count
$okCount = (@($arr | Where-Object { $_.engineOk -eq 'True' })).Count
$L.Add(("taskType=concept_fact = {0}/11" -f $cfCount))
$L.Add(("engineOk=True       = {0}/11" -f $okCount))
$L.Add(("finalHit(keyword)   = {0}/11" -f $hitCount))

$txt = $L -join "`r`n"
[IO.File]::WriteAllText($outFile, $txt, $utf8)
$results | ConvertTo-Json -Depth 6 | Out-File -FilePath $respFile -Encoding utf8
Write-Output $txt
