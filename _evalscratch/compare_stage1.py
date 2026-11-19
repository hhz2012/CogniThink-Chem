# -*- coding: utf-8 -*-
"""对比 after8b（旧口径） vs after_stage1（严格口径）：
   确认严格剔除只挤掉 pureAnswer/evidence-LLM 注水，未丢失任何真正引擎结论。
"""
import json, io, sys, re
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

def load(p):
    with open(p, encoding="utf-8-sig") as f: return json.load(f)

old = load(r"D:\github\CogniThink-Chem\_evalscratch\after8b.json")
new = load(r"D:\github\CogniThink-Chem\_evalscratch\after_stage1.json")

def cases(d):
    out = {}
    for key in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
        for c in d.get(key, []):
            out[c["id"]] = c
    return out

o = cases(old); n = cases(new)
ids = sorted(set(o) & set(n))

def norm(s): return "".join((s or "").split())
def is_concrete(concl):
    if not concl: return False
    if "已匹配" in concl or "相关反应" in concl: return False
    return any(w in concl for w in ["能","不能","共存","不共存","可以","不可以","正确","错误"])
def is_relevant(concl, q):
    if not concl: return False
    if "共存" in concl and "共存" not in (q or ""): return False
    return True

def is_genuine(c):
    """after8b 中是否为真正引擎结论（非 pureAnswer 回退、非 evidence-LLM）"""
    if not c.get("gatewayUsedEngine"): return False
    if norm(c.get("gatewayAnswer")) == norm(c.get("pureLlmAnswer")) and (c.get("pureLlmAnswer") or ""): return False
    tt = c.get("taskType") or "?"
    concl = c.get("gatewayConclusion") or ""
    q = c.get("question") or ""
    if tt == "calculation": return True
    if tt == "equilibrium": return True
    if tt == "ion_check": return True
    if tt in ("reaction_check","balance"):
        return is_relevant(concl, q) and is_concrete(concl)
    return False

# after8b 真正引擎结论且判正的题 → 必须在 stage1 仍判正（零回归）
genuine_correct_old = [i for i in ids if o[i].get("gatewayCorrect") and is_genuine(o[i])]
still_correct = [i for i in genuine_correct_old if n[i].get("gatewayCorrect")]
lost = [i for i in genuine_correct_old if not n[i].get("gatewayCorrect")]

print(f"after8b 真正引擎结论且判正: {len(genuine_correct_old)}")
print(f"  stage1 仍判正: {len(still_correct)}")
print(f"  stage1 丢失（引擎真值回归）: {len(lost)}  (要求 = 0)")
for i in lost[:20]:
    print(f"    丢失: {i} [{o[i].get('taskType')}] 旧答='{(o[i].get('gatewayAnswer') or '')[:30]}' 新答='{(n[i].get('gatewayAnswer') or '')[:30]}'")

# 总览
print(f"\n=== 总览对比 ===")
print(f"  after8b  gatewayCorrect: {old.get('gatewayCorrect')}  (旧口径，含注水)")
print(f"  stage1   gatewayCorrect: {new.get('gatewayCorrect')}  (严格口径)")
print(f"  after8b  pureLlmCorrect: {old.get('pureLlmCorrect')}")
print(f"  stage1   pureLlmCorrect: {new.get('pureLlmCorrect')}  (纯LLM基线，不变)")
print(f"  stage1   engineSolved:   {new.get('engineSolved')}  coverage={new.get('engineCoverage')}  precision={new.get('enginePrecision')}")
print(f"  stage1   harmCount:      {new.get('harmCount')}")

# 逐题翻转：stage1 判正但 after8b 判错（不应有，因为 stage1 是旧口径子集）
flip_gain = [i for i in ids if n[i].get("gatewayCorrect") and not o[i].get("gatewayCorrect")]
print(f"\n  stage1 新增判正（旧判错）: {len(flip_gain)}  (应=0，严格口径是子集)")
for i in flip_gain[:10]:
    print(f"    {i}")

# 按 taskType：stage1 真值判正分布
from collections import Counter
print(f"\n=== stage1 引擎真值判正 按 taskType ===")
c = Counter(n[i].get("taskType") for i in ids if n[i].get("gatewayCorrect"))
for k,v in c.most_common(): print(f"  {k}: {v}")
