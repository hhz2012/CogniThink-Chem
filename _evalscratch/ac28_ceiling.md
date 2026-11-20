# AC-28 覆盖评估留痕（structure_verify / titration）

评估基线：`_evalscratch/after_stage6_eq.json`（阶段 6 至 equilibrium 后的全量 723 题逻辑推导快照，已通过 `verify_stage6_eq.ps1` 全红线）。
取证：`_evalscratch/engine_dump_v3.json`（真实 `CallEngineAsync` + 真实采纳门控 + `IsAnswerCorrect`，无 LLM）。

## 1. structure_verify 作用域

全量 723 题中 `taskType=structure_verify` 共 **3 题**：

| id | 题干 | 答案键 | 引擎状态 | 说明 |
|----|------|--------|----------|------|
| pdf-199 | （分子结构题，含 molecule 资产） | — | ✅ 已解 | 经 molecule 资产分支（`TryRouteMolecule` → RDKit 结构特征）命中，`engineUsedEngine=true`、`gatewayCorrect=true`，`delta=unchanged` |
| d7-3 | 异丁烷（2-甲基丙烷）有几种等效氢？ | `2`、`两种` | ❌ 未解 | 需文本→结构 + 等效氢对称性分析 |
| d7-4 | CH3CH(OH)COOH 有没有手性碳？ | `有`、`是`、`手性` | ❌ 未解 | 需文本→结构 + 手性中心检测 |

d7-3 / d7-4 在 dump 中均为 `entryTaskType=structure_verify`、`newCorrect=false`、`delta=unchanged`，且 `pureLlmCorrect=true`（LLM 已判正）。

### 可解性分析

两题均需两步：
1. **文本 → 分子结构解析**：d7-3 由中文名「异丁烷」、d7-4 由缩简式「CH3CH(OH)COOH」还原为带连接关系的分子图（SMILES）。
2. **结构性质计算**：d7-3 对称性分析 → 等效氢组数；d7-4 手性中心检测 → 是否存在连 4 个不同基团的碳。

**现有代码能力盘点**（`src/` grep 确认）：
- `LlmGatewayService.CallEngineAsync` **无 `structure_verify` 分发处理器**——`structure_verify` 仅出现在 `ClassifyTaskDeterministic`（关键词「结构式/电子式/价键」），引擎调用链走兜底 `Ok=null`。
- 无文本→结构解析器（中文名 / 缩简式 → SMILES）；`MoleculeAnalyzer`（RDKit）只消费已有的 molecule 资产（来自图像 OCR 的 SMILES），不消费题干文本。
- 无等效氢计数 / 手性碳检测的暴露接口。

### 为何本阶段不扩展（诚实留痕）

- **harm 风险**：文本→结构解析（任意中文命名 / 缩简式 → SMILES）对非平凡题干 fragile，一旦结构还原错误即答案错误 → 违反冻结红线「不新增会引入 harm 的低质量求解器」。d7-3/d7-4 虽可特例命中，但通用化不可靠；特例硬编码又违反「事实在 JSON、零硬编码化学」。
- **收益/风险比低**：d7-3/d7-4 的 `pureLlmCorrect` 已为 True（LLM 已判正），引擎化仅使 `engineSolved` +2，`gatewayCorrect` 不增（无准确率收益），却要承担 harm 风险。
- **范围**：pdf 题库中「手性」42 处、「等效氢」10 处多落在 `choice` 等其它 taskType（由 `CallChoice` 处理，属 Task 27 范畴），独立 `structure_verify` 作用域仅 3 题，ROI 不足以支撑新建解析器。
- 通用化路径（RDKit 对称性 + 手性 + 健壮名称/缩简式解析器）属后续语境规则扩展任务，需先建立零假阳性护栏，非本低优先级任务可安全落地项。

## 2. titration 作用域

全量 723 题中 `taskType=titration` 共 **0 题**。pdf 文本中「滴定/标定」55 处均落在 `choice`/`calculation` 等 taskType，不存在独立 `titration` 作用域的候选 → **数据天花板**，无候选可引擎化。

## 3. 结论（AC-28 天花板条款）

按 AC-28 / TR-28.1 ②：「数据量/可规则化程度不足以引擎化」→ 留痕即达标，不强行扩展。

- structure_verify：已解 1/3（pdf-199）保持不变；d7-3/d7-4 保持 `pureLlmCorrect=true` 且**不采纳**（`gatewayTruthAdopted=false`）→ 零 harm、零回归。
- titration：评估集 0 题 → 数据天花板。
- 未新增任何求解器 / 分类分支 → 由构造保证 `LOST=[]`、`harm 增量=0`、`precision=1.0` 不下降。

本任务（AC-28）以**评估留痕**方式达标，无代码变更、无回归风险。
