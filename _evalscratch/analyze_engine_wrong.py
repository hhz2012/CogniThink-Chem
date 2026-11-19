# -*- coding: utf-8 -*-
"""分析 after_stage3c.json 中引擎答错/未解的具体原因。"""
import json
from collections import defaultdict, Counter

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
cmap = {c["id"]: c for c in cases}

print("=== 总览 ===")
print(f"total={len(cases)}  pureLlmCorrect={data.get('pureLlmCorrect')}  gatewayTruthCorrect={data.get('gatewayTruthCorrect')}  engineSolved={data.get('engineSolved')}  harmCount={data.get('harmCount')}")
for p in PARTS:
    print(f"  {p}={len(data.get(p, []))}")

# 1) 引擎介入但最终答错（潜在 harm / 回归）：used=true & correct=false
print("\n=== 1) 引擎介入(used=true)但 gatewayCorrect=false ===")
harm_like = [c for c in cases if c.get("gatewayUsedEngine") and not c.get("gatewayCorrect")]
print(f"数量 = {len(harm_like)}")
for c in harm_like[:30]:
    print(f"  {c['id']:14} part={c['_part']:24} tt={c.get('taskType'):16} adopted={c.get('gatewayTruthAdopted')}  gwAns={(c.get('gatewayAnswer') or '')[:50]!r}")

# 2) 引擎介入但未采纳真值（used=true & adopted=false）：引擎介入了但结论没被采用 → 回退纯LLM
print("\n=== 2) 引擎介入(used=true)但 truthAdopted=false（结论未被采用，回退纯LLM）===")
used_not_adopted = [c for c in cases if c.get("gatewayUsedEngine") and not c.get("gatewayTruthAdopted")]
print(f"数量 = {len(used_not_adopted)}")
tt_cnt = Counter(c.get("taskType") for c in used_not_adopted)
print(f"  按 taskType: {dict(tt_cnt)}")

# 3) 引擎未解（used=false）的题：按 taskType + 分区分布 → 覆盖盲区
print("\n=== 3) 引擎未介入(used=false)按 taskType 分布（覆盖盲区）===")
not_used = [c for c in cases if not c.get("gatewayUsedEngine")]
print(f"总数 = {len(not_used)}")
tt_notused = Counter(c.get("taskType") for c in not_used)
for tt, n in sorted(tt_notused.items(), key=lambda x: -x[1]):
    wrong = sum(1 for c in not_used if c.get("taskType") == tt and not c.get("gatewayCorrect"))
    pure_right = sum(1 for c in not_used if c.get("taskType") == tt and c.get("pureLlmCorrect"))
    print(f"  {tt:18} 未介入={n:4}  其中网关错={wrong:4}  纯LLM对={pure_right:4}")

# 4) bothWrong（引擎未解且LLM也错）按 taskType —— 最值得引擎接管的盲区
print("\n=== 4) bothWrong（双错）按 taskType —— 引擎潜在接管区 ===")
both_wrong = [c for c in cases if c["_part"] == "bothWrongCases"]
tt_bw = Counter(c.get("taskType") for c in both_wrong)
for tt, n in sorted(tt_bw.items(), key=lambda x: -x[1]):
    print(f"  {tt:18} {n}")

# 5) 抽样：each taskType 看几道 bothWrong 的题干+答案，定位为什么引擎没解
print("\n=== 5) bothWrong 抽样（每个 taskType 前 3 道，看题干与期望答案）===")
by_tt = defaultdict(list)
for c in both_wrong:
    by_tt[c.get("taskType")].append(c)
for tt in sorted(by_tt):
    print(f"\n--- taskType={tt} (共{len(by_tt[tt])}道) ---")
    for c in by_tt[tt][:3]:
        q = (c.get("question") or c.get("Question") or "")[:90].replace("\n", " ")
        ga = (c.get("gatewayAnswer") or "")[:50]
        pa = (c.get("pureLlmAnswer") or "")[:50]
        print(f"  [{c['id']}] Q={q!r}")
        print(f"       gwAns={ga!r}")
        print(f"       pureAns={pa!r}")
