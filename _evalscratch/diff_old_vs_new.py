import json
def load(p): return json.load(open(p,encoding='utf-8-sig'))
OLD=load(r'D:\github\CogniThink-Chem\_evalscratch\after_stage3c_rerun.json')  # old code, 5080
NEW=load(r'D:\github\CogniThink-Chem\_evalscratch\after_stage4.json')         # new code, 5090
def allcases(o):
    out={}
    for lst in ['bothCorrectCases','gatewayOnlyCorrectCases','pureLlmOnlyCorrectCases','bothWrongCases']:
        for c in (o.get(lst) or []): out[c['id']]=c
    return out
O=allcases(OLD); N=allcases(NEW)
def b(c,k): return str(c.get(k))=='True'
Oad={i for i,c in O.items() if b(c,'gatewayTruthAdopted')}
Nad={i for i,c in N.items() if b(c,'gatewayTruthAdopted')}
print('adopted: old=%d new=%d' % (len(Oad),len(Nad)))
print('=== gained (adopted in NEW, not OLD) ===')
for i in sorted(Nad-Oad):
    c=N[i]; print('  %-12s task=%-18s correct=%s ans=%s' % (i,c.get('taskType'),b(c,'gatewayCorrect'),repr(str(c.get('gatewayAnswer'))[:50])))
print('=== lost (adopted in OLD, not NEW) ===')
for i in sorted(Oad-Nad):
    c=O[i]; print('  %-12s task=%-18s correct(old)=%s ans=%s' % (i,c.get('taskType'),b(c,'gatewayCorrect'),repr(str(c.get('gatewayAnswer'))[:50])))

Oharm={i for i,c in O.items() if b(c,'gatewayTruthAdopted') and not b(c,'gatewayCorrect') and b(c,'pureLlmCorrect')}
Nharm={i for i,c in N.items() if b(c,'gatewayTruthAdopted') and not b(c,'gatewayCorrect') and b(c,'pureLlmCorrect')}
print()
print('harm: old=%s  new=%s' % (sorted(Oharm),sorted(Nharm)))
print('harm NEW-only (caused by new code):', sorted(Nharm-Oharm))
print('harm OLD-only (fixed by new code):', sorted(Oharm-Nharm))
print()
print('=== NEW atomic_structure correctness ===')
for i,c in N.items():
    if c.get('taskType')=='atomic_structure':
        print('  ',i,'correct=',b(c,'gatewayCorrect'),'adopted=',b(c,'gatewayTruthAdopted'))
print('=== any correct(old)->wrong(new) that is atomic/concept? ===')
for i in O:
    oc=O[i]; nc=N.get(i)
    if nc and b(oc,'gatewayCorrect') and not b(nc,'gatewayCorrect'):
        print('  regress',i,'task new=',nc.get('taskType'),'old=',oc.get('taskType'))
