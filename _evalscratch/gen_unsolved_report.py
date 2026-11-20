# -*- coding: utf-8 -*-
"""Generate a categorized list of unsolved questions from after_tier2.json.
'Unsolved' = gatewayCorrect == False (engine did not correctly solve it).
"""
import json, re
from collections import Counter, defaultdict

SRC = r'd:\github\CogniThink-Chem\_evalscratch\after_tier2.json'
OUT = r'd:\github\CogniThink-Chem\_evalscratch\unsolved_tier2_report.md'

d = json.load(open(SRC, encoding='utf-8'))

def all_cases():
    for n in ['bothCorrectCases', 'gatewayOnlyCorrectCases',
              'pureLlmOnlyCorrectCases', 'bothWrongCases']:
        for c in d[n]:
            yield c

def engine_flag(c):
    if c.get('gatewayUsedEngine'):
        if c.get('gatewayTruthAdopted'):
            return '采纳-错(harm)'  # shouldn't happen, harm=0
        return '引擎跑了未采纳'
    return '引擎未覆盖'

def cause(c):
    """A/B/C 成因细分（仅对未解出题有意义）。"""
    llm = c.get('pureLlmCorrect')
    eng = c.get('gatewayUsedEngine')
    if llm and not eng:
        return 'A'   # LLM 答对，引擎未参与，但网关最终错 → 链路/后处理问题
    if llm and eng:
        return 'B'   # LLM 答对，引擎跑了但未采纳 → 错失
    return 'C'       # LLM 也错 → 真盲区/难题

def cause_tag(c):
    ca = cause(c)
    if ca == 'A':
        return 'A·参照'   # LLM 已答对，可作引擎扩展的验证参照
    if ca == 'B':
        return 'B·错失'   # 引擎跑了但坏键未采纳
    return 'C·未覆盖' if engine_flag(c) == '引擎未覆盖' else 'C·未采纳'  # LLM 也错，真盲区

CAUSE_DESC = {
    'A': 'LLM 已答对 + 引擎未参与，但网关最终判错（需扩展引擎使其自行给出正确解；LLM 答对可作验证参照）',
    'B': 'LLM 已答对 + 引擎跑了但未采纳（坏键/null），需修引擎使其正确解出并采纳',
    'C': 'LLM 也答错（引擎与 LLM 都解不出 → 需加化学规则，适合扩展校验器）',
}

# (category_name, compiled regex). First match wins (priority order).
TOPIC_RULES = [
    ('有机化学', re.compile(r'冠醚|PLGA|合成路线|有机|官能团|同分异构|消去|加成反应|取代反应|加聚|缩聚|高分子|聚合物|手性|葡萄糖|果糖|蔗糖|淀粉|纤维素|蛋白质|核酸|氨基酸|乙烯|乙炔|苯|甲苯|苯酚|乙醇|乙醛|乙酸|乙酸乙酯|卤代烃|烯烃|炔烃|芳香|酯类|醛类|羧酸|酮|醚类|胺|酰胺|生物相容|抗生素|中间体|水杨酸|麻醉|催化加氢|水解反应|酯化')),
    ('电化学', re.compile(r'电解(?!质)|原电池|电池|电极|阳极|阴极|正极|负极|电镀|电冶金|燃料电池|二次电池|锂离子|海水直接制备氢气|放电|充电|电化学腐蚀|牺牲阳极|外加电流')),
    ('化学反应速率与化学平衡', re.compile(r'反应速率|化学平衡|平衡移动|转化率|产率|活化能|催化剂|勒夏特列|恒容|恒压|等效平衡|反应进行方向|反应历程|能垒|决速步')),
    ('水溶液中的离子平衡', re.compile(r'\bpH\b|电离|水解|Ksp|溶度积|沉淀溶解|缓冲|弱电解质|强电解质|解离|质子守恒|电荷守恒|物料守恒|水的电离|盐类水解|难溶|溶度积常数')),
    ('离子反应/离子方程式/共存', re.compile(r'离子方程式|离子共存|大量共存|离子组|能大量共存|不能大量共存')),
    ('物质结构与元素周期律', re.compile(r'电负性|电离能|离子半径|原子半径|电子排布|核外电子|元素周期|周期律|周期表|同周期|同主族|化学键|离子键|共价键|配位键|金属键|范德华|氢键|杂化|VSEPR|价层电子对|空间构型|分子构型|晶体|晶胞|晶格能|原子序数|同位素|核素|电子亲和能|第一电离能|轨道|能级|价电子|基态')),
    ('化学实验', re.compile(r'实验|操作|现象|结论|检验|提纯|分离|除杂|过滤|蒸发|蒸馏|萃取|分液|配制|滴定|装置|试剂|实验室|安全|方案|探究|测定|鉴别|鉴定|实验方案|实验目的')),
    ('氧化还原反应', re.compile(r'氧化产物|还原产物|被氧化|被还原|电子转移|单线桥|双线桥|化合价|氧化剂|还原剂|氧化性|还原性|重铬酸钾|高锰酸钾')),
    ('化学与生活/材料/能源', re.compile(r'材料|金属材料|石墨烯|合金|温室气体|固氮|能源|燃料|环境|污染|绿色化学|海水淡化|资源|矿物|半导体|光导纤维|陶瓷|水泥|玻璃|橡胶|纤维|塑料')),
    ('化学计量与计算', re.compile(r'物质的量|阿伏伽德罗|NA|浓度|气体摩尔体积|质量分数|密度|标准状况|配制一定物质的量浓度')),
    ('物质分类与概念辨析', re.compile(r'电解质|胶体|分散系|物质分类|分类|纯净物|混合物|单质|化合物|氧化物|酸|碱|盐|概念')),
    ('元素化合物(综合)', re.compile(r'.')),  # fallback
]

