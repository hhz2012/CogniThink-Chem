# 阶段3F 核对：equilibrium 曲线 harm 修复（pdf-259 / pdf-272）
# 对比 after_stage3e.json（基线）→ after_stage3f.json（修复后）
# 红线：harmCount 2→0；equilibrium lost=[]；gained 含 pdf-259；无新增全局 harm；enginePrecision 不降。
# 输出同时打印到控制台并写入 verify_stage3f_out.txt（UTF-8）。
# 运行方式（重要）：PS5.1 可能按 ANSI(GBK) 读取本脚本，中文注释会干扰解析导致 eqB=0、结果异常。
# 可靠做法：$c=[IO.File]::ReadAllText($PSScriptRoot+'\verify_stage3f.ps1',[Text.Encoding]::UTF8); iex $c
# （内容本身正确；直接 -File/点源若异常，改动任一行或按上述 UTF-8 读入即可。）

function B($v) { return ($null -ne $v) -and ($v -is [bool]) -and $v }

function Rows($d) {
    $o = @()
    foreach ($b in 'bothCorrectCases','gatewayOnlyCorrectCases','pureLlmOnlyCorrectCases','bothWrongCases') {
        if ($null -ne $d.$b) { foreach ($c in $d.$b) { $o += $c } }
    }
    return $o
}

function ScopeStats($rows, $tt) {
    $rs = @($rows | Where-Object { $_.taskType -eq $tt })
    $n = $rs.Count
    $g = @($rs | Where-Object { B $_.gatewayCorrect }).Count
    $a = @($rs | Where-Object { B $_.gatewayTruthAdopted }).Count
    $ac = @($rs | Where-Object { (B $_.gatewayTruthAdopted) -and (B $_.gatewayCorrect) }).Count
    $h = @($rs | Where-Object { (B $_.gatewayTruthAdopted) -and (-not (B $_.gatewayCorrect)) -and (B $_.pureLlmCorrect) }).Count
    $p = if ($a -gt 0) { $ac / $a } else { 0.0 }
    return [pscustomobject]@{ n = $n; gcorr = $g; adopted = $a; adopted_correct = $ac; harm = $h; prec = $p }
}

# 显式 UTF-8 读取：PS5.1 默认按 ANSI(GBK) 读取会导致中文乱码、JSON 解析失败
$utf8 = [System.Text.Encoding]::UTF8
$base = [System.IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage3e.json", $utf8) | ConvertFrom-Json
$new  = [System.IO.File]::ReadAllText("D:\github\CogniThink-Chem\_evalscratch\after_stage3f.json", $utf8) | ConvertFrom-Json

$rb = Rows $base
$rn = Rows $new
$sb = ScopeStats $rb 'equilibrium'
$sn = ScopeStats $rn 'equilibrium'

# equilibrium 逐案翻转（嵌套循环，避免哈希表在 .ps1 下的索引异常）
$eqB = @($rb | Where-Object { $_.taskType -eq 'equilibrium' })
$eqN = @($rn | Where-Object { $_.taskType -eq 'equilibrium' })
$lost = @(); $gained = @()
foreach ($b in $eqB) {
    $n = $eqN | Where-Object { $_.id -eq $b.id } | Select-Object -First 1
    if ($null -ne $n) {
        $c3 = B $b.gatewayCorrect
        $c3f = B $n.gatewayCorrect
        if ($c3 -and -not $c3f) { $lost += $b.id }
        if (-not $c3 -and $c3f) { $gained += $b.id }
    }
}

# 全局 harm 扫描
$globalHarm = @($rn | Where-Object { (B $_.gatewayTruthAdopted) -and (-not (B $_.gatewayCorrect)) -and (B $_.pureLlmCorrect) })

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("=== stage3F verification: equilibrium curve harm fix (pdf-259/pdf-272) ===")
$lines.Add("")
$lines.Add("--- top-level metrics (stage3E -> stage3F) ---")
foreach ($k in 'totalCases','gatewayCorrect','gatewayTruthCorrect','gatewayEngineUsed','engineSolved','enginePrecision','harmCount') {
    $lines.Add(("  {0,-20}: {1} -> {2}" -f $k, $base.$k, $new.$k))
}
$lines.Add("")
$lines.Add("--- equilibrium scope (stage3E -> stage3F) ---")
foreach ($k in 'n','gcorr','adopted','adopted_correct','harm') {
    $lines.Add(("  {0,-16}: {1} -> {2}  (delta {3:+0})" -f $k, $sb.$k, $sn.$k, ($sn.$k - $sb.$k)))
}
$lines.Add(("  {0,-16}: {1:F4} -> {2:F4}" -f 'precision', $sb.prec, $sn.prec))
$lines.Add("  eq LOST  (correct->wrong) : [$($lost -join ', ')]   (must be empty)")
$lines.Add("  eq GAINED (wrong->correct) : [$(($gained | Sort-Object) -join ', ')]   (must contain pdf-259)")
$lines.Add("")
$lines.Add("--- global harm scan (adopted && !correct && pureLlmCorrect) ---")
$lines.Add("  stage3F global harm count = $($globalHarm.Count)")
foreach ($h in $globalHarm) { $lines.Add("    - $($h.id) [$($h.taskType)] concl=$($h.gatewayConclusion)") }
$lines.Add("")
$lines.Add("--- target case check ---")
foreach ($id in 'pdf-259','pdf-272') {
    $r = $eqN | Where-Object { $_.id -eq $id } | Select-Object -First 1
    if ($null -eq $r) { $lines.Add("  $id : MISSING"); continue }
    $lines.Add("  $id : gwCorrect=$(B $r.gatewayCorrect) adopted=$(B $r.gatewayTruthAdopted) pureLlmCorrect=$(B $r.pureLlmCorrect)")
    $lines.Add("         concl=$($r.gatewayConclusion)")
}
$lines.Add("")

$pass = $true
function Ck($name, $cond) {
    $script:pass = $script:pass -and $cond
    $script:lines.Add(("  {0,-34}: {1}" -f $name, $(if ($cond) { 'PASS' } else { 'FAIL' })))
}
$lines.Add("--- red lines ---")
Ck 'global harm 2 -> 0'                     ($new.harmCount -eq 0)
Ck 'equilibrium lost = []'                  ($lost.Count -eq 0)
Ck 'equilibrium gained contains pdf-259'    ($gained -contains 'pdf-259')
Ck 'no new global harm'                     ($globalHarm.Count -eq 0)
Ck 'enginePrecision not drop'               ([double]$new.enginePrecision -ge [double]$base.enginePrecision - 1e-9)
$p259 = $eqN | Where-Object { $_.id -eq 'pdf-259' } | Select-Object -First 1
$p272 = $eqN | Where-Object { $_.id -eq 'pdf-272' } | Select-Object -First 1
Ck 'pdf-259 adopted & correct'              ((B $p259.gatewayTruthAdopted) -and (B $p259.gatewayCorrect))
Ck 'pdf-272 non-harm (not adopted)'         (-not (B $p272.gatewayTruthAdopted))
$lines.Add("")
$lines.Add("OVERALL: $(if ($pass) { 'PASS' } else { 'FAIL' })")

$txt = $lines -join "`r`n"
$outPath = "D:\github\CogniThink-Chem\_evalscratch\verify_stage3f_out.txt"
[System.IO.File]::WriteAllText($outPath, $txt, [System.Text.UTF8Encoding]::new($false))
Write-Output $txt
Write-Output "`n(saved to $outPath)"
