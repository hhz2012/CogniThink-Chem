# -*- coding: utf-8 -*-
import json, io, sys, re, collections
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
PATH = r"D:\github\CogniThink-Chem\_evalscratch\after5.json"
data = json.load(open(PATH, encoding="utf-8-sig"))

def cases(d):
    out=[]
    for p in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
        out.extend(d.get(p,[]) or [])
    return out
cs = cases(data)
print("fields sample:", list(cs[0].keys()))

LETTER = re.compile(r'(?<![a-zA-Z])[A-D](?![a-zA-Z])')
def letters(s):
    return set(LETTER.findall(s or ""))

def is_choice(c):
    q = c.get("question") or ""
    # rendered options: "A. xxx"  "B. xxx"
    return bool(re.search(r'(?:^|\n)\s*A\.\s', q)) and bool(re.search(r'(?:^|\n)\s*B\.\s', q))

# Simulate rule: choice Q AND gateway letter-count < pure letter-count -> use pure (correctness = pureLlmCorrect)
flips = []
manual_protected = 0
for c in cs:
    if not is_choice(c): continue
    gl = letters(c.get("gatewayAnswer")); pl = letters(c.get("pureLlmAnswer"))
    if len(gl) < len(pl):
        old = c.get("gatewayCorrect"); new = c.get("pureLlmCorrect")
        if old != new:
            flips.append((c.get("id"), c.get("taskType"), old, new, c.get("id","").startswith("pdf-"), len(gl), len(pl), c.get("gatewayConclusion","")[:30]))
        if not c.get("id","").startswith("pdf-"):
            manual_protected += 1

tr = [f for f in flips if not f[2] and f[3]]
tw = [f for f in flips if f[2] and not f[3]]
print(f"\n=== 模拟规则: 选择题且 gateway字母数 < pure字母数 -> 回退pure ===")
print(f"触发总数(含对错不变): 见下; 对错翻转: 错->对 {len(tr)}, 对->错 {len(tw)}")
print(f"手工题触发数: {manual_protected} (需检查是否伤到手工)")
print("\n-- 错->对 (帮倒忙修复) --")
for f in tr: print(f"  [{f[0]}] {f[1]} pdf={f[4]} gw={f[5]}纯={f[6]} 结论={f[7]}")
print("\n-- 对->错 (潜在回归) --")
for f in tw: print(f"  [{f[0]}] {f[1]} pdf={f[4]} gw={f[5]}纯={f[6]} 结论={f[7]}")

# Also report: choice questions total, and how many have gateway<pure letters
ch = [c for c in cs if is_choice(c)]
narrow = [c for c in ch if len(letters(c.get('gatewayAnswer'))) < len(letters(c.get('pureLlmAnswer')))]
print(f"\n选择题总数={len(ch)}, 其中gateway字母数<pure的={len(narrow)}")