def classify(c):
    text = (c.get('question','') + ' ' + c.get('note',''))
    for name, rx in TOPIC_RULES:
        if rx.search(text):
            return name
    return '元素化合物(综合)'

unsolved = [c for c in all_cases() if not c.get('gatewayCorrect')]
solved = [c for c in all_cases() if c.get('gatewayCorrect')]

# stats
by_topic = Counter()
by_topic_task = defaultdict(Counter)
by_topic_eng = defaultdict(Counter)
by_topic_cause = defaultdict(Counter)
by_topic_list = defaultdict(list)
for c in unsolved:
    t = classify(c)
    by_topic[t] += 1
    by_topic_task[t][c.get('taskType')] += 1
    by_topic_eng[t][engine_flag(c)] += 1
    by_topic_cause[t][cause(c)] += 1
    by_topic_list[t].append(c)

# overall
eng_overall = Counter(engine_flag(c) for c in unsolved)
cause_overall = Counter(cause(c) for c in unsolved)
task_overall = Counter(c.get('taskType') for c in unsolved)

# order topics by count desc
topic_order = [t for t,_ in by_topic.most_common()]

def short(s, n=64):
    s = re.sub(r'\s+', ' ', s or '').strip()
    return s if len(s) <= n else s[:n] + '…'

lines = []
lines.append('# Tier 2 未解出题目分类清单')
lines.append('')
lines.append(f'- 数据来源：`after_tier2.json`（确定性快照，Task 19）')
lines.append(f'- 总题量：**{d["totalCases"]}**；引擎正确解出：**{len(solved)}**；**未解出：{len(unsolved)}**')
lines.append(f'- "未解出"定义：`gatewayCorrect == false`（引擎/网关未给出正确最终答案）')
lines.append(f'- 修引擎口径：**LLM 答对不计入引擎正确**——540 道全部是引擎未解出，全部待修；统一靠**引擎自身给出正确解**，不靠接 LLM 答案。A/B/C 只用于区分扩展难度与参照来源。')
lines.append(f'- 引擎精确率：{d["enginePrecision"]}（harmCount={d["harmCount"]}，无引擎采纳错误）')
lines.append('')
lines.append('> 注：知识点分类为基于题干+note 关键词的启发式主分类（按优先级首次命中），')
lines.append('> 综合判断题可能涉及多个主题，归类仅供定位盲区参考。')
lines.append('')

# Section 1: cause A/B/C — most informative
lines.append('## 一、按未解出成因（A/B/C）')
lines.append('')
lines.append('| 成因 | 题数 | 说明 |')
lines.append('|---|---:|---|')
for ca in ['A', 'B', 'C']:
    lines.append(f'| **{ca} 类** | {cause_overall.get(ca,0)} | {CAUSE_DESC[ca]} |')
