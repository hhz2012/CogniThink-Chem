import json, re
from collections import Counter

O = json.load(open(r'D:\github\CogniThink-Chem\_evalscratch\after_stage4.json', encoding='utf-8-sig'))

def allcases(o):
    out = {}
    for lst in ['bothCorrectCases','gatewayOnlyCorrectCases','pureLlmOnlyCorrectCases','bothWrongCases']:
        for c in (o.get(lst) or []):
            out[c['id']] = c
    return out

C = allcases(O)
def b(c,k): return str(c.get(k)) == 'True'

solved = {i for i,c in C.items() if b(c,'gatewayTruthAdopted')}
unsolved = [c for i,c in C.items() if not b(c,'gatewayTruthAdopted')]
print(f"total={len(C)} engineSolved(truthAdopted)={len(solved)} unsolved={len(unsolved)}")

# distribution by taskType: solved vs unsolved
by_type = Counter()
for c in C.values():
    t = c.get('taskType') or '?'
    by_type[(t, 'solved' if b(c,'gatewayTruthAdopted') else 'unsolved')] += 1
print("\n=== taskType 分布 (solved/unsolved) ===")
types = sorted(set(t for t,_ in by_type))
for t in types:
    s = by_type.get((t,'solved'),0); u = by_type.get((t,'unsolved'),0)
    print(f"  {t:20s} solved={s:4d}  unsolved={u:4d}")

# unsolved by isPdf / type
print("\n=== unsolved 中 choice vs 非choice ===")
ch = sum(1 for c in unsolved if (c.get('type') or '').lower()=='choice')
print(f"  unsolved choice={ch}  unsolved non-choice={len(unsolved)-ch}")

# For unsolved, keyword scan on question text to find deterministic clusters
KW = {
 '电子排布':'电子排布|排布式|轨道表示|价层电子|核外电子|电子构型',
 '周期表':'周期表|第.*周期|第.*族|位于.*周期',
 '杂化':'杂化|sp3|sp2|sp|σ键|π键|配位键|键角',
 '分子构型':'分子构型|空间构型|VSEPR|价层电子对|孤电子对',
 '晶胞':'晶胞|均摊|配位数|堆积|晶格',
 '反应热/焓':'反应热|焓变|ΔH|燃烧热|中和热|键能',
 '速率':'反应速率|速率常数|活化能|催化剂',
 '平衡':'平衡|转化率|勒夏特列|Kc|Kp|三段式',
 '电解质':'电解质|电离|水解|pH|Kw|Ka|Kb|溶度积|Ksp',
 '原电池/电解':'原电池|电解池|电极|放电|充电|电镀|电化学',
 '氧化还原':'氧化还原|氧化剂|还原剂|电子转移|化合价',
 '阿伏伽德罗':'阿伏伽德罗|NA|6.02|微粒数',
 '物质的量':'物质的量|摩尔|mol|摩尔质量|气体摩尔体积',
 '浓度':'物质的量浓度|c=|ω|质量分数|稀释',
 '有机':'有机物|同分异构|官能团|命名|酯化|加成|取代|消去',
 '实验':'实验|操作|现象|装置|试剂|除杂|检验|提纯',
 '元素推断':'元素推断|短周期|原子序数|质子数|中子数',
}
def norm(s): return re.sub(r'\s+','',s or '')
print("\n=== unsolved 题关键词命中(非choice，去重计数) ===")
hit = Counter()
hit_ids = {}
for c in unsolved:
    if (c.get('type') or '').lower()=='choice': continue
    q = norm(c.get('question',''))
    for k,pat in KW.items():
        if re.search(pat, q):
            hit[k]+=1
            hit_ids.setdefault(k,[]).append(c['id'])
for k,n in hit.most_common():
    print(f"  {k:14s} {n:4d}  例: {hit_ids[k][:5]}")

# Show a sample of unsolved concept/unknown non-choice questions (first 40 ids + taskType + short question)
print("\n=== unsolved 非choice 前60 (id | taskType | question前50字) ===")
shown=0
for c in unsolved:
    if (c.get('type') or '').lower()=='choice': continue
    if shown>=60: break
    shown+=1
    print(f"  {c['id']:10s} | {str(c.get('taskType')):14s} | {norm(c.get('question',''))[:50]}")
