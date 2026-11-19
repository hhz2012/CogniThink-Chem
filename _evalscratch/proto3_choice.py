# -*- coding: utf-8 -*-
"""原型 v3：选择题逐选项原子结构/NA 校验，看能否唯一锁定正确字母。"""
import json, re, os
HERE=r"D:\github\CogniThink-Chem\_evalscratch"
uns=json.load(open(os.path.join(HERE,"stage3c_unsolved_pdf.json"),encoding="utf-8-sig"))
full=json.load(open(r"D:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json",encoding="utf-8-sig"))
uns_ids_nc=set(o["id"] for o in uns if not o.get("chart"))

SUP=str.maketrans('0123456789','⁰¹²³⁴⁵⁶⁷⁸⁹')
SUB=str.maketrans('0123456789','₀₁₂₃₄₅₆₇₈₉')
def norm(s):
    if not s: return ""
    m={'₀':'0','₁':'1','₂':'2','₃':'3','₄':'4','₅':'5','₆':'6','₇':'7','₈':'8','₉':'9',
       '⁰':'0','¹':'1','²':'2','³':'3','⁴':'4','⁵':'5','⁶':'6','⁷':'7','⁸':'8','⁹':'9',
       '⁺':'+','⁻':'-','－':'-','−':'-','﹣':'-'}
    out=[]
    for c in s:
        nc=m.get(c,c)
        if 0xFF01<=ord(nc)<=0xFF5E: nc=chr(ord(nc)-0xFEE0)
        if ord(nc)==0x3000: nc=' '
        out.append(nc)
    return ''.join(out).lower()

PT={1:'H',2:'He',3:'Li',4:'Be',5:'B',6:'C',7:'N',8:'O',9:'F',10:'Ne',11:'Na',12:'Mg',13:'Al',14:'Si',15:'P',16:'S',17:'Cl',18:'Ar',19:'K',20:'Ca',21:'Sc',22:'Ti',23:'V',24:'Cr',25:'Mn',26:'Fe',27:'Co',28:'Ni',29:'Cu',30:'Zn',31:'Ga',32:'Ge',33:'As',34:'Se',35:'Br',36:'Kr',37:'Rb',38:'Sr',39:'Y',40:'Zr',41:'Nb',42:'Mo',43:'Tc',44:'Ru',45:'Rh',46:'Pd',47:'Ag',48:'Cd',49:'In',50:'Sn',51:'Sb',52:'Te',53:'I',54:'Xe',55:'Cs',56:'Ba',57:'La',58:'Ce',59:'Pr',60:'Nd',61:'Pm',62:'Sm',63:'Eu',64:'Gd',65:'Tb',66:'Dy',67:'Ho',68:'Er',69:'Tm',70:'Yb',71:'Lu',72:'Hf',73:'Ta',74:'W',75:'Re',76:'Os',77:'Ir',78:'Pt',79:'Au',80:'Hg',81:'Tl',82:'Pb',83:'Bi',84:'Po',85:'At',86:'Rn',87:'Fr',88:'Ra',89:'Ac',90:'Th',91:'Pa',92:'U'}
SYM={v:k for k,v in PT.items()}
CN={'H':'氢','He':'氦','Li':'锂','Be':'铍','B':'硼','C':'碳','N':'氮','O':'氧','F':'氟','Ne':'氖','Na':'钠','Mg':'镁','Al':'铝','Si':'硅','P':'磷','S':'硫','Cl':'氯','Ar':'氩','K':'钾','Ca':'钙','Sc':'钪','Ti':'钛','V':'钒','Cr':'铬','Mn':'锰','Fe':'铁','Co':'钴','Ni':'镍','Cu':'铜','Zn':'锌','Ga':'镓','Ge':'锗','As':'砷','Se':'硒','Br':'溴','Kr':'氪','Rb':'铷','Sr':'锶','Y':'钇','Zr':'锆','Nb':'铌','Mo':'钼','Tc':'锝','Ru':'钌','Rh':'铑','Pd':'钯','Ag':'银','Cd':'镉','In':'铟','Sn':'锡','Sb':'锑','Te':'碲','I':'碘','Xe':'氙','Cs':'铯','Ba':'钡','La':'镧','Ce':'铈','Pr':'镨','Nd':'钕','Pm':'钷','Sm':'钐','Eu':'铕','Gd':'钆','Tb':'铽','Dy':'镝','Ho':'钬','Er':'铒','Tm':'铥','Yb':'镱','Lu':'镥','Hf':'铪','Ta':'钽','W':'钨','Re':'铼','Os':'锇','Ir':'铱','Pt':'铂','Au':'金','Hg':'汞','Tl':'铊','Pb':'铅','Bi':'铋','Po':'钋','At':'砹','Rn':'氡','Fr':'钫','Ra':'镭','Ac':'锕','Th':'钍','Pa':'镤','U':'铀'}
SUBSHELLS=[('1s',2),('2s',2),('2p',6),('3s',2),('3p',6),('4s',2),('3d',10),('4p',6),('5s',2),('4d',10),('5p',6),('6s',2),('4f',14),('5d',10),('6p',6),('7s',2),('5f',14),('6d',10),('7p',6)]
EXC={24:{'4s':1,'3d':5},29:{'4s':1,'3d':10},41:{'5s':1,'4d':4},42:{'5s':1,'4d':5},44:{'5s':1,'4d':7},45:{'5s':1,'4d':8},46:{'5s':0,'4d':10},47:{'5s':1,'4d':10},78:{'6s':1,'5d':9},79:{'6s':1,'5d':10}}
ORDER=['1s','2s','2p','3s','3p','3d','4s','4p','4d','4f','5s','5p','5d','5f','6s','6p','6d','7s','7p']
def config(z):
    occ={}; rem=z
    for sh,cap in SUBSHELLS:
        if rem<=0: break
        occ[sh]=occ.get(sh,0)+min(cap,rem); rem-=min(cap,rem)
    if z in EXC:
        for sh,c in EXC[z].items(): occ[sh]=c
    return {sh:occ[sh] for sh in ORDER if sh in occ}