lines.append('')
lines.append('> 解读（修引擎口径）：**A/B/C 全部 540 道都靠引擎自身给出正确解，不靠接 LLM 答案**。区别只在扩展难度与参照来源：')
lines.append('> - **A 类（375）**：LLM 已答对 → 说明有确定性解法、引擎应能覆盖；**LLM 的正确答案可作验证参照，扩展难度低、优先度高**。')
lines.append('> - **B 类（7）**：引擎已有路径但坏键未采纳 → 修引擎使其正确解出并采纳，临门一脚。')
lines.append('> - **C 类（158）**：LLM 也答错 → 可能更难或需更专业化学知识；加事实/规则库、扩展校验器，无 LLM 参照需自行求真值。')
lines.append('')

# Section 2: engine involvement
lines.append('## 二、按引擎参与情况')
lines.append('')
lines.append('| 引擎状态 | 题数 | 说明 |')
lines.append('|---|---:|---|')
lines.append(f'| 引擎未覆盖（gatewayUsedEngine=false） | {eng_overall["引擎未覆盖"]} | 引擎未参与 |')
lines.append(f'| 引擎跑了未采纳（坏键/null） | {eng_overall["引擎跑了未采纳"]} | 引擎尝试但未采纳 |')
lines.append(f'| 采纳-错(harm) | {eng_overall.get("采纳-错(harm)",0)} | 本次快照 0 |')
lines.append('')

# Section 3: taskType
lines.append('## 三、按题型（taskType）')
lines.append('')
lines.append('| 题型 | 未解出题数 |')
lines.append('|---|---:|')
for k,v in task_overall.most_common():
    lines.append(f'| {k} | {v} |')
lines.append('')

# Section 4: topic summary
lines.append('## 四、按知识点大类（汇总）')
lines.append('')
lines.append('| 知识点大类 | 未解出 | A类(链路) | B类(错失) | C类(真盲区) | 其中choice | 引擎跑了未采纳 |')
lines.append('|---|---:|---:|---:|---:|---:|---:|')
for t in topic_order:
    tt = by_topic_task[t]
    tc = by_topic_cause[t]
    te = by_topic_eng[t]
    lines.append(f'| {t} | {by_topic[t]} | {tc.get("A",0)} | {tc.get("B",0)} | {tc.get("C",0)} | {tt.get("choice",0)} | {te.get("引擎跑了未采纳",0)} |')
lines.append(f'| **合计** | **{len(unsolved)}** | {cause_overall.get("A",0)} | {cause_overall.get("B",0)} | {cause_overall.get("C",0)} | {task_overall.get("choice",0)} | {eng_overall["引擎跑了未采纳"]} |')
lines.append('')

# Section 5: "engine ran but not adopted" detail — high value
eng_ran = [c for c in unsolved if engine_flag(c)=='引擎跑了未采纳']
if eng_ran:
    lines.append('## 五、引擎已尝试但未解出（引擎跑了未采纳）— 高优先级盲区')
    lines.append('')
    lines.append('这些题引擎已进入某条确定性路径，但返回 null/坏键未被采纳，最终落到 LLM 答错。是扩展校验器的直接候选。')
    lines.append('')
    for c in sorted(eng_ran, key=lambda x: x['id']):
        lines.append(f'- **{c["id"]}** `{c.get("taskType")}` — {short(c["question"],90)}')
        lines.append(f'  - 期望：{short(c.get("expected"),80)}')
        lines.append(f'  - note：{short(c.get("note"),100)}')
        lines.append(f'  - gatewayConclusion：{short(c.get("gatewayConclusion"),120)}')
    lines.append('')
    lines.append('> **观察**：这 14 道中绝大多数是 `reaction_check` 题型（工艺流程/元素推断大题的某一小问）。')
    lines.append('> 引擎已进入反应解析路径并给出结论（如"已匹配到相关反应"），但该题型的 `expected` 是**整道大题所有小问的关键词拼接**，')
    lines.append('> 而引擎只对应当前小问作答 → 判分未对齐，记为未解出。这更可能是**评估数据对齐问题**而非引擎推理错误，')
    lines.append('> 后续若要消化这批题，应先把 `expected` 按小问拆分对齐。')
    lines.append('')

