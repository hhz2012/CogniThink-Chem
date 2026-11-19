# -*- coding: utf-8 -*-
"""验证 after7 locant 修复：确认仅 pdf-322 的关键词集变化，且不影响当前判分。"""
import json, io, sys, re
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

PDF = r"D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json"
A6 = r"D:\github\CogniThink-Chem\_evalscratch\after6.json"

pdf = json.load(open(PDF, encoding="utf-8-sig"))
a6 = json.load(open(A6, encoding="utf-8-sig"))
def cases(d):
    out=[]
    for p in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
        out.extend(d.get(p,[]) or [])
    return out
a6map = {r['id']:r for r in cases(a6)}

# 复刻 C# 的拆分（旧 vs 新）
STRIP = re.compile(r'[（(]\s*\d+\s*[）)]\s*')
LOCANT = re.compile(r'(?<![.\d])(\d)\s*[,，]\s*(\d)')
PH = '\x01'
def split_old(answer):
    s = STRIP.sub(' ', (answer or '').strip())
    parts = re.split(r'[,，;；、]', s)
    return [p.strip().strip('.。').strip() for p in parts if p.strip().strip('.。').strip()]
def split_new(answer):
    s = STRIP.sub(' ', (answer or '').strip())
    s2 = LOCANT.sub(lambda m: m.group(1) + PH + m.group(2), s)
    parts = re.split(r'[,，;；、]', s2)
    return [p.strip().strip('.。').strip().replace(PH, ',') for p in parts if p.strip().strip('.。').strip()]

changed = []
locant_answers = []
for item in pdf:
    ans = item.get('answer') or ''
    typ = item.get('type') or ''
    if typ == 'choice':
        continue
    existing = item.get('answerKeywords') or []
    if len(existing) > 1:
        continue  # 沿用已有，不走拆分
    old = split_old(ans)
    new = split_new(ans)
    if old != new:
        changed.append((item.get('id'), old, new))
    if LOCANT.search(STRIP.sub(' ', ans)):
        locant_answers.append((item.get('id'), ans[:80]))

print(f"含 \\d,\\d locant 的非选择题答案数: {len(locant_answers)}")
for qid, a in locant_answers:
    print(f"  [{qid}] {a}")
print(f"\n拆分结果发生变化的题数: {len(changed)}")
for qid, old, new in changed:
    print(f"  [{qid}]")
    print(f"    OLD: {old}")
    print(f"    NEW: {new}")
    r = a6map.get(qid, {})
    print(f"    after6 pureCorrect={r.get('pureLlmCorrect')} gwCorrect={r.get('gatewayCorrect')}")
    pu = (r.get('pureLlmAnswer') or '')
    gw = (r.get('gatewayAnswer') or '')
    # 旧关键词是否命中
    old_hit_pure = any(k in pu for k in old if k)
    old_hit_gw = any(k in gw for k in old if k)
    new_hit_pure = any(k in pu for k in new if k)
    new_hit_gw = any(k in gw for k in new if k)
    print(f"    pure 旧命中={old_hit_pure} 新命中={new_hit_pure} | gw 旧命中={old_hit_gw} 新命中={new_hit_gw}")
    print(f"    pureAnswer 含'2'={'2' in pu} 含'4'={'4' in pu}; gwAnswer 含'2'={'2' in gw} 含'4'={'4' in gw}")

print("\n结论: 若 changed 仅 pdf-322 且其 pure/gw 新旧命中都不变 -> 指标中性，无需重跑评估")
