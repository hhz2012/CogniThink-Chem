# -*- coding: utf-8 -*-
"""全量归因 after8b 中网关做错的题目。"""
import json, os, re, collections
HERE=r"D:\github\CogniThink-Chem\_evalscratch"
d=json.load(open(os.path.join(HERE,"after8b.json"),encoding="utf-8-sig"))
def cases(d):
    out=[]
    for p in ["bothCorrectCases","gatewayOnlyCorrectCases","pureLlmOnlyCorrectCases","bothWrongCases"]:
        out.extend(d.get(p,[]) or [])
    return out
cs=cases(d)
def is_pdf(r): return (r.get("id") or "").startswith("pdf-")

# 网关做错 = bothWrong ∪ pureLlmOnly
gw_wrong=[r for r in cs if not r.get("gatewayCorrect")]
both_wrong=[r for r in cs if not r.get("gatewayCorrect") and not r.get("pureLlmCorrect")]
pure_only=[r for r in cs if r.get("pureLlmCorrect") and not r.get("gatewayCorrect")]

print(f"总题数 {d.get('totalCases')}")
print(f"网关做错: {len(gw_wrong)}  (bothWrong={len(both_wrong)} + pureLlmOnly={len(pure_only)})")
print(f"网关做对: {d.get('gatewayCorrect')}  (bothCorrect={d.get('bothCorrect')} + gatewayOnly={d.get('gatewayOnlyCorrect')})")
print()

# 按 taskType 分布
print("=== 网关做错 按 taskType 分布 ===")
by=collections.Counter(r.get("taskType") or "?" for r in gw_wrong)
for t,n in by.most_common():
    eng=sum(1 for r in gw_wrong if (r.get("taskType") or "?")==t and r.get("gatewayUsedEngine"))
    noeng=n-eng
    print(f"  {t:18s} 错{n}  (引擎介入{eng} / 未介入{noeng})")
print()

# 根因自动分类
def classify(r):
    q=(r.get("question") or "")
    exp=(r.get("expected") or "")
    t=r.get("taskType") or "?"
    reason=[]
    # A. 缺图/表/数据
    if re.search(r"如图|下图|上图|图所示|图像|曲线|下表|表格|表中|流程图|流程如下|晶胞|装置图|实验装置|如图所示|见图|据图", q):
        reason.append("A-缺图表数据")
    # B. 选择题被分到 calc/eq 且引擎没给选项级答案（误分类）
    is_choice = bool(re.match(r"关键词:\s*[A-D][,，]", exp))
    if is_choice and t in ("calculation","equilibrium"):
        reason.append("B-选择题误分到calc/eq")
    if is_choice and t in ("calculation","equilibrium","concept","unknown") and not r.get("gatewayUsedEngine"):
        reason.append("B-选择题引擎未介入")
    # C. 引擎介入但判错
    if r.get("gatewayUsedEngine") and not r.get("gatewayCorrect"):
        reason.append("C-引擎介入但答错/未命中")
    # D. 纯LLM回退答错（taskType 概念类或引擎 null 回退）
    if not r.get("gatewayUsedEngine") and not is_choice:
        reason.append("D-回退纯LLM答错")
    # E. 引擎 ok=null 回退
    if r.get("gatewayOk") is False or (r.get("gatewayOk") is None and r.get("gatewayUsedEngine")):
        # gatewayUsedEngine requires Ok.HasValue; so if used engine Ok is not None
        pass
    if not reason: reason.append("Z-其他")
    return reason

print("=== 网关做错 根因标签分布（可多选） ===")
cnt=collections.Counter()
for r in gw_wrong:
    for x in classify(r): cnt[x]+=1
for k,v in cnt.most_common():
    print(f"  {k:24s} {v}")
print()

# 导出明细供人工复核
rows=[]
for r in gw_wrong:
    rows.append({
        "id":r.get("id"),"task":r.get("taskType"),"eng":r.get("gatewayUsedEngine"),
        "ok":r.get("gatewayOk"),"pure":r.get("pureLlmCorrect"),
        "reasons":classify(r),
        "expected":r.get("expected","")[:90],
        "gw":(r.get("gatewayConclusion") or r.get("gatewayAnswer") or "")[:80],
        "q":(r.get("question") or "")[:70].replace("\n"," "),
    })
# 按根因分组打印
from collections import defaultdict
groups=defaultdict(list)
for r in rows:
    for rr in r["reasons"]: groups[rr].append(r)
for tag in sorted(groups):
    print(f"\n##### {tag}  ({len(groups[tag])}题) #####")
    for r in groups[tag]:
        print(f"  [{r['id']}] task={r['task']} eng={r['eng']} pure={r['pure']} | exp={r['expected']}")
        print(f"        q={r['q']}")
        print(f"        gw={r['gw']}")

# 保存 JSON 明细供后续
with open(os.path.join(HERE,"wrong_breakdown.json"),"w",encoding="utf-8") as f:
    json.dump(rows,f,ensure_ascii=False,indent=1)
print("\n明细已存 _evalscratch/wrong_breakdown.json")
