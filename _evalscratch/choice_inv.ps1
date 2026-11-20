$ErrorActionPreference = 'Stop'
$root = 'd:\github\CogniThink-Chem'
$ckpt = Join-Path $root '_evalscratch\checkpoint_choice_base.jsonl'
$kwFile = Join-Path $root '_evalscratch\choice_keywords.txt'
$outMd = Join-Path $root '_evalscratch\choice_unsolved_report_v2.md'
$outTsv = Join-Path $root '_evalscratch\choice_unsolved_v2.tsv'

$utf8 = New-Object System.Text.UTF8Encoding($false)

# --- load keyword categories from UTF-8 file ---
$kwByCat = @{}
foreach ($l in [System.IO.File]::ReadAllLines($kwFile, $utf8)) {
  if ($l -match '^\s*#') { continue }
  if ($l.Trim() -eq '') { continue }
  $idx = $l.IndexOf("`t")
  if ($idx -lt 0) { continue }
  $cat = $l.Substring(0, $idx).Trim()
  $kw = $l.Substring($idx + 1)
  if (-not $kwByCat.ContainsKey($cat)) { $kwByCat[$cat] = New-Object System.Collections.Generic.List[string] }
  $kwByCat[$cat].Add($kw)
}

$rows = New-Object System.Collections.Generic.List[object]
$parseFailIds = New-Object System.Collections.Generic.List[string]

foreach ($line in [System.IO.File]::ReadLines($ckpt, $utf8)) {
  if ([string]::IsNullOrWhiteSpace($line)) { continue }
  $rec = $null
  try { $rec = $line | ConvertFrom-Json } catch { continue }
  if ($null -eq $rec) { continue }
  $tt = [string]$rec.taskType
  if ($tt -ne 'choice') { continue }
  if ($rec.gatewayCorrect -eq $true) { continue }
  $c = $rec.case
  $id = [string]$c.id
  $q = [string]$c.question
  $ans = [string]$c.answer
  $isPdf = [bool]$c.isPdf
  $imgCount = 0
  if ($null -ne $c.images) { $imgCount = @($c.images).Count }
  $assetTypes = New-Object System.Collections.Generic.List[string]
  if ($null -ne $c.imageAssets) {
    foreach ($a in @($c.imageAssets)) {
      $t = [string]$a.Type
      if ($t -ne '') { $assetTypes.Add($t) }
    }
  }
  $optCount = 0
  if ($null -ne $c.options) { $optCount = ($c.options.PSObject.Properties | Measure-Object).Count }
  $ansTrim = $ans.Trim()
  $shape = 'other'
  if ($ansTrim -match '^[A-D]$') { $shape = 'single' }
  elseif ($ansTrim -match '^[A-D]{2,4}$') { $shape = 'multi' }
  $buckets = New-Object System.Collections.Generic.List[string]
  foreach ($cat in $kwByCat.Keys) {
    foreach ($kw in $kwByCat[$cat]) {
      if ($q.Contains($kw)) { if (-not $buckets.Contains($cat)) { $buckets.Add($cat) }; break }
    }
  }
  $rows.Add([pscustomobject]@{
    id = $id; isPdf = $isPdf; usedEngine = [bool]$rec.gatewayUsedEngine
    hasImages = ($imgCount -gt 0); imgCount = $imgCount
    hasAssets = ($assetTypes.Count -gt 0); assetTypes = ($assetTypes -join ',')
    optCount = $optCount; ansShape = $shape
    buckets = ($buckets -join ','); question = $q
  })
}

# pdf-412 failed ConvertFrom-Json; add manually as known C/choice data defect
$rows.Add([pscustomobject]@{
  id = 'pdf-412'; isPdf = $true; usedEngine = $false
  hasImages = $false; imgCount = 0; hasAssets = $false; assetTypes = ''
  optCount = 0; ansShape = 'other'; buckets = 'PARSEFAIL'; question = '(options duplicate-key parse failure; data defect)'
})

# --- aggregate ---
function CountOf($pred) { ($rows | Where-Object $pred).Count }

$total = $rows.Count
$nB = CountOf { $_.usedEngine -eq $true }
$nC = CountOf { $_.usedEngine -eq $false }
$nAsset = CountOf { $_.hasAssets -eq $true }
$nImgOnly = CountOf { $_.hasAssets -eq $false -and $_.hasImages -eq $true }
$nNoImg = CountOf { $_.hasAssets -eq $false -and $_.hasImages -eq $false }
$nMulti = CountOf { $_.ansShape -eq 'multi' }
$nSingle = CountOf { $_.ansShape -eq 'single' }
$nOtherAns = CountOf { $_.ansShape -eq 'other' }
$nOpt4 = CountOf { $_.optCount -eq 4 }
$nOptLt4 = CountOf { $_.optCount -lt 4 }

# asset type distribution
$assetDist = @{}
foreach ($r in $rows) {
  if ($r.assetTypes -eq '') { continue }
  foreach ($t in ($r.assetTypes -split ',')) {
    if ($t -eq '') { continue }
    if (-not $assetDist.ContainsKey($t)) { $assetDist[$t] = 0 }
    $assetDist[$t]++
  }
}

