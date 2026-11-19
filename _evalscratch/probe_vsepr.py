import json, re

O = json.load(open(r'D:\github\CogniThink-Chem\_evalscratch\after_stage4.json', encoding='utf-8-sig'))
QB = json.load(open(r'D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json', encoding='utf-8-sig'))

def allcases(o):
    out = {}
    for lst in ['bothCorrectCases','gatewayOnlyCorrectCases','pureLlmOnlyCorrectCases','bothWrongCases']:
        for c in (o.get(lst) or []):
            out[c['id']] = c
    return out
C = allcases(O)
def b(c,k): return str(c.get(k)) == 'True'

# build qbank lookup by id
qbank = {}
if isinstance(QB, list):
    for q in QB:
        qbank[str(q.get('id'))] = q
elif isinstance(QB, dict):
    # maybe {questions:[...]}
    for k in QB:
        if isinstance(QB[k], list):
            for q in QB[k]:
                qbank[str(q.get('id'))] = q

print(f"eval cases={len(C)}  qbank={len(qbank)}")
print("qbank top-level type:", type(QB).__name__, "keys:" , list(QB.keys())[:5] if isinstance(QB,dict) else 'list')

KW = re.compile(r'VSEPR|价层电子对|孤电子对|孤对电子|空间构型|分子构型|杂化|sp3|sp2|键角|平面三角形|正四面体|三角锥形|V形|直线形|折线形')

# gather candidates
cands = []
for cid, c in C.items():
    t = c.get('taskType') or ''
    q = (c.get('question') or '')
    is_vsepr_type = (t == 'vsepr')
    kw_hit = bool(KW.search(q))
    if is_vsepr_type or kw_hit:
        cands.append((cid, c, is_vsepr_type, kw_hit))

print(f"\n=== vsepr 候选 (taskType=vsepr 或 关键词命中): {len(cands)} ===")
for cid, c, ist, kwh in sorted(cands):
    solved = b(c, 'gatewayTruthAdopted')
    correct = b(c, 'gatewayCorrect')
    ptype = (c.get('type') or '').lower()
    print(f"\n--- {cid}  bucket? taskType={c.get('taskType')} type={ptype} isPdf={c.get('isPdf')} "
          f"adopted={solved} gatewayCorrect={correct} [typeMatch={ist} kw={kwh}]")
    print("  Q:", re.sub(r'\s+',' ', (c.get('question') or '')).strip()[:300])
    print("  A:", repr(c.get('answer')))
    print("  Keywords:", c.get('answerKeywords'))
    opts = c.get('options')
    if opts:
        print("  Options:", opts)
    # enrich from qbank
    qb = qbank.get(cid)
    if qb:
        qbq = qb.get('question') or qb.get('Question') or ''
        qba = qb.get('answer') or qb.get('Answer') or ''
        if qbq and qbq != (c.get('question') or ''):
            print("  >> QB Q:", re.sub(r'\s+',' ',str(qbq)).strip()[:300])
        if qba and str(qba) != str(c.get('answer')):
            print("  >> QB A:", repr(qba), " KB Kw:", qb.get('answerKeywords') or qb.get('AnswerKeywords'))
