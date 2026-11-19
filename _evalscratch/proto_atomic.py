# -*- coding: utf-8 -*-
"""原型验证：原子结构/周期表 + VSEPR 求解器对 PDF 题库的真实覆盖与零假阳性。
对每个非选择 PDF 题，模拟计算答案，用与 IsAnswerCorrect 一致的归一化做关键词命中。
输出：可新解题(当前未解出) + 假阳性(必须加守卫返回null)。"""
import json, re, os
HERE=r"D:\github\CogniThink-Chem\_evalscratch"
data=json.load(open(os.path.join(HERE,"stage3c_unsolved_pdf.json"),encoding="utf-8-sig"))
# 也读全部题库以覆盖当前未解出之外的新题（含已解出？只需找未解出的新增）
full=json.load(open(r"D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json",encoding="utf-8-sig"))
by_id={o["id"]:o for o in full}

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
    s=re.sub(r"\s*[\r\n]+\s*","\x02",s)
    s=re.sub(r"[ \t]{2,}","\x02",s)
    parts=re.split(r"[,，;；、\x02]",s)
    kws=[]
    for p in parts:
        t=p.strip().strip('.。').strip().replace('\x01',',')
        if t: kws.append(t)
    return kws if kws else (existing or [])

def keywords_of(o):
    return split_kw(o.get("answer"), o.get("type"), o.get("answer_keywords") or o.get("answerKeywords"))

# ---------- 周期表 ----------
# Z->symbol, symbol->Z, name
PT = {
1:'H',2:'He',3:'Li',4:'Be',5:'B',6:'C',7:'N',8:'O',9:'F',10:'Ne',
11:'Na',12:'Mg',13:'Al',14:'Si',15:'P',16:'S',17:'Cl',18:'Ar',19:'K',20:'Ca',
21:'Sc',22:'Ti',23:'V',24:'Cr',25:'Mn',26:'Fe',27:'Co',28:'Ni',29:'Cu',30:'Zn',
31:'Ga',32:'Ge',33:'As',34:'Se',35:'Br',36:'Kr',37:'Rb',38:'Sr',39:'Y',40:'Zr',
41:'Nb',42:'Mo',43:'Tc',44:'Ru',45:'Rh',46:'Pd',47:'Ag',48:'Cd',49:'In',50:'Sn',
51:'Sb',52:'Te',53:'I',54:'Xe',55:'Cs',56:'Ba',57:'La',58:'Ce',59:'Pr',60:'Nd',
61:'Pm',62:'Sm',63:'Eu',64:'Gd',65:'Tb',66:'Dy',67:'Ho',68:'Er',69:'Tm',70:'Yb',
71:'Lu',72:'Hf',73:'Ta',74:'W',75:'Re',76:'Os',77:'Ir',78:'Pt',79:'Au',80:'Hg',
81:'Tl',82:'Pb',83:'Bi',84:'Po',85:'At',86:'Rn',87:'Fr',88:'Ra',89:'Ac',90:'Th',
91:'Pa',92:'U'}
SYM={v:k for k,v in PT.items()}
CN={'H':'氢','He':'氦','Li':'锂','Be':'铍','B':'硼','C':'碳','N':'氮','O':'氧','F':'氟','Ne':'氖',
'Na':'钠','Mg':'镁','Al':'铝','Si':'硅','P':'磷','S':'硫','Cl':'氯','Ar':'氩','K':'钾','Ca':'钙',
'Sc':'钪','Ti':'钛','V':'钒','Cr':'铬','Mn':'锰','Fe':'铁','Co':'钴','Ni':'镍','Cu':'铜','Zn':'锌',
'Ga':'镓','Ge':'锗','As':'砷','Se':'硒','Br':'溴','Kr':'氪','Rb':'铷','Sr':'锶','Y':'钇','Zr':'锆',
'Nb':'铌','Mo':'钼','Tc':'锝','Ru':'钌','Rh':'铑','Pd':'钯','Ag':'银','Cd':'镉','In':'铟','Sn':'锡',
'Sb':'锑','Te':'碲','I':'碘','Xe':'氙','Cs':'铯','Ba':'钡','La':'镧','Ce':'铈','Pr':'镨','Nd':'钕',
'Pm':'钷','Sm':'钐','Eu':'铕','Gd':'钆','Tb':'铽','Dy':'镝','Ho':'钬','Er':'铒','Tm':'铥','Yb':'镱',
'Lu':'镥','Hf':'铪','Ta':'钽','W':'钨','Re':'铼','Os':'锇','Ir':'铱','Pt':'铂','Au':'金','Hg':'汞',
'Tl':'铊','Pb':'铅','Bi':'铋','Po':'钋','At':'砹','Rn':'氡','Fr':'钫','Ra':'镭','Ac':'锕','Th':'钍',
'Pa':'镤','U':'铀'}

