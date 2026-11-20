$path = "_evalscratch/checkpoint_choice_base_now.jsonl"
$lines = Get-Content $path -Encoding UTF8
foreach($line in $lines){
  if($line -match '"id"\s*:\s*"ion-6"'){
    $line | Out-File -FilePath "_evalscratch/_ion6_raw.txt" -Encoding utf8
    break
  }
}
"done"
