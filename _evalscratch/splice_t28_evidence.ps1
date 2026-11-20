# Mark Task 28 done + insert its evidence block before the Task 29 heading.
$tasksPath = "D:\github\CogniThink-Chem\.trae\specs\chem-engine-question-solving\tasks.md"
$evPath    = "D:\github\CogniThink-Chem\_evalscratch\t28_evidence_block.txt"

$tasks = [IO.File]::ReadAllLines($tasksPath, [Text.Encoding]::UTF8)
$ev    = [IO.File]::ReadAllLines($evPath, [Text.Encoding]::UTF8)

$t28 = -1; $t29 = -1
for ($i = 0; $i -lt $tasks.Length; $i++) {
    if ($tasks[$i] -match '^## Task 28') { $t28 = $i }
    if ($tasks[$i] -match '^## Task 29') { $t29 = $i; break }
}
if ($t28 -lt 0) { throw "Task 28 heading not found" }
if ($t29 -lt 0) { throw "Task 29 heading not found" }
if ($t29 -le $t28) { throw "Task 29 must come after Task 28" }

$statusIdx = -1
for ($i = $t28 + 1; $i -lt $t29; $i++) {
    if ($tasks[$i] -match '^- \*\*Status\*\*:\s*pending') { $statusIdx = $i; break }
}
if ($statusIdx -lt 0) { throw "Task 28 Status:pending line not found between Task 28 and Task 29" }

$list = New-Object System.Collections.Generic.List[string]
$list.AddRange([string[]]$tasks)
$list[$statusIdx] = '- **Status**: done'

$insert = New-Object System.Collections.Generic.List[string]
foreach ($l in $ev) { $insert.Add($l) }
$insert.Add("")
$list.InsertRange($t29, $insert)

$utf8NoBom = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllLines($tasksPath, $list, $utf8NoBom)
Write-Output ("Flipped Task 28 Status->done at line $statusIdx; inserted " + $insert.Count + " evidence lines before Task 29 (was line $t29); total now " + $list.Count)
