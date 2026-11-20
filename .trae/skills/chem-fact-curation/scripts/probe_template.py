r"""
探针骨架（chem-fact-curation skill 自带）。
字节级复刻 C# ConceptChoiceValidator 的归一化 + FactChoiceMatcher 语义，
在落库前模拟：① 每条 draft 事实是否只命中唯一目标选项（无 stray-hit）；
② 目标题每选项是否恰好命中 1 条事实且决策与答案键一致。

用法：
  1. 复制本文件到工作区  _evalscratch\probe_b<N>.py
  2. 填写 QS（目标题 id 列表）、drafts（拟落库的事实，4 选项×题数）、tgt（事实前缀→题 id）
  3. 运行：  C:\Users\Administrator\.local\bin\python3.14.exe _evalscratch\probe_b<N>.py
  4. 必须看到  TOTAL PROBLEMS=0  才允许落库。

硬约束：norm() / opt_contains / WRONG 必须与 C# 行为逐字节一致，禁止自行改动
（见 references/batch-playbook.md 的"C# 归一化"一节）。
空 QS/drafts 时运行应输出 TOTAL PROBLEMS=0，仅用于验证骨架可执行。
"""
import json, re

def _isdigit(ch): return '0' <= ch <= '9'

def norm(s):
    # 与 C# ConceptChoiceValidator.Norm / NormOpt 字节级对齐
    if not s: return ''
    out=[]
    for ch in s:
        if ch.isspace(): continue
        o=ord(ch)
        if 0x2080 <= o <= 0x2089: out.append(chr(ord('0')+(o-0x2080))); continue   # 下标 ₀-₉
        if o==0x2070: out.append('0'); continue                                   # ⁰
        if o==0x00B9: out.append('1'); continue                                   # ¹
        if o==0x00B2: out.append('2'); continue                                   # ²
        if o==0x00B3: out.append('3'); continue                                   # ³
        if 0x2074 <= o <= 0x2079: out.append(chr(ord('4')+(o-0x2074))); continue  # ⁴-⁹
        if o==0x207A: out.append('+'); continue                                   # ⁺
        if o==0x207B: out.append('-'); continue                                   # ⁻
        if 0xFF01 <= o <= 0xFF5E: out.append(chr(o-0xFEE0)); continue             # 全角→ASCII
        if o==0x3000: continue                                                    # 全角空格
        out.append(ch)
    return ''.join(out).replace('—','-').replace('–','-').replace('−','-')        # NormOpt 额外破折号归一

def opt_contains(text, kw):
    # 复刻 FactChoiceMatcher.OptContains：数字开头的关键词要求前边界非数字
    if not kw: return False
    sd=_isdigit(kw[0]); idx=text.find(kw)
    while idx>=0:
        pd=idx>0 and _isdigit(text[idx-1])
        if not sd or not pd: return True
        idx=text.find(kw,idx+1)
    return False

# 必须与 MultiSelectChoiceValidator / FactChoiceMatcher 的 WRONG 正则完全一致；禁止增删
WRONG=re.compile(r'错误|不正确|不相符|不符|不具有对应|不能达到|无法达到|不能实现|不能用来|不能用|不属于|不合理|不可行|不能说明|不能证明|不能用于|没有关联')

data=json.load(open(r'src/CogniThink.TutorBrain.Api/data/pdf_questions.json',encoding='utf-8'))
d=json.load(open(r'data/highschool/general-choice-facts.json',encoding='utf-8'))
existing=d['statements']
by_id={e['id']:e for e in data}

# ===== 每批填写区（复制到 _evalscratch\probe_b<N>.py 后修改）=====
QS=[]   # 例：QS=['pdf-140','pdf-014','pdf-139']
# 先打印目标题的真实 stem/option（norm 后），便于写 match 时核对码点
for qid in QS:
    e=by_id[qid]; stem=e.get('question',''); opts=e.get('options') or {}
    print(f"\n##### {qid} ans={e.get('answer')} askDir={'wrong' if WRONG.search(stem) else 'correct'}")
    print(f"  STEM(norm): {norm(stem)}")
    for k in 'ABCD':
        print(f"  {k}(norm): {norm(str(opts.get(k,'')))}")