# bucket distribution (a row can be in multiple)
$bucketDist = @{}
foreach ($r in $rows) {
  if ($r.buckets -eq '') { continue }
  foreach ($b in ($r.buckets -split ',')) {
    if ($b -eq '') { continue }
    if (-not $bucketDist.ContainsKey($b)) { $bucketDist[$b] = 0 }
    $bucketDist[$b]++
  }
}

# primary bucket assignment (priority order)
function PrimaryBucket($r) {
  if ($r.buckets -eq 'PARSEFAIL') { return 'PARSEFAIL' }
  if ($r.hasAssets) { return 'ASSET' }
  if ($r.optCount -lt 4) { return 'NODATA' }
  if ($r.ansShape -eq 'multi' -or $r.buckets -match 'multiselect') { return 'MULTI' }
  $b = $r.buckets
  if ($b -match 'calc') { return 'CALC' }
  if ($b -match 'nuclide') { return 'NUCLIDE' }
  if ($b -match 'curve') { return 'CURVE' }
  if ($b -match 'orgstruct') { return 'ORGSTRUCT' }
  if ($b -match 'electrochem') { return 'ELECTROCHEM' }
  if ($b -match 'crystal') { return 'CRYSTAL' }
  if ($b -match 'exp') { return 'EXP' }
  return 'FACT'
}
foreach ($r in $rows) { $r | Add-Member -NotePropertyName primary -NotePropertyValue (PrimaryBucket $r) -Force }

$primDist = @{}
foreach ($r in $rows) {
  $p = $r.primary
  if (-not $primDist.ContainsKey($p)) { $primDist[$p] = New-Object System.Collections.Generic.List[string] }
  $primDist[$p].Add($r.id)
}

# --- write markdown ---
$md = New-Object System.Collections.Generic.List[string]
$md.Add('# choice unsolved inventory (from checkpoint_cur_engine.jsonl)')
$md.Add('')
$md.Add("- total unsolved choice (not gatewayCorrect): $total")
$md.Add("- B (engaged, not adopted): $nB")
$md.Add("- C (not engaged / Ok=null): $nC")
$md.Add('')
$md.Add('## by answer shape')
$md.Add("- single letter: $nSingle")
$md.Add("- multi letter: $nMulti")
$md.Add("- other: $nOtherAns")
$md.Add('')
$md.Add('## by image/asset presence')
$md.Add("- has structured assets (graph/curve/molecule/figure): $nAsset")
$md.Add("- has raw images but no parsed assets: $nImgOnly")
$md.Add("- no images at all (pure text): $nNoImg")
$md.Add('')
$md.Add('## by options completeness')
$md.Add("- 4 options present: $nOpt4")
$md.Add("- <4 options (data defect): $nOptLt4")
$md.Add('')
$md.Add('## asset type distribution (multi-count)')
foreach ($k in ($assetDist.Keys | Sort-Object)) { $md.Add("- $k : $($assetDist[$k])") }
$md.Add('')
$md.Add('## keyword bucket distribution (multi-count)')
foreach ($k in ($bucketDist.Keys | Sort-Object)) { $md.Add("- $k : $($bucketDist[$k])") }
$md.Add('')
$md.Add('## PRIMARY bucket (priority: PARSEFAIL > ASSET > NODATA > MULTI > CALC > NUCLIDE > CURVE > ORGSTRUCT > ELECTROCHEM > CRYSTAL > EXP > FACT)')
foreach ($k in ($primDist.Keys | Sort-Object)) {
  $ids = ($primDist[$k] | Sort-Object) -join ', '
  $md.Add("### $k ($($primDist[$k].Count))")
  $md.Add($ids)
  $md.Add('')
}
[System.IO.File]::WriteAllLines($outMd, $md, $utf8)

# --- write tsv (id, primary, usedEngine, isPdf, hasAssets, assetTypes, hasImages, optCount, ansShape, buckets, question) ---
$tsv = New-Object System.Collections.Generic.List[string]
$tsv.Add('id' + "`t" + 'primary' + "`t" + 'usedEngine' + "`t" + 'isPdf' + "`t" + 'hasAssets' + "`t" + 'assetTypes' + "`t" + 'hasImages' + "`t" + 'optCount' + "`t" + 'ansShape' + "`t" + 'buckets' + "`t" + 'question')
foreach ($r in ($rows | Sort-Object primary, id)) {
  $qn = $r.question -replace "`r?`n", ' '
  if ($qn.Length -gt 200) { $qn = $qn.Substring(0, 200) }
  $tsv.Add(($r.id, $r.primary, $r.usedEngine, $r.isPdf, $r.hasAssets, $r.assetTypes, $r.hasImages, $r.optCount, $r.ansShape, $r.buckets, $qn) -join "`t")
}
[System.IO.File]::WriteAllLines($outTsv, $tsv, $utf8)

Write-Output ("WROTE " + $outMd)
Write-Output ("WROTE " + $outTsv)
Write-Output ("total unsolved choice = " + $total)
