$files = Get-ChildItem -Recurse -File -Path "src/CogniThink.TutorBrain.Api/Application/Gateway" -Filter "*.cs" -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch '\\bin\\|\\obj\\' }
$out = @()
foreach($f in $files){
  $ns = Select-String -Path $f.FullName -Pattern "^namespace " | Select-Object -First 1
  if($ns){ $out += "$($f.Name) => $($ns.Line.Trim())" }
}
$out | Out-File -FilePath _evalscratch\_ns.txt -Encoding utf8
