$tasksPath = "D:\github\CogniThink-Chem\.trae\specs\chem-engine-question-solving\tasks.md"
$evPath    = "D:\github\CogniThink-Chem\_evalscratch\t23_evidence_block.txt"

$tasks = [IO.File]::ReadAllLines($tasksPath, [Text.Encoding]::UTF8)
$ev    = [IO.File]::ReadAllLines($evPath, [Text.Encoding]::UTF8)

# find the line index where Task 24 heading begins
$idx = -1
for ($i = 0; $i -lt $tasks.Length; $i++) {
    if ($tasks[$i] -match '^## Task 24') { $idx = $i; break }
}
if ($idx -lt 0) { throw "Task 24 heading not found" }

$list = New-Object System.Collections.Generic.List[string]
$list.AddRange([string[]]$tasks)

# insert evidence block + a trailing blank line before the Task 24 heading
$insert = New-Object System.Collections.Generic.List[string]
foreach ($l in $ev) { $insert.Add($l) }
$insert.Add("")   # blank line separating Evidence from Task 24 heading
$list.InsertRange($idx, $insert)

$utf8NoBom = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllLines($tasksPath, $list, $utf8NoBom)
Write-Output ("Inserted " + $insert.Count + " lines at index " + $idx + "; total now " + $list.Count)
