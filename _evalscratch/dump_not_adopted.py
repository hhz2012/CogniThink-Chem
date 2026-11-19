# -*- coding: utf-8 -*-
"""深入：dump 18 道 used=true & adopted=false 的引擎原始输出，定位未采纳原因。"""
import json

SCRATCH = r"D:\github\CogniThink-Chem\_evalscratch"
PARTS = ["bothCorrectCases", "gatewayOnlyCorrectCases", "pureLlmOnlyCorrectCases", "bothWrongCases"]


def load(path):
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def collect(data):
    cases = []
    for p in PARTS:
        for c in data.get(p, []):
            c = dict(c)
            c["_part"] = p
            cases.append(c)
    return cases


data = load(SCRATCH + r"\after_stage3c.json")
cases = collect(data)

# 打印一个 case 的所有字段名
print("=== 字段名（取自首个 case）===")
print(sorted(cases[0].keys()))

targets = [c for c in cases if c.get("gatewayUsedEngine") and not c.get("gatewayTruthAdopted")]
print(f"\n=== 18 道 used=true & adopted=false 的引擎原始输出 ===")
for c in targets:
    print(f"\n--- {c['id']} (part={c['_part']}, tt={c.get('taskType')}) ---")
    print(f"  question: {(c.get('question') or c.get('Question') or '')[:110]!r}")
    # 找引擎相关字段
    for k in sorted(c.keys()):
        if k in ("id", "question", "Question", "gatewayAnswer", "pureLlmAnswer", "gatewayCorrect",
                 "pureLlmCorrect", "gatewayTruthAdopted", "gatewayUsedEngine", "taskType", "_part"):
            continue
        v = c.get(k)
        if v is None:
            continue
        s = str(v)
        if len(s) > 200:
            s = s[:200] + "...<trunc>"
        print(f"  {k} = {s!r}")