# 构造 Z->电子排布（基态，含常见例外）。用 aufbau + 例外覆盖
# 亚层顺序与容量
SUBSHELLS=[('1s',2),('2s',2),('2p',6),('3s',2),('3p',6),('4s',2),('3d',10),('4p',6),
('5s',2),('4d',10),('5p',6),('6s',2),('4f',14),('5d',10),('6p',6),('7s',2),('5f',14),('6d',10),('7p',6)]
# 例外：Z -> {subshell: count} 覆盖（相对aufbau微调）。只覆盖前4周期常考 + 几个重点
EXC={
 24:{'4s':1,'3d':5},  # Cr
 29:{'4s':1,'3d':10}, # Cu
 41:{'5s':1,'4d':4},  # Nb
 42:{'5s':1,'4d':5},  # Mo
 44:{'5s':1,'4d':7},  # Ru
 45:{'5s':1,'4d':8},  # Rh
 46:{'5s':0,'4d':10}, # Pd
 47:{'5s':1,'4d':10}, # Ag
 78:{'6s':1,'5d':9},  # Pt
 79:{'6s':1,'5d':10}, # Au
 26:{'4s':2,'3d':6},  # Fe（aufbau 正常，显式确认）
}
def config(z):
    occ={}
    rem=z
    for sh,cap in SUBSHELLS:
        if rem<=0: break
        put=min(cap,rem)
        occ[sh]=occ.get(sh,0)+put
        rem-=put
    if z in EXC:
        for sh,c in EXC[z].items():
            occ[sh]=c
    # 按主量子数重排输出
    order=['1s','2s','2p','3s','3p','3d','4s','4p','4d','4f','5s','5p','5d','5f','6s','6p','6d','7s','7p']
    return {sh:occ.get(sh,0) for sh in order if occ.get(sh,0)>0}
SUP=str.maketrans('0123456789','⁰¹²³⁴⁵⁶⁷⁸⁹')
def fmt_cfg(occ, sup=True):
    parts=[]
    for sh,c in occ.items():
        parts.append(sh+(str(c).translate(SUP) if sup else str(c)))
    return ''.join(parts)
NOBLE={2:'He',10:'Ne',18:'Ar',36:'Kr',54:'Xe',86:'Rn'}
def noble_core(z):
    # 返回 [noble] + 外层
    prev=0
    for nz in sorted(NOBLE):
        if nz<z: prev=nz
        else: break
    return prev
def valence_cfg(z):
    """价层电子排布：主族=最外nsnp；过渡=(n-1)d+ns。返回 (occ_valence, label)"""
    occ=config(z)
    # 主量子数最大值
    nmax=max(int(sh[0]) for sh in occ)
    val={}
    for sh,c in occ.items():
        n=int(sh[0])
        l=sh[1]
        if n==nmax: val[sh]=c
        elif l=='d' and n==nmax-1: val[sh]=c  # 过渡：(n-1)d
        elif l=='f' and n==nmax-2: val[sh]=c
    return val
def period_group(z):
    occ=config(z); nmax=max(int(sh[0]) for sh in occ)
    period=nmax
    # 族：主族 = ns+np 电子数；过渡看 (n-1)d + ns
    s=sum(c for sh,c in occ.items() if int(sh[0])==nmax and sh[1]=='s')
    p=sum(c for sh,c in occ.items() if int(sh[0])==nmax and sh[1]=='p')
    d=sum(c for sh,c in occ.items() if int(sh[0])==nmax-1 and sh[1]=='d')
    if nmax==1:
        grp = 'IA' if (s==1) else '0'  # He
    elif p>0 or d==0:
        g=s+p
        grp={1:'IA',2:'IIA',3:'IIIA',4:'IVA',5:'VA',6:'VIA',7:'VIIA',8:'0'}.get(g,'?')
    else:
        g=s+d
        grp={3:'IIIB',4:'IVB',5:'VB',6:'VIB',7:'VIIB',8:'VIII',9:'VIII',10:'VIII',11:'IB',12:'IIB'}.get(g,'?')
    return period, grp

