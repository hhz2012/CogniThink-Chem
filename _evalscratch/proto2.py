# -*- coding: utf-8 -*-
"""原型 v2：改进抽取 + 多记法输出 + VSEPR。目标：零假阳性，最大化干净新解。"""
import json, re, os
HERE=r"D:\github\CogniThink-Chem\_evalscratch"
uns=json.load(open(os.path.join(HERE,"stage3c_unsolved_pdf.json"),encoding="utf-8-sig"))
full=json.load(open(r"D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json",encoding="utf-8-sig"))
uns_ids_nc=set(o["id"] for o in uns if not o.get("chart"))  # 未解出且去图表

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
def kws_of(o): return split_kw(o.get("answer"),o.get("type"),o.get("answer_keywords") or o.get("answerKeywords"))
def matches(answer,kws):
    na=norm(answer)
    return any(norm(k) in na for k in kws)  # 关键词是答案子串（与评估一致：答案含关键词）

# ---------- 周期表 ----------
PT={1:'H',2:'He',3:'Li',4:'Be',5:'B',6:'C',7:'N',8:'O',9:'F',10:'Ne',11:'Na',12:'Mg',13:'Al',14:'Si',15:'P',16:'S',17:'Cl',18:'Ar',19:'K',20:'Ca',21:'Sc',22:'Ti',23:'V',24:'Cr',25:'Mn',26:'Fe',27:'Co',28:'Ni',29:'Cu',30:'Zn',31:'Ga',32:'Ge',33:'As',34:'Se',35:'Br',36:'Kr',37:'Rb',38:'Sr',39:'Y',40:'Zr',41:'Nb',42:'Mo',43:'Tc',44:'Ru',45:'Rh',46:'Pd',47:'Ag',48:'Cd',49:'In',50:'Sn',51:'Sb',52:'Te',53:'I',54:'Xe',55:'Cs',56:'Ba',57:'La',58:'Ce',59:'Pr',60:'Nd',61:'Pm',62:'Sm',63:'Eu',64:'Gd',65:'Tb',66:'Dy',67:'Ho',68:'Er',69:'Tm',70:'Yb',71:'Lu',72:'Hf',73:'Ta',74:'W',75:'Re',76:'Os',77:'Ir',78:'Pt',79:'Au',80:'Hg',81:'Tl',82:'Pb',83:'Bi',84:'Po',85:'At',86:'Rn',87:'Fr',88:'Ra',89:'Ac',90:'Th',91:'Pa',92:'U'}
SYM={v:k for k,v in PT.items()}
CN={'H':'氢','He':'氦','Li':'锂','Be':'铍','B':'硼','C':'碳','N':'氮','O':'氧','F':'氟','Ne':'氖','Na':'钠','Mg':'镁','Al':'铝','Si':'硅','P':'磷','S':'硫','Cl':'氯','Ar':'氩','K':'钾','Ca':'钙','Sc':'钪','Ti':'钛','V':'钒','Cr':'铬','Mn':'锰','Fe':'铁','Co':'钴','Ni':'镍','Cu':'铜','Zn':'锌','Ga':'镓','Ge':'锗','As':'砷','Se':'硒','Br':'溴','Kr':'氪','Rb':'铷','Sr':'锶','Y':'钇','Zr':'锆','Nb':'铌','Mo':'钼','Tc':'锝','Ru':'钌','Rh':'铑','Pd':'钯','Ag':'银','Cd':'镉','In':'铟','Sn':'锡','Sb':'锑','Te':'碲','I':'碘','Xe':'氙','Cs':'铯','Ba':'钡','La':'镧','Ce':'铈','Pr':'镨','Nd':'钕','Pm':'钷','Sm':'钐','Eu':'铕','Gd':'钆','Tb':'铽','Dy':'镝','Ho':'钬','Er':'铒','Tm':'铥','Yb':'镱','Lu':'镥','Hf':'铪','Ta':'钽','W':'钨','Re':'铼','Os':'锇','Ir':'铱','Pt':'铂','Au':'金','Hg':'汞','Tl':'铊','Pb':'铅','Bi':'铋','Po':'钋','At':'砹','Rn':'氡','Fr':'钫','Ra':'镭','Ac':'锕','Th':'钍','Pa':'镤','U':'铀'}
SUBSHELLS=[('1s',2),('2s',2),('2p',6),('3s',2),('3p',6),('4s',2),('3d',10),('4p',6),('5s',2),('4d',10),('5p',6),('6s',2),('4f',14),('5d',10),('6p',6),('7s',2),('5f',14),('6d',10),('7p',6)]
EXC={24:{'4s':1,'3d':5},29:{'4s':1,'3d':10},41:{'5s':1,'4d':4},42:{'5s':1,'4d':5},44:{'5s':1,'4d':7},45:{'5s':1,'4d':8},46:{'5s':0,'4d':10},47:{'5s':1,'4d':10},78:{'6s':1,'5d':9},79:{'6s':1,'5d':10}}
ORDER=['1s','2s','2p','3s','3p','3d','4s','4p','4d','4f','5s','5p','5d','5f','6s','6p','6d','7s','7p']
SUP=str.maketrans('0123456789','⁰¹²³⁴⁵⁶⁷⁸⁹')
def config(z):
    occ={}; rem=z
    for sh,cap in SUBSHELLS:
        if rem<=0: break
        put=min(cap,rem); occ[sh]=occ.get(sh,0)+put; rem-=put
    if z in EXC:
        for sh,c in EXC[z].items(): occ[sh]=c
    return {sh:occ[sh] for sh in ORDER if sh in occ}