# Section 6: A-class subset (LLM already correct — high priority extension)
a_only = [c for c in unsolved if cause(c) == 'A']
lines.append('## 六、A 类清单：LLM 已答对的子集（共 {} 题）→ 扩展优先（LLM 答案可作验证参照）'.format(len(a_only)))
lines.append('')
lines.append('> 这些题 LLM 已答对，说明存在确定性解法、引擎应能覆盖。**LLM 的正确答案可直接作验证参照**，')
lines.append('> 扩展难度低、投入产出比高，建议优先消化。每题的题干摘要与期望答案见第八节完整列表；LLM 正确答案可从快照按需导出。')
lines.append('')
a_by_topic = Counter(classify(c) for c in a_only)
lines.append('| 知识点大类 | A 类题数（LLM 已答对） |')
lines.append('|---|---:|')
for t in topic_order:
    if a_by_topic.get(t, 0):
        lines.append(f'| {t} | {a_by_topic[t]} |')
lines.append(f'| **合计** | **{len(a_only)}** |')
lines.append('')

# Section 7: C-class subset (rule-missing) — no LLM reference
c_only = [c for c in unsolved if cause(c) == 'C']
lines.append('## 七、C 类清单：LLM 也答错的子集（共 {} 题）→ 需加化学规则'.format(len(c_only)))
lines.append('')
lines.append('> 这是 540 道待修题中"LLM 也答错"的子集，无 LLM 参照需自行求真值，适合通过加事实/规则库消化。')
lines.append('> **全部 540 道待修主清单见第八节**；A 类（375 道，LLM 已答对可作参照、扩展优先）和 B 类（7 道，修坏键）同样靠引擎自身解出。')
lines.append('')
lines.append('按知识点大类分组。标记：`C·未覆盖`=引擎未参与；`C·未采纳`=引擎跑了未采纳。')
lines.append('')
c_by_topic = defaultdict(list)
for c in c_only:
    c_by_topic[classify(c)].append(c)
for t in topic_order:
    items = sorted(c_by_topic.get(t, []), key=lambda x: x['id'])
    if not items:
        continue
    lines.append(f'### {t}（{len(items)} 题）')
    lines.append('')
    lines.append('| 题号 | 题型 | 标记 | 题干摘要 | 期望答案 |')
    lines.append('|---|---|---|---|---|')
    for c in items:
        q = short(c['question'], 70).replace('|', '/')
        e = short(c.get('expected'), 40).replace('|', '/')
        lines.append(f'| {c["id"]} | {c.get("taskType")} | {cause_tag(c)} | {q} | {e} |')
    lines.append('')

# Section 8: full list (all 540)
lines.append('## 八、未解出题目完整列表（全部 {} 题，按知识点大类分组）'.format(len(unsolved)))
lines.append('')
lines.append('标记含义：`A·参照`=LLM 已答对（可作引擎扩展验证参照）；`B·错失`=引擎跑了但坏键未采纳；`C·未覆盖/未采纳`=LLM 也错（真盲区）。组内按 C→B→A 排序。')
lines.append('')
def sort_key(c):
    order = {'C': 0, 'B': 1, 'A': 2}
    return (order.get(cause(c), 9), c['id'])
for t in topic_order:
    items = sorted(by_topic_list[t], key=sort_key)
    lines.append(f'### {t}（{len(items)} 题）')
    lines.append('')
    lines.append('| 题号 | 题型 | 标记 | 题干摘要 | 期望答案 |')
    lines.append('|---|---|---|---|---|')
    for c in items:
        q = short(c['question'], 70).replace('|', '/')
        e = short(c.get('expected'), 40).replace('|', '/')
        lines.append(f'| {c["id"]} | {c.get("taskType")} | {cause_tag(c)} | {q} | {e} |')
    lines.append('')

with open(OUT, 'w', encoding='utf-8') as f:
    f.write('\n'.join(lines))

print('written', OUT)
print('total unsolved', len(unsolved))
print('topics:')
for t in topic_order:
    print(' ', t, by_topic[t])
