# -*- coding: utf-8 -*-
"""Isolation probe: revert ONLY Task-42 data changes (my 35 IDs) in the TEST
OUTPUT directory, leaving the concurrent agent's data edits intact. Then the
operator re-runs the 6 failing tests with --no-build. If they still fail, my
data patch is provably innocent.

Does NOT touch the shared source pdf_questions.json. Restores the output file
afterwards from the current source.
"""
import json, subprocess, shutil, os

SRC = r"src/CogniThink.TutorBrain.Api/data/pdf_questions.json"
OUT = r"tests/CogniThink.TutorBrain.Tests/bin/Debug/net8.0/data/pdf_questions.json"
BAK = OUT + ".v1bak"

# Exactly the IDs touched by _apply_ocr_fix.py (Task 42)
MINE = ["pdf-051","pdf-117","pdf-134","pdf-466","pdf-467","pdf-468","pdf-469",
        "pdf-470","pdf-471","pdf-472","pdf-473","pdf-478","pdf-480",
        "pdf-119","pdf-120","pdf-121","pdf-122",
        "pdf-275","pdf-463","pdf-544",
        "pdf-009","pdf-123","pdf-135","pdf-136","pdf-137","pdf-138","pdf-139",
        "pdf-140","pdf-141","pdf-142","pdf-143","pdf-144","pdf-145","pdf-187","pdf-189"]

head = subprocess.run(['git','show','HEAD:'+SRC.replace('\\','/')],
                      capture_output=True, text=True, encoding='utf-8').stdout
a = {q['id']: q for q in json.loads(head)}
cur = json.load(open(SRC, encoding='utf-8'))
b = {q['id']: q for q in cur}

reverted = []
for qid in MINE:
    if qid in a and qid in b and a[qid] != b[qid]:
        # replace current question object with HEAD version (revert mine only)
        idx = next(i for i,q in enumerate(cur) if q['id']==qid)
        cur[idx] = a[qid]
        reverted.append(qid)

# backup current output, write reverted version to output
shutil.copy2(OUT, BAK)
with open(OUT, 'w', encoding='utf-8') as f:
    json.dump(cur, f, ensure_ascii=False, indent=2)
    f.write('\n')

print(f"Reverted {len(reverted)} of my IDs in output dir: {reverted}")
print(f"Backup at {BAK}")
print("Now run: dotnet test ... --no-build --filter <6 failing tests>")
print("Then restore with: copy /Y src... no — run _v1_restore.py")
