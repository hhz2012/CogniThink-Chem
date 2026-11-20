# Orchestrator: start detached server + detached eval client, poll for done marker.
$ErrorActionPreference = 'Continue'
$utf8 = New-Object Text.UTF8Encoding($false)
$dotnet = (Get-Command dotnet).Source
$proj   = "D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\CogniThink.TutorBrain.Api.csproj"
$client = "D:\github\CogniThink-Chem\_evalscratch\eval_client.ps1"
$done   = "D:\github\CogniThink-Chem\_evalscratch\after_stage6_concept.done"
$json   = "D:\github\CogniThink-Chem\_evalscratch\after_stage6_concept.json"
$srvOut = "D:\github\CogniThink-Chem\_evalscratch\srv3_out.log"
$srvErr = "D:\github\CogniThink-Chem\_evalscratch\srv3_err.log"
$cliOut = "D:\github\CogniThink-Chem\_evalscratch\cli3_out.log"
$cliErr = "D:\github\CogniThink-Chem\_evalscratch\cli3_err.log"

Remove-Item $done -ErrorAction SilentlyContinue
Remove-Item $json -ErrorAction SilentlyContinue

$env:ASPNETCORE_URLS = "http://localhost:5080"
$srv = Start-Process -FilePath $dotnet -ArgumentList "run","-c","Release","--no-build","--project",$proj `
  -RedirectStandardOutput $srvOut -RedirectStandardError $srvErr -NoNewWindow -PassThru
Write-Output "server PID=$($srv.Id)"

# wait ready
$ready = $false
for ($i=0; $i -lt 50; $i++) {
  try { $null = Invoke-WebRequest -Uri "http://localhost:5080/" -UseBasicParsing -TimeoutSec 3; Write-Output "ready at attempt $i"; $ready = $true; break }
  catch { if ($srv.HasExited) { Write-Output "server exited early code=$($srv.ExitCode)"; break }; Start-Sleep -Seconds 2 }
}

if ($ready) {
  Start-Sleep -Seconds 3
  $cli = Start-Process -FilePath "powershell.exe" -ArgumentList "-NoProfile","-File",$client `
    -RedirectStandardOutput $cliOut -RedirectStandardError $cliErr -NoNewWindow -PassThru
  Write-Output "client PID=$($cli.Id); polling for done marker (max ~500s)..."
  for ($i=0; $i -lt 100; $i++) {
    if (Test-Path $done) { Write-Output "marker appeared at poll $i"; break }
    if ($srv.HasExited) { Write-Output "server DIED at poll $i (exit=$($srv.ExitCode))"; break }
    if ($cli.HasExited) { Write-Output "client exited at poll $i (exit=$($cli.ExitCode))"; break }
    Start-Sleep -Seconds 5
  }
  if (Test-Path $done) {
    $m = [IO.File]::ReadAllText($done, $utf8).Trim()
    Write-Output "MARKER=$m"
    if ((Test-Path $json) -and $m -eq "DONE") {
      $len = (Get-Item $json).Length
      Write-Output "JSON saved size=$len"
    }
  } else {
    Write-Output "NO MARKER (timeout or died). server alive=$(-not $srv.HasExited) client alive=$(-not $cli.HasExited)"
  }
}

# cleanup
if (-not $srv.HasExited) { Stop-Process -Id $srv.Id -Force -ErrorAction SilentlyContinue; Write-Output "server stopped" }
if (-not $cli.HasExited) { Stop-Process -Id $cli.Id -Force -ErrorAction SilentlyContinue; Write-Output "client stopped" }
Write-Output "orchestrator done"
