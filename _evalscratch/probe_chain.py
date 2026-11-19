# -*- coding: utf-8 -*-
"""Tier 2-B 转化链校验器 Python 探针（C# 实现的镜像，先验证逻辑再移植）。
输入：data/highschool/transformation-rules.json + 内联测试规则 + 题目。
输出：每题判定（答案字母 / null）、未覆盖步骤告警。
红线：任一步缺规则 -> 整题 null；非唯一 -> null；非链 -> null。
"""
import json, re

RULES = json.load(open(r'D:\github\CogniThink-Chem\data\highschool\transformation-rules.json', encoding='utf-8'))['steps']

# 待入库的新增规则（探针验证通过后写入 transformation-rules.json）
TEST_RULES = [
    # --- pdf-013 不可行步 ---
    {"from":"NO","to":"HNO3","reagents":["H2O"],"conditions":[],"feasible":False,"reason":"NO 与水不反应，需 O2：4NO+3O2+2H2O=4HNO3"},
    # --- pdf-016 不可行步 ---
    {"from":"Fe","to":"Fe2O3","reagents":["H2O"],"conditions":["高温"],"feasible":False,"reason":"Fe 与水蒸气高温生成 Fe3O4，非 Fe2O3"},
    {"from":"K2FeO4","to":"H2","reagents":["H2O"],"conditions":[],"feasible":False,"reason":"K2FeO4 水解生成 Fe(OH)3/O2，不生成 H2"},
    {"from":"Fe2O3","to":"Fe(OH)3","reagents":["H2O"],"conditions":[],"feasible":False,"reason":"Fe2O3 难溶于水，不能直接与水反应"},
    # --- pdf-016 可行补步 ---
    {"from":"Fe2O3","to":"K2FeO4","reagents":["KClO","KOH"],"conditions":[],"feasible":True,"reason":"碱性次氯酸钾氧化 Fe2O3"},
    {"from":"FeCl3","to":"Fe2O3","reagents":[],"conditions":["蒸干","灼烧"],"feasible":True,"reason":"FeCl3 水解蒸干得 Fe(OH)3，灼烧得 Fe2O3"},
    {"from":"Fe","to":"FeCl3","reagents":["Cl2"],"conditions":["△"],"feasible":True,"reason":"2Fe+3Cl2=2FeCl3 (加热)"},
    # --- pdf-017 不可行/可行补步（保证 A、D 均可行 -> 非唯一 -> null，不破坏现有正确路径）---
    {"from":"S","to":"SO3","reagents":["O2"],"conditions":["点燃"],"feasible":False,"reason":"S 点燃生成 SO2，不能直接生成 SO3"},
    {"from":"MgCl2(溶液)","to":"无水MgCl2","reagents":[],"conditions":["蒸干"],"feasible":False,"reason":"MgCl2 溶液蒸干水解得 Mg(OH)2/MgO，得不到无水 MgCl2"},
    {"from":"无水MgCl2","to":"Mg","reagents":[],"conditions":["熔融电解"],"feasible":True,"reason":"MgCl2(熔融)=Mg+Cl2"},
    # --- t2t 题库备用可行步 ---
    {"from":"SO2","to":"Na2SO3","reagents":["NaOH"],"conditions":[],"feasible":True,"reason":"SO2+2NaOH=Na2SO3+H2O"},
    {"from":"SO2","to":"CaSO3","reagents":["Ca(OH)2"],"conditions":[],"feasible":True,"reason":"SO2+Ca(OH)2=CaSO3+H2O"},
    {"from":"Na2SO3","to":"SO2","reagents":["H2SO4"],"conditions":[],"feasible":True,"reason":"Na2SO3+H2SO4=Na2SO4+SO2+H2O"},
    {"from":"H2S","to":"S","reagents":["Cl2"],"conditions":[],"feasible":True,"reason":"H2S+Cl2=2HCl+S"},
    {"from":"S","to":"Cu2S","reagents":["Cu"],"conditions":["△"],"feasible":True,"reason":"2Cu+S=Cu2S"},
]

RULES = RULES + TEST_RULES

