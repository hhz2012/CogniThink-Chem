# Mark Task 26 done + insert its evidence block before the Task 27 heading.
$tasksPath = "D:\github\CogniThink-Chem\.trae\specs\chem-engine-question-solving\tasks.md"
$evPath    = "D:\github\CogniThink-Chem\_evalscratch\t26_evidence_block.txt"

$tasks = [IO.File]::ReadAllLines($tasksPath, [Text.Encoding]::UTF8)
$ev    = [IO.File]::ReadAllLines($evPath, [Text.Encoding]::UTF8)

$t26 = -1; $t27 = -1
for ($i = 0; $i -lt $tasks.Length; $i++) {
    if ($tasks[$i] -match '^## Task 26') { $t26 = $i }
    if ($tasks[$i] -match '^## Task 27') { $t27 = $i; break }
}
if ($t26 -lt 0) { throw "Task 26 heading not found" }
if ($t27 -lt 0) { throw "Task 27 heading not found" }
if ($t27 -le $t26) { throw "Task 27 must come after Task 26" }

# 1) flip Task 26 Status pending -> done (the first Status line after the Task 26 heading)
$statusIdx = -1
for ($i = $t26 + 1; $i -lt $t27; $i++) {
    if ($tasks[$i] -match '^- \*\*Status\*\*:\s*pending') { $statusIdx = $i; break }
}
if ($statusIdx -lt 0) { throw "Task 26 Status:pending line not found between Task 26 and Task 27" }

$list = New-Object System.Collections.Generic.List[string]
$list.AddRange([string[]]$tasks)
$list[$statusIdx] = '- **Status**: done'

# 2) insert evidence block + trailing blank line before the Task 27 heading
$insert = New-Object System.Collections.Generic.List[string]
foreach ($l in $ev) { $insert.Add($l) }
$insert.Add("")
$list.InsertRange($t27, $insert)

$utf8NoBom = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllLines($tasksPath, $list, $utf8NoBom)
Write-Output ("Flipped Task 26 Status->done at line $statusIdx; inserted " + $insert.Count + " evidence lines before Task 27 (was line $t27); total now " + $list.Count)
