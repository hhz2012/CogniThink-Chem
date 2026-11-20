r"""
从 eval 快照生成未解单选题候选池（chem-fact-curation skill 自带）。

用法：
  C:\Users\Administrator\.local\bin\python3.14.exe scripts\\unsolved_pool.py <tag>

  <tag> 取上一批的 after 快照名（如 after_svg_batch28），脚本会读取
  _evalscratch\snapshot_<tag>.json，并把候选池写到
  _evalscratch\unsolved_<tag>_out.txt。

快照是 6 键结构（非 results 列表）：
  solved   = bothCorrectCases + gatewayOnlyCorrectCases + pureLlmOnlyCorrectCases
  decided  = 上述 3 个 + bothWrongCases + gatewayOnlyWrongCases + pureLlmOnlyWrongCases
  未解单选 = pdf 题库中 type==choice、4 选项齐全、答案为单字母 A-D、且不在 solved 中。
  其中 decided=True 表示 Concept 返回 null（0 harm）→ 可安全加事实。
"""
import json, re, sys, os

def norm(s):
    if not s: return ''
    out=[]
    for ch in s:
        if ch.isspace(): continue
        o=ord(ch)
        if 0x2080 <= o <= 0x2089: out.append(chr(ord('0')+(o-0x2080))); continue
        if o==0x2070: out.append('0'); continue
        if o==0x00B9: out.append('1'); continue
        if o==0x00B2: out.append('2'); continue
        if o==0x00B3: out.append('3'); continue
        if 0x2074 <= o <= 0x2079: out.append(chr(ord('4')+(o-0x2074))); continue
        if o==0x207A: out.append('+'); continue
        if o==0x207B: out.append('-'); continue
        if 0xFF01 <= o <= 0xFF5E: out.append(chr(o-0xFEE0)); continue
        if o==0x3000: continue
        out.append(ch)
    return ''.join(out)

WRONG=re.compile(r'错误|不正确|不相符|不符|不具有对应|不能达到|无法达到|不能实现|不能用来|不能用|不属于|不合理|不可行|不能说明|不能证明|不能用于|没有关联')

def main():
    if len(sys.argv) < 2:
        print("usage: unsolved_pool.py <tag>   (reads _evalscratch/snapshot_<tag>.json)")
        sys.exit(2)
    tag = sys.argv[1].strip()
    snap_path = os.path.join('_evalscratch', f'snapshot_{tag}.json')
    if not os.path.exists(snap_path):
        print(f"ERROR: snapshot not found: {snap_path}")
        sys.exit(1)

    snap=json.load(open(snap_path,encoding='utf-8'))
    solved=set(); decided=set()
    for lst in ['bothCorrectCases','gatewayOnlyCorrectCases','pureLlmOnlyCorrectCases']:
        for c in (snap.get(lst) or []):
            if c.get('id'): solved.add(c['id']); decided.add(c['id'])
    for lst in ['bothWrongCases','gatewayOnlyWrongCases','pureLlmOnlyWrongCases']:
        for c in (snap.get(lst) or []):
            if c.get('id'): decided.add(c['id'])

    data=json.load(open(r'src/CogniThink.TutorBrain.Api/data/pdf_questions.json',encoding='utf-8'))
    unsolved=[]
    for e in data:
        qid=e['id']
        if not qid.startswith('pdf-'): continue
        if e.get('type','')!='choice': continue
        opts=e.get('options') or {}
        if not all(k in opts for k in 'ABCD'): continue
        ans=e.get('answer','')
        if ans not in 'ABCD' or len(ans)!=1: continue
        if qid in solved: continue
        unsolved.append((qid,ans,e))

    n_decided = sum(1 for q,_,_ in unsolved if q in decided)
    print(f"unsolved single-choice (pdf,4 opts,single-letter ans): {len(unsolved)}")
    print(f"  of which decided(Concept null, safe to curate): {n_decided}")
    print(f"  of which NOT-decided(skip): {len(unsolved)-n_decided}")
    out=[]
    for qid,ans,e in unsolved:
        stem=e.get('question','')
        ask='WRONG' if WRONG.search(stem) else 'correct'
        imgs=e.get('images') or []
        dec = 'decided' if qid in decided else 'NOT-decided'
        line=f"\n===== {qid} ans={ans} askDir={ask} [{dec}] imgs={imgs} =====\n  STEM: {stem[:140]}\n  NOTE: {(e.get('note','') or '')[:330]}"
        out.append(line)
        print(line)
    out_path = os.path.join('_evalscratch', f'unsolved_{tag}_out.txt')
    open(out_path,'w',encoding='utf-8').write('\n'.join(out))
    print(f"\nWROTE {out_path}")

if __name__ == '__main__':
    main()
