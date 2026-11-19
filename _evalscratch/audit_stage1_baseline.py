# -*- coding: utf-8 -*-
"""分解 after8b 中'引擎介入且回答≠纯LLM'的 146 题，区分：
   - 真正引擎结论（calc 数值 / eq 结论 / ion 结论 / reaction 具体且相关）
   - evidence-LLM（reaction 非具体结论，LLM 基于证据推理）—— 按原则阶段1也应剔除
"""
import json, io, sys, re
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

with open(r"D:\github\CogniThink-Chem\_evalscratch\after8b.json", encoding="utf-8-sig") as f:
    data = json.load(f)

cases = []
for key in ["bothCorrectCases", "gatewayOnlyCorrectCases", "pureLlmOnlyCorrectCases", "bothWrongCases"]:
    for c in data.get(key, []):
        cases.append(c)

def norm(s):
    return "".join((s or "").split())

def is_concrete(concl):
    if not concl: return False
    if "已匹配" in concl or "相关反应" in concl: return False
    return any(w in concl for w in ["能","不能","共存","不共存","可以","不可以","正确","错误"])

def is_relevant(concl, q):
    if not concl: return False
    if "共存" in concl and "共存" not in (q or ""):
        return False
    return True

def is_choice(q):
    return bool(re.search(r"(^|\n)\s*[A-D][\.．、]\s", q or ""))

def has_letter(text):
    return bool(re.search(r"(?<![A-Za-z])[A-Da-d](?![A-Za-z])", text or ""))

# 重建 BuildGatewayAnswerAsync 的决策（after8b 旧逻辑），对每个 gatewayCorrect 判定其答案来源
genuine = 0
evidence_llm = 0
other = 0
genuine_by_type = {}
evidence_by_question = []

for c in cases:
    if not c.get("gatewayCorrect"): continue
    used = c.get("gatewayUsedEngine")
    gwa = c.get("gatewayAnswer") or ""
    pla = c.get("pureLlmAnswer") or ""
    if not used: continue                 # 引擎未介入 → 不算 146
    if norm(gwa) == norm(pla) and pla: continue  # 回退 pureAnswer → 不算 146
    # 至此属于 146（引擎介入且回答≠纯LLM）
    tt = c.get("taskType") or "?"
    concl = c.get("gatewayConclusion") or ""
    q = c.get("question") or ""
    choice = is_choice(q)
    # 模拟旧 BuildGatewayAnswerAsync 走了哪条 truth 路径
    if tt == "calculation":
        genuine += 1; genuine_by_type[tt] = genuine_by_type.get(tt,0)+1
    elif tt == "equilibrium":
        if choice and not has_letter(concl):
            other += 1  # 选择题无字母应回退 pureAnswer，但这里回答≠pureAnswer，说明结论含字母或非选择
        genuine += 1; genuine_by_type[tt] = genuine_by_type.get(tt,0)+1
    elif tt == "ion_check":
        genuine += 1; genuine_by_type[tt] = genuine_by_type.get(tt,0)+1
    elif tt in ("reaction_check","balance"):
        if not is_relevant(concl, q):
            other += 1
        elif is_concrete(concl):
            genuine += 1; genuine_by_type[tt] = genuine_by_type.get(tt,0)+1
        else:
            # 非具体结论 → 走了 evidence-LLM 路径（旧逻辑）
            evidence_llm += 1
            evidence_by_question.append((c.get("id"), tt, concl[:40], (gwa or "")[:50]))
    else:
        other += 1

print(f"146 子集（引擎介入且回答≠纯LLM）的来源分解：")
print(f"  真正引擎结论(genuine): {genuine}")
print(f"  evidence-LLM(应剔除): {evidence_llm}")
print(f"  other/边界: {other}")
print(f"  genuine 按 taskType: {genuine_by_type}")
print(f"\n  严格剔除后预期 gatewayTruthCorrect ≈ {genuine}")
print(f"\nevidence-LLM 样例（前 15）：")
for x in evidence_by_question[:15]:
    print(f"  {x[0]} [{x[1]}] concl='{x[2]}' ans='{x[3]}'")
