# -*- coding: utf-8 -*-
import json, sys
io = sys.stdout.reconfigure(encoding='utf-8') if hasattr(sys.stdout,'reconfigure') else None

def load(p):
    return json.load(open(p, encoding='utf-8'))

def rows(d):
    out=[]
    for b in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
        for c in d.get(b, []):
            c['_bucket']=b; out.append(c)
    return out

d3 = load(r'd:\github\CogniThink-Chem\_evalscratch\after_stage3d.json')
d3e= load(r'd:\github\CogniThink-Chem\_evalscratch\after_stage3e.json')

def rc_stats(d):
    rs=[r for r in rows(d) if (r.get('taskType') or '')=='reaction_check']
    n=len(rs)
    gcorr=sum(1 for r in rs if r.get('gatewayCorrect'))
    plcorr=sum(1 for r in rs if r.get('pureLlmCorrect'))
    used=sum(1 for r in rs if r.get('gatewayUsedEngine'))
    adopted=sum(1 for r in rs if r.get('gatewayTruthAdopted'))
    adopted_correct=sum(1 for r in rs if r.get('gatewayTruthAdopted') and r.get('gatewayCorrect'))
    harm=sum(1 for r in rs if r.get('gatewayTruthAdopted') and not r.get('gatewayCorrect') and r.get('pureLlmCorrect'))
    acls=[r.get('id') for r in rs if r.get('gatewayUsedEngine') and not r.get('gatewayTruthAdopted')]
    # TR-9.1: gateway used engine but gatewayAnswer == pureLlmAnswer (纯LLM回答被原样采纳)
    tr91=sum(1 for r in rs if r.get('gatewayUsedEngine') and (r.get('gatewayAnswer') or '')==(r.get('pureLlmAnswer') or '') and (r.get('gatewayAnswer') or '')!='')
    return dict(n=n,gcorr=gcorr,plcorr=plcorr,used=used,adopted=adopted,adopted_correct=adopted_correct,
                harm=harm,precision=(adopted_correct/adopted if adopted else 0),acls=acls,tr91=tr91)

print("=== reaction_check 作用域：stage3D → stage3E ===")
a=rc_stats(d3); b=rc_stats(d3e)
for k in ['n','gcorr','plcorr','used','adopted','adopted_correct','harm','tr91']:
    print(f"  {k:16}: {a[k]} -> {b[k]}  (Δ{b[k]-a[k]:+d})")
print(f"  precision       : {a['precision']:.4f} -> {b['precision']:.4f}")
print(f"  A-class count   : {len(a['acls'])} -> {len(b['acls'])}")
print(f"  stage3E A-class remaining: {sorted(b['acls'])}")

# Per-case correctness diff (by id)
def idx(d): return {r.get('id'):r for r in rows(d)}
i3=idx(d3); i3e=idx(d3e)
print("\n=== reaction_check 逐题翻转 ===")
lost=[]; gained=[]
for cid in sorted(set(i3)&set(i3e)):
    r3=i3[cid]; r3e=i3e[cid]
    if (r3.get('taskType') or '')!='reaction_check': continue
    c3=bool(r3.get('gatewayCorrect')); c3e=bool(r3e.get('gatewayCorrect'))
    if c3 and not c3e: lost.append(cid)
    if not c3 and c3e: gained.append(cid)
print(f"  LOST (correct→wrong) : {lost}  -> 必须为空")
print(f"  GAINED (wrong→correct): {sorted(gained)}")

# Verify the 4 targets flipped AND are adopted+correct; verify 11 kept-null stay null
targets=["d9-1","pdf-539","pdf-456","pdf-467"]
print("\n=== 4 个目标题核对 ===")
for cid in targets:
    r=i3e.get(cid)
    if not r: print(f"  {cid}: MISSING"); continue
    print(f"  {cid}: gwCorrect={r.get('gatewayCorrect')} adopted={r.get('gatewayTruthAdopted')} bucket={r['_bucket']} concl={r.get('gatewayConclusion')!r}")

kept_null=["pdf-010","pdf-119","pdf-122","pdf-164","pdf-171","pdf-174","pdf-175","pdf-179","pdf-233","pdf-417","pdf-462"]
print("\n=== 11 题保持 null 核对（应仍未采纳、结论非我新规则产出） ===")
bad=[]
for cid in kept_null:
    r=i3e.get(cid)
    if not r: print(f"  {cid}: MISSING"); continue
    adopted=bool(r.get('gatewayTruthAdopted'))
    concl=r.get('gatewayConclusion') or ''
    # must not be adopted by my new writers (would indicate misfire)
    if adopted: bad.append(cid)
    print(f"  {cid}: adopted={adopted} gwCorrect={r.get('gatewayCorrect')} concl={concl!r}")
print(f"  误采纳数: {len(bad)} {bad if bad else '(无)'}")

print("\n=== 红线核对 ===")
print("  lost=0        :", "PASS" if not lost else "FAIL")
print("  harm=0 (rc)   :", "PASS" if b['harm']==0 else "FAIL")
print("  precision=1.0 :", "PASS" if abs(b['precision']-1.0)<1e-9 else "FAIL")
print("  TR-9.1=0 (rc) :", "PASS" if b['tr91']==0 else "FAIL")
print("  gained==4     :", "PASS" if sorted(gained)==sorted(targets) else f"FAIL {sorted(gained)}")
