# -*- coding: utf-8 -*-
import json

SCRATCH = r"D:\github\CogniThink-Chem\_evalscratch"
PARTS = ["bothCorrectCases", "gatewayOnlyCorrectCases", "pureLlmOnlyCorrectCases", "bothWrongCases"]

# 阶段3C 目标 17 题（当前 pureLlmCorrect=true 但网关拒答，应被引擎真值解出并采纳）
TARGETS = [
    "redox-1", "redox-2", "redox-3",            # R1 氧化还原角色
    "d2-1", "d2-2", "d2-3", "d2-4",             # R2 还原产物
    "d8-1", "d8-2",                             # R2 电极产物
    "d1-1", "d1-2", "d1-3", "d1-4", "d1-5",     # P1 少量/过量产物书写
    "pdf-515",
    "d8-5", "trap-limit-1",                     # P3 小可行性
]


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
    return {c["id"]: c for c in cases}


cur = load(SCRATCH + r"\after_stage3c.json")
prev = load(SCRATCH + r"\after_stage3b.json")
cur_map = collect(cur)
prev_map = collect(prev)

fails = []


def check(name, cond, detail=""):
    status = "PASS" if cond else "FAIL"
    print(f"[{status}] {name}" + (f"  -- {detail}" if detail else ""))
    if not cond:
        fails.append(name)


print("=== 阶段3C / AC-13 核对 (after_stage3c vs after_stage3b) ===\n")

# 1) 17 题全部 gatewayCorrect=true 且 truthAdopted=true
print("--- 1) 17 题目标：gatewayCorrect && truthAdopted ---")
target_ok = 0
for tid in TARGETS:
    c = cur_map.get(tid)
    if c is None:
        check(f"目标 {tid} 存在", False, "case 缺失")
        continue
    gc = bool(c.get("gatewayCorrect"))
    ta = bool(c.get("gatewayTruthAdopted"))
    ok = gc and ta
    if ok:
        target_ok += 1
    ga = (c.get("gatewayAnswer") or "").replace("\n", " ")[:70]
    print(f"  {tid:14} gwCorrect={gc!s:5} truthAdopted={ta!s:5} used={c.get('gatewayUsedEngine')!s:5} | {ga}")
    check(f"{tid} gatewayCorrect && truthAdopted", ok)
check("17 题全部解出并采纳", target_ok == 17, f"{target_ok}/17")

# 2) 零回归：stage3b 正确 ⊆ stage3c 正确
prev_correct = {i for i, c in prev_map.items() if c.get("gatewayCorrect")}
cur_correct = {i for i, c in cur_map.items() if c.get("gatewayCorrect")}
lost = sorted(prev_correct - cur_correct)
gained = sorted(cur_correct - prev_correct)
print(f"\n--- 2) 回归核对 ---")
print(f"stage3b 正确={len(prev_correct)}  stage3c 正确={len(cur_correct)}")
print(f"lost（必须 0）={len(lost)} {lost}")
print(f"gained={len(gained)} {gained}")
check("lost=0（零回归）", len(lost) == 0, str(lost))
# 17 个目标必须全部 gained（错→对），且不能有任何目标 lost
missing_target = sorted(set(TARGETS) - set(gained))
check("17 目标全部 gained（错→对）", not missing_target, f"未 gained 的目标={missing_target}")
# 多余 gained：允许外部并行工作（如 ChoiceVerifier 图题 pdf-015）带来的正确增益，
# 但不得有 reaction_check 的多余项（那意味着本阶段 solver 误触发了范围外题）。
extra = sorted(set(gained) - set(TARGETS))
extra_rc = [i for i in extra if (cur_map.get(i, {}).get("taskType") or "") == "reaction_check"]
print(f"extra gained（非本阶段目标，应为外部工作且正确）={extra}")
check("无 reaction_check 多余增益（solver 未误触发范围外）", not extra_rc, f"误触发={extra_rc}")

# 3) 红线：harmCount=0, TR-9.1=0, enginePrecision=1
harm = cur.get("harmCount")
used = sum(1 for c in cur_map.values() if c.get("gatewayUsedEngine"))
truth_adopted = sum(1 for c in cur_map.values() if c.get("gatewayTruthAdopted"))
solved = sum(1 for c in cur_map.values() if c.get("gatewayTruthAdopted") and c.get("gatewayCorrect"))
eq_ans = 0
for c in cur_map.values():
    ga = (c.get("gatewayAnswer") or "").strip()
    pa = (c.get("pureLlmAnswer") or "").strip()
    if ga and ga == pa:
        eq_ans += 1
# 官方 enginePrecision 定义 = 采纳真值且正确 / 采纳真值（gatewayTruthCorrect / engineSolved），
# 即"被采用的引擎真值全部正确"，不是 solved/used（used 含引擎介入但未采用、回退纯LLM的题）。
prec = (solved / truth_adopted) if truth_adopted else 0.0
print(f"\n--- 3) 红线 ---")
print(f"harmCount={harm}  TR-9.1(gwAns==pureAns)={eq_ans}  used={used}  truthAdopted={truth_adopted}  solved={solved}  enginePrecision={prec:.4f}")
print(f"summary.gatewayCorrect={cur.get('gatewayCorrect')}  summary.gatewayTruthCorrect={cur.get('gatewayTruthCorrect')}  summary.engineSolved={cur.get('engineSolved')}  summary.enginePrecision={cur.get('enginePrecision')}")
check("harmCount=0", harm == 0, str(harm))
check("TR-9.1=0", eq_ans == 0, str(eq_ans))
check("enginePrecision=1（采纳真值全对）", abs(prec - 1.0) < 1e-9, f"solved={solved} truthAdopted={truth_adopted}")
check("summary.enginePrecision==1", cur.get("enginePrecision") == 1, str(cur.get("enginePrecision")))

# 4) 17 题 gatewayAnswer 内容含期望关键词（防御性：确认 adopted 的是真值结论而非占位）
print("\n--- 4) 17 题答案内容核对（含期望关键词）---")
expect = {
    "redox-1": ["Cu"], "redox-2": ["FeCl3"], "redox-3": ["KI"],
    "d2-1": ["Mn2+"], "d2-2": ["MnO2"], "d2-3": ["Cl-"], "d2-4": ["NO"],
    "d8-1": ["O2"], "d8-2": ["Cl2"],
    "d1-1": ["2Fe2+", "2Fe3+"], "d1-2": ["4Br-", "3Cl2", "2Br2"],
    "d1-3": ["2AlO2-", "CO3"], "d1-4": ["HCO3-"], "d1-5": ["CaCO3"],
    "pdf-515": ["CN- + CO2 + H2O → HCN + HCO3-"],
    "d8-5": ["不能"], "trap-limit-1": ["能"],
}
for tid in TARGETS:
    c = cur_map.get(tid)
    if c is None:
        continue
    ga = (c.get("gatewayAnswer") or "")
    miss = [k for k in expect[tid] if k not in ga]
    status = "OK " if not miss else "MISS"
    print(f"  [{status}] {tid:14} missing={miss}  ans={ga[:60]!r}")
    check(f"{tid} 答案含期望关键词 {expect[tid]}", not miss, f"missing={miss}")

print("\n=== 总结 ===")
if not fails:
    print("ALL CHECKS PASSED — AC-13 达成：17/17、零回归、harm=0、TR-9.1=0、precision=1")
else:
    print(f"{len(fails)} 项未通过：{fails}")
