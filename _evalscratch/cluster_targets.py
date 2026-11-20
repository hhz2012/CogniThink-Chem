# -*- coding: utf-8 -*-
"""对 A 类 346 题做精细题簇聚类（按真实求解模式，而非粗略知识点）。
每道题可命中多个题簇关键词，统计覆盖量，再按优先级分配主簇。"""
import json
from collections import Counter, defaultdict

T = json.load(open(r"d:\github\CogniThink-Chem\_evalscratch\a_class_engine_targets.json", encoding="utf-8"))

# 题簇定义：(簇名, 关键词列表) —— 匹配题干或 LLM 参照答案
CLUSTERS = [
    ("ionic_eq",      ["离子方程式", "离子方程", "方程式书写正确", "方程式正确", "电离方程式"]),
    ("ion_coexist",   ["离子共存", "大量共存", "一定能大量", "能否共存"]),
    ("redox_balance", ["氧化还原", "配平", "化合价", "电子转移", "氧化剂", "还原剂", "氧化性", "还原性", "得失电子", "歧化", "归中"]),
    ("electrolyte",   ["电解质", "非电解质", "强电解质", "弱电解质", "电离", "导电"]),
    ("na_count",      ["阿伏加德罗", "阿伏伽德罗", "NA表示", "用NA", "N_A", "粒子数", "微粒数"]),
    ("equilibrium",   ["化学平衡", "电离平衡", "水解", "盐类水解", "pH", "溶解平衡", "Ksp", "溶度积", "转化率", "平衡移动", "勒夏特列", "缓冲"]),
    ("colloid",       ["胶体", "丁达尔", "渗析", "电泳", "聚沉"]),
    ("organic",       ["有机", "烃", "醇", "醛", "羧酸", "酯", "苯", "乙烯", "甲烷", "乙醇", "乙酸", "葡萄糖", "淀粉", "纤维素", "蛋白质", "油脂", "高分子", "聚合", "加聚", "缩聚", "同分异构", "官能团", "消去", "加成", "取代", "酯化", "同系物"]),
    ("structure_periodic", ["同位素", "同素异形体", "核素", "中子", "质子", "质量数", "元素周期", "周期律", "晶胞", "晶体", "离子晶体", "原子晶体", "分子晶体", "金属晶体", "杂化", "空间构型", "VSEPR", "电子排布", "化学键", "共价键", "离子键"]),
    ("experiment",    ["实验", "操作", "现象", "装置", "试剂", "滴定", "焰色", "除杂", "检验", "提纯", "分离"]),
    ("calc",          []),  # taskType==calculation 兜底
]

PRIORITY = ["ionic_eq","ion_coexist","redox_balance","na_count","electrolyte",
            "equilibrium","colloid","structure_periodic","organic","experiment"]

def hit_clusters(t):
    text = (t["question"] + " " + t["llmReference"])
    hits = []
    if t["taskType"] == "calculation":
        hits.append("calc")
    for name, kws in CLUSTERS:
        if name == "calc":
            continue
        if any(k in text for k in kws):
            hits.append(name)
    return hits

# 统计每个题簇被命中的题数（可重叠）
cover = Counter()
for t in T:
    for c in hit_clusters(t):
        cover[c] += 1
print("=== 题簇覆盖量（可重叠，一道题可能属多簇）===")
for c, n in cover.most_common():
    print(f"  {c:20} {n}")
print(f"  总题数: {len(T)}")

# 按优先级分配主簇（每道题只归一个主簇）
main = Counter()
unassigned = []
for t in T:
    hits = hit_clusters(t)
    assigned = None
    for p in PRIORITY:
        if p in hits:
            assigned = p
            break
    if assigned is None and "calc" in hits:
        assigned = "calc"
    if assigned is None:
        assigned = "other"
        unassigned.append(t)
    main[assigned] += 1
    t["cluster"] = assigned

print("\n=== 主簇分配（每道题唯一主簇，按优先级）===")
for c, n in main.most_common():
    print(f"  {c:20} {n}")

print(f"\n未命中任何关键词（other）: {len(unassigned)} 题，抽样：")
for t in unassigned[:12]:
    print(f"  {t['id']:10} [{t['taskType']:8}] {t['question'][:55].replace(chr(10),' ')}")

# 导出带 cluster 的文件
json.dump(T, open(r"d:\github\CogniThink-Chem\_evalscratch\a_class_clustered.json","w",encoding="utf-8"),
          ensure_ascii=False, indent=1)
print("\n写出 a_class_clustered.json（每题带 cluster 字段）")