def extract_element(q):
    """从题干提取目标元素符号（中文或符号）与电荷。返回 (symbol, charge) or None"""
    # 模式："基态<X>原子/离子"、"<X>位于"、"<X>的价层电子"、"基态<X>²⁺"、"<X>²⁺的"
    # 优先找形如 基态(Be)(?:²⁺|2\+)? 的
    pat=re.compile(r"基态\s*([A-Z][a-z]?)([⁰¹²³⁴⁵⁶⁷⁸⁹⁺⁻0-9+\-]*)\s*(?:原子|离子|价层|核外|的)?")
    m=pat.search(q)
    charge=0
    if m:
        sym=m.group(1); chstr=m.group(2) or ''
        # 解析电荷：如 ²⁺ -> +2 ; 3+ -> +3 ; ^2+ -> +2
        mm=re.search(r"([⁰¹²³⁴⁵⁶⁷⁸⁹]+)([⁺⁻])|(\d+)([+\-])", chstr)
        if mm:
            if mm.group(1):
                dig=mm.group(1).translate(str.maketrans('⁰¹²³⁴⁵⁶⁷⁸⁹','0123456789'))
                charge=int(dig)*(1 if mm.group(2)=='⁺' else -1)
            else:
                charge=int(mm.group(3))*(1 if mm.group(4)=='+' else -1)
        if sym in SYM: return sym,charge
    # 找 <sym>位于 / <sym>的价层
    m2=re.search(r"\b([A-Z][a-z]?)(?:位于|的价层|原子的)",q)
    if m2 and m2.group(1) in SYM: return m2.group(1),0
    # 中文元素名
    for sym,name in CN.items():
        if name in q and ("价层" in q or "电子排布" in q or "位于" in q or "基态" in q):
            # 简单：若题干含该中文名且问电子/周期
            if f"{name}" in q:
                return sym,0
    return None

def try_atomic(q):
    """返回 (answer_string, ask_type) 或 None"""
    el=extract_element(q)
    if not el: return None
    sym,charge=el
    z=SYM[sym]
    # 问法判断
    low=q
    if re.search(r"价层电子排布式|价电子排布式|价层电子排布", q):
        occ=valence_cfg(z-charge if charge else z)
        if charge>0:
            occ=valence_cfg(z-charge)
        return fmt_cfg(occ, sup=True),'valence_cfg'
    if re.search(r"电子排布式|核外电子排布|简化电子排布", q) and '价层' not in q:
        occ=config(z-charge if charge else z)
        return fmt_cfg(occ),'full_cfg'
    if re.search(r"第几周期|第.*周期|周期第", q) and re.search(r"第.*族|族", q):
        p,g=period_group(z)
        return f"{num_cn(p)},{g}",'period_group'
    if re.search(r"未成对电子", q):
        occ=valence_cfg(z)
        unpaired=sum(unpaired_in_subshell(sh,c) for sh,c in occ.items())
        return str(unpaired),'unpaired'
    if re.search(r"两种自旋|自旋.*比|自旋状态", q):
        occ=config(z)
        up,down=spin_counts(occ)
        # 简化电子：价层两种自旋比（pdf-485 S -> 1:1）
        occv=valence_cfg(z)
        up,down=spin_counts(occv)
        return f"{up}:{down}",'spin_ratio'
    return None

def num_cn(n):
    return {1:'一',2:'二',3:'三',4:'四',5:'五',6:'六',7:'七'}.get(n,str(n))

def unpaired_in_subshell(sh,c):
    if sh[1]=='s': return 1 if c==1 else 0
    if sh[1]=='p':
        if c<=3: return c
        return c-3  # 4->1,5->2,6->0
    if sh[1]=='d':
        if c<=5: return c
        return c-5
    if sh[1]=='f':
        if c<=7: return c
        return c-7
    return 0
def spin_counts(occ):
    up=down=0
    for sh,c in occ.items():
        cap={'s':2,'p':6,'d':10,'f':14}[sh[1]]
        half=cap//2
        if c<=half: up+=c
        else: up+=half; down+=c-half
    return up,down

# ---------- 主流程 ----------
hit_new=[]; fp=[]; solved_now=set()
# 当前未解出集合
unsolved_ids=set(o["id"] for o in data if not o.get("chart"))
for o in full:
    qid=o["id"]; typ=o.get("type") or ""
    if typ=="choice": continue
    q=o.get("question") or ""
    kws=keywords_of(o)
    if not kws: continue
    ans=try_atomic(q)
    if not ans: continue
    answer,_=ans
    nk=norm(answer)
    matched=any(nk in norm(k) or norm(k) in nk for k in kws)
    row=(qid,answer,[k for k in kws])
    if matched:
        if qid in unsolved_ids: hit_new.append(row)
        else: solved_now.add(qid)
    else:
        fp.append(row)

print(f"原子结构原型：可新解(当前未解出) {len(hit_new)} 题；假阳性(产出但不命中) {len(fp)} 题；对已解出题再命中 {len(solved_now)}")
print("\n--- 可新解 ---")
for r in hit_new[:40]: print(f"  {r[0]:<10} ans={r[1]!r:<22} kws={r[2][:4]}")
if fp:
    print("\n--- 假阳性（必须加守卫，否则会误判/帮倒忙）---")
    for r in fp[:30]: print(f"  {r[0]:<10} ans={r[1]!r:<22} kws={r[2][:4]}")