def valence(z):
    occ=config(z); nmax=max(int(s[0]) for s in occ); v={}
    for sh,c in occ.items():
        n=int(sh[0]); l=sh[1]
        if n==nmax: v[sh]=c
        elif l=='d' and n==nmax-1: v[sh]=c
        elif l=='f' and n==nmax-2: v[sh]=c
    return v
def fmt(occ, sup=True):
    return ''.join(sh+(str(c).translate(SUP) if sup else str(c)) for sh,c in occ.items())

def parse_options(o):
    opts=o.get('options') or {}
    if opts: return {k.upper():v for k,v in opts.items()}
    # 从题干渲染解析
    q=o.get('question') or ''
    res={}
    for m in re.finditer(r"(?:^|\n)\s*([A-D])[\.．、]\s*(.+?)(?=\n\s*[A-D][\.．、]|$)", q, re.S):
        res[m.group(1)]=m.group(2).strip()
    return res

def verify_option(text):
    """校验一个选项的原子结构/周期表声明。返回 (verified:bool, is_correct:bool|None, claim_kind)。
    只在能确定性判定时返回 verified=True。"""
    t=norm(text)
    # 1) 电子排布声明：含 ns/np/nd 且提到某元素
    if re.search(r"[1-7][spdf][⁰¹²³⁴⁵⁶⁷⁸⁹0-9]", t) and ('电子' in t or '排布' in t or '轨道' in t):
        # 找元素
        sym=None; pos=-1
        for s in SYM:
            i=text.find(s)
            if i>pos: sym,pos=s,i
        # 中文
        for s,name in CN.items():
            i=text.find(name)
            if i>pos: sym,pos=s,i
        if sym is None: return (False,None,None)
        z=SYM[sym]
        # 选项里写的排布
        occ_written=parse_config_string(t)
        if occ_written is None: return (False,None,None)
        # 判断问的是价层还是全排布/简化
        is_valence = '价层' in text or '价电子' in text
        target = fmt(valence(z)) if is_valence else fmt(config(z))
        correct = (norm(occ_written)==norm(target))
        return (True, correct, 'config')
    # 2) 周期/族声明：第X周期第Y族
    m=re.search(r"第([一二三四五六七八九十\d]+)周期\s*第\s*([IVX]+[AB]?|0)\s*族", text)
    if m:
        sym=None; pos=-1
        for s in SYM:
            i=text.find(s)
            if i>pos: sym,pos=s,i
        for s,name in CN.items():
            i=text.find(name)
            if i>pos: sym,pos=s,i
        if sym is None: return (False,None,None)
        z=SYM[sym]
        p,g=period_group(z)
        pn=num_cn_parse(m.group(1)); gn=m.group(2)
        correct=(pn==p and (gn==g or norm(gn)==norm(g)))
        return (True,correct,'period_group')
    return (False,None,None)

