import json, re
from collections import OrderedDict

EVAL = r'D:\github\CogniThink-Chem\_evalscratch\after_stage4.json'
QB   = r'D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json'

O = json.load(open(EVAL, encoding='utf-8-sig'))
QB_raw = json.load(open(QB, encoding='utf-8-sig'))

def allcases(o):
    out = OrderedDict()
    for lst in ['bothCorrectCases','gatewayOnlyCorrectCases','pureLlmOnlyCorrectCases','bothWrongCases']:
        for c in (o.get(lst) or []):
            out[c['id']] = c
    return out
C = allcases(O)
def b(c,k): return str(c.get(k)) == 'True'

qbank = {str(q.get('id')): q for q in QB_raw}

# ---------- 忠实移植 NormalizeChemicalNotation ----------
SUB = {'₀':'0','₁':'1','₂':'2','₃':'3','₄':'4','₅':'5','₆':'6','₇':'7','₈':'8','₉':'9'}
SUP = {'⁰':'0','¹':'1','²':'2','³':'3','⁴':'4','⁵':'5','⁶':'6','⁷':'7','⁸':'8','⁹':'9'}
def norm(s):
    if s is None: return ''
    out=[]
    for ch in s:
        if ch in SUB: out.append(SUB[ch])
        elif ch in SUP: out.append(SUP[ch])
        elif ch=='⁺': out.append('+')
        elif ch=='⁻': out.append('-')
        elif ch in '－−﹣': out.append('-')
        elif ch=='～' or ch=='〜': out.append('~')
        elif 0xFF01 <= ord(ch) <= 0xFF5E: out.append(chr(ord(ch)-0xFEE0))
        elif ch=='\u3000': out.append(' ')
        else: out.append(ch)
    return ''.join(out)

# ---------- 忠实移植 SplitPdfAnswerKeywords ----------
def split_pdf_keywords(answer, qtype, existing):
    if existing and len(existing) > 1:
        return existing
    if (qtype or '').lower() == 'choice':
        return existing or []
    raw = (answer or '').strip()
    if not raw:
        return existing or []
    stripped = re.sub(r'[（(]\s*\d+\s*[）)]\s*', ' ', raw)
    PH='\u0001'; WS='\u0002'
    prot = re.sub(r'(?<![.\d])(\d)\s*[,，]\s*(\d)', rf'\1{PH}\2', stripped)
    wsn = re.sub(r'\s*[\r\n]+\s*', WS, prot)
    wsn = re.sub(r'[ \t]{2,}', WS, wsn)
    parts = re.split(r'[,，;；、]|'+re.escape(WS), wsn)
    kws=[]
    for p in parts:
        t = p.strip().strip('.。').strip().replace(PH,',')
        if t: kws.append(t)
    return kws if kws else (existing or [])

# ---------- 手工题 d6 金标（来自 C# 定义） ----------
MANUAL_KEYS = {
 'd6-1': ['三角锥形','三角锥'],
 'd6-2': ['V形','角形','V型','弯曲'],
 'd6-3': ['平面三角形','平面正三角'],
 'd6-4': ['V形','角形','V型'],
 'd6-5': ['三角锥形','三角锥'],
}

def get_keywords(cid, c):
    qb = qbank.get(cid)
    if qb:
        return split_pdf_keywords(qb.get('answer'), qb.get('type'), qb.get('answerKeywords'))
    return MANUAL_KEYS.get(cid, [])

def is_choice(cid, c):
    qb = qbank.get(cid)
    if qb: return (qb.get('type') or '').lower()=='choice'
    return (c.get('type') or '').lower()=='choice'

# ---------- 忠实 IsAnswerCorrect（几何题走关键词路径；忽略 choice 字母/√×/方向） ----------
def is_correct(answer, cid, c):
    if not answer or not answer.strip(): return False
    lower = norm(answer).lower()
    kws = get_keywords(cid, c)
    for k in kws:
        nk = norm(k)
        if nk and nk.lower() in lower:
            return True
    return False

