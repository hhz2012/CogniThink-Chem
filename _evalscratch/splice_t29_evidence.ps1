# Mark Task 29 done + insert its evidence block before the Task 30 heading.
$tasksPath = "D:\github\CogniThink-Chem\.trae\specs\chem-engine-question-solving\tasks.md"
$evPath    = "D:\github\CogniThink-Chem\_evalscratch\t29_evidence_block.txt"

$tasks = [IO.File]::ReadAllLines($tasksPath, [Text.Encoding]::UTF8)
$ev    = [IO.File]::ReadAllLines($evPath, [Text.Encoding]::UTF8)

$t29 = -1; $t30 = -1
for ($i = 0; $i -lt $tasks.Length; $i++) {
    if ($tasks[$i] -match '^## Task 29') { $t29 = $i }
    if ($tasks[$i] -match '^## Task 30') { $t30 = $i; break }
}
if ($t29 -lt 0) { throw "Task 29 heading not found" }
if ($t30 -lt 0) { throw "Task 30 heading not found" }
if ($t30 -le $t29) { throw "Task 30 must come after Task 29" }

$statusIdx = -1
for ($i = $t29 + 1; $i -lt $t30; $i++) {
    if ($tasks[$i] -match '^- \*\*Status\*\*:\s*pending') { $statusIdx = $i; break }
}
if ($statusIdx -lt 0) { throw "Task 29 Status:pending line not found between Task 29 and Task 30" }

$list = New-Object System.Collections.Generic.List[string]
$list.AddRange([string[]]$tasks)
$list[$statusIdx] = '- **Status**: done'

$insert = New-Object System.Collections.Generic.List[string]
foreach ($l in $ev) { $insert.Add($l) }
$insert.Add("")
$list.InsertRange($t30, $insert)

$utf8NoBom = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllLines($tasksPath, $list, $utf8NoBom)
Write-Output ("Flipped Task 29 Status->done at line $statusIdx; inserted " + $insert.Count + " evidence lines before Task 30 (was line $t30); total now " + $list.Count)
