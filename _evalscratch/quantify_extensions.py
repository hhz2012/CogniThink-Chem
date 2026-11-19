# -*- coding: utf-8 -*-
"""量化各候选引擎扩展在『未解出 PDF（去图表）351 题』上的潜在覆盖（按问法匹配）。"""
import json, re, os
from collections import defaultdict
HERE=r"D:\github\CogniThink-Chem\_evalscratch"
data=json.load(open(os.path.join(HERE,"stage3c_unsolved_pdf.json"),encoding="utf-8-sig"))
data=[o for o in data if not o.get("chart")]
print(f"未解出去图表 PDF: {len(data)}\n")

def q(o): return o.get("question") or ""
def exp(o): return o.get("expected") or ""

CATS = {
 "周期表/原子结构(PT)": [r"价层电子|电子排布|基态|轨道表示|电子构型|原子序数|质子数|第几周期|第.*族|主族|副族|短周期|核外电子|价电子"],
 "物质分类(Cat)": [r"分类|酸性氧化物|碱性氧化物|两性氧化物|电解质|非电解质|同素异形体|同位素|混合物|纯净物|胶体|分散系|酸\.|碱\.|盐\.|有机物|无机物"],
 "转化能否实现(Conv)": [r"能实现|不能实现|转化|一步.*制备|可以.*制取|工业制取|实验室制备"],
 "Ka/酸性强弱(Acid)": [r"电离平衡常数|Ka|酸性|碱性强弱|强酸制弱酸|弱酸.*电离|pH.*大小"],
 "物质的量/计量(Stoich)": [r"物质的量|NA|阿伏|6\.02|标况|标准状况|mol/L|浓度|稀释|质量分数|体积.*分数|n=|c="],
 "化学用语/方程式书写(Eq)": [r"写出.*方程式|离子方程式|化学方程式|配平|双线桥|单线桥|电子式|结构式|结构简式"],
 "正误判断(√×)": [r"正确的填|错误的填|是否正确|下列说法.*(正确|错误)|表述.*(正确|错误)"],
 "实验/检验/鉴别(Exp)": [r"检验|鉴别|除杂|分离|提纯|实验|现象|试剂|仪器|装置"],
 "有机(Org)": [r"同分异构|官能团|单体|加聚|缩聚|消去|加成|取代|酯化|水解|核磁共振|氢谱|命名"],
 "晶胞/晶体(Cryst)": [r"晶胞|晶体|密度|配位数|面心|体心|六方"],
 "电化学(Electro)": [r"原电池|电解池|阳极|阴极|正极|负极|电极反应|放电|充电|电镀"],
}
counts={}
ids={}
for name,pats in CATS.items():
    hit=[o for o in data if any(re.search(p,q(o)) for p in pats)]
    counts[name]=len(hit)
    llm=sum(1 for o in hit if o.get("llm_ok"))
    ids[name]=[o["id"] for o in hit]
    print(f"  {name:<28} {len(hit):>4}  其中纯LLM答对 {llm:>4}")

# 重叠：一题可能命中多类
all_hit=set()
for v in ids.values(): all_hit.update(v)
print(f"\n命中至少一类的去重题数: {len(all_hit)} / {len(data)}")
print(f"未命中任何一类（开放/上下文依赖）: {len(data)-len(all_hit)}")

# 输出各类前 8 个 id 供核验
print("\n各类样本 id（前8）:")
for name,v in ids.items():
    print(f"  {name:<28} {v[:8]}")

# 重点：周期表类的 expected 关键词长什么样（判断能否精确匹配）
print("\n=== 周期表/原子结构类 题干片段 + expected（判断可精确匹配性）===")
pt=[o for o in data if any(re.search(p,q(o)) for p in CATS["周期表/原子结构(PT)"])]
for o in pt[:25]:
    print(f"  [{o['id']}] llm={o['llm_ok']}")
    print(f"    Q: {re.sub(chr(10),' ',q(o))[:130]}")
    print(f"    exp: {re.sub(chr(10),' ',exp(o))[:90]}")
