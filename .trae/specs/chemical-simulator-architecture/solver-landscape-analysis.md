# 求解器版图分类分析报告

> 目的：回答"化学模拟器能否概括所有求解需求"，并给出**其余求解过程是否需要其他模拟器**的分类结论与迁移路线。
> 依据：[object-responsibility-map.md](file:///d:/github/CogniThink-Chem/.trae/specs/chemical-simulator-architecture/object-responsibility-map.md) 的对象清单 + 源码摘要注释勘察。
> 范围：`src/CogniThink.TutorBrain.Api/Application/Gateway/`（71 文件）与 `src/CogniThink.Chem.Core/Inference|Solving|Rules|Molecules|Balancing`。
> 本轮**只读分析**，不修改任何代码或数据。

---

## 0. 判定标准

按"计算范式"而非目录划分：

| 类 | 定义 | 是否纳入模拟器体系 |
|---|---|---|
| **A** | 本质是"**反应 + 状态 + 多步推断**"——给定物质/条件，推演会发生什么、状态如何变化、或从现象反推路径 | 可由 `ChemicalSimulator` + `ChemicalMultiStepReasoningSimulator` **承载或作为上层查询** |
| **B** | 非反应状态范式——**离散结构枚举/计数**、**守恒代数配平**、**纯数值/能量计算**、**图像数据提取** | **需要独立模拟器/计算器**，不能塞进化学模拟器 |
| **C** | 不产生反应状态——**事实库匹配、真伪判定、判分聚合** | **保持现状**，不纳入模拟器体系 |

> 核心区分：A/B 是**真值源**（产出结论，守 precision=1/不蒙数），C 是**判定层**（依赖真值源或静态事实，不产出新化学事实）。

---

## 1. A 类：可组合到新模拟器之上

### A-1 推断类引擎（Core/Inference）— 可由"模拟器 + 多步推理"承载

这些引擎面向"题目类型"，但底层都是"反应推演 + 状态演化 + 反推验证"，与新架构范式同构。迁入方式：**底层反应推演委托 `ChemicalSimulator`，上层推断编排（未知物搜索/分支/验证）委托 `ChemicalMultiStepReasoningSimulator`**，而非把整段代码塞进模拟器。

| 对象 | 文件 | 职责 | 迁入方式 | 风险 |
|---|---|---|---|---|
| `ProcessFlowEngine` | Core/Inference | 工艺流程推断：多步转化链中各物质推断 | 多步推理的 `Reason`（轨迹）+ `Search`（反推未知物） | 中：需保留其特定推断启发式作为 Search 的排序信号 |
| `ProcessInferenceEngine` | Core/Inference | 过程推断：步骤现象→中间产物 | 同上 | 中 |
| `ExperimentEngine` | Core/Inference | 实验推断：试剂+现象→物质鉴别 | `Search`（目标=鉴别现象）+ 验证 | 中 |
| `OrganicInferenceEngine` | Core/Inference | 有机推断：官能团转化链 | `Reason` 轨迹（有机反应）+ `Search` | 中：有机反应库覆盖度依赖 KB |
| `IdentificationEngine` | Core/Inference | 物质鉴别：特征反应区分一组物质 | `WhatIf`（对每种候选加试剂）+ 比较 | 低 |
| `ProductInferenceEngine` | Core/Inference | 产物推断：反应物+条件→可能产物 | 直接用 `ChemicalSimulator` 正向推演 | 低 |
| `ReactionMatcher` | Core/Inference | 给定物质对匹配 KB 反应 | `ChemicalSimulator` 候选收集（`ReturnConsidered`） | 低 |

> 这 7 个是新模拟器**最直接的受益方**，迁入后可消除重复的反应筛选逻辑。

### A-2 平衡/溶液类引擎（Core/Inference）— 定性可承载，数值由本期 `EquilibriumStateSimulator` 承载

| 对象 | 职责 | 迁入方式 | 说明 |
|---|---|---|---|
| `EquilibriumEngine` | 平衡移动：体系+扰动→移动方向 | 移动**方向**可用模拟器+规则判定 | **浓度数值**由本期 `EquilibriumStateSimulator` 承载（见设计 §7） |
| `AqueousEquilibriumEngine` | 水溶液粒子浓度/三大守恒 | 守恒**关系**可用规则；`EquilibriumStateSimulator` 委托其排序/守恒 | **浓度/pH 数值**由本期 `EquilibriumStateSimulator` 承载（Core 引擎不输出绝对数值，由本模拟器补全） |
| `CoexistenceEngine` | 离子共存（沉淀/气体/弱电解质/氧化还原/络合） | **反应性部分**（沉淀/气体生成）= `ChemicalSimulator` 正向推演；氧化还原/络合冲突 = 规则 | 反应性冲突可迁入模拟器，纯规则冲突保持 |
| `IonizationMap` | 酸碱分步电离物种分布 | 模拟器多步推演（逐级电离）+ 条件门控 | 分布**比例数值**由本期 `EquilibriumStateSimulator` 承载（Ka 已知时） |

### A-3 Gateway 窄域求解器中"反应预测/状态"类

| 对象 | 文件 | 职责 | 迁入方式 | 风险 |
|---|---|---|---|---|
| `KspPhResidualSolver` | Gateway | 沉淀平衡残留离子浓度计算 | 沉淀生成 = 模拟器推演；**残留浓度数值**由本期 `EquilibriumStateSimulator` 承载（本期不替换 Gateway 原文件） | 中：数值部分本期新增并行能力，原文件不动 |
| `ProductWriterSolver` | Gateway | 限量离子方程式/产物书写 | `ChemicalSimulator` 候选 + 限量试剂 | 低 |
| `KnownEquationWriterSolver` | Gateway | 已知方程式书写 | 模拟器输出 `Equation` 字段 | 低 |
| `ReactionKbWriterSolver` | Gateway | 反应知识库方程书写 | 同上 | 低 |
| `AcidFirstIonizationSolver` | Gateway | 弱酸第一步电离方程式 | 模拟器推演（门控只取第一步） | 低 |
| `HydrolysisEquationSolver` | Gateway | 水解方程式（除杂语境） | 模拟器推演水解反应 | 低 |
| `EsterificationEquationSolver` | Gateway | 酯化方程式 | 模拟器推演酯化反应 | 低 |
| `EsterificationProductSolver` | Gateway | 酯化产物 | 同上 | 低 |
| `HydrolysisPhCalculator` | Gateway | 水解 pH 计算 | 水解**反应**可迁；**pH 数值**由本期 `EquilibriumStateSimulator` 承载（算法口径收敛，原文件不动） | 中 |
| `PhOrderingSolver` | Gateway | pH 排序 | **定性比较**可用模拟器+规则；绝对 pH 数值由本期 `EquilibriumStateSimulator` 承载 | 中 |
| `CatalystSolver` | Gateway | 多步反应中催化剂识别 | 天然是多步推理的一个**查询**：在轨迹中找"前后量不变且参与中间步骤"的物质 | 低 |
| `ConceptFactSolver` | Gateway | 概念事实求解（事实库） | 偏事实查询，更接近 C 类；若涉及反应性可查模拟器 | 低 |

**A 类小结**：约 20 个对象可被新模拟器承载或部分承载。其中**纯反应预测/产物书写/鉴别**类（低风险）可优先迁入；**涉及平衡浓度/pH 数值**的部分由本期第三个主体类 `EquilibriumStateSimulator` 承载（详见 [chemical-simulator-design.md §7](file:///d:/github/CogniThink-Chem/.trae/specs/chemical-simulator-architecture/chemical-simulator-design.md)），本 spec 覆盖。

---

## 2. B 类：需要独立模拟器（非反应状态范式）

这些对象的计算空间不是"物质体系随反应演化"，强行塞进化学模拟器会扭曲其模型。应保持为**独立计算器/模拟器**，或在后续归并为几个独立主体。

### B-1 分子结构模拟器（建议独立主体：`MolecularStructureSimulator`）

离散结构的枚举/计数/构型判定，输入是分子式/结构图，输出是计数或构型。

| 对象 | 文件 | 职责 | 建议归属 |
|---|---|---|---|
| `CrystalCellValidator` | Gateway | 晶胞位点/分数坐标/均摊计数（结构化资产 + 通用规则） | 晶胞子域（**晶胞模拟器**，可独立） |
| `CountingChoiceValidator`（计数部分） | Gateway | 同分异构体/取代物计数（构造异构枚举） | 分子结构模拟器 |
| `ConstitutionalIsomerCounter` | Core/Molecules | 构造异构体计数 | 分子结构模拟器 |
| `TextChiralCenterSolver` | Gateway | 手性碳原子数计数 | 分子结构模拟器 |
| `SigmaBondCountSolver` | Gateway | σ 键数计数 | 分子结构模拟器 |
| `VseprSolver` | Gateway | VSEPR 构型 / 键角比较原因 | 分子构型子域（价层电子对互斥） |
| `MoleculeAnalyzer` / `MoleculeNamer` | Core/Molecules | 分子结构分析 / 命名 | 分子结构模拟器（基础设施） |
| `AtomicStructureSolver` | Gateway | 原子结构（电子排布等） | 原子结构子域（可独立小计算器） |
| `Carbonyl18ElectronSolver` | Gateway | 金属羰基配合物 18 电子规则计数 | 电子计数子域 |

> 注：`VseprSolver` 已在项目内验证为确定性求解器（mp27 27/27），迁移时必须保持其行为，仅做"归位"不重写。

### B-2 方程配平/判定模拟器（建议独立主体：`EquationBalancer`，Core 已有雏形）

守恒代数运算（原子/电子/电荷守恒），不是状态推演。

| 对象 | 文件 | 职责 | 建议归属 |
|---|---|---|---|
| `EquationBalancer` | Core/Balancing | 通用方程式配平（原子守恒代数） | 配平模拟器（已有，保持） |
| `RedoxBalancer` | Core/Balancing | 氧化还原配平（半反应法、电子守恒） | 同上 |
| `OxidationStateAnalyzer` | Core/Balancing | 氧化态赋值与变化分析 | 配平模拟器的子能力 |
| `HalfReactionBalancer` | Gateway | 半反应配平（题干给氧化/还原对与介质时触发） | 委托 Core `RedoxBalancer`，去重 |
| `BalanceGatewaySolver` | Gateway | 配平网关入口 | 委托 Core 配平器 |

> 与化学模拟器的关系：模拟器**消费**已配平的方程式（`ReactionEquationParser` 从 `reactions.json` 读取已入库校验的系数），**不负责**配平；配平由独立配平器在数据入库/校验时完成。两者解耦。

### B-3 计量/组成计算器（建议独立主体：`StoichiometryCalculator`，Core 已有 `MoleCalculator`）

纯数值计算，不推演反应路径。

| 对象 | 文件 | 职责 | 建议归属 |
|---|---|---|---|
| `CombustionEmpiricalFormulaSolver` | Gateway | 燃烧法 → 实验式/分子式 | 计量计算器 |
| `VaporDensityMolarMassSolver` | Gateway | 相对密度 → 平均摩尔质量 | 计量计算器 |
| `IodometryMassFractionSolver` | Gateway | 碘量法 → 质量分数 | 滴定计算子域 |
| `Batch2NarrowCalcSolver` | Gateway | 批次窄域计算 | 计量计算器 |
| `MoleCalculator` | Core/Stoichiometry | 物质的量计算（底层工具） | 计量计算器基础设施 |

### B-4 热化学/能量计算器（建议独立主体：`ThermochemistryCalculator`）

能量计算，不是反应状态。

| 对象 | 文件 | 职责 | 建议归属 |
|---|---|---|---|
| `BondEnergySolver` | Gateway | 键能计算反应焓 | 热化学计算器 |
| `ThermochemicalHessSolver` | Gateway | Hess 定律反应焓加和 | 同上 |
| `GibbsSpontaneitySolver` | Gateway | 吉布斯自由能 / 自发性判据 | 同上 |

### B-5 曲线/图像读图引擎（建议独立主体：`CurveReadingEngine`）

从结构化曲线资产提取数据点，不是化学推演。

| 对象 | 文件 | 职责 | 建议归属 |
|---|---|---|---|
| `CurveChoiceValidator`（数据提取部分） | Gateway | 滴定/溶解度曲线读图 | 曲线读图引擎 |
| `GraphSolver` | Gateway | 图/曲线求解 | 同上 |
| `CurveIntentExtractor` | Gateway | 曲线意图提取（从资产取数据） | 同上（基础设施） |

### B-6 【已提升为本轮范围】平衡态数值模拟器（`EquilibriumStateSimulator`）

A-2 / A-3 中标注"数值部分"的对象（`EquilibriumEngine` 浓度数值、`AqueousEquilibriumEngine` 粒子浓度、`KspPhResidualSolver` 残留浓度、`HydrolysisPhCalculator`/`PhOrderingSolver` 的 pH 数值）共同需要一个**平衡态数值求解**能力。经用户决策（`spec.md` D-5），该项**从"后续待立项"提升为本轮设计范围**：作为第三个主体类 `EquilibriumStateSimulator`，基于 `acid-constants.json`（Ka/Kb/Ksp/Kw）实现，详见 [chemical-simulator-design.md §7](file:///d:/github/CogniThink-Chem/.trae/specs/chemical-simulator-architecture/chemical-simulator-design.md)。

> 说明：B-6 原列于 B 类，是因为它的计算空间（常数/浓度/pH）与反应状态推演解耦；用户要求本期一并设计，故提升。它仍是与 `ChemicalSimulator` 解耦的独立主体（不并入反应主循环），仅通过多步推理层按需组合（`ReasoningStep.Numerical` → `StateSnapshot.Numerical`）。

**B 类小结**：约 25 个对象需要独立模拟器/计算器。它们与化学模拟器**解耦**：化学模拟器只管"反应状态推演"，结构/配平/计量/热化学/曲线各自有独立计算空间。

---

## 3. C 类：非模拟器（判定/匹配/聚合，保持现状）

不产生反应状态，结论依赖真值源（A/B）或静态事实 JSON。**不纳入模拟器体系，保持现有架构**。

| 子类 | 代表对象（文件） | 职责 | 为何非模拟器 |
|---|---|---|---|
| 事实/选择匹配 | `ConceptChoiceValidator`、`FactChoiceMatcher`、`ConceptVerdictSolver`、`NaChoiceValidator`、`ClassificationValidator`、`MultiSelectChoiceValidator`、`EquilibriumChoiceValidator`、`ExperimentChoiceValidator`、`MoleculeChoiceValidator`、`OrganicChoiceValidator`、`CurveChoiceValidator`（校验部分） | 在事实库中匹配选项真伪 | 只查静态事实，不推演状态 |
| 选择校验内核 | `ChoiceVerifier` | 选择题统一校验内核 | 聚合调用各 validator，非真值源 |
| 陈述判定原语 | `EquationCorrectnessJudge`（调用 `ChemicalCheckerService`）、`CompositeStatementJudge`、`FactBaseStatementJudge`、`CountingStatementJudge`、`RedoxElectronStatementJudge` | 分解陈述→原子断言→判定→聚合 | 判定层，依赖真值源 |
| 判分聚合 | `JudgmentStatementAggregator`、`MultiSubCorrectnessSolver` | 多子问/多陈述判分聚合 | 聚合，不产出化学事实 |
| 规则/护栏 | `ImpurityTrapSolver`、`CorrectnessContextRules`、`InfeasiblePattern`、`IonConflictRule`、`IonSolutionContext`、`ConflictScanner`、`RuleSetValidator`、`WhitelistGuard`、`IonicEquationChecker`（Core） | 规则判定与正确性护栏 | 规则匹配，非模拟 |
| 真值源判定（特殊） | `ChemicalCheckerService`（Core） | 配平/守恒/离子规则的确定性校验 | **是真值源**但范式是"校验"而非"模拟"，保持独立 |
| 推理链编排 | `ChemistryReasoningEngine`（Api/Reasoning） | 网关推理链 `IReasoningEngine` 实现 | 编排多引擎输出，非单一真值源 |

> `ChemicalCheckerService` 是 C 类中唯一的真值源：它做"方程/守恒校验"，产出 `CheckResult`。它与化学模拟器的关系是**协作**（模拟器产出的事件可交给它二次校验），但它本身不是模拟器，保持独立。

**C 类小结**：约 25 个对象保持现状。它们是判定/聚合层，未来接入新模拟器时由它们来"消费"模拟器结果（如把模拟器产出的反应事件交给 `ChemicalCheckerService` 校验、交给 `ChoiceVerifier` 判选项），而非被模拟器取代。

---

## 4. 边界模糊项的处置

| 对象 | 模糊点 | 处置 |
|---|---|---|
| `KspPhResidualSolver` | 既有沉淀反应（A）又有浓度数值（B-6） | 反应性部分迁入模拟器；浓度数值由本期 `EquilibriumStateSimulator` 承载（本期新增并行能力，Gateway 原文件保留，后续归并） |
| `CoexistenceEngine` | 反应性冲突（A）vs 纯规则冲突（C） | 反应性（沉淀/气体）用模拟器；氧化还原/络合冲突保持规则层 |
| `CurveChoiceValidator` | 数据提取（B-5）vs 选项校验（C） | 拆分：提取归曲线读图引擎，校验归 C 类 |
| `CountingChoiceValidator` | 计数真值（B-1）vs 选择判分（C） | 计数逻辑归分子结构模拟器；判分外壳保持 C 类 |
| `ConceptFactSolver` | 概念事实（C）vs 可能的反应性查询 | 主体保持 C；如需反应性可查模拟器，不强制 |

---

## 5. 推荐迁移顺序（分阶段，风险递增）

### 阶段 0（本轮）：不迁移，只产出设计
- 交付化学模拟器详细设计 + 本分析报告。
- **不触碰任何现有求解器代码**。

### 阶段 1（低风险，优先）：三个主体类落地 + 纯反应预测类迁入
- 编码实现 `ChemicalSimulator`（吸收合并）+ `ChemicalMultiStepReasoningSimulator` + **`EquilibriumStateSimulator`**（平衡态数值，见设计 §7 与 §11 任务 5/6）。
- 迁入 A 类中**低风险**的纯反应预测/产物书写：`ProductWriterSolver`、`KnownEquationWriterSolver`、`ReactionKbWriterSolver`、`AcidFirstIonizationSolver`、`HydrolysisEquationSolver`、`Esterification*Solver`、`ProductInferenceEngine`、`ReactionMatcher`、`CatalystSolver`。
- 验证：每迁入一个跑该题域 eval，确认 GAINED≥0、LOST=0。
- **红线**：迁入前该求解器未解（返回 null）的题目，迁入后仍未解不视为回归；但已解的题目不能翻转。

### 阶段 2（中风险）：推断类引擎迁入
- 迁入 `ProcessFlowEngine` / `ProcessInferenceEngine` / `ExperimentEngine` / `OrganicInferenceEngine` / `IdentificationEngine`，用多步推理模拟器的轨迹 + Search 承载。
- 风险：推断启发式需保留为 Search 排序信号；有机反应库覆盖度依赖 KB。
- 每个引擎独立迁移 + 独立验证，不批量。

### 阶段 3（中风险）：平衡/溶液数值类归并
- `EquilibriumStateSimulator` 已在阶段 1 实现；本阶段把 `EquilibriumEngine`（数值）、`AqueousEquilibriumEngine`、`KspPhResidualSolver`（浓度）、`HydrolysisPhCalculator`、`PhOrderingSolver` 的数值口径逐步归并到统一入口（**Gateway 原文件先不动，行为等价后再切换**）。
- **本阶段前，这些对象的数值部分保持原求解器不动**，新模拟器作为并行能力存在。

### 阶段 4（独立归位，低风险）：B 类独立模拟器整理
- 把 B-1 ~ B-5 中散落在 Gateway 的窄域求解器归位到对应独立主体（分子结构模拟器、配平器、计量计算器、热化学计算器、曲线读图引擎），**去重但不重写逻辑**（如 `HalfReactionBalancer` 委托 Core `RedoxBalancer`）。
- 风险低：行为不变，只改调用路径。

### 阶段 5（C 类消费模拟器，持续）：判定层接入
- 让 C 类校验器/匹配器在需要时消费模拟器结果（如 `ChemicalCheckerService` 校验模拟器产出的事件），逐步减少重复的反应筛选。
- 不替换 C 类，只增强其数据源。

---

## 6. 红线风险提示

1. **precision=1 / 不蒙数**：A 类迁入模拟器时，模拟器无命中必须返回 null/空，**不得**用 LLM 或猜测兜底；`EquilibriumStateSimulator` 常数只来自 `acid-constants.json` 或题给，缺常数/缺输入返回 `Underdetermined`（绝不猜数，见设计 §7.4）；归并前相关题目保持原求解器，未解即未解。
2. **LOST=0 / WORSEN=0**：每次迁入单题域，必须跑该域 eval diff 确认无已判正题翻转；发现翻转立即回退该次迁移。
3. **行为等价**：阶段 1 的迁入必须保证"原求解器能解的题，迁入后仍能解且答案一致"——可用字节对齐探针（比对原求解器与迁入后模拟器对同一输入的输出）。
4. **不扩范围**：本轮（阶段 0）不做任何迁移，分析报告仅为路线图；迁移由后续 spec 逐项承接，每项独立验证。

---

## 7. 结论

- **化学模拟器 + 多步推理模拟器 + 平衡态数值模拟器不能概括所有求解需求**，但能覆盖核心的"反应/状态/多步推断/平衡数值"范式（A 类，约 20 个对象）。
- **B 类（约 25 个）需要独立模拟器/计算器**：分子结构、晶胞、配平、计量、热化学、曲线读图——它们的计算空间与反应状态推演解耦。其中平衡态数值（B-6）已提升为本轮第三个主体类 `EquilibriumStateSimulator`。
- **C 类（约 25 个）是非模拟器**：判定/匹配/聚合，保持现状，未来消费模拟器结果而非被取代。
- 迁移应**分阶段、单题域验证、严守红线**，优先迁入低风险的纯反应预测类；平衡数值能力本期已设计实现，归并现有求解器留待阶段 3 行为等价后切换。
