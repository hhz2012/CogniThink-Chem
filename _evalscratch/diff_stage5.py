import json
from collections import Counter

S4 = json.load(open(r'D:\github\CogniThink-Chem\_evalscratch\after_stage4.json', encoding='utf-8-sig'))
S5 = json.load(open(r'D:\github\CogniThink-Chem\_evalscratch\after_stage5.json', encoding='utf-8-sig'))

def allcases(o):
    out = {}
    for lst in ['bothCorrectCases','gatewayOnlyCorrectCases','pureLlmOnlyCorrectCases','bothWrongCases']:
        for c in (o.get(lst) or []):
            out[c['id']] = c
    return out
C4 = allcases(S4); C5 = allcases(S5)
def b(c,k): return str(c.get(k)) == 'True'

def agg(C):
    n=len(C)
    adopted={i for i,c in C.items() if b(c,'gatewayTruthAdopted')}
    correct={i for i,c in C.items() if b(c,'gatewayCorrect')}
    pure={i for i,c in C.items() if b(c,'pureLlmCorrect')}
    harm={i for i in adopted if i not in correct and i in pure}
    prec = (len(correct & adopted)/len(adopted)) if adopted else 0.0
    return dict(n=n, adopted=len(adopted), correct=len(correct), pure=len(pure),
                harm=len(harm), prec=prec,
                adopted_set=adopted, correct_set=correct, pure_set=pure, harm_set=harm)

A4=agg(C4); A5=agg(C5)
print("=== 聚合指标对比 ===")
print(f"{'metric':16s} {'stage4':>10s} {'stage5':>10s} {'delta':>8s}")
for k in ['n','adopted','correct','pure','harm']:
    print(f"{k:16s} {A4[k]:>10d} {A5[k]:>10d} {A5[k]-A4[k]:>+8d}")
print(f"{'precision':16s} {A4['prec']:>10.4f} {A5['prec']:>10.4f} {A5['prec']-A4['prec']:>+8.4f}")

print(f"\n=== harm 新增 (stage5 - stage4) ===")
new_harm = A5['harm_set'] - A4['harm_set']
print(f"  count={len(new_harm)}  ids={sorted(new_harm)}")
print(f"=== harm 消失 (stage4 - stage5) ===")
print(f"  count={len(A4['harm_set']-A5['harm_set'])}  ids={sorted(A4['harm_set']-A5['harm_set'])}")

print(f"\n=== 新增引擎解出 (stage5 adopted - stage4 adopted) ===")
new_solved = A5['adopted_set'] - A4['adopted_set']
print(f"  count={len(new_solved)}")
for i in sorted(new_solved):
    c=C5[i]
    print(f"    {i:10s} taskType={str(c.get('taskType')):14s} correct={b(c,'gatewayCorrect')} pureLlmCorrect={b(c,'pureLlmCorrect')} "
          f"gwAns={(c.get('gatewayAnswer') or '')[:40]!r}")

print(f"\n=== 丢失引擎解出 (stage4 adopted - stage5 adopted) ===")
lost = A4['adopted_set'] - A5['adopted_set']
print(f"  count={len(lost)}  ids={sorted(lost)}")

print(f"\n=== correct→wrong 回归 (stage4 correct 但 stage5 不正确) ===")
regress = A4['correct_set'] - A5['correct_set']
print(f"  count={len(regress)}  ids={sorted(regress)}")

print(f"\n=== wrong→correct 改善 (stage5 correct - stage4 correct) ===")
improved = A5['correct_set'] - A4['correct_set']
print(f"  count={len(improved)}  ids={sorted(improved)}")

# taskType 分布对比
def by_type(C):
    cc=Counter(); uu=Counter()
    for c in C.values():
        t=c.get('taskType') or '?'
        if b(c,'gatewayTruthAdopted'): cc[t]+=1
        else: uu[t]+=1
    return cc,uu
c4,u4=by_type(C4); c5,u5=by_type(C5)
types=sorted(set(list(c4)+list(u4)+list(c5)+list(u5)))
print("\n=== taskType 解出分布 (solved/unsolved) ===")
print(f"{'taskType':18s} {'s4_sol':>7s} {'s4_uns':>7s} {'s5_sol':>7s} {'s5_uns':>7s}")
for t in types:
    print(f"{t:18s} {c4.get(t,0):>7d} {u4.get(t,0):>7d} {c5.get(t,0):>7d} {u5.get(t,0):>7d}")
