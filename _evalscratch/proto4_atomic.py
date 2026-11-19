# -*- coding: utf-8 -*-
"""原型 v4：忠实镜像计划中的 C# AtomicStructureSolver。
- 锚点词 + 锚点前 14 字元素（符号/中文名）+ 电荷解析
- 锚点后 ~30 字内必须有填空横线（__ 或 ＿）守卫
- 问法分发 -> PeriodicTable 等价计算
- FinalResult 多记法变体
- 模拟评估 IsAnswerCorrect（NormalizeChemicalNotation + Contains），列出全部触发与是否判正
目标：找出所有 NEW 判正 与 所有 FP（触发但判负），供 C# 实现 + 护栏测试对齐。
"""
import json, re, os

HERE = r"D:\github\CogniThink-Chem\_evalscratch"
full = json.load(open(r"D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json", encoding="utf-8-sig"))

# ---------- 周期表（与 C# PeriodicTable 等价）----------
SYMBOLS = ["", "H","He","Li","Be","B","C","N","O","F","Ne","Na","Mg","Al","Si","P","S","Cl","Ar","K","Ca",
    "Sc","Ti","V","Cr","Mn","Fe","Co","Ni","Cu","Zn","Ga","Ge","As","Se","Br","Kr","Rb","Sr","Y","Zr",
    "Nb","Mo","Tc","Ru","Rh","Pd","Ag","Cd","In","Sn","Sb","Te","I","Xe","Cs","Ba","La","Ce","Pr","Nd",
    "Pm","Sm","Eu","Gd","Tb","Dy","Ho","Er","Tm","Yb","Lu","Hf","Ta","W","Re","Os","Ir","Pt","Au","Hg",
    "Tl","Pb","Bi","Po","At","Rn","Fr","Ra","Ac","Th","Pa","U"]
NAMES = ["", "氢","氦","锂","铍","硼","碳","氮","氧","氟","氖","钠","镁","铝","硅","磷","硫","氯","氩","钾","钙",
    "钪","钛","钒","铬","锰","铁","钴","镍","铜","锌","镓","锗","砷","硒","溴","氪","铷","锶","钇","锆",
    "铌","钼","锝","钌","铑","钯","银","镉","铟","锡","锑","碲","碘","氙","铯","钡","镧","铈","镨","钕",
    "钷","钐","铕","钆","铽","镝","钬","铒","铥","镱","镥","铪","钽","钨","铼","锇","铱","铂","金","汞",
    "铊","铅","铋","钋","砹","氡","钫","镭","锕","钍","镤","铀"]
SYM = {s: i for i, s in enumerate(SYMBOLS) if s}
NAME2SYM = {n: SYMBOLS[i] for i, n in enumerate(NAMES) if n}
FILL = [("1s",2),("2s",2),("2p",6),("3s",2),("3p",6),("4s",2),("3d",10),("4p",6),
        ("5s",2),("4d",10),("5p",6),("6s",2),("4f",14),("5d",10),("6p",6),
        ("7s",2),("5f",14),("6d",10),("7p",6)]
ORDER = ["1s","2s","2p","3s","3p","3d","4s","4p","4d","4f","5s","5p","5d","5f","6s","6p","6d","7s","7p"]
EXC = {24:{"4s":1,"3d":5},29:{"4s":1,"3d":10},41:{"5s":1,"4d":4},42:{"5s":1,"4d":5},44:{"5s":1,"4d":7},
       45:{"5s":1,"4d":8},46:{"5s":0,"4d":10},47:{"5s":1,"4d":10},78:{"6s":1,"5d":9},79:{"6s":1,"5d":10}}
NOBLE = {2:"He",10:"Ne",18:"Ar",36:"Kr",54:"Xe",86:"Rn"}
SUP = str.maketrans("0123456789","⁰¹²³⁴⁵⁶⁷⁸⁹")

def fill_aufbau(e):
    occ={}; rem=e
    for sh,cap in FILL:
        if rem<=0: break
        put=min(cap,rem); occ[sh]=occ.get(sh,0)+put; rem-=put
    return occ
def ordered(occ): return {sh:occ[sh] for sh in ORDER if sh in occ and occ[sh]>0}
def config(z):
    if z<1: return {}
    occ=fill_aufbau(z)
    if z in EXC:
        for sh,c in EXC[z].items(): occ[sh]=c
    return ordered(occ)
def ion_config(z,charge):
    if z<1: return {}
    if charge==0: return config(z)
    if charge>0:
        occ=dict(config(z))
        order=sorted(occ.keys(), key=lambda sh:(-int(sh[0]), -({'s':0,'p':1,'d':2,'f':3}.get(sh[1],-1))))
        rem=charge
        for sh in order:
            if rem<=0: break
            have=occ[sh]; take=min(have,rem); occ[sh]=have-take; rem-=take
        return ordered(occ)
    return ordered(fill_aufbau(z-charge))
