# -*- coding: utf-8 -*-
"""互斥根因归因 + 深挖可优化项（引擎答错 / 判分漏判）。"""
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
def needs_chart(r):
    return bool(CHART_RE.search(r.get("question") or ""))
def is_choice(r):
    return bool(re.match(r"关键词:\s*[A-D]", (r.get("expected") or "")))
def choice_letter(exp):
    m=re.match(r"关键词:\s*([A-D]+)", exp or "")
    return m.group(1) if m else ""

# 互斥分类（优先级从高到低）
def bucket(r):
    t=r.get("taskType") or "?"
    eng=r.get("gatewayUsedEngine")
    ch=is_choice(r)
    if needs_chart(r):
        return "①缺图表/数据(天花板)"
    if eng:
        # 引擎介入但仍错 -> 引擎答错/不相关
        return "②引擎介入但答错"
    if ch and t in ("calculation","equilibrium","concept","unknown"):
        # 选择题被分到无求解器/不判选项的类型，回退LLM答错
        return "③选择题误分类(回退答错)"
    if t in ("concept","unknown","structure_verify","vsepr","electrolysis","titration"):
        # 无对应引擎的题型，纯LLM答错
        return "④无引擎题型(纯LLM答错)"
    if t in ("calculation","equilibrium"):
        # calc/eq 非选择题，引擎无求解器→回退LLM答错
        return "⑤calc/eq缺求解器(回退答错)"
    return "⑥其他"

cnt=collections.Counter(bucket(r) for r in wrong)
print("=== 互斥根因桶（网关做错 184 题）===")
for k,v in cnt.most_common():
    print(f"  {k:26s} {v:3d}")
print(f"  合计 {sum(cnt.values())}")
print()

# 每个桶按 taskType 细分
print("=== 各桶内 taskType 细分 ===")
buckets=collections.defaultdict(list)
for r in wrong: buckets[bucket(r)].append(r)
for tag in sorted(buckets):
    c=collections.Counter(r.get("taskType") or "?" for r in buckets[tag])
    print(f"  [{tag}] {len(buckets[tag])}题: " + ", ".join(f"{t}={n}" for t,n in c.most_common()))
print()

# ----- 深挖 ② 引擎介入但答错 -----
print("\n========== ② 引擎介入但答错 明细（45题）==========")
for r in sorted(buckets["②引擎介入但答错"], key=lambda x:(x.get("taskType") or "", x.get("id") or "")):
    exp=r.get("expected","")[:70]
    gw=(r.get("gatewayConclusion") or r.get("gatewayAnswer") or "")[:75]
    print(f"  [{r.get('id')}] {r.get('taskType')} ok={r.get('gatewayOk')} | exp={exp}")
    print(f"        gw={gw}")

# ----- 检查判分假阴性：引擎/LLM答案里其实含 expected 某关键词子串但被判错 -----
print("\n========== 疑似判分漏判（答案含 expected 关键词子串但 gatewayCorrect=False）==========")
# 从 expected 拆出关键词（控制器用 string.Join(",", AnswerKeywords)；这里反推）
def norm(s):
    if not s: return ""
    s=s.replace("₀","0").replace("₁","1").replace("₂","2").replace("₃","3").replace("₄","4")
    s=s.replace("₅","5").replace("₆","6").replace("₇","7").replace("₈","8").replace("₉","9")
    s=s.replace("⁰","0").replace("¹","1").replace("²","2").replace("³","3").replace("⁴","4")
    s=s.replace("⁵","5").replace("⁶","6").replace("⁷","7").replace("⁸","8").replace("⁹","9")
    return s.lower()
suspect=[]
for r in wrong:
    exp=r.get("expected","")
    body=exp[len("关键词:"):].strip() if exp.startswith("关键词:") else exp
    kws=[k.strip() for k in re.split(r"[,，;；、]", body) if k.strip()]
    ans=norm(r.get("gatewayAnswer") or "")
    for k in kws:
        nk=norm(k)
        if len(nk)>=3 and nk in ans:
            suspect.append((r.get("id"),r.get("taskType"),k,r.get("gatewayCorrect")))
            break
print(f"  疑似漏判 {len(suspect)} 题（答案含≥3字关键词子串却判错）：")
for x in suspect[:60]:
    print(f"    [{x[0]}] {x[1]} 命中关键词={x[2]!r}")
if len(suspect)>60: print(f"    ...共{len(suspect)}")
