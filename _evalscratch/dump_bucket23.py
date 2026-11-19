# -*- coding: utf-8 -*-
"""导出 ②引擎介入答错(33) + ③选择题误分类(40) 的完整内容，评估可解性。"""
import json, os, re, collections
HERE=r"D:\github\CogniThink-Chem\_evalscratch"
d=json.load(open(os.path.join(HERE,"after8b.json"),encoding="utf-8-sig"))
def cases(d):
    out=[]
    for p in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
        out.extend(d.get(p,[]) or [])
    return out
cs=cases(d)
wrong=[r for r in cs if not r.get("gatewayCorrect")]
CHART_RE=re.compile(r"如图|下图|上图|图所示|图像|曲线|下表|表格|表中|流程图|流程如下|晶胞|装置图|实验装置|如图所示|见图|据图|坐标|滴定曲线|变化如图|关系图")
def needs_chart(r): return bool(CHART_RE.search(r.get("question") or ""))
def is_choice(r): return bool(re.match(r"关键词:\s*[A-D]", (r.get("expected") or "")))

b2=[r for r in wrong if r.get("gatewayUsedEngine")]   # ②
b3=[r for r in wrong if (not r.get("gatewayUsedEngine")) and is_choice(r) and (r.get("taskType") in ("calculation","equilibrium","concept","unknown"))]  # ③

def dump(tag, rows):
    print(f"\n{'#'*70}\n### {tag}  ({len(rows)}题)\n{'#'*70}")
    for r in sorted(rows, key=lambda x:(x.get("taskType") or "", x.get("id") or "")):
        print(f"\n----- [{r.get('id')}] task={r.get('taskType')} eng={r.get('gatewayUsedEngine')} ok={r.get('gatewayOk')} chart={needs_chart(r)} -----")
        print(f"  EXP: {(r.get('expected') or '')[:200]}")
        q=(r.get("question") or "").replace("\n"," ")
        print(f"  Q  : {q[:260]}")
        print(f"  GW : {(r.get('gatewayConclusion') or r.get('gatewayAnswer') or '')[:160]}")

# 只打印题目类型分布+可解性信号，不全打（太多）
print("=== ② 引擎介入答错：题目问法类型统计 ===")
# 判断 expected 形式
forms=collections.Counter()
for r in b2:
    exp=r.get("expected") or ""
    if re.match(r"关键词:\s*[A-D]", exp): forms["选择题(字母)"]+=1
    elif "√" in exp or "×" in exp: forms["判断(√×)"]+=1
    elif "==" in exp or "→" in exp or "=" in exp: forms["方程式书写"]+=1
    else: forms["其他(填空/简答)"]+=1
for k,v in forms.most_common(): print(f"  {k}: {v}")
print(f"  ② 合计 {len(b2)}, 其中缺图 {sum(1 for r in b2 if needs_chart(r))}")

print("\n=== ③ 选择题误分类：原 taskType 分布 + 缺图 ===")
c=collections.Counter(r.get("taskType") or "?" for r in b3)
for k,v in c.most_common(): print(f"  {k}: {v}")
print(f"  ③ 合计 {len(b3)}, 其中缺图 {sum(1 for r in b3 if needs_chart(r))}")
# 正确答案字母分布
letters=collections.Counter()
for r in b3:
    m=re.match(r"关键词:\s*([A-D]+)", r.get("expected") or "")
    if m: letters[m.group(1)]+=1
print("  正确选项分布:", dict(letters))

# 全量导出到文件供人工看
with open(os.path.join(HERE,"bucket23_dump.txt"),"w",encoding="utf-8") as f:
    import io,sys
    old=sys.stdout; sys.stdout=io.StringIO()
    dump("② 引擎介入但答错 (33)", b2)
    dump("③ 选择题误分类 (40)", b3)
    f.write(sys.stdout.getvalue()); sys.stdout=old
print("\n完整内容已存 _evalscratch/bucket23_dump.txt")