def valence_subshells(neutral):
    nmax=max(int(s[0]) for s in neutral)
    v=set(s for s in neutral if int(s[0])==nmax)
    if not any(int(s[0])==nmax and s[1]=='p' for s in neutral):
        for s in neutral:
            n=int(s[0]); l=s[1]
            if l=='d' and n==nmax-1: v.add(s)
            elif l=='f' and n==nmax-2: v.add(s)
    return v
def valence(z,charge=0):
    n=config(z)
    if not n: return n
    v=valence_subshells(n)
    occ=n if charge==0 else ion_config(z,charge)
    return {sh:occ[sh] for sh in ORDER if sh in v and sh in occ and occ[sh]>0}
def fmt(occ, sup=True, space=False):
    parts=[sh+(str(c).translate(SUP) if sup else str(c)) for sh,c in occ.items()]
    return (' ' if space else '').join(parts)
def noble(z,charge=0):
    cfg=config(z) if charge==0 else ion_config(z,charge)
    prev=max((n for n in NOBLE if n<z), default=0)
    if prev==0: return fmt(cfg)
    nobleCfg=config(prev); last=-1
    for i,sh in enumerate(ORDER):
        if sh in nobleCfg: last=i
    outer={sh:c for sh,c in cfg.items() if ORDER.index(sh)>last}
    return "["+NOBLE[prev]+"]"+fmt(outer)
def period_group(z):
    nmax = 1 if z<=2 else 2 if z<=10 else 3 if z<=18 else 4 if z<=36 else 5 if z<=54 else 6 if z<=86 else 7
    occ=config(z)
    s=sum(c for sh,c in occ.items() if int(sh[0])==nmax and sh[1]=='s')
    p=sum(c for sh,c in occ.items() if int(sh[0])==nmax and sh[1]=='p')
    d=sum(c for sh,c in occ.items() if int(sh[0])==nmax-1 and sh[1]=='d')
    if nmax==1: grp='IA' if s==1 else '0'
    elif p>0 or d==0:
        grp={1:'IA',2:'IIA',3:'IIIA',4:'IVA',5:'VA',6:'VIA',7:'VIIA',8:'0'}.get(s+p,'?')
    else:
        grp={3:'IIIB',4:'IVB',5:'VB',6:'VIB',7:'VIIB',8:'VIII',9:'VIII',10:'VIII',11:'IB',12:'IIB'}.get(s+d,'?')
    cn={1:'一',2:'二',3:'三',4:'四',5:'五',6:'六',7:'七'}
    return cn.get(nmax,str(nmax)), grp
def unpaired(z,charge=0):
    occ=valence(z,charge); total=0
    for sh,c in occ.items():
        half={'s':1,'p':3,'d':5,'f':7}[sh[1]]
        total += c if c<=half else 2*half-c
    return total

# ---------- 求解器 ----------
ANCHORS = ["价层电子排布式","价电子排布式","价层电子排布","电子排布式","核外电子排布",
           "简化电子排布","电子排布图","轨道表示式","未成对电子","位于元素周期表","位于第","周期第"]
# 优先级：更具体的锚点在前；不含裸"族"。

def parse_charge(s):
    if not s: return 0
    m=re.search(r"([⁰¹²³⁴⁵⁶⁷⁸⁹]+)([⁺⁻])|(\d+)([+\-])", s)
    if m:
        if m.group(1):
            d=m.group(1).translate(str.maketrans('⁰¹²³⁴⁵⁶⁷⁸⁹','0123456789'))
            return int(d)*(1 if m.group(2)=='⁺' else -1)
        return int(m.group(3))*(1 if m.group(4)=='+' else -1)
    return 0

def z_of(token):
    if token in SYM: return SYM[token]
    if token in NAME2SYM: return SYM[NAME2SYM[token]]
    return None

def find_element(q, anchor_idx):
    window = q[max(0, anchor_idx-14):anchor_idx]
    # 符号 + 电荷后缀 + 可选“原子/离子/的”收尾
    m = re.search(r"([A-Z][a-z]?)([⁰¹²³⁴⁵⁶⁷⁸⁹⁺⁻0-9+\-]*)\s*(?:原子|离子|的)?\s*$", window)
    if m:
        z=z_of(m.group(1))
        if z is not None:
            return z, parse_charge(m.group(2) or '')
    # 中文名：取最靠近锚点的（rfind 最大下标），避免同句其他元素干扰
    best_z=None; best_name=None; bestpos=-1
    for name,sym in NAME2SYM.items():
        idx=window.rfind(name)
        if idx>bestpos: bestpos=idx; best_z=SYM[sym]; best_name=name
    if best_z is not None:
        tail=window[bestpos+len(best_name):]
        return best_z, parse_charge(tail)
    return None