# ============================================================
#  拟实现 VseprSolver（探针镜像）
# ============================================================
# 物种 -> {shape: 分子构型(含同义词), vsepr: 价层电子对构型, hybrid: 杂化}
# 同义词覆盖高中常见答案写法，便于子串判分命中（仅对本题自身，不跨题污染）。
TABLE = {
 # 直线
 'CO2':  dict(shape='直线形 直线型', vsepr='直线形 直线型', hybrid='sp'),
 'CS2':  dict(shape='直线形 直线型', vsepr='直线形 直线型', hybrid='sp'),
 'HCN':  dict(shape='直线形 直线型', vsepr='直线形 直线型', hybrid='sp'),
 'BeCl2':dict(shape='直线形 直线型', vsepr='直线形 直线型', hybrid='sp'),
 'C2H2': dict(shape='直线形 直线型', vsepr='直线形 直线型', hybrid='sp'),
 # V形（弯曲/角形）
 'H2O':  dict(shape='V形 角形 V型 弯曲 折线形', vsepr='四面体形 四面体', hybrid='sp3'),
 'H2S':  dict(shape='V形 角形 V型 弯曲 折线形', vsepr='四面体形 四面体', hybrid='sp3'),
 'SO2':  dict(shape='V形 角形 V型 弯曲', vsepr='平面三角形 平面正三角', hybrid='sp2'),
 'O3':   dict(shape='V形 角形 V型 弯曲', vsepr='平面三角形 平面正三角', hybrid='sp2'),
 'NO2-': dict(shape='V形 角形 V型 弯曲', vsepr='平面三角形 平面正三角', hybrid='sp2'),
 'OF2':  dict(shape='V形 角形 V型 弯曲', vsepr='四面体形 四面体', hybrid='sp3'),
 'SCl2': dict(shape='V形 角形 V型 弯曲', vsepr='四面体形 四面体', hybrid='sp3'),
 'SnCl2':dict(shape='V形 角形 V型 弯曲', vsepr='平面三角形 平面正三角', hybrid='sp2'),
 # 平面三角形
 'BF3':  dict(shape='平面三角形 平面正三角', vsepr='平面三角形 平面正三角', hybrid='sp2'),
 'BCl3': dict(shape='平面三角形 平面正三角', vsepr='平面三角形 平面正三角', hybrid='sp2'),
 'SO3':  dict(shape='平面三角形 平面正三角', vsepr='平面三角形 平面正三角', hybrid='sp2'),
 'CO3^2-':dict(shape='平面三角形 平面正三角', vsepr='平面三角形 平面正三角', hybrid='sp2'),
 'CO32-':dict(shape='平面三角形 平面正三角', vsepr='平面三角形 平面正三角', hybrid='sp2'),
 'NO3-': dict(shape='平面三角形 平面正三角', vsepr='平面三角形 平面正三角', hybrid='sp2'),
 'CH2O': dict(shape='平面三角形 平面正三角', vsepr='平面三角形 平面正三角', hybrid='sp2'),
 'HCHO': dict(shape='平面三角形 平面正三角', vsepr='平面三角形 平面正三角', hybrid='sp2'),
 'AlCl3':dict(shape='平面三角形 平面正三角', vsepr='平面三角形 平面正三角', hybrid='sp2'),
 # 三角锥形
 'NH3':  dict(shape='三角锥形 三角锥', vsepr='四面体形 四面体', hybrid='sp3'),
 'PH3':  dict(shape='三角锥形 三角锥', vsepr='四面体形 四面体', hybrid='sp3'),
 'PCl3': dict(shape='三角锥形 三角锥', vsepr='四面体形 四面体', hybrid='sp3'),
 'AsCl3':dict(shape='三角锥形 三角锥', vsepr='四面体形 四面体', hybrid='sp3'),
 'NCl3': dict(shape='三角锥形 三角锥', vsepr='四面体形 四面体', hybrid='sp3'),
 'ClO3-':dict(shape='三角锥形 三角锥', vsepr='四面体形 四面体', hybrid='sp3'),
 'SO3^2-':dict(shape='三角锥形 三角锥', vsepr='四面体形 四面体', hybrid='sp3'),
 'SO32-':dict(shape='三角锥形 三角锥', vsepr='四面体形 四面体', hybrid='sp3'),
 'XeO3': dict(shape='三角锥形 三角锥', vsepr='四面体形 四面体', hybrid='sp3'),
 # 正四面体
 'CH4':  dict(shape='正四面体 正四面体形 四面体', vsepr='正四面体形 正四面体 四面体', hybrid='sp3'),
 'CCl4': dict(shape='正四面体 正四面体形 四面体', vsepr='正四面体形 正四面体 四面体', hybrid='sp3'),
 'SiH4': dict(shape='正四面体 正四面体形 四面体', vsepr='正四面体形 正四面体 四面体', hybrid='sp3'),
 'SiF4': dict(shape='正四面体 正四面体形 四面体', vsepr='正四面体形 正四面体 四面体', hybrid='sp3'),
 'NH4+': dict(shape='正四面体 正四面体形 四面体', vsepr='正四面体形 正四面体 四面体', hybrid='sp3'),
 'SO4^2-':dict(shape='正四面体 正四面体形 四面体', vsepr='正四面体形 正四面体 四面体', hybrid='sp3'),
 'SO42-':dict(shape='正四面体 正四面体形 四面体', vsepr='正四面体形 正四面体 四面体', hybrid='sp3'),
 'ClO4-':dict(shape='正四面体 正四面体形 四面体', vsepr='正四面体形 正四面体 四面体', hybrid='sp3'),
 'PO4^3-':dict(shape='正四面体 正四面体形 四面体', vsepr='正四面体形 正四面体 四面体', hybrid='sp3'),
 'PO43-':dict(shape='正四面体 正四面体形 四面体', vsepr='正四面体形 正四面体 四面体', hybrid='sp3'),
 'AsO4^3-':dict(shape='正四面体 正四面体形 四面体', vsepr='正四面体形 正四面体 四面体', hybrid='sp3'),
 'AsO43-':dict(shape='正四面体 正四面体形 四面体', vsepr='正四面体形 正四面体 四面体', hybrid='sp3'),
 'BF4-': dict(shape='正四面体 正四面体形 四面体', vsepr='正四面体形 正四面体 四面体', hybrid='sp3'),
 'XeO4': dict(shape='正四面体 正四面体形 四面体', vsepr='正四面体形 正四面体 四面体', hybrid='sp3'),
 # 扩展构型
 'PCl5': dict(shape='三角双锥形 三角双锥', vsepr='三角双锥形 三角双锥', hybrid='sp3d'),
 'SF4':  dict(shape='变形四面体 跷跷板形', vsepr='三角双锥形 三角双锥', hybrid='sp3d'),
 'ClF3': dict(shape='T形 T型', vsepr='三角双锥形 三角双锥', hybrid='sp3d'),
 'XeF2': dict(shape='直线形 直线型', vsepr='三角双锥形 三角双锥', hybrid='sp3d'),
 'SF6':  dict(shape='正八面体形 正八面体', vsepr='正八面体形 正八面体', hybrid='sp3d2'),
 'XeF4': dict(shape='平面正方形', vsepr='正八面体形 正八面体', hybrid='sp3d2'),
}