# ===================== 归一化 =====================
COND_WORDS = {'高温','加热','点燃','电解','催化剂','放电','光照','灼烧','蒸干','熔融电解',
              '熔融','沸腾','酶','一定条件','高压','常温','冷水','热水','催化剂△'}
SUBST_ALIAS = {
    '饱和食盐水':'NaCl','食盐水':'NaCl','食盐':'NaCl',
    '漂白粉':'漂白粉','漂粉精':'漂白粉',
    '澄清石灰水':'Ca(OH)2','石灰乳':'Ca(OH)2','熟石灰':'Ca(OH)2','消石灰':'Ca(OH)2',
    '生石灰':'CaO','石灰石':'CaCO3','大理石':'CaCO3','石灰水':'Ca(OH)2',
    '纯碱':'Na2CO3','苏打':'Na2CO3','小苏打':'NaHCO3',
    '烧碱':'NaOH','苛性钠':'NaOH','火碱':'NaOH',
    '胆矾':'CuSO4·5H2O','绿矾':'FeSO4·7H2O','明矾':'KAl(SO4)2·12H2O',
    '无水MgCl2':'无水MgCl2','MgCl2溶液':'MgCl2(溶液)','氯化镁溶液':'MgCl2(溶液)',
    '高纯Si':'Si','高纯硅':'Si','粗Si':'Si','粗硅':'Si','纯Si':'Si','单晶硅':'Si','晶体硅':'Si',
    '稀硫酸':'H2SO4(稀)','浓硫酸':'H2SO4(浓)','硫酸':'H2SO4',
    '稀盐酸':'HCl','浓盐酸':'HCl','盐酸':'HCl',
    '稀硝酸':'HNO3','浓硝酸':'HNO3','硝酸':'HNO3',
    '氨水':'NH3·H2O','一水合氨':'NH3·H2O',
    '液氯':'Cl2','氯气':'Cl2','氧气':'O2','氢气':'H2','氮气':'N2',
    '铜':'Cu','铁':'Fe','铝':'Al','锌':'Zn','钠':'Na','镁':'Mg','硫':'S','碳':'C','硅':'Si',
    '水':'H2O',
}
def norm_unicode(s):
    s = s.replace('₂','2').replace('₃','3').replace('₄','4').replace('₅','5').replace('₆','6').replace('₇','7').replace('₈','8')
    s = s.replace('²','2').replace('³','3')
    s = s.translate(str.maketrans('０１２３４５６７８９（）．％','0123456789().%'))
    return s
def strip_state(s):
    return re.sub(r'[\(（]\s*(s|g|l|aq|S|G|L|AQ)\s*[\)）]', '', s, flags=re.I)
def clean_node(s):
    s = norm_unicode(s.strip())
    s = strip_state(s)
    # 剥前导描述性前缀（取最后一个中/英文冒号之后的内容）
    if '：' in s: s = s.rsplit('：', 1)[-1]
    if ':' in s: s = s.rsplit(':', 1)[-1]
    s = re.sub(r'^[\(（]\s*[^）\)]{0,6}[\)）]\s*', '', s)  # 去掉前缀 (粗) 等
    s = re.sub(r'(固体|气体|晶体|粉末)$', '', s)       # 去掉尾部状态名词（保留「溶液」，化学上有意义）
    s = s.replace(' ','').replace('\u3000','')
    return s
def norm_substance(s):
    s = clean_node(s)
    return SUBST_ALIAS.get(s, s)
def norm_temp(tok):
    m = re.match(r'^(\d+)\s*℃$', tok)
    if m:
        return '高温' if int(m.group(1)) >= 500 else '加热'
    return None
def parse_block(block):
    inner = block.strip()
    if inner[:1] in '([': inner = inner[1:]
    if inner[-1:] in ')]': inner = inner[:-1]
    inner = norm_unicode(inner)
    toks = re.split(r'[,，]', inner)
    regs=[]; conds=[]
    for t in toks:
        t = re.sub(r'^[①②③④⑤⑥⑦⑧⑨⑩]','',t.strip().replace(' ',''))
        if not t: continue
        t = re.sub(r'^(过量|少量|足量|纯)\s*','',t)
        ct = norm_temp(t)
        if ct: conds.append(ct); continue
        cn = {'△':'△','Δ':'△','加热':'△'}.get(t)   # 统一条件符号，规则库用 △
        if cn is not None: conds.append(cn); continue
        if t in COND_WORDS: conds.append(t); continue
        a = SUBST_ALIAS.get(t, t)
        regs.append(a)
    return regs, conds

