$ErrorActionPreference='Stop'
# 归一化：与 C# FactChoiceMatcher.NormOpt 保持一致（ClassificationValidator.Norm + 破折号→连字符）
function Norm([string]$s){
  if([string]::IsNullOrEmpty($s)){return ""}
  $sb = New-Object System.Text.StringBuilder
  foreach($ch in $s.ToCharArray()){
    if([char]::IsWhiteSpace($ch)){continue}
    $c=[int]$ch
    if($c -ge 0x2080 -and $c -le 0x2089){[void]$sb.Append([char](0x30+($c-0x2080)));continue}
    if($c -eq 0x2070){[void]$sb.Append('0');continue}
    if($c -eq 0x00B9){[void]$sb.Append('1');continue}
    if($c -eq 0x00B2){[void]$sb.Append('2');continue}
    if($c -eq 0x00B3){[void]$sb.Append('3');continue}
    if($c -ge 0x2074 -and $c -le 0x2079){[void]$sb.Append([char](0x34+($c-0x2074)));continue}
    if($c -eq 0x207A){[void]$sb.Append('+');continue}
    if($c -eq 0x207B){[void]$sb.Append('-');continue}
    if($c -ge 0xFF01 -and $c -le 0xFF5E){[void]$sb.Append([char]($c-0xFEE0));continue}
    if($c -eq 0x3000){continue}
    [void]$sb.Append($ch)
  }
  $r=$sb.ToString()
  return $r.Replace([char]0x2014,'-').Replace([char]0x2013,'-').Replace([char]0x2212,'-')
}
function ParseOpts([string]$raw){
  $opts=@{}
  $ms=[regex]::Matches($raw,'(^|\n)\s*(?<letter>[A-D])[\.\uFF0E\u3001]\s')
  if($ms.Count -eq 0){return @($raw.Trim(),$opts)}
  for($i=0;$i -lt $ms.Count;$i++){
    $m=$ms[$i]; $l=$m.Groups['letter'].Value; $s=$m.Index+$m.Length
    $e= if($i+1 -lt $ms.Count){$ms[$i+1].Index}else{$raw.Length}
    $opts[$l]=$raw.Substring($s,$e-$s).Trim()
  }
  $stem=$raw.Substring(0,$ms[0].Index).Trim()
  return @($stem,$opts)
}
$j = Get-Content "d:\github\CogniThink-Chem\_evalscratch\task_inputs\T3.json" -Raw | ConvertFrom-Json
$organic = Get-Content "d:\github\CogniThink-Chem\data\highschool\organic-facts.json" -Raw | ConvertFrom-Json
$struct = Get-Content "d:\github\CogniThink-Chem\data\highschool\structure-periodic-facts.json" -Raw | ConvertFrom-Json
$script:bad = 0
function Check($file,$facts,$id){
  $q=$j|Where-Object{$_.id -eq $id}
  if(-not $q){ Write-Host "!! $id not found"; $script:bad++; return }
  $p=ParseOpts $q.question; $stemN=Norm $p[0]
  Write-Host "===== $id (file=$file) ====="
  $allOne=$true
  foreach($k in 'A','B','C','D'){
    if(-not $p[1].ContainsKey($k)){continue}
    $optN=Norm $p[1][$k]
    $matched=@()
    foreach($f in $facts.statements){
      $ok=$true
      foreach($mk in $f.match){ if(-not $optN.Contains((Norm $mk))){$ok=$false;break} }
      if($ok){ foreach($sk in $f.stemMatch){ if(-not $stemN.Contains((Norm $sk))){$ok=$false;break} } }
      if($ok){ $matched += "$($f.id)=v$($f.verdict)" }
    }
    if($matched.Count -ne 1){ $allOne=$false }
    Write-Host ("  {0}: matches={1}  [{2}]" -f $k,($matched -join ','),$p[1][$k])
  }
  if($allOne){ Write-Host "  => ALL OPTIONS EXACT-1 MATCH" } else { Write-Host "  => !! NON-EXACT-1 DETECTED"; $script:bad++ }
}
$orgIds='pdf-335','pdf-366','pdf-369','pdf-373','pdf-374','pdf-375','pdf-379','pdf-385','pdf-392','pdf-443','pdf-447','pdf-448','pdf-449'
$stIds='pdf-069','pdf-154','pdf-181','pdf-183','pdf-186','pdf-192','pdf-200','pdf-209','pdf-210','pdf-320','pdf-326','pdf-430','pdf-499','pdf-500','pdf-501','pdf-502'
foreach($id in $orgIds){ Check "organic" $organic $id }
foreach($id in $stIds){ Check "structure" $struct $id }
Write-Host ""
Write-Host ("=== SUMMARY: {0} questions checked, {1} with non-exact-1 options ===" -f ($orgIds.Count+$stIds.Count),$script:bad)