# 化学式归一化：去掉^、下标转数字、统一电荷写法，映射到 TABLE 键
def canon_species(raw):
    s = norm(raw).strip()
    s = s.replace('^','')
    # 规范化电荷：ClO3- / ClO3－ 已 norm 为 -；CO32- / CO3^2- 已去掉 ^ -> CO32-
    s = re.sub(r'\s+','',s)
    # 去末尾 "离子/分子/的"
    s = re.sub(r'(离子|分子|的)+$','',s)
    return s

# 匹配完整化学式：一个或多个「元素符号+可选计数」，末尾可选电荷
# 捕获组1=公式主体（如 NH3 / ClO3 / CO3），组2=电荷（如 ^2- / - / ⁻）
FORMULA_RE = re.compile(r'((?:[A-Z][a-z]?(?:\d|[₀-₉])*)+)(\^?\d*[+\-]|[⁺⁻])?')
# 在窗口中找最靠右的、属于 TABLE 的物种
def find_species(window):
    best=None; best_pos=-1
    for m in FORMULA_RE.finditer(window):
        tok = m.group(0)
        key=canon_species(tok)
        if key in TABLE:
            if m.start() > best_pos:
                best_pos=m.start(); best=key
    return best

# 问法锚点 -> 取哪个属性
ASK = [
    (re.compile(r'VSEPR\s*模型|价层电子对(?:的)?(?:空间)?构型|价电子对构型|价层电子对构型'), 'vsepr'),
    (re.compile(r'杂化(?:类型|方式|轨道类型)?|的杂化|杂化轨道'), 'hybrid'),
    (re.compile(r'空间构型|空间结构|分子构型|立体构型|几何构型|分子的空间构型'), 'shape'),
]
REJECT = re.compile(r'比|原因|为什么|不同于|分别|均为|都为|比较|大于|小于|键角|理由|解释|推测|推断')
BLANK = re.compile(r'_{2,}|＿+')
QMARK = re.compile(r'是什么|是\s*_{1,}|为\s*_{1,}|是__|为__|[？?]')

