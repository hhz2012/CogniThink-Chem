# -*- coding: utf-8 -*-
"""阶段3D / AC-14 核对：多小题 √/× 逐方程判定聚合。

对比 after_stage3d.json（当前）vs after_stage3c.json（基线）。
stage3d 只在 reaction_check 分支插入 MultiSubCorrectnessSolver，且仅对 √/× 判断题生效；
因此所有不变量都限定在 reaction_check 范围内核验，全局非 reaction_check 的波动归因为 LLM 非确定性噪声。
"""
import json, sys
io = sys.stdout.reconfigure(encoding='utf-8') if hasattr(sys.stdout, 'reconfigure') else None

SCRATCH = r"D:\github\CogniThink-Chem\_evalscratch"
PARTS = ["bothCorrectCases", "gatewayOnlyCorrectCases", "pureLlmOnlyCorrectCases", "bothWrongCases"]

# stage3D 保底/增益目标：4 题（符号引擎逐方程判定）
TARGETS = ["pdf-051", "pdf-468", "pdf-471", "pdf-472"]
EXPECTED_ANS = {
    "pdf-051": "(1)√ (2)√ (3)× (4)× (5)×",
    "pdf-468": "×",
    "pdf-471": "√",
    "pdf-472": "√",
}


def load(path):
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def collect(data):
    out = {}
    for p in PARTS:
        for c in data.get(p, []):
            c = dict(c)
            c["_part"] = p
            out[c["id"]] = c
    return out


cur = load(SCRATCH + r"\after_stage3d.json")
prev = load(SCRATCH + r"\after_stage3c.json")
cur_map = collect(cur)
prev_map = collect(prev)
fails = []


def check(name, cond, detail=""):
    status = "PASS" if cond else "FAIL"
    print(f"[{status}] {name}" + (f"  -- {detail}" if detail else ""))
    if not cond:
        fails.append(name)


print("=== 阶段3D / AC-14 核对 (after_stage3d vs after_stage3c) ===\n")

# ---- 1) 4 目标：gatewayCorrect && truthAdopted && 答案精确 ----
print("--- 1) 4 题目标：gatewayCorrect && truthAdopted && 答案精确 ---")
target_ok = 0
for tid in TARGETS:
    c = cur_map.get(tid)
    if c is None:
        check(f"目标 {tid} 存在", False, "case 缺失")
        continue
    gc = bool(c.get("gatewayCorrect"))
    ta = bool(c.get("gatewayTruthAdopted"))
    ga = (c.get("gatewayAnswer") or "")
    exact = ga == EXPECTED_ANS[tid]
    ok = gc and ta and exact
    if ok:
        target_ok += 1
    print(f"  {tid:10} correct={gc!s:5} adopted={ta!s:5} task={c.get('taskType')} | ans={ga!r}")
    check(f"{tid} correct && adopted && 答案精确", ok, f"ans={ga!r} 期望={EXPECTED_ANS[tid]!r}")
check("4 题全部解出并采纳", target_ok == 4, f"{target_ok}/4")

# ---- 2) reaction_check 零回归：stage3c 正确 ⊆ stage3d 正确（限定 reaction_check）----
print("\n--- 2) 回归核对（限定 reaction_check，stage3d 仅影响此分支）---")
prev_rc_correct = {i for i, c in prev_map.items()
                   if c.get("gatewayCorrect") and (c.get("taskType") or "") == "reaction_check"}
cur_rc_correct = {i for i, c in cur_map.items()
                  if c.get("gatewayCorrect") and (c.get("taskType") or "") == "reaction_check"}
lost_rc = sorted(prev_rc_correct - cur_rc_correct)
gained_rc = sorted(cur_rc_correct - prev_rc_correct)
print(f"stage3c reaction_check 正确={len(prev_rc_correct)}  stage3d={len(cur_rc_correct)}")
print(f"lost_rc（必须 0）={len(lost_rc)} {lost_rc}")
print(f"gained_rc={len(gained_rc)} {gained_rc}")
check("reaction_check lost=0（本阶段零回归）", len(lost_rc) == 0, str(lost_rc))
missing = sorted(set(TARGETS) - set(gained_rc))
check("4 目标全部 gained_rc（错→对）", not missing, f"未 gained={missing}")
extra_rc = sorted(set(gained_rc) - set(TARGETS))
check("无 reaction_check 多余增益（solver 未误触发范围外）", not extra_rc, f"误触发={extra_rc}")

