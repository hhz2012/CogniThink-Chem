# Mark Task 27 done + insert its evidence block before the Task 28 heading.
$tasksPath = "D:\github\CogniThink-Chem\.trae\specs\chem-engine-question-solving\tasks.md"
$evPath    = "D:\github\CogniThink-Chem\_evalscratch\t27_evidence_block.txt"

$tasks = [IO.File]::ReadAllLines($tasksPath, [Text.Encoding]::UTF8)
$ev    = [IO.File]::ReadAllLines($evPath, [Text.Encoding]::UTF8)

$t27 = -1; $t28 = -1
for ($i = 0; $i -lt $tasks.Length; $i++) {
    if ($tasks[$i] -match '^## Task 27') { $t27 = $i }
    if ($tasks[$i] -match '^## Task 28') { $t28 = $i; break }
}
if ($t27 -lt 0) { throw "Task 27 heading not found" }
if ($t28 -lt 0) { throw "Task 28 heading not found" }
if ($t28 -le $t27) { throw "Task 28 must come after Task 27" }

# 1) flip Task 27 Status pending -> done (the first Status line after the Task 27 heading)
$statusIdx = -1
for ($i = $t27 + 1; $i -lt $t28; $i++) {
    if ($tasks[$i] -match '^- \*\*Status\*\*:\s*pending') { $statusIdx = $i; break }
}
if ($statusIdx -lt 0) { throw "Task 27 Status:pending line not found between Task 27 and Task 28" }

$list = New-Object System.Collections.Generic.List[string]
$list.AddRange([string[]]$tasks)
$list[$statusIdx] = '- **Status**: done'

# 2) insert evidence block + trailing blank line before the Task 28 heading
$insert = New-Object System.Collections.Generic.List[string]
foreach ($l in $ev) { $insert.Add($l) }
$insert.Add("")
$list.InsertRange($t28, $insert)

$utf8NoBom = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllLines($tasksPath, $list, $utf8NoBom)
Write-Output ("Flipped Task 27 Status->done at line $statusIdx; inserted " + $insert.Count + " evidence lines before Task 28 (was line $t28); total now " + $list.Count)
