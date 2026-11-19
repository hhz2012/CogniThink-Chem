import json
def load(p): return json.load(open(p,encoding='utf-8-sig'))
a=load(r'D:\github\CogniThink-Chem\_evalscratch\after_stage3c.json')
b=load(r'D:\github\CogniThink-Chem\_evalscratch\after_stage4.json')
def allcases(o):
    out={}
    for lst in ['bothCorrectCases','gatewayOnlyCorrectCases','pureLlmOnlyCorrectCases','bothWrongCases']:
        for c in (o.get(lst) or []): out[c['id']]=c
    return out
A=allcases(a); B=allcases(b)
def adopted(c): return str(c.get('gatewayTruthAdopted'))=='True'
def correct(c): return str(c.get('gatewayCorrect'))=='True'
def usedeng(c): return str(c.get('gatewayUsedEngine'))=='True'
def pureok(c): return str(c.get('pureLlmCorrect'))=='True'

print('=== stage4 harm cases (adopted & !correct & pureLlmCorrect) ===')
for c in B.values():
    if adopted(c) and not correct(c) and pureok(c):
        ac=A.get(c['id'])
        print('  id=%s task=%s' % (c['id'], c.get('taskType')))
        print('     gwAns  :', repr(str(c.get('gatewayAnswer'))[:90]))
        print('     expected:', repr(str(c.get('expected'))[:90]))
        print('     pureAns:', repr(str(c.get('pureLlmAnswer'))[:70]))
        print('     stage3c: correct=%s adopted=%s task=%s' % (correct(ac) if ac else '?', adopted(ac) if ac else '?', ac.get('taskType') if ac else '?'))

print()
print('=== stage4 ALL atomic_structure cases ===')
n=0
for c in B.values():
    if c.get('taskType')=='atomic_structure':
        n+=1
        print('  ',c['id'],'correct=',correct(c),'adopted=',adopted(c),'usedEng=',usedeng(c),'ans=',repr(str(c.get('gatewayAnswer'))[:55]))
print('count =',n)

print()
Aset={i for i,c in A.items() if adopted(c)}
Bset={i for i,c in B.items() if adopted(c)}
gained=sorted(Bset-Aset); lost=sorted(Aset-Bset)
print('engineSolved stage3c=%d stage4=%d  gained=%d lost=%d' % (len(Aset),len(Bset),len(gained),len(lost)))
print('--- gained (newly adopted in stage4) ---')
for i in gained:
    c=B[i]
    print('  %-12s task=%-16s correct=%s usedEng=%s ans=%s' % (i, c.get('taskType'), correct(c), usedeng(c), repr(str(c.get('gatewayAnswer'))[:50])))
print('--- lost (adopted in stage3c, not in stage4) ---')
for i in lost:
    c=A[i]
    print('  %-12s task=%-16s correct(stage3c)=%s' % (i, c.get('taskType'), correct(c)))