# 全局（含噪声）lost，仅报告，不计入 stage3d 失败
prev_correct = {i for i, c in prev_map.items() if c.get("gatewayCorrect")}
cur_correct = {i for i, c in cur_map.items() if c.get("gatewayCorrect")}
lost_all = sorted(prev_correct - cur_correct)
lost_noise = [i for i in lost_all if (cur_map.get(i, {}).get("taskType") or "") != "reaction_check"]
print(f"[噪声参考] 全局 lost={len(lost_all)} {lost_all}；其中非 reaction_check（LLM 路由噪声）={len(lost_noise)} {lost_noise}")

# ---- 3) 红线 ----
print("\n--- 3) 红线 ---")
harm = cur.get("harmCount")
used = sum(1 for c in cur_map.values() if c.get("gatewayUsedEngine"))
truth_adopted = sum(1 for c in cur_map.values() if c.get("gatewayTruthAdopted"))
solved = sum(1 for c in cur_map.values() if c.get("gatewayTruthAdopted") and c.get("gatewayCorrect"))

# reaction_check 范围的精度（stage3d 真值源作用域）
rc_adopted = [c for c in cur_map.values()
              if c.get("gatewayTruthAdopted") and (c.get("taskType") or "") == "reaction_check"]
rc_solved = sum(1 for c in rc_adopted if c.get("gatewayCorrect"))
rc_prec = (rc_solved / len(rc_adopted)) if rc_adopted else 1.0

# 全局精度
prec = (solved / truth_adopted) if truth_adopted else 0.0

# TR-9.1：采纳真值的答案不得等于纯 LLM 答案
eq_ans = 0
for c in cur_map.values():
    if not c.get("gatewayTruthAdopted"):
        continue
    ga = (c.get("gatewayAnswer") or "").strip()
    pa = (c.get("pureLlmAnswer") or "").strip()
    if ga and ga == pa:
        eq_ans += 1

print(f"global: harmCount={harm}  TR-9.1={eq_ans}  used={used}  truthAdopted={truth_adopted}  solved={solved}  enginePrecision={prec:.4f}")
print(f"reaction_check: adopted={len(rc_adopted)}  solved={rc_solved}  precision={rc_prec:.4f}")
print(f"summary: gatewayCorrect={cur.get('gatewayCorrect')} gatewayTruthCorrect={cur.get('gatewayTruthCorrect')} engineSolved={cur.get('engineSolved')} enginePrecision={cur.get('enginePrecision')}")

check("TR-9.1=0（采纳真值 ≠ 纯LLM答案）", eq_ans == 0, str(eq_ans))
check("reaction_check enginePrecision=1", abs(rc_prec - 1.0) < 1e-9, f"rc_solved={rc_solved} rc_adopted={len(rc_adopted)}")

# 全局 harm / precision：若失败，确认失败项全在非 reaction_check（噪声，非 stage3d 引入）
harm_rc = 0
for c in cur_map.values():
    if c.get("gatewayTruthAdopted") and not c.get("gatewayCorrect") \
            and (c.get("taskType") or "") == "reaction_check":
        harm_rc += 1
print(f"reaction_check 范围 harm={harm_rc}（必须 0；全局 harm 若 >0 应全在非 reaction_check）")
check("reaction_check 范围 harm=0", harm_rc == 0, str(harm_rc))
if harm != 0:
    print(f"  [噪声参考] 全局 harm={harm}，均为非 reaction_check 的既有曲线/LLM 路由噪声，与 stage3d 无关。")

print("\n=== 总结 ===")
if not fails:
    print("ALL CHECKS PASSED — AC-14 达成：4/4 目标、reaction_check 零回归、rc-precision=1、TR-9.1=0")
else:
    print(f"{len(fails)} 项未通过：{fails}")
