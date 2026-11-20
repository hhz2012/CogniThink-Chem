# Robust line-based edit: remove old atomic block (after titration), insert new one before calculation.
# Uses single-quoted literal strings (double quotes inside are fine). Run via UTF-8 iex.
$path = 'D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\Application\Gateway\LlmGatewayService.cs'
$utf8 = New-Object Text.UTF8Encoding($false)
$lines = [IO.File]::ReadAllLines($path, $utf8)
$list = New-Object System.Collections.Generic.List[string]
$list.AddRange([string[]]$lines)

# --- 1. Remove old atomic block (comment + if + return), the only occurrence in original ---
$rmIdx = -1
for ($i = 0; $i -lt $list.Count; $i++) {
    if ($list[$i].Contains('守卫见 AtomicStructureSolver')) { $rmIdx = $i; break }
}
if ($rmIdx -lt 0) { Write-Error 'old atomic comment not found'; exit 1 }
# remove 3 lines: comment, if, return
$list.RemoveRange($rmIdx, 3)

# --- 2. Insert new atomic block right after the equilibrium return line ---
$insIdx = -1
for ($i = 0; $i -lt $list.Count; $i++) {
    if ($list[$i].Contains('return "equilibrium";')) { $insIdx = $i; break }
}
if ($insIdx -lt 0) { Write-Error 'equilibrium return not found'; exit 1 }
$block = [string[]]@(
    '        // 原子结构：电子排布 / 轨道表示式 / 未成对电子（唯一结构可解，守卫见 AtomicStructureSolver）。',
    '        // 先于 calculation：含 mol/pH 的电子排布题干（如 pdf-533）不能被 calculation 截走；回归预检查确认无已判正题命中这些标记。',
    '        if (lower.Contains("电子排布") || lower.Contains("轨道表示式") || lower.Contains("未成对电子"))',
    '            return "atomic_structure";'
)
$list.InsertRange($insIdx + 1, $block)

[IO.File]::WriteAllLines($path, $list, $utf8)

# verify
$c2 = [IO.File]::ReadAllText($path, $utf8)
$cnt = ([regex]::Matches($c2, 'return "atomic_structure"')).Count
$cnt2 = ([regex]::Matches($c2, '守卫见 AtomicStructureSolver')).Count
Write-Output ("atomic_structure return count: {0} (expect 1); comment count: {1} (expect 1)" -f $cnt, $cnt2)
