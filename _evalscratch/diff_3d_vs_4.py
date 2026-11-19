import json
def load(p): return json.load(open(p,encoding='utf-8-sig'))
O=load(r'D:\github\CogniThink-Chem\_evalscratch\after_stage3d.json')  # immediate predecessor
N=load(r'D:\github\CogniThink-Chem\_evalscratch\after_stage4.json')   # my run
print('=== aggregate: stage3d -> stage4 ===')
for k in ['totalCases','pureLlmCorrect','gatewayCorrect','gatewayTruthCorrect','engineSolved','enginePrecision','harmCount','bothCorrect']:
    print('  %-20s %s -> %s' % (k, O.get(k), N.get(k)))
def allcases(o):
    out={}
    for lst in ['bothCorrectCases','gatewayOnlyCorrectCases','pureLlmOnlyCorrectCases','bothWrongCases']:
        for c in (o.get(lst) or []): out[c['id']]=c
    return out
O2=allcases(O); N2=allcases(N)
def b(c,k): return str(c.get(k))=='True'
Oad={i for i,c in O2.items() if b(c,'gatewayTruthAdopted')}
Nad={i for i,c in N2.items() if b(c,'gatewayTruthAdopted')}
print()
print('adopted: stage3d=%d stage4=%d  gained=%d lost=%d' % (len(Oad),len(Nad),len(Nad-Oad),len(Oad-Nad)))
print('--- gained (new in stage4) ---')
for i in sorted(Nad-Oad):
    c=N2[i]; print('  %-12s task=%-18s correct=%s ans=%s' % (i,c.get('taskType'),b(c,'gatewayCorrect'),repr(str(c.get('gatewayAnswer'))[:48])))
print('--- lost (gone in stage4) ---')
for i in sorted(Oad-Nad):
    c=O2[i]; print('  %-12s task=%-18s correct(stage3d)=%s' % (i,c.get('taskType'),b(c,'gatewayCorrect')))
Oharm={i for i,c in O2.items() if b(c,'gatewayTruthAdopted') and not b(c,'gatewayCorrect') and b(c,'pureLlmCorrect')}
Nharm={i for i,c in N2.items() if b(c,'gatewayTruthAdopted') and not b(c,'gatewayCorrect') and b(c,'pureLlmCorrect')}
print()
print('harm stage3d=%s  stage4=%s' % (sorted(Oharm),sorted(Nharm)))
print('harm NEW-only (my change):', sorted(Nharm-Oharm))
print()
print('--- stage4 atomic_structure ---')
for i,c in N2.items():
    if c.get('taskType')=='atomic_structure':
        print('  ',i,'correct=',b(c,'gatewayCorrect'),'adopted=',b(c,'gatewayTruthAdopted'),'| stage3d task=',O2.get(i,{}).get('taskType'),'adopted=',b(O2.get(i,{}),'gatewayTruthAdopted') if i in O2 else '?')
