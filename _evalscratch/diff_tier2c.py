# -*- coding: utf-8 -*-
"""AC-18（Tier 2-C / NA 粒子计数校验器）隔离验收对比：
after_tier2a (基线) vs after_tier2c (本次)。

隔离方法：after_tier2c 仅在 NaChoiceValidator 采纳的题上覆盖/新增（实测仅 20 道 t2n），
非 NA 题与 pureLlm 列原样复用基线。因此本 diff 干净分离 Tier 2-C 的净增，
不依赖活评 LLM，零回归由构造保证。

核查红线（8 条）：
  1. 新增引擎解出 >= 18（期望 +20，全部 t2n）
  2. 精确率不下降（tier2c >= tier2a）
  3. 新增解出零假阳性（新增 adopted 全部 correct）
  4. harm 不新增
  5. correct->wrong 回归 = 0
  6. 丢失解出 = 0
  7. 20 道 t2n 全部 adopted 且 correct
  8. pdf NA 真题（pdf-026/093/419/421-427）诚实 null（未新增采纳）
"""
import json

BASE = json.load(open(r'D:\github\CogniThink-Chem\_evalscratch\after_tier2a.json', encoding='utf-8-sig'))
NEW = json.load(open(r'D:\github\CogniThink-Chem\_evalscratch\after_tier2c.json', encoding='utf-8-sig'))

LISTS = ['bothCorrectCases', 'gatewayOnlyCorrectCases', 'pureLlmOnlyCorrectCases', 'bothWrongCases']


def allcases(o):
    out = {}
    for lst in LISTS:
        for c in (o.get(lst) or []):
            out[c['id']] = c
    return out


Cb = allcases(BASE); Cn = allcases(NEW)


def b(c, k):
    return str(c.get(k)) == 'True'


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

print("=== 聚合指标对比 (tier2a 基线 -> tier2c 本次) ===")
print(f"{'metric':14s} {'tier2a':>10s} {'tier2c':>10s} {'delta':>8s}")
for k in ['n', 'adopted', 'correct', 'pure', 'harm']:
    print(f"{k:14s} {Ab[k]:>10d} {An[k]:>10d} {An[k]-Ab[k]:>+8d}")
print(f"{'precision':14s} {Ab['prec']:>10.4f} {An['prec']:>10.4f} {An['prec']-Ab['prec']:>+8.4f}")

print("\n=== t2n-* 新题解出情况 (期望 20 道全部 adopted 且 correct) ===")
t2n_ids = sorted(i for i in Cn if i.startswith('t2n-'))
print(f"  t2n total in new eval: {len(t2n_ids)}")
t2n_adopted = [i for i in t2n_ids if b(Cn[i], 'gatewayTruthAdopted')]
t2n_correct = [i for i in t2n_adopted if b(Cn[i], 'gatewayCorrect')]
print(f"  adopted: {len(t2n_adopted)}  adopted&correct: {len(t2n_correct)}")
not_adopted = [i for i in t2n_ids if not b(Cn[i], 'gatewayTruthAdopted')]
not_correct = [i for i in t2n_adopted if not b(Cn[i], 'gatewayCorrect')]
print(f"  NOT adopted: {not_adopted}")
print(f"  adopted but wrong: {not_correct}")
for i in not_correct:
    c = Cn[i]
    print(f"    {i}: expected={c.get('expected')!r} gatewayAnswer={c.get('gatewayAnswer')!r}")

print("\n=== pdf NA 真题诚实 null (期望未新增采纳：adopted=False) ===")
pdf_na_ids = ['pdf-026', 'pdf-093', 'pdf-419',
              'pdf-421', 'pdf-422', 'pdf-423', 'pdf-424', 'pdf-425', 'pdf-426', 'pdf-427']
pdf_na_adopted = []
for i in pdf_na_ids:
    if i in Cn:
        c = Cn[i]
        adp = b(c, 'gatewayTruthAdopted')
        print(f"  {i}: adopted={adp} correct={b(c,'gatewayCorrect')} gwAns={(c.get('gatewayAnswer') or '')[:30]!r}")
        if adp:
            pdf_na_adopted.append(i)
    else:
        print(f"  {i}: NOT IN EVAL")

print("\n=== harm 新增 (tier2c - tier2a) ===")
new_harm = An['harm_set'] - Ab['harm_set']
print(f"  count={len(new_harm)}  ids={sorted(new_harm)}")
print("=== harm 消失 (tier2a - tier2c) ===")
print(f"  count={len(Ab['harm_set']-An['harm_set'])}  ids={sorted(Ab['harm_set']-An['harm_set'])}")

print("\n=== 新增引擎解出 (tier2c adopted - tier2a adopted) ===")
new_solved = An['adopted_set'] - Ab['adopted_set']
print(f"  count={len(new_solved)}")
non_t2n_new = sorted(i for i in new_solved if not i.startswith('t2n-'))
print(f"  其中非 t2n (期望空，零假阳性/零越界): {non_t2n_new}")
for i in sorted(new_solved):
    c = Cn[i]
    print(f"    {i:10s} taskType={str(c.get('taskType')):14s} correct={b(c,'gatewayCorrect')} pureLlmCorrect={b(c,'pureLlmCorrect')} "
          f"gwAns={(c.get('gatewayAnswer') or '')[:36]!r}")

print("\n=== 丢失引擎解出 (tier2a adopted - tier2c adopted) ===")
lost = Ab['adopted_set'] - An['adopted_set']
print(f"  count={len(lost)}  ids={sorted(lost)}")

print("\n=== correct->wrong 回归 (tier2a correct 但 tier2c 不正确) ===")
regress = Ab['correct_set'] - An['correct_set']
print(f"  count={len(regress)}  ids={sorted(regress)}")

print("\n=== wrong->correct 改善 (tier2c correct - tier2a correct) ===")
improved = An['correct_set'] - Ab['correct_set']
print(f"  count={len(improved)}  ids={sorted(improved)}")

# 新增解出零假阳性：新增 adopted 的题必须全部 correct
new_solved_all_correct = all(b(Cn[i], 'gatewayCorrect') for i in new_solved)

print("\n=== AC-18 验收结论 ===")
ok = True
checks = [
    ("新增解出 >= 18（期望 +20）", len(new_solved) >= 18),
    ("新增解出恰为 20 道 t2n（隔离）", len(new_solved) == 20 and len(non_t2n_new) == 0),
    ("精确率不下降 (tier2c >= tier2a)", An['prec'] >= Ab['prec'] - 1e-9),
    ("新增解出零假阳性（全部 correct）", new_solved_all_correct),
    ("harm 不新增", len(new_harm) == 0),
    ("correct->wrong 回归 = 0", len(regress) == 0),
    ("丢失解出 = 0", len(lost) == 0),
    ("20 道 t2n 全部 adopted 且 correct", len(t2n_adopted) == 20 and len(t2n_correct) == 20),
    ("pdf NA 真题诚实 null（未新增采纳）", len(pdf_na_adopted) == 0),
]
for name, passed in checks:
    print(f"  [{'PASS' if passed else 'FAIL'}] {name}")
    ok = ok and passed
print(f"\n总体: {'ALL PASS' if ok else 'HAS FAILURE'}")
