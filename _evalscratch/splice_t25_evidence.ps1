# Mark Task 25 done + insert its evidence block before the Task 26 heading.
$tasksPath = "D:\github\CogniThink-Chem\.trae\specs\chem-engine-question-solving\tasks.md"
$evPath    = "D:\github\CogniThink-Chem\_evalscratch\t25_evidence_block.txt"

$tasks = [IO.File]::ReadAllLines($tasksPath, [Text.Encoding]::UTF8)
$ev    = [IO.File]::ReadAllLines($evPath, [Text.Encoding]::UTF8)

$t25 = -1; $t26 = -1
for ($i = 0; $i -lt $tasks.Length; $i++) {
    if ($tasks[$i] -match '^## Task 25') { $t25 = $i }
    if ($tasks[$i] -match '^## Task 26') { $t26 = $i; break }
}
if ($t25 -lt 0) { throw "Task 25 heading not found" }
if ($t26 -lt 0) { throw "Task 26 heading not found" }
if ($t26 -le $t25) { throw "Task 26 must come after Task 25" }

# 1) flip Task 25 Status pending -> done (the first Status line after the Task 25 heading)
$statusIdx = -1
for ($i = $t25 + 1; $i -lt $t26; $i++) {
    if ($tasks[$i] -match '^- \*\*Status\*\*:\s*pending') { $statusIdx = $i; break }
}
if ($statusIdx -lt 0) { throw "Task 25 Status:pending line not found between Task 25 and Task 26" }

$list = New-Object System.Collections.Generic.List[string]
$list.AddRange([string[]]$tasks)
$list[$statusIdx] = '- **Status**: done'

# 2) insert evidence block + trailing blank line before the Task 26 heading
$insert = New-Object System.Collections.Generic.List[string]
foreach ($l in $ev) { $insert.Add($l) }
$insert.Add("")
$list.InsertRange($t26, $insert)

$utf8NoBom = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllLines($tasksPath, $list, $utf8NoBom)
Write-Output ("Flipped Task 25 Status->done at line $statusIdx; inserted " + $insert.Count + " evidence lines before Task 26 (was line $t26); total now " + $list.Count)
