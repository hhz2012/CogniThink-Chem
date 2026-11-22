# Knowledge-Point Gap Report (auto-generated, CANDIDATE gaps)

> Generated: `2026-11-22T06:59:32.316823+00:00`  
> Generator: `tools/data-assets/gap_report.py`  
> Knowledge points: **50** (tier=Main: **44**)  
> Fact files scanned: **24** (`data/highschool/*.json`)  
> Questions scanned: **675** (pdf_questions + benchmark)  

## ⚠️ Honesty disclaimer — read before trusting this report

- There is **no `knowledge_id` foreign key** linking facts or questions to
  knowledge points (adding one is out of scope for P3-1). Coverage is inferred
  by name / textbookRef substring matching against fact-entry and question text.
- A "no-fact" / "no-question" label is a **CANDIDATE gap**, not a definitive
  one. A fact may cover this knowledge point under different phrasing
  (false negative possible). **Every gap here needs human review.**
- This report is a **diagnostic tool only**. It does not enter the eval
  pipeline and does not change engine behavior — **zero eval impact**.
- Two matchers are shown (STRICT keywords are always a subset of LENIENT):
  - **STRICT** (drives the gap label): full `name` / `textbookRef` substring —
    high precision, lower recall.
  - **LENIENT** (context only): STRICT plus ≥2-char tokens from splitting
    name/textbookRef on generic delimiters (与和及的其·标点等) — higher recall,
    lower precision, indicates *likely* coverage under different phrasing.
  - Lexical variants (e.g. 周期律 vs 周期表) are NOT matched by either →
    possible false negatives even in LENIENT. Hence every gap is CANDIDATE.

## Summary (labels assigned by STRICT matcher)

- Total knowledge points: **50** (Main: 44)
- Covered by both fact + question (strict): **12**
- Candidate **no-fact** (strict; has question): **4**
- Candidate **no-question** (strict; has fact): **0**
- Candidate **no-fact-and-no-question** (strict): **34**

How to read a row: if `fact(strict)=0` but `fact(lenient)>0`, the gap is likely
a phrasing false-negative (lower priority); if both are 0 it is a stronger
candidate gap. Either way, human review is required.

## All knowledge points