def valence(z):
    occ=config(z); nmax=max(int(s[0]) for s in occ)
    v={}
    for sh,c in occ.items():
        n=int(sh[0]); l=sh[1]
        if n==nmax: v[sh]=c
        elif l=='d' and n==nmax-1: v[sh]=c
        elif l=='f' and n==nmax-2: v[sh]=c
    return v
NOBLE={2:'He',10:'Ne',18:'Ar',36:'Kr',54:'Xe',86:'Rn'}
def noble_short(z):
    prev=max([n for n in NOBLE if n<z],default=0)
    occ=config(z); rest={sh:c for sh,c in occ.items() if int(sh[0])>(1 if prev==2 else int(list(config(prev).keys())[-1][0])-0) }
    # 简单：主量子数 > 稀有气体周期
    n_noble=0
    for nzz in sorted(NOBLE):
        if nzz==prev: n_noble=max(int(s[0]) for s in config(nzz))
    rest={sh:c for sh,c in occ.items() if int(sh[0])>n_noble}
    return f"[{NOBLE[prev]}]"+fmt(rest,False)
def fmt(occ, sup=True, space=False):
    parts=[]
    for sh,c in occ.items():
        parts.append(sh+(str(c).translate(SUP) if sup else str(c)))
    return (' ' if space else '').join(parts)
def period_group(z):
    occ=config(z); nmax=max(int(s[0]) for s in occ)
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

def find_element(q):
    """找被询问的元素：优先紧邻问法的符号/中文名。返回 (sym, charge) 或 None。"""
    # 问法锚点
    anchor = None
    for a in ["价层电子排布式","价电子排布式","价层电子排布","电子排布式","核外电子排布","简化电子排布","电子排布图","轨道表示式","未成对电子","位于元素周期表","位于第","周期第"]:
        i=q.find(a)
        if i>=0: anchor=i; break
    if anchor is None: return None
    # 在锚点前 14 字内找元素符号或中文名
    window=q[max(0,anchor-14):anchor]
    # 符号 + 电荷（可选 原子/离子/的 收尾）
    m=re.search(r"([A-Z][a-z]?)([⁰¹²³⁴⁵⁶⁷⁸⁹⁺⁻0-9+\-]*)\s*(?:原子|离子|的)?\s*$", window)
    if m and m.group(1) in SYM:
        sym=m.group(1); ch=m.group(2) or ''
        charge=parse_charge(ch)
        return sym,charge
    # 中文名：取最靠近锚点（rfind 最大）的那个，避免同句其他元素干扰
    best=None; bestpos=-1
    for sym,name in CN.items():
        idx=window.rfind(name)
        if idx>bestpos: bestpos=idx; best=sym
    if best is not None:
        name=CN[best]; idx=window.rfind(name); tail=window[idx+len(name):]
        return best, parse_charge(tail)
    return None

def parse_charge(s):
    if not s: return 0
    m=re.search(r"([⁰¹²³⁴⁵⁶⁷⁸⁹]+)([⁺⁻])|(\d+)([+\-])", s)
    if m:
        if m.group(1):
            d=m.group(1).translate(str.maketrans('⁰¹²³⁴⁵⁶⁷⁸⁹','0123456789'))
            return int(d)*(1 if m.group(2)=='⁺' else -1)
        return int(m.group(3))*(1 if m.group(4)=='+' else -1)
    return 0

