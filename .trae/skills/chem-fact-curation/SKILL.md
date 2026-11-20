---
name: chem-fact-curation
description: 在 chem-engine 红线内逐批追加 JSON 化学事实，解出原本未解的单选题并保持零误伤。用户要求继续批次策展、攻 SVG 硬骨头或提升题库覆盖率时调用。不用于修改引擎代码、判分逻辑或处理多选/曲线/填空题。
---

# Chem Fact Curation（化学事实批次策展）

在不修改引擎代码的前提下，向 `data/highschool/general-choice-facts.json` 增量追加化学事实，让 `ConceptChoiceValidator` 解出原本返回 null 的单选题。每批 +3，全程保持 precision=1、harmCount=0、LOST=0。

## 硬红线（不可违反）

- **precision=1**（零假阳性）、**harmCount=0**（零错解）、**LOST=0**（零回归）。
- **不蒙数**：某题 4 个选项无法唯一判定真假（出现两项同真或同假、或化学推理与答案键冲突）→ 跳过该题，绝不为凑数写事实。
- 化学事实**只存 JSON**（`data/highschool/*.json`），不硬编码进逻辑。
- 事实库**必须用 Edit 工具手动增量追加**，禁止 `json.dump` 重写（会格式漂移）。追加前给原末条补尾逗号。
- **不修改** `MultiSelectChoiceValidator` / `FactChoiceMatcher` 的 WRONG 正则。
- 用户未要求 commit，**不要主动 commit**。
- Concept 事实**不得**与 Experiment / Organic / Structure 验证器断言同一题（`Concept_NoOverlap` 测试是硬门）。

## 每批流程（8 步）

1. **核状态**：跑 `check_json_count.py` 确认事实总数与末条 ID；确认上一批 `after_svg_batch<N-1>` 快照；跑 `base_b<N>` eval，与上一批 `cmp` 必须 LOST=[] GAINED=[]（零漂移）。
2. **生成候选池**：运行 `scripts/unsolved_pool.py <tag>`（tag 取上一批 after 快照），从快照读 6 个列表，输出未解单选题；`decided=True` 即 Concept 返回 null → 可安全加事实。
3. **分诊**：排除 [不可解清单](references/batch-playbook.md#不可解清单)；优先选安全域（电化学/热化学/动力学/沉淀溶解/酸碱/元素推断周期律/气体定律/化工流程/价类图/NA）；避开有机/晶体结构图/实验装置（会与 Organic/Structure/Experiment 重叠）。
4. **独立核实 4-verdict**：用 dbg 脚本取真实 stem/option 文本（C# 对齐 norm + 码点转义）；有图就读图（Read PNG，路径以题库 `images` 字段为准）；从结构图 + stem 约束**独立推出**化学，**不要信**"千问求解"note；逐选项判真假，核对答案键恰好等于唯一候选。冲突/歧义 → 跳过。
5. **预检**：grep 事实库确认该题 stemMatch 特征无已有事实覆盖（防撞车）；跑 `screen_overlap.py <qids>` 确认 experiment/organic/structure 的 stemMatch 均不命中。
6. **探针**：复制 `scripts/probe_template.py` → `_evalscratch/probe_b<N>.py`，填 `QS`、`drafts`（4 选项×题数）、`tgt`，运行；**必须 TOTAL PROBLEMS=0**（每条事实命中唯一目标、无 stray、决策与答案键一致、每选项恰好命中 1 条）。
7. **落库**：Edit 追加事实（4 空格缩进单行，给原末条补尾逗号）；`ConceptTruth` 字典加对应行。
8. **验证门**：JSON 校验 → stale-DLL 仪式 → build → **Batch3 全绿**（NoOverlap + Coverage + 零误伤）→ Chem 905/0 → eval `after_svg_batch<N>` → `cmp base_b<N> after_svg_batch<N>`，必须 LOST=[]、prec=1、harm=0、GAINED 恰为新落库的题。

## 关键文件与参考

- [references/batch-playbook.md](references/batch-playbook.md) — C# 归一化字节级规范、WRONG 正则、匹配器语义、命令速查、不可解清单、错误与规避。
- 工作区已有辅助脚本直接复用：`_evalscratch/check_json_count.py`、`cmp.py`、`screen_overlap.py`、`show_tail.py`、`inspect_one.py`。

## 本 skill 自带脚本

- `scripts/probe_template.py` — 探针骨架（C# 对齐 norm + stray-hit 检查 + 决策模拟）。每批复制为 `_evalscratch/probe_b<N>.py` 后改 `drafts`/`QS`/`tgt`。
- `scripts/unsolved_pool.py <tag>` — 从 `_evalscratch/snapshot_<tag>.json` 生成未解单选候选池，写到 `_evalscratch/unsolved_<tag>_out.txt`。

## 下一个可用事实 ID

落库前先跑 `check_json_count.py` 看真实末条 ID（例如末条 `GC-744-D` → 下一批从 `GC-745` 起）。ConceptTruth 下一行接在当前末条之后。