| id | name | module | tier | fact(strict) | fact(lenient) | q(strict) | q(lenient) | label (strict) |
|----|------|--------|------|-------------|---------------|-----------|------------|----------------|
| `KH-IN-01` | 物质分类与胶体 | Inorganic | Main | 0 (0) | 5 | 0 | 32 | no-fact-and-no-question |
| `KH-IN-02` | 离子反应与离子方程式 | Inorganic | Main | 0 (0) | 9 | 0 | 103 | no-fact-and-no-question |
| `KH-IN-03` | 氧化还原基本概念 | Inorganic | Main | 0 (0) | 2 | 0 | 17 | no-fact-and-no-question |
| `KH-IN-04` | 钠及其化合物 | Inorganic | Main | 2 (6) | 11 | 9 | 170 | covered |
| `KH-IN-05` | 镁铝及其化合物 | Inorganic | Main | 0 (0) | 12 | 0 | 153 | no-fact-and-no-question |
| `KH-IN-06` | 铁铜及其化合物 | Inorganic | Main | 0 (0) | 12 | 0 | 154 | no-fact-and-no-question |
| `KH-IN-07` | 氯与卤素 | Inorganic | Main | 0 (0) | 3 | 0 | 56 | no-fact-and-no-question |
| `KH-IN-08` | 硫及其化合物 | Inorganic | Main | 1 (4) | 11 | 6 | 136 | covered |
| `KH-IN-09` | 氮及其化合物 | Inorganic | Main | 1 (4) | 11 | 5 | 136 | covered |
| `KH-IN-10` | 硅与无机非金属材料 | Inorganic | Extended | 0 (0) | 5 | 0 | 18 | no-fact-and-no-question |
| `KH-IN-11` | 金属冶炼与金属材料 | Inorganic | Extended | 0 (0) | 5 | 0 | 17 | no-fact-and-no-question |
| `KH-IN-12` | 化工工艺流程 | Inorganic | Main | 0 (0) | 1 | 0 | 6 | no-fact-and-no-question |
| `KH-IN-13` | 物质的量与化学计量 | Inorganic | Main | 0 (0) | 11 | 0 | 208 | no-fact-and-no-question |
| `KH-OR-01` | 烃：烷、烯、炔与苯 | Organic | Main | 0 (0) | 3 | 0 | 7 | no-fact-and-no-question |
| `KH-OR-02` | 卤代烃 | Organic | Main | 1 (2) | 4 | 7 | 42 | covered |
| `KH-OR-03` | 醇与酚 | Organic | Main | 0 (0) | 3 | 0 | 36 | no-fact-and-no-question |
| `KH-OR-04` | 醛与羧酸 | Organic | Main | 0 (0) | 5 | 0 | 49 | no-fact-and-no-question |
| `KH-OR-05` | 酯与油脂 | Organic | Main | 0 (0) | 6 | 0 | 42 | no-fact-and-no-question |
| `KH-OR-06` | 同分异构体 | Organic | Main | 5 (11) | 7 | 11 | 37 | covered |
| `KH-OR-07` | 有机反应类型 | Organic | Main | 0 (0) | 2 | 0 | 57 | no-fact-and-no-question |
| `KH-OR-08` | 有机合成路线 | Organic | Main | 0 (0) | 2 | 0 | 21 | no-fact-and-no-question |
| `KH-OR-09` | 糖类、蛋白质 | Organic | Main | 1 (1) | 7 | 1 | 37 | covered |
| `KH-OR-10` | 高分子与单体判断 | Organic | Main | 0 (0) | 6 | 0 | 37 | no-fact-and-no-question |
| `KH-RP-01` | 反应热与盖斯定律 | ReactionPrinciples | Main | 0 (0) | 2 | 0 | 58 | no-fact-and-no-question |
| `KH-RP-02` | 化学反应速率 | ReactionPrinciples | Main | 1 (2) | 2 | 13 | 74 | covered |
| `KH-RP-03` | 化学平衡 | ReactionPrinciples | Main | 0 (0) | 1 | 12 | 73 | no-fact |
| `KH-RP-04` | 弱电解质电离 | ReactionPrinciples | Main | 1 (1) | 3 | 1 | 61 | covered |
| `KH-RP-05` | 水的电离与 pH | ReactionPrinciples | Main | 0 (0) | 12 | 0 | 220 | no-fact-and-no-question |
| `KH-RP-06` | 盐类水解 | ReactionPrinciples | Main | 0 (0) | 2 | 3 | 61 | no-fact |
| `KH-RP-07` | 沉淀溶解平衡 | ReactionPrinciples | Main | 1 (6) | 3 | 6 | 64 | covered |
| `KH-RP-08` | 原电池 | ReactionPrinciples | Main | 3 (11) | 3 | 17 | 46 | covered |
| `KH-RP-09` | 电解池 | ReactionPrinciples | Main | 0 (0) | 0 | 13 | 44 | no-fact |
| `KH-RP-10` | 金属腐蚀与防护 | ReactionPrinciples | Main | 0 (0) | 1 | 0 | 40 | no-fact-and-no-question |
| `KH-EX-01` | 基本实验操作 | Experiment | Main | 0 (0) | 2 | 0 | 17 | no-fact-and-no-question |
| `KH-EX-02` | 气体制备与净化 | Experiment | Main | 0 (0) | 3 | 0 | 51 | no-fact-and-no-question |
| `KH-EX-03` | 物质检验与鉴别 | Experiment | Main | 0 (0) | 4 | 0 | 54 | no-fact-and-no-question |
| `KH-EX-04` | 分离与提纯 | Experiment | Main | 0 (0) | 6 | 1 | 49 | no-fact |
| `KH-EX-05` | 定量实验与误差分析 | Experiment | Main | 0 (0) | 3 | 0 | 60 | no-fact-and-no-question |
| `KH-EX-06` | 探究实验与方案评价 | Experiment | Main | 0 (0) | 3 | 0 | 50 | no-fact-and-no-question |
| `KH-EX-07` | 实验安全与环保 | Experiment | Main | 0 (0) | 2 | 0 | 19 | no-fact-and-no-question |
| `KH-EX-08` | 减压蒸馏与色谱简介 | Experiment | Extended | 0 (0) | 2 | 0 | 26 | no-fact-and-no-question |
| `KH-ST-01` | 原子结构与核外电子排布 | Structure | Main | 0 (0) | 2 | 0 | 23 | no-fact-and-no-question |
| `KH-ST-02` | 元素周期律 | Structure | Main | 0 (0) | 1 | 0 | 17 | no-fact-and-no-question |
| `KH-ST-03` | 化学键 | Structure | Main | 3 (6) | 4 | 12 | 61 | covered |
| `KH-ST-04` | 分子空间构型与杂化 | Structure | Main | 0 (0) | 5 | 0 | 104 | no-fact-and-no-question |
| `KH-ST-05` | 分子间作用力与氢键 | Structure | Main | 0 (0) | 6 | 0 | 77 | no-fact-and-no-question |
| `KH-ST-06` | 晶体类型与晶胞计算 | Structure | Main | 0 (0) | 3 | 0 | 37 | no-fact-and-no-question |
| `KH-ST-07` | 配合物 | Structure | Extended | 2 (3) | 4 | 8 | 43 | covered |
| `KH-ST-08` | 波谱简单判读（红外、核磁） | Structure | Extended | 0 (0) | 4 | 0 | 33 | no-fact-and-no-question |
| `KH-HT-01` | 手性碳与对映异构 | Organic | Extended | 0 (0) | 6 | 0 | 73 | no-fact-and-no-question |

