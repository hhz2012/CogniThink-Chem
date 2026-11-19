# -*- coding: utf-8 -*-
"""Tier 2-B 验收对比：after_tier2a (基线) vs after_tier2b (本次)。
核查红线：新增解出>=20、精确率不下降、新增解出零假阳性、harm 不新增、
correct->wrong 回归=0、丢失解出=0、24 道 t2t 全对、pdf-013/015/016 解出。
"""
import json

BASE = json.load(open(r'D:\github\CogniThink-Chem\_evalscratch\after_tier2a.json', encoding='utf-8-sig'))
NEW = json.load(open(r'D:\github\CogniThink-Chem\_evalscratch\after_tier2b.json', encoding='utf-8-sig'))

LISTS = ['bothCorrectCases', 'gatewayOnlyCorrectCases', 'pureLlmOnlyCorrectCases', 'bothWrongCases']

def allcases(o):
    out = {}
    for lst in LISTS:
        for c in (o.get(lst) or []):
            out[c['id']] = c
    return out

Cb = allcases(BASE); Cn = allcases(NEW)
def b(c, k): return str(c.get(k)) == 'True'

def agg(C):
    n = len(C)
    adopted = {i for i, c in C.items() if b(c, 'gatewayTruthAdopted')}
    correct = {i for i, c in C.items() if b(c, 'gatewayCorrect')}
    pure = {i for i, c in C.items() if b(c, 'pureLlmCorrect')}
    harm = {i for i in adopted if i not in correct and i in pure}
    prec = (len(correct & adopted) / len(adopted)) if adopted else 0.0
    return dict(n=n, adopted=len(adopted), correct=len(correct), pure=len(pure),
                harm=len(harm), prec=prec,
                adopted_set=adopted, correct_set=correct, pure_set=pure, harm_set=harm)

Ab = agg(Cb); An = agg(Cn)

print("=== 聚合指标对比 (tier2a -> tier2b) ===")
print(f"{'metric':14s} {'tier2a':>10s} {'tier2b':>10s} {'delta':>8s}")
for k in ['n', 'adopted', 'correct', 'pure', 'harm']:
    print(f"{k:14s} {Ab[k]:>10d} {An[k]:>10d} {An[k]-Ab[k]:>+8d}")
print(f"{'precision':14s} {Ab['prec']:>10.4f} {An['prec']:>10.4f} {An['prec']-Ab['prec']:>+8.4f}")

print("\n=== t2t-* 新题解出情况 (期望 24 道全部 adopted 且 correct) ===")
t2t_ids = sorted(i for i in Cn if i.startswith('t2t-'))
print(f"  t2t total in new eval: {len(t2t_ids)}")
t2t_adopted = [i for i in t2t_ids if b(Cn[i], 'gatewayTruthAdopted')]
t2t_correct = [i for i in t2t_adopted if b(Cn[i], 'gatewayCorrect')]
print(f"  adopted: {len(t2t_adopted)}  adopted&correct: {len(t2t_correct)}")
not_adopted = [i for i in t2t_ids if not b(Cn[i], 'gatewayTruthAdopted')]
not_correct = [i for i in t2t_adopted if not b(Cn[i], 'gatewayCorrect')]
print(f"  NOT adopted: {not_adopted}")
print(f"  adopted but wrong: {not_correct}")
for i in not_correct:
    c = Cn[i]
    print(f"    {i}: gatewayAnswer={c.get('gatewayAnswer')!r}")

print("\n=== 真题 pdf-013/015/016 (期望 gatewayCorrect=True) ===")
for i in ['pdf-013','pdf-015','pdf-016','pdf-017']:
    if i in Cn:
        c = Cn[i]
        print(f"  {i}: adopted={b(c,'gatewayTruthAdopted')} correct={b(c,'gatewayCorrect')} gwAns={c.get('gatewayAnswer')!r}")
    else:
        print(f"  {i}: NOT IN EVAL")

print("\n=== harm 新增 (tier2b - tier2a) ===")
new_harm = An['harm_set'] - Ab['harm_set']
print(f"  count={len(new_harm)}  ids={sorted(new_harm)}")
print("=== harm 消失 (tier2a - tier2b) ===")
print(f"  count={len(Ab['harm_set']-An['harm_set'])}  ids={sorted(Ab['harm_set']-An['harm_set'])}")

print("\n=== 新增引擎解出 (tier2b adopted - tier2a adopted) ===")
new_solved = An['adopted_set'] - Ab['adopted_set']
print(f"  count={len(new_solved)}")
for i in sorted(new_solved):
    c = Cn[i]
    print(f"    {i:10s} taskType={str(c.get('taskType')):14s} correct={b(c,'gatewayCorrect')} pureLlmCorrect={b(c,'pureLlmCorrect')} "
          f"gwAns={(c.get('gatewayAnswer') or '')[:36]!r}")

print("\n=== 丢失引擎解出 (tier2a adopted - tier2b adopted) ===")
lost = Ab['adopted_set'] - An['adopted_set']
print(f"  count={len(lost)}  ids={sorted(lost)}")

print("\n=== correct->wrong 回归 (tier2a correct 但 tier2b 不正确) ===")
regress = Ab['correct_set'] - An['correct_set']
print(f"  count={len(regress)}  ids={sorted(regress)}")

print("\n=== wrong->correct 改善 (tier2b correct - tier2a correct) ===")
improved = An['correct_set'] - Ab['correct_set']
print(f"  count={len(improved)}  ids={sorted(improved)}")

# 新增解出零假阳性：新增 adopted 的题必须全部 correct
new_solved_all_correct = all(b(Cn[i], 'gatewayCorrect') for i in new_solved)

print("\n=== 验收结论 ===")
ok = True
checks = [
    ("新增解出>=20", len(new_solved) >= 20),
    ("精确率不下降 (tier2b>=tier2a)", An['prec'] >= Ab['prec'] - 1e-9),
    ("新增解出零假阳性 (全部correct)", new_solved_all_correct),
    ("harm 不新增", len(new_harm) == 0),
    ("correct->wrong 回归=0", len(regress) == 0),
    ("丢失解出=0", len(lost) == 0),
    ("24 道 t2t 全部 adopted", len(t2t_adopted) == 24),
    ("24 道 t2t 全部 correct", len(t2t_correct) == 24),
]
for name, passed in checks:
    print(f"  [{'PASS' if passed else 'FAIL'}] {name}")
    ok = ok and passed
print(f"\n总体: {'ALL PASS' if ok else 'HAS FAILURE'}")