drafts=[
 # 每题 4 条（A/B/C/D 各一），verdict 为该选项的独立化学真假；reason 写清推理。
 # 示例（批次28，已落库，仅作格式参考，复制后删除）：
 # {"id":"GC-742-A","match":["发生氧化反应与发生还原反应的硫","2:1"],"stemMatch":["硫单质可以与NaOH","2Na2S"],"verdict":False,"reason":"..."},
 # {"id":"GC-742-B","match":["S6","S-S的数目是6NA"],"stemMatch":["硫单质可以与NaOH","2Na2S"],"verdict":True,"reason":"..."},
 # {"id":"GC-742-C","match":["S8、S6、S4","化学变化"],"stemMatch":["硫单质可以与NaOH","2Na2S"],"verdict":True,"reason":"..."},
 # {"id":"GC-742-D","match":["Na2SO3","稀硫酸反应产生SO2"],"stemMatch":["硫单质可以与NaOH","2Na2S"],"verdict":True,"reason":"..."},
]
tgt={}  # 例：tgt={'GC-742':'pdf-140','GC-743':'pdf-014','GC-744':'pdf-139'} （事实前缀→目标题）
# ===== 填写区结束 =====

if not QS and not drafts:
    print("[probe_template] QS 与 drafts 均为空 —— 这是骨架自检。复制到 _evalscratch\\probe_b<N>.py 并填写后再运行。")

problems=0
print("\n=== stray-hit check (existing + drafts) against ALL questions ===")
allfacts=existing+drafts
for f in drafts:
    hits=[]
    for e in data:
        stemN=norm(e.get('question',''))
        if not all(opt_contains(stemN,norm(x)) for x in (f.get('stemMatch') or [])): continue
        for k in ['A','B','C','D']:
            optN=norm(str((e.get('options') or {}).get(k,'')))
            if all(opt_contains(optN,norm(x)) for x in (f.get('match') or [])):
                hits.append((e['id'],k))
    pref='GC-'+f['id'].split('-')[1][:3]
    expect=(tgt.get(pref), f['id'][-1])
    ok=(len(hits)==1 and hits[0]==expect)
    print(f"  {f['id']} v={f['verdict']} hits={hits} {'OK' if ok else '!! PROBLEM'}")
    if not ok: problems+=1

print("\n=== decision simulation (existing + drafts) for target Qs ===")
for qid in QS:
    e=by_id[qid]; stem=e.get('question',''); stemN=norm(stem)
    askwrong=bool(WRONG.search(stem))
    verdicts={}; decided=True
    for k in ['A','B','C','D']:
        optN=norm(str((e.get('options') or {}).get(k,'')))
        matched=[f for f in allfacts if f.get('match') and all(opt_contains(optN,norm(x)) for x in f['match']) and all(opt_contains(stemN,norm(x)) for x in (f.get('stemMatch') or []))]
        if len(matched)!=1:
            print(f"  {qid} {k}: matched={len(matched)} {[m['id'] for m in matched]} -> NULL !! PROBLEM"); decided=False; problems+=1; break
        verdicts[k]=matched[0]['verdict']
    if decided:
        cands=[k for k in 'ABCD' if (verdicts[k] if not askwrong else not verdicts[k])]
        exp=e.get('answer')
        ok = cands==[exp]
        print(f"  {qid} askDir={'wrong' if askwrong else 'correct'} verdicts={verdicts} candidates={cands} expected={exp} {'OK' if ok else 'MISMATCH !! PROBLEM'}")
        if not ok: problems+=1
print(f"\nTOTAL PROBLEMS={problems}")