def parse_config_string(t):
    """从文本提取电子排布 occ dict（归一化后）。返回 None 若解析不出。"""
    # 匹配 1s² 2p3 等
    found=re.findall(r"([1-7])([spdf])([⁰¹²³⁴⁵⁶⁷⁸⁹0-9]+)", t)
    if not found: return None
    occ={}
    for n,l,c in found:
        c=int(c.translate(str.maketrans('⁰¹²³⁴⁵⁶⁷⁸⁹','0123456789')))
        occ[n+l]=c
    if not occ: return None
    # 稀有气体简写：[Ar]... 需要知道前面的；这里只比较提取到的部分是否等于目标的后缀，复杂。先不处理简写。
    return fmt(occ)

def period_group(z):
    occ=config(z); nmax=max(int(s[0]) for s in occ)
    s=sum(c for sh,c in occ.items() if int(sh[0])==nmax and sh[1]=='s')
    p=sum(c for sh,c in occ.items() if int(sh[0])==nmax and sh[1]=='p')
    d=sum(c for sh,c in occ.items() if int(sh[0])==nmax-1 and sh[1]=='d')
    if nmax==1: grp='IA' if s==1 else '0'
    elif p>0 or d==0: grp={1:'IA',2:'IIA',3:'IIIA',4:'IVA',5:'VA',6:'VIA',7:'VIIA',8:'0'}.get(s+p,'?')
    else: grp={3:'IIIB',4:'IVB',5:'VB',6:'VIB',7:'VIIB',8:'VIII',9:'VIII',10:'VIII',11:'IB',12:'IIB'}.get(s+d,'?')
    return num_cn(nmax), grp
def num_cn(n): return {1:'一',2:'二',3:'三',4:'四',5:'五',6:'六',7:'七'}.get(n,str(n))
def num_cn_parse(s):
    m={'一':1,'二':2,'三':3,'四':4,'五':5,'六':6,'七':7,'八':8,'九':9,'十':10}
    return m.get(s,int(s) if s.isdigit() else -1)

def decide(o):
    opts=parse_options(o)
    if not opts: return None
    q=o.get('question') or ''
    ask_correct = ('正确' in q and '错误' not in q.split('下列')[-1][:30]) or '正确的是' in q
    ask_wrong = '错误' in q or '不正确' in q
    direction = 'wrong' if ask_wrong else ('correct' if ask_correct else None)
    if direction is None: return None
    verdicts={}
    for letter,opt in opts.items():
        v,ok,kind=verify_option(opt)
        if v: verdicts[letter]=(ok,kind)
    if not verdicts: return None
    if direction=='correct':
        correct_opts=[l for l,(ok,k) in verdicts.items() if ok]
        if len(correct_opts)==1: return (correct_opts[0], 'only_correct', verdicts)
    else:
        wrong_opts=[l for l,(ok,k) in verdicts.items() if not ok]
        if len(wrong_opts)==1: return (wrong_opts[0], 'only_wrong', verdicts)
    return ('ambig', verdicts, None)

# 评估
new=[]; fp=[]; ambig=0; verified_total=0
for o in full:
    if (o.get('type') or '')!='choice': continue
    ans=(o.get('answer') or '').strip().upper()
    # 期望字母
    exp=re.search(r"[A-D]", ans)
    if not exp: continue
    expL=exp.group(0)
    d=decide(o)
    if d is None: continue
    if d[0]=='ambig':
        ambig+=1; continue
    letter=d[0]; verified_total+=1
    if letter==expL:
        if o['id'] in uns_ids_nc: new.append((o['id'],letter,expL,d[1],d[2]))
    else:
        fp.append((o['id'],letter,expL,d[2], (o.get('question') or '')[:60]))
print(f"选择原子结构校验：可唯一判定 {verified_total}，其中新解(当前未解出且判对) {len(new)}，判错(假阳性) {len(fp)}，歧义(放弃) {ambig}")
for r in new[:30]: print(f"  NEW {r[0]:<9} pick={r[1]} exp={r[2]} reason={r[3]} verdicts={r[4]}")
if fp:
    print("  -- 假阳性(判错，必须修) --")
    for r in fp[:20]: print(f"  FP  {r[0]:<9} pick={r[1]} exp={r[2]} verdicts={r[3]} Q={r[4]}")