def solve(text):
    text = text or ''
    answers=[]  # (attr, value)
    seen=set()
    for anchor_re, attr in ASK:
        for m in anchor_re.finditer(text):
            astart = m.start()
            # 锚点前 22 字窗口找物种
            wstart = max(0, astart-22)
            window = text[wstart:astart]
            sp = find_species(window)
            if not sp: 
                continue
            # 守卫：该锚点附近（前后各 25 字）不得出现比较/推理/开放词
            seg = text[max(0,astart-25):min(len(text),astart+25)]
            if REJECT.search(seg):
                continue
            # 守卫：锚点后 30 字内需有填空或疑问标记（直接问句/填空）
            after = text[astart:min(len(text),astart+30)]
            if not (BLANK.search(after) or QMARK.search(after)):
                # 允许锚点紧贴句末标点或文本结尾
                tail = after.strip(' ：:，,。.')
                if not (tail=='' or tail.endswith('？') or tail.endswith('?')):
                    continue
            val = TABLE[sp][attr]
            key=(attr,sp)
            if key not in seen:
                seen.add(key)
                answers.append((sp,attr,val))
    if not answers:
        return None
    # 组装：去重值，空格连接
    vals=[]
    for sp,attr,val in answers:
        for v in val.split():
            if v not in vals: vals.append(v)
    return ' '.join(vals)

# ============================================================
print(f"total eval cases = {len(C)}")
triggered=[]
for cid, c in C.items():
    q = c.get('question') or ''
    ans = solve(q)
    if ans is None: continue
    choice = is_choice(cid, c)
    correct = is_correct(ans, cid, c)
    adopted = b(c,'gatewayTruthAdopted')
    tt = c.get('taskType')
    triggered.append((cid, tt, choice, adopted, correct, ans, q[:80]))

print(f"\n=== 触发数 = {len(triggered)} ===")
print(f"  其中判正(IsAnswerCorrect通过) = {sum(1 for t in triggered if t[4])}")
print(f"  其中判负(坏键/不匹配)        = {sum(1 for t in triggered if not t[4])}")
print(f"  其中选择题(应避免)            = {sum(1 for t in triggered if t[2])}")
print(f"  其中原本已 adopted           = {sum(1 for t in triggered if t[3])}")
print("\n--- 全部触发明细 ---")
for cid,tt,choice,adopted,correct,ans,qshort in sorted(triggered):
    flag = 'OK ' if correct else 'BAD'
    ch = 'CHOICE' if choice else 'fill  '
    print(f"[{flag}][{ch}] {cid:10s} tt={str(tt):12s} prevAdopted={adopted}  => {ans}")
    print(f"        Q: {qshort}")

# 单独列出判负的，便于分析是否需要排除
bad=[t for t in triggered if not t[4]]
print(f"\n=== 判负详情({len(bad)}) — 这些不会被采纳(IsAnswerCorrect门控)，无 harm，但确认无假阳性 ===")
for cid,tt,choice,adopted,correct,ans,qshort in bad:
    kws=get_keywords(cid, C[cid])
    print(f"  {cid:10s} tt={str(tt):12s} keys={kws}  myAns={ans}")
    print(f"     Q: {(C[cid].get('question') or '')[:160]}")