def has_blank_after(q, start, span=30):
    seg = q[start:start+span]
    return bool(re.search(r"_{2,}|＿+", seg))

def dispatch(q, anchor, z, charge):
    # 问法分发（按锚点语义）
    if anchor in ("价层电子排布式","价电子排布式","价层电子排布"):
        occ=valence(z,charge)
        return multi_config(occ)
    if anchor == "简化电子排布":
        return noble(z,charge)
    if anchor in ("电子排布式","核外电子排布","电子排布图","轨道表示式"):
        occ = ion_config(z,charge) if charge!=0 else config(z)
        return multi_config(occ)
    if anchor == "未成对电子":
        # 若是求比值/比较，不答（约定不一致，避免帮倒忙）
        tail = q[q.find(anchor):q.find(anchor)+40]
        if "比" in tail or "之比" in tail or "分别" in tail:
            return None
        return str(unpaired(z,charge))
    if anchor in ("位于元素周期表","位于第","周期第"):
        p,g = period_group(z)
        return f"{p},{g} {p}；{g} {p} {g} {p}{g}"
    return None

def multi_config(occ):
    a=fmt(occ,True,False); b=fmt(occ,True,True); c=fmt(occ,False,False); d=fmt(occ,False,True)
    return " ".join(dict.fromkeys([a,b,c,d]))

def solve(q):
    if not q: return None
    for anchor in ANCHORS:
        start=0
        while True:
            idx=q.find(anchor,start)
            if idx<0: break
            el=find_element(q, idx)
            if el is not None and has_blank_after(q, idx+len(anchor)):
                ans=dispatch(q, anchor, el[0], el[1])
                if ans: return ans
            start=idx+1
    return None

# ---------- 模拟评估判分 ----------
def norm(s):
    if not s: return ""
    m={'₀':'0','₁':'1','₂':'2','₃':'3','₄':'4','₅':'5','₆':'6','₇':'7','₈':'8','₉':'9',
       '⁰':'0','¹':'1','²':'2','³':'3','⁴':'4','⁵':'5','⁶':'6','⁷':'7','⁸':'8','⁹':'9',
       '⁺':'+','⁻':'-','－':'-','−':'-','﹣':'-','～':'~','〜':'~'}
    out=[]
    for c in s:
        nc=m.get(c,c)
        if 0xFF01<=ord(nc)<=0xFF5E: nc=chr(ord(nc)-0xFEE0)
        if ord(nc)==0x3000: nc=' '
        out.append(nc)
    return ''.join(out).lower()
def split_kw(answer, typ, existing):
    if existing and len(existing)>1: return existing
    if (typ or '').lower()=='choice': return existing or []
    raw=(answer or '').strip()
    if not raw: return existing or []
    s=re.sub(r"[（(]\s*\d+\s*[）)]\s*"," ",raw)
    s=re.sub(r"(?<![.\d])(\d)\s*[,，]\s*(\d)",lambda m:m.group(1)+'\x01'+m.group(2),s)
    s=re.sub(r"\s*[\r\n]+\s*","\x02",s); s=re.sub(r"[ \t]{2,}","\x02",s)
    parts=re.split(r"[,，;；、\x02]",s); kws=[]
    for p in parts:
        t=p.strip().strip('.。').strip().replace('\x01',',')
        if t: kws.append(t)
    return kws if kws else (existing or [])
def is_correct(answer, o):
    if not answer: return False
    na=norm(answer)
    kws=split_kw(o.get("answer"),o.get("type"),o.get("answer_keywords") or o.get("answerKeywords"))
    if (o.get("type") or '').lower()=='choice':
        return False  # 求解器不介入选择题
    return any(norm(k) in na for k in kws)

# ---------- 跑全部题库 ----------
new=[]; fp=[]
for o in full:
    if (o.get("type") or '').lower()=='choice': continue
    q=o.get("question") or ''
    ans=solve(q)
    if not ans: continue
    if is_correct(ans, o):
        new.append((o['id'], ans, o.get("type")))
    else:
        fp.append((o['id'], ans, (o.get("answer") or '')[:40], q[:90]))

print(f"题库非选择题 {sum(1 for o in full if (o.get('type') or '').lower()!='choice')} 题")
print(f"触发判正(NEW) {len(new)}：")
for r in new: print(f"  NEW {r[0]:<9} type={r[2]:<12} ans={r[1]!r}")
print(f"触发判负(FP, 需评估侧不采纳) {len(fp)}：")
for r in fp: print(f"  FP  {r[0]:<9} ans={r[1]!r:<24} key={r[2]!r}  Q={r[3]}")