## Candidate gaps: no fact coverage (4)

| id | name | module | tier | fact(strict) | fact(lenient) | q(strict) | q(lenient) | lenient verdict |
|----|------|--------|------|-------------|---------------|-----------|------------|-----------------|
| `KH-RP-03` | 化学平衡 | ReactionPrinciples | Main | 0 | 1 | 12 | 73 | likely phrasing FN (lenient fact=1) |
| `KH-RP-06` | 盐类水解 | ReactionPrinciples | Main | 0 | 2 | 3 | 61 | likely phrasing FN (lenient fact=2) |
| `KH-RP-09` | 电解池 | ReactionPrinciples | Main | 0 | 0 | 13 | 44 | strong candidate (lenient 0) |
| `KH-EX-04` | 分离与提纯 | Experiment | Main | 0 | 6 | 1 | 49 | likely phrasing FN (lenient fact=6) |

## Candidate gaps: no question coverage (0)

_none_

## Candidate gaps: no fact AND no question (34)

| id | name | module | tier | fact(strict) | fact(lenient) | q(strict) | q(lenient) | lenient verdict |
|----|------|--------|------|-------------|---------------|-----------|------------|-----------------|
| `KH-IN-01` | 物质分类与胶体 | Inorganic | Main | 0 | 5 | 0 | 32 | likely phrasing FN (lenient fact=5) |
| `KH-IN-02` | 离子反应与离子方程式 | Inorganic | Main | 0 | 9 | 0 | 103 | likely phrasing FN (lenient fact=9) |
| `KH-IN-03` | 氧化还原基本概念 | Inorganic | Main | 0 | 2 | 0 | 17 | likely phrasing FN (lenient fact=2) |
| `KH-IN-05` | 镁铝及其化合物 | Inorganic | Main | 0 | 12 | 0 | 153 | likely phrasing FN (lenient fact=12) |
| `KH-IN-06` | 铁铜及其化合物 | Inorganic | Main | 0 | 12 | 0 | 154 | likely phrasing FN (lenient fact=12) |
| `KH-IN-07` | 氯与卤素 | Inorganic | Main | 0 | 3 | 0 | 56 | likely phrasing FN (lenient fact=3) |
| `KH-IN-10` | 硅与无机非金属材料 | Inorganic | Extended | 0 | 5 | 0 | 18 | likely phrasing FN (lenient fact=5) |
| `KH-IN-11` | 金属冶炼与金属材料 | Inorganic | Extended | 0 | 5 | 0 | 17 | likely phrasing FN (lenient fact=5) |
| `KH-IN-12` | 化工工艺流程 | Inorganic | Main | 0 | 1 | 0 | 6 | likely phrasing FN (lenient fact=1) |
| `KH-IN-13` | 物质的量与化学计量 | Inorganic | Main | 0 | 11 | 0 | 208 | likely phrasing FN (lenient fact=11) |
| `KH-OR-01` | 烃：烷、烯、炔与苯 | Organic | Main | 0 | 3 | 0 | 7 | likely phrasing FN (lenient fact=3) |
| `KH-OR-03` | 醇与酚 | Organic | Main | 0 | 3 | 0 | 36 | likely phrasing FN (lenient fact=3) |
| `KH-OR-04` | 醛与羧酸 | Organic | Main | 0 | 5 | 0 | 49 | likely phrasing FN (lenient fact=5) |
| `KH-OR-05` | 酯与油脂 | Organic | Main | 0 | 6 | 0 | 42 | likely phrasing FN (lenient fact=6) |
| `KH-OR-07` | 有机反应类型 | Organic | Main | 0 | 2 | 0 | 57 | likely phrasing FN (lenient fact=2) |
| `KH-OR-08` | 有机合成路线 | Organic | Main | 0 | 2 | 0 | 21 | likely phrasing FN (lenient fact=2) |
| `KH-OR-10` | 高分子与单体判断 | Organic | Main | 0 | 6 | 0 | 37 | likely phrasing FN (lenient fact=6) |
| `KH-RP-01` | 反应热与盖斯定律 | ReactionPrinciples | Main | 0 | 2 | 0 | 58 | likely phrasing FN (lenient fact=2) |
| `KH-RP-05` | 水的电离与 pH | ReactionPrinciples | Main | 0 | 12 | 0 | 220 | likely phrasing FN (lenient fact=12) |
| `KH-RP-10` | 金属腐蚀与防护 | ReactionPrinciples | Main | 0 | 1 | 0 | 40 | likely phrasing FN (lenient fact=1) |
| `KH-EX-01` | 基本实验操作 | Experiment | Main | 0 | 2 | 0 | 17 | likely phrasing FN (lenient fact=2) |
| `KH-EX-02` | 气体制备与净化 | Experiment | Main | 0 | 3 | 0 | 51 | likely phrasing FN (lenient fact=3) |
| `KH-EX-03` | 物质检验与鉴别 | Experiment | Main | 0 | 4 | 0 | 54 | likely phrasing FN (lenient fact=4) |
| `KH-EX-05` | 定量实验与误差分析 | Experiment | Main | 0 | 3 | 0 | 60 | likely phrasing FN (lenient fact=3) |
| `KH-EX-06` | 探究实验与方案评价 | Experiment | Main | 0 | 3 | 0 | 50 | likely phrasing FN (lenient fact=3) |
| `KH-EX-07` | 实验安全与环保 | Experiment | Main | 0 | 2 | 0 | 19 | likely phrasing FN (lenient fact=2) |
| `KH-EX-08` | 减压蒸馏与色谱简介 | Experiment | Extended | 0 | 2 | 0 | 26 | likely phrasing FN (lenient fact=2) |
| `KH-ST-01` | 原子结构与核外电子排布 | Structure | Main | 0 | 2 | 0 | 23 | likely phrasing FN (lenient fact=2) |
| `KH-ST-02` | 元素周期律 | Structure | Main | 0 | 1 | 0 | 17 | likely phrasing FN (lenient fact=1) |
| `KH-ST-04` | 分子空间构型与杂化 | Structure | Main | 0 | 5 | 0 | 104 | likely phrasing FN (lenient fact=5) |
| `KH-ST-05` | 分子间作用力与氢键 | Structure | Main | 0 | 6 | 0 | 77 | likely phrasing FN (lenient fact=6) |
| `KH-ST-06` | 晶体类型与晶胞计算 | Structure | Main | 0 | 3 | 0 | 37 | likely phrasing FN (lenient fact=3) |
| `KH-ST-08` | 波谱简单判读（红外、核磁） | Structure | Extended | 0 | 4 | 0 | 33 | likely phrasing FN (lenient fact=4) |
| `KH-HT-01` | 手性碳与对映异构 | Organic | Extended | 0 | 6 | 0 | 73 | likely phrasing FN (lenient fact=6) |

## 主干知识点 (tier=Main) focus

- Main KPs: **44**
- Main KPs with candidate **no-fact**: **33**
- Main KPs with candidate **no-question**: **29**