def atomic_answer(q):
    el=find_element(q)
    if not el: return None
    sym,charge=el; z=SYM[sym]-charge
    if '价层电子' in q or '价电子排布' in q:
        occ=valence(z); return fmt(occ,True)+' '+fmt(occ,True,space=True)  # 两种记法
    if '简化电子排布' in q:
        return noble_short(z)
    if '电子排布' in q or '核外电子' in q or '轨道表示' in q:
        occ=config(z); return fmt(occ,True)+' '+fmt(occ,True,space=True)
    if '未成对电子' in q:
        occ=valence(z); u=sum(unpaired(sh,c) for sh,c in occ.items()); return str(u)
    # 自旋比：约定不一致（答案键可能与化学真值不同），保守不做，避免帮倒忙
    if ('周期' in q and '族' in q and '第' in q) or '位于' in q:
        p,g=period_group(SYM[sym]); return f"{p},{g} {p} {g}"
    return None

def unpaired(sh,c):
    l=sh[1]
    if l=='s': return 1 if c==1 else 0
    half={'p':3,'d':5,'f':7}[l]
    return c if c<=half else c-half
def spin(occ):
    up=dn=0
    for sh,c in occ.items():
        half={'s':1,'p':3,'d':5,'f':7}[sh[1]]
        if c<=half: up+=c
        else: up+=half; dn+=c-half
    return up,dn

# ---------- VSEPR（中心原子杂化/构型）----------
def vsepr_answer(q):
    # 找 "XCl2 / SOCl2 / NH3 ... 的 VSEPR/杂化/构型/空间结构"
    m=re.search(r"([A-Z][a-z]?)([A-Z][a-z]?\d*)(?:分子|离子)?\s*的\s*(?:VSEPR|杂化|空间结构|空间构型|构型|分子结构)", q)
    if not m: return None
    central=m.group(1); ligand=m.group(2)
    if central not in SYM: return None
    # 配体数：ligand 末尾数字
    lm=re.search(r"(\d+)$", ligand); n=int(lm.group(1)) if lm else 1
    zc=SYM[central]
    # 价电子数（中性原子）
    ve=valence_e(zc)
    # 配体提供：卤素/H 提供1，O/S 提供0（双键），N 提供-1（简化高中规则）
    donor=1
    last=ligand.rstrip('0123456789')
    if last in ('O','S'): donor=0
    elif last in ('N',): donor=-1
    steric=(ve + n*donor)//2
    lone=steric-n
    hyb={2:'sp',3:'sp2',4:'sp3',5:'sp3d',6:'sp3d2'}.get(steric)
    if not hyb: return None
    eg={2:'直线形',3:'平面三角形',4:'四面体形',5:'三角双锥形',6:'八面体形'}.get(steric)
    # 分子构型
    mg=mol_geom(steric,lone)
    parts=[]
    if '杂化' in q: parts.append(hyb)
    if 'VSEPR' in q: parts.append(eg or '')
    if '空间结构' in q or '空间构型' in q or '构型' in q or '分子结构' in q: parts.append(mg or '')
    return ' '.join(p for p in parts if p)

def valence_e(z):
    occ=valence(z); return sum(occ.values())
def mol_geom(steric,lone):
    table={(2,0):'直线形',(3,0):'平面三角形',(3,1):'V形',(4,0):'正四面体形',(4,1):'三角锥形',(4,2):'V形',(5,0):'三角双锥形',(5,1):'变形四面体形',(5,2):'T形',(5,3):'直线形',(6,0):'八面体形',(6,1):'四方锥形',(6,2):'平面正方形'}
    return table.get((steric,lone))

# ---------- 评估两求解器 ----------
def run(solver):
    new=[]; fp=[]
    for o in full:
        if (o.get('type') or '')=='choice': continue
        q=o.get('question') or ''; kws=kws_of(o)
        if not kws: continue
        ans=solver(q)
        if not ans: continue
        if matches(ans,kws):
            if o['id'] in uns_ids_nc: new.append((o['id'],ans,kws[:3]))
        else:
            fp.append((o['id'],ans,kws[:3],q[:80]))
    return new,fp

for name,solver in [('原子结构',atomic_answer),('VSEPR',vsepr_answer)]:
    new,fp=run(solver)
    print(f"\n===== {name}：干净新解 {len(new)}，假阳性 {len(fp)} =====")
    for r in new: print(f"  NEW {r[0]:<9} ans={r[1]!r:<28} kws={r[2]}")
    if fp:
        print("  -- 假阳性(需守卫) --")
        for r in fp[:15]: print(f"  FP  {r[0]:<9} ans={r[1]!r:<22} kws={r[2]}  Q={r[3]}")
