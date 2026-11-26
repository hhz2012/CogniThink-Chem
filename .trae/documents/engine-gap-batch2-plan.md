# 引擎能力缺口下一轮补强计划（batch2）

> 目标：在守住红线（precision=1 / harm=0 / 引擎 LOST=0 / 不蒙数 / 不硬编码单题）的前提下，从当前「LLM 答对、引擎答错」的剩余题集中，挑出可用**通用确定性规则**低风险翻正的题，扩展引擎覆盖。
> 计划性质：先做可行性 spike 验证，通过才实现；未通过者降级或剔除，绝不强行蒙数。

---

## 1. Repository Research（证据，均来自实际快照，非记忆）

### 1.1 63 题现状（基线 → 本批次后）

证据脚本：[_evalscratch/analyze_leads_current.py](file:///d:/github/CogniThink-Chem/_evalscratch/analyze_leads_current.py)，对比原始 [llm_leads_63.json](file:///d:/github/CogniThink-Chem/_evalscratch/llm_leads_63.json) 与最新快照 [snapshot_gap_batch1.json](file:///d:/github/CogniThink-Chem/_evalscratch/snapshot_gap_batch1.json)。

| 去向 | 题数 | 题号 |
|---|---:|---|
| 引擎已解出（本批次及之前 GAINED） | 10 | d5-1, d5-2, d7-4, pdf-134, pdf-168, pdf-272, pdf-278, pdf-328, pdf-506, trap-limit-3 |
| 仍是「LLM 领先、引擎未解」 | **50** | 见 §1.2 |
| LLM 本轮答错（纯 LLM 波动，引擎仍未解但非当前领先） | 3 | pdf-197, pdf-356, pdf-376 |
| 新增领先 | 0 | — |

> 关键：**0 新增领先** → 当前 50 = 原始 63 的真子集；没有引擎回归引入的新缺口（与 diff_engine LOST=[] 一致）。

### 1.2 当前 50 题分布（taskType × 失败模式）

证据：[_evalscratch/analyze_leads_classify.py](file:///d:/github/CogniThink-Chem/_evalscratch/analyze_leads_classify.py) → [leads_50_current.txt](file:///d:/github/CogniThink-Chem/_evalscratch/leads_50_current.txt)

| taskType | 题数 | 失败模式 |
|---|---:|---|
| choice | 19 | 全部 B（引擎空结论/占位） |
| equation_writer | 8 | 全部 B |
| concept | 7 | 全部 A（设计不走引擎，LLM 回退） |
| calculation | 6 | 全部 B |
| equilibrium | 5 | 全部 B |
| unknown | 4 | 全部 A |
| reaction_check | 1 | B（占位） |

失败模式：**A 类 11**（concept 7 + unknown 4，按设计不路由引擎）／**B 类 39**（引擎未产出真值结论）／**C 类 0**。

> 重要：C 类从 1（pdf-272，上批次已解）降到 **0** → 当前没有「引擎算对但被采纳门误拦」的纯门控修复点，全部是覆盖缺口或设计性回退。

### 1.3 根因重判（在原 63 文档 R1–R4 基础上更新）

| 根因类 | 现状 | 说明 |
|---|---|---|
| R1 路由未触发（已有能力未命中入口） | 集中在 equilibrium/calculation 的复合题 | 引擎有 molecule/stoichiometry/redox 能力，但复合题整题分发失败 → 可用「last-resort 单条子问命中 → OR 采纳」兜底（已验证模式：pdf-168/272/328） |
| R2 能力缺失（无对应求解器） | 窄域算术类 | 相对密度→摩尔质量、滴定质量分数、σ 键计数、文本结构简式→手性 |
| R3 图像/开放依赖 | ~30 题 | 装置图/曲线图/结构简式图/开放论述 → 按红线保持 LLM，**不补** |
| R4 数据缺陷 | pdf-324/337/357 等 | 缺图/资产错配/答案键疑似错配 → 先修数据再谈求解 |

### 1.4 50 题逐题可补全性重评估

详见 [leads_50_current.txt](file:///d:/github/CogniThink-Chem/_evalscratch/leads_50_current.txt)。结论：

- **Tier1（候选，需 spike 验证）**：pdf-169、pdf-024、pdf-098、pdf-146 —— 复合题中存在**一个可确定计算的子问**，且该子问答案在 answerKeywords 中，走 OR 采纳可翻正整题。
- **Tier2/探索（高 FP 或需新能力，本批谨慎）**：pdf-391（醛类同分异构计数=4，需官能团约束异构枚举，现有 `ConstitutionalIsomerCounter` 只覆盖烷烃/烷基）、pdf-458（BCl₃ 键角 120°，choice 关键词精确匹配风险）。
- **数据缺陷（先修数据）**：pdf-337（缺卤代烃结构资产，仅有位图）、pdf-357（molecule 资产名错配为 GA3）、pdf-324（[CH=CH]n 单体答案键「苯乙炔」疑似错配）。
- **不补（保持 LLM/null，~40 题）**：图像依赖（pdf-138/255/284/303/325/336/338/406/498/511/524…）、开放论述（pdf-207/274/163/531…）、复合多空流程（equation_writer 多数）、A 类设计性回退（concept/unknown）。这些是架构性合理分工，非引擎缺陷。

---

## 2. Tier1 候选逐项可行性（spike 前预判）

> 每个候选必须通过 spike：(a) 子问数据齐全可唯一确定；(b) 计算结果命中 answerKeywords；(c) 规则通用、不硬编码单题、数据不全时返回 null。三项全过才进入实现。

### 2.1 pdf-169（equilibrium）—— 文本结构简式 → 手性碳数
- 可计算子问：(1)「H2A=酒石酸，结构简式 `HOOC(CHOH)2COOH`，手性碳原子数目」→ **2**（命中关键词「2」）。
- 现状：taskType=equilibrium，有 graph 资产（流程图）但**无 molecule 资产**；`TryRouteMolecule` 仅对 molecule 资产触发，故未分析；结构在文本里。
- 通用规则：题干含「手性碳/手性碳原子」+ 文本中存在可解析的结构简式 → 解析为 SMILES → `MoleculeAnalyzer.ChiralCenterCount`（RDKit 真值）。
- 风险/难点：`HOOC(CHOH)2COOH` 不是合法 SMILES，需「缩合结构简式 → SMILES」归一化（或名称解析：酒石酸）。解析失败一律 null。**spike 重点**。
- 预期：通过则 +1（OR 采纳翻正整题，子问 2–7 保持未解但不影响）。

### 2.2 pdf-024（calculation）—— 相对密度 → 平均摩尔质量
- 可计算子问：(3)「空气平均分子量 29，瓶内气体对氢气相对密度 9.5 → 平均摩尔质量」→ **2 × 9.5 = 19**（命中关键词「19」）。
- 通用规则：`对氢气的相对密度为 D` → M = 2D（g/mol）；`对空气的相对密度为 D` → M = 29D。纯算术，窄触发。
- 风险：极低（数据齐全、唯一解）。
- 预期：通过则 +1。

### 2.3 pdf-098（calculation）—— σ 键计数
- 可计算子问：(1)「1 mol NH4Cl 含 σ 键的物质的量」→ **4 mol**（命中「4 mol」）。
- 通用规则：给定化学式 → 统计共价 σ 键数（离子键不计，与高中口径一致）。
- 风险/难点：离子化合物（NH4Cl）口径：NH4⁺ 内 4 个 N−H σ 键，Cl⁻ 不计。需确认现有分子分析是否支持；`MoleculeAnalyzer` 未直接暴露 σ 键数，可能需新增。**spike 重点**。
- 预期：通过则 +1。

### 2.4 pdf-146（calculation）—— 氧化还原滴定质量分数
- 可计算子问：(5) 碘量法滴定 SO₂：5×10⁻³ mol/L × 5 mL I₂ → n=2.5×10⁻⁵ mol；SO₂:I₂=1:1 → 50 mL 中 1.6×10⁻³ g → 1000 mL 中 0.032 g / 1000 g 样品 → **0.032 g/kg**（命中「0.032」）。
- 通用规则：滴定剂 c·V + 电子转移比（或反应计量比）+ 取样稀释倍数 → 质量分数。
- 风险：多步（稀释倍数），必须数据齐全才计算；缺任一量 → null。需评估复用现有 redox/stoichiometry 还是新建窄求解器。**spike 重点**。
- 预期：通过则 +1。

### 预期收益
4 个候选若全部通过 spike：**+4**；现实预计 **+2~3**（pdf-024 最稳，pdf-169/098/146 依赖通用解析器质量）。红线保持：LOST=0 / harm=0。

---

## 3. Files and Modules（预计改动；仅 spike 通过者落地）

| 文件 | 预计改动 |
|---|---|
| 新建 `src/.../Gateway/TextChiralCenterSolver.cs`（或扩展现有通路） | pdf-169：缩合结构简式 → SMILES → `MoleculeAnalyzer` 手性碳数。解析失败/无手性碳问法/歧义 → null |
| 新建 `src/.../Gateway/VaporDensityMolarMassSolver.cs` | pdf-024：`对氢气/空气相对密度 D` → M = 2D / 29D |
| 新建 `src/.../Gateway/SigmaBondCountSolver.cs`（或扩展 MoleculeAnalyzer 暴露 σ 键数） | pdf-098：化学式 → 共价 σ 键数（离子键不计） |
| 新建 `src/.../Gateway/TitrationMassFractionSolver.cs`（或扩展现有 stoichiometry） | pdf-146：滴定 c·V + 计量比 + 稀释倍数 → 质量分数 |
| `src/.../Gateway/LlmGatewayService.cs` | 在 last-resort 钩子链（`engineResponse.Ok is not true` 守卫内）按序接入通过验证的求解器；设置对应 taskType（equilibrium/calculation）→ 走 OR 采纳门。**不修改 `ClassifyTaskDeterministic`**（零漂移） |
| `tests/.../` 各求解器测试 + 护栏测试 | 通用规则单测 + **全库 fired 集锁定护栏测试**（断言 fired 仅含目标题且 baseFalse 必判正），防 FP 回退 |
| `data-defect-register.md` | 登记：翻正题、spike 未通过降级项、数据缺陷项（pdf-324/337/357） |

> 已验证的复用资产：[MoleculeAnalyzer.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Molecules/MoleculeAnalyzer.cs)（`ChiralCenterCount`）、[ConstitutionalIsomerCounter.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Molecules/ConstitutionalIsomerCounter.cs)（仅烷烃/烷基，pdf-391 不覆盖）、last-resort OR-gate 钩子模式（pdf-168/272/328 先例）。

---

## 4. Implementation Steps（依赖序）

1. **可行性 spike（不落地生产代码）**：对 pdf-169/024/098/146 各写探针，确认 §2 的 (a)(b)(c)。把证据写入 `_evalscratch/`。未通过的候选**降级或剔除**，不强行实现。
2. **实现通过验证的求解器**：每个求解器带严格 null 守卫；接入 `LlmGatewayService` last-resort 链（`Ok is not true` 守卫，构造性零 LOST）。
3. **单测 + 护栏**：每个新求解器配通用规则单测 + 全库 fired 集锁定护栏测试；`dotnet test -c Release` 全绿。
4. **全量回归**：仅在所有候选实现完毕后跑（tag=gap_batch2）；用 `diff_engine` / `verify_mp27` 确认 LOST=[]、harm=[]、27/27；`dotnet test -c Release` 全绿。
5. **登记**：更新 `data-defect-register.md`；翻正题、诚实保持 False 题、降级题逐项登记。

---

## 5. Dependencies and Considerations

- **红线不可破**：precision=1、harm=0、引擎 LOST=0；无唯一解不蒙数（返回 null）；不硬编码单题（必须通用规则/事实）。
- **采用门**：calculation/equilibrium 走 `IsAnswerCorrect`（OR）→ 复合题只需一个正确子问命中即翻正，这是 pdf-168/272/328 已验证的合规模式。
- **选择题不短路**：`TryRouteMolecule` 既有约束保留，避免分子特征文本替换选项答案；choice 类候选（pdf-458）因需驱动选项字母，本批谨慎。
- **不提前跑全量回归**：所有候选实现完毕才跑（项目纪律）。
- **A 类（concept/unknown，11 题）**：按既定架构不路由引擎，本批不改路由（改路由漂移面大、风险高），保持 LLM。
- **PowerShell 5**：不用 `&&`/`||`；临时探针 `.cs` 用完即删，证据留 `_evalscratch/`（已 gitignore）。

---

## 6. Validation

- 每个候选：全库 dry-fire，断言 fired 集 = {目标题} 且 fired 题在基线上为 baseFalse、求解后 `IsAnswerCorrect`=True（零假阳性）。
- 护栏单测锁定 fired 集，防未来 FP 回退。
- 全量回归：`diff_engine after_vsepr509 gap_batch2`（或 gap_batch1→gap_batch2）→ LOST=[] 且 harm=[]；`verify_mp27` 27/27；`dotnet test -c Release` 0 failed。
- 诚实登记：spike 未通过 / 数据不全的题保持 False，不蒙数。

---

## 7. Risks

| 风险 | 处置 |
|---|---|
| pdf-169 缩合结构简式（`HOOC(CHOH)2COOH`）非标准 SMILES，通用性不足 | 建归一化器；无法解析 → null。若通用性差（只覆盖极少数），降级不做 |
| pdf-098 离子化合物 σ 键口径（离子键是否计入）与高中不符 → 假阳性 | 严格按「共价 σ 键」口径；单元测试锁定 NH4Cl=4 等教材值；口径存疑则不做 |
| pdf-146 滴定多步（稀释倍数）数据缺失时误算 | 缺任一量（c/V/计量比/稀释比/样品质量）→ null；spike 验证数据齐全性 |
| 复合题 OR 翻正是否合规 | 已有 pdf-168/272/328 先例且 harm=0 兜底；只输出确定正确的子问答案 |
| spike 后候选大面积不通过 → 收益低于预期 | 诚实降级，登记数据缺陷/延后；不为凑数硬做（红线优先于收益） |