# 支持一层嵌套括号：(Ca(OH)2,△) 或 [O2(g)]；避免化学式内嵌括号打断箭头匹配
BLOCK_ARROW = re.compile(r'--\s*(\((?:[^()]*|\([^()]*\))*\)|\[[^\]]*\])\s*[-＝→>]+')
def parse_chain(text):
    """返回 [(from,to,regs_set,conds_set), ...]；非链或节点含 +/= 返回 None。"""
    text = norm_unicode(text)
    text = strip_state(text)   # 先剥 (g)/(l)/(s)/(aq)，避免试剂块内嵌括号打断箭头正则
    ms = list(BLOCK_ARROW.finditer(text))
    if len(ms) < 1: return None
    nodes = [text[:ms[0].start()]]
    for i in range(len(ms)-1):
        nodes.append(text[ms[i].end():ms[i+1].start()])
    nodes.append(text[ms[-1].end():])
    nodes = [clean_node(n) for n in nodes]
    if any(('+' in n or '=' in n or '⇌' in n) for n in nodes): return None
    edges = []
    for i,m in enumerate(ms):
        regs,conds = parse_block(m.group(1))
        fr = norm_substance(nodes[i]); to = norm_substance(nodes[i+1])
        edges.append((fr, to, set(regs), set(conds)))
    return edges

def match_rule(edge):
    fr,to,regs,conds = edge
    inf_reason = None
    for r in RULES:
        if r['from']!=fr or r['to']!=to: continue
        rr = set(r.get('reagents') or []); cc = set(r.get('conditions') or [])
        if rr.issubset(regs) and cc.issubset(conds):
            if r.get('feasible', True):
                return 'feasible', r.get('reason','')
            else:
                inf_reason = r.get('reason','')
    if inf_reason is not None:
        return 'infeasible', inf_reason
    return 'unknown', f'{fr}→{to} regs={sorted(regs)} conds={sorted(conds)}'

def ask_dir(stem):
    if ('不能实现' in stem) or ('不能一步' in stem) or ('无法实现' in stem) or ('不能均' in stem): return 'wrong'
    if ('能实现' in stem) or ('均能一步' in stem) or ('能一步' in stem) or ('可以实现' in stem): return 'correct'
    return '?'

def decide(stem, opts):
    ask = ask_dir(stem)
    if ask not in ('correct','wrong'): return None, 'ask=?'
    chain = {}
    for L,txt in opts.items():
        e = parse_chain(txt)
        if e is None: return None, f'非链或含方程节点 (opt {L})'
        chain[L]=e
    uncovered = []
    verdicts = {}
    for L,edges in chain.items():
        allf=True; anyi=False
        for e in edges:
            v,info = match_rule(e)
            if v=='unknown':
                uncovered.append(f'{L}: {info}')
            elif v=='infeasible':
                allf=False; anyi=True
        verdicts[L]=(allf,anyi)
    if uncovered:
        return None, '未覆盖步骤: '+ '; '.join(uncovered)
    if ask=='correct':
        cands=[L for L,(af,ai) in verdicts.items() if af]
    else:
        cands=[L for L,(af,ai) in verdicts.items() if ai]
    if len(cands)==1: return cands[0], ''
    return None, f'非唯一 cands={cands}'

# ===================== 测试题 =====================
if __name__ == '__main__':
    PDF = json.load(open(r'D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json', encoding='utf-8'))
    def get(id_):
        for it in PDF:
            if it['id']==id_: return it
        raise KeyError(id_)

    print("=== 现有真题 ===")
    for id_ in ['pdf-013','pdf-015','pdf-016','pdf-017','pdf-144']:
        it = get(id_)
        ans = re.search(r'[A-D]', str(it.get('answer') or ''))
        ans = ans.group(0) if ans else 'NO-KEY'
        d,info = decide(it['question'], it.get('options') or {})
        print(f"{id_:10s} bank={ans:7s} got={str(d):5s} {info}")
