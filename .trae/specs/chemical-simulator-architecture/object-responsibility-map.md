# 化学求解域对象职责地图

> 目的：梳理现有系统中「模拟器 / 引擎 / 计算器 / 求解器 / 校验器 / 匹配器 / 服务 / 知识库」等对象的作用与范式归属，为后续"化学模拟器统一架构"的边界划分提供依据。
> 范围：`src/CogniThink.TutorBrain.Api` 与 `src/CogniThink.Chem.Core` 中化学求解域对象；导学/能力画像/评测等周边对象仅归类，不展开。
> 分类原则：按**是否为真值源**（能否独立产出化学事实/数值/结论）与**计算范式**划分，而非按目录。

---

## 0. 分类总览

| 范式 | 角色 | 是否真值源 | 核心职责 | 代表对象数 |
|---|---|---|---|---|
| A | 模拟器（Simulator） | 是 | 给定体系与条件，推演**反应路径 + 状态变化**（多步、定量）；含平衡态数值 | 2 现有 + 3 规划 |
| B | 引擎（Engine） | 是 | 针对**一类化学问题**的推理（平衡/电化学/有机/实验流程…） | ~13 |
| C | 计算器/求解器（Calculator/Solver） | 是 | 窄域**数值计算或方程生成**（配平/计量/热化学/计数） | ~25 |
| D | 校验器/匹配器/Judge | 否（判定） | 不产出新事实，对已有陈述做**真伪判定或事实库匹配** | ~20 |
| E | 编排服务（Service） | 否（编排） | 组合调用真值源 + 采纳门 + 讲解话术 | ~8 |
| F | 知识库/数据加载 | 否（数据） | 加载与索引 JSON 事实/规则 | ~6 |
| G | 领域模型/DTO | 否（数据） | 纯数据结构，无逻辑 | 多 |
| H | 控制器/入口 | 否（入口） | HTTP 路由 | ~10 |

> 红线相关：A/B/C 是真值源（产出结论），必须严守 precision=1 / 不蒙数；D 是判定层（依赖真值源或事实库）；E 中 LLM 相关对象**只读**、不做化学真值判定。

---

## A. 模拟器（Simulator）— 反应状态推演范式

**是什么**：输入"物质 + 用量 + 环境条件"，输出"会发生哪些反应（多步）→ 每步的参与物质/依据/优先级/定量 → 最终体系状态"。核心是**状态随反应推进的演化**。

| 对象 | 落点 | 作用 | 状态 |
|---|---|---|---|
| `ChemicalSimulationEngine` | Api/Application/Chemistry | 正向反应状态推演：KB 检索可发生反应 → 按类型优先级/金属活动性择一 → 计量算限量试剂与剩余量 → 输出事件与最终状态 | 现有，将被 `ChemicalSimulator` **吸收合并** |
| `ChemicalReverseInferenceEngine` | Api/Application/Chemistry | 逆向：从目标产物/现象反查 KB 反应，列缺失反应物，构建上游制备路径，再正向回放验证 | 现有，其反查+回放逻辑将并入 `ChemicalMultiStepReasoningSimulator.Search` |
| `ChemicalSimulator` | Api/Application/Chemistry（规划） | **唯一正向模拟主体类**：统一输入 `ChemicalSystemInput`，输出多步 `ReactionEvent`（含参与物质/化学依据/优先级/定量） | 规划中（本轮设计） |
| `ChemicalMultiStepReasoningSimulator` | 同上（规划） | **多步推理主体类**：分步条件设定 → 状态轨迹；what-if 分支；目标导向搜索 | 规划中（本轮设计） |
| `EquilibriumStateSimulator` | 同上（规划） | **平衡态数值主体类**：基于 `acid-constants.json`（Ka/Kb/Ksp/Kw）计算 pH/离子浓度/Ksp 残留/沉淀转化 K；排序守恒定性委托 Core `AqueousEquilibriumEngine`；缺数据返回欠定不蒙数；可被多步推理模拟器挂载到状态快照 | 规划中（本轮设计，D-5 追加） |

**能覆盖的题型**：推断题、实验流程题、反应预测、产物判断、离子共存的反应性部分。
**不能覆盖**：结构计数、方程配平代数、纯数值计算、曲线读图（见 B/C）。

---

## B. 引擎（Engine）— 分类推理范式

**是什么**：针对**某一类化学问题**的专用推理，输入是该类问题的结构化条件，输出是该类问题的结论与证据链。与模拟器的区别：引擎通常面向"题目类型"而非"通用体系状态"。

| 对象 | 落点 | 作用 |
|---|---|---|
| `CoexistenceEngine` | Core/Inference | 离子共存判断（沉淀/气体/弱电解质/氧化还原/络合冲突） |
| `EquilibriumEngine` | Core/Inference | 化学平衡：已知体系 + 扰动（浓度/温度/压强）→ 平衡移动方向与结果 |
| `AqueousEquilibriumEngine` | Core/Inference | 水溶液平衡：电离/水解/缓冲，粒子浓度与三大守恒（物料/电荷/质子） |
| `ElectrochemEngine` | Core/Inference | 电化学：原电池/电解池的电极反应、电子流向、产物 |
| `ProcessFlowEngine` | Core/Inference | 工艺流程推断：多步转化链中各物质的推断 |
| `ProcessInferenceEngine` | Core/Inference | 过程推断：给定步骤现象反推中间产物 |
| `ExperimentEngine` | Core/Inference | 实验推断：试剂+现象→物质鉴别 |
| `OrganicInferenceEngine` | Core/Inference | 有机推断：官能团转化链 |
| `IdentificationEngine` | Core/Inference | 物质鉴别：用特征反应区分一组物质 |
| `ProductInferenceEngine` | Core/Inference | 产物推断：反应物+条件→可能产物 |
| `ReactionMatcher` | Core/Inference | 反应匹配：给定物质对，匹配 KB 中能发生的反应 |
| `EquationStoichiometry` | Core/Inference | 方程式计量关系计算 |
| `IonizationMap` | Core/Inference | 电离图谱：酸碱的分步电离物种分布 |
| `ChemistryReasoningEngine` | Api/Application/Reasoning | 网关推理链 `IReasoningEngine` 实现（编排多引擎输出推理节点） |
| `WorldModel` | Core/WorldModel | 趋势级/机理级解释（金属性递变/沉淀配位溶解/盐水解），**EvidenceLevel=Trend，不作判定依据** |
| `RuleEngine` | Core/Rules | 规则引擎：对事实/规则库求值（`rules.json` 等） |

> 注：B 类中 `ProcessFlowEngine` / `ProcessInferenceEngine` / `ExperimentEngine` / `OrganicInferenceEngine` / `ProductInferenceEngine` 本质是"反应 + 多步状态"，**可被新模拟器吸收或作为其上层查询**；`EquilibriumEngine` / `AqueousEquilibriumEngine` / `ElectrochemEngine` 涉及平衡态数值——其中 `AqueousEquilibriumEngine` 的排序/守恒/定性由本期 `EquilibriumStateSimulator` 委托承载、绝对数值由其补全（见分析报告 B-6 与设计 §7），电化学数值仍待后续评估。

---

## C. 计算器 / 求解器（Calculator / Solver）— 窄域数值或方程范式

**是什么**：窄域、确定性的**数值计算**或**方程/结构生成**。能唯一算出时返回结果，不能唯一判定时返回 null（不蒙数）。多为 `static class` + `TrySolve(...) → EngineResponse?` 模式，命中后置 `task_type` 走采纳门。

### C-1 方程配平 / 氧化态（Core/Balancing + Gateway）
| 对象 | 作用 |
|---|---|
| `EquationBalancer`（Core） | 通用化学方程式配平（原子守恒代数） |
| `RedoxBalancer`（Core） | 氧化还原配平（半反应法、电子守恒） |
| `OxidationStateAnalyzer`（Core） | 氧化态赋值与变化分析 |
| `HalfReactionBalancer`（Gateway） | 半反应配平（题干给氧化/还原对与介质时触发） |
| `BalanceGatewaySolver`（Gateway） | 配平网关入口 |

### C-2 计量 / 组成计算（Gateway）
| 对象 | 作用 |
|---|---|
| `CombustionEmpiricalFormulaSolver` | 燃烧法 → 实验式/分子式 |
| `VaporDensityMolarMassSolver` | 相对密度 → 平均摩尔质量 |
| `IodometryMassFractionSolver` | 碘量法 → SO₂ 质量分数 |
| `Batch2NarrowCalcSolver` | 批次窄域计算题 |
| `MoleCalculator`（Core/Stoichiometry） | 物质的量计算（底层工具） |

### C-3 热化学 / 能量（Gateway）
| 对象 | 作用 |
|---|---|
| `BondEnergySolver` | 键能计算反应焓 |
| `ThermochemicalHessSolver` | Hess 定律反应焓加和 |
| `GibbsSpontaneitySolver` | 吉布斯自由能 / 自发性判据 |
| `Carbonyl18ElectronSolver` | 18 电子规则（金属羰基配合物配位数） |

### C-4 结构 / 计数（Gateway + Core/Molecules）
| 对象 | 作用 |
|---|---|
| `CrystalCellValidator` | 晶胞确定性求解（位点/分数坐标/均摊计数）— 虽名 Validator 实为真值源 |
| `CountingChoiceValidator`（计数部分） | 同分异构体/取代物计数（构造异构枚举） |
| `TextChiralCenterSolver` | 手性碳原子数计数 |
| `SigmaBondCountSolver` | σ 键数计数 |
| `VseprSolver` | VSEPR 构型 / 键角比较原因 |
| `ConstitutionalIsomerCounter`（Core） | 构造异构体计数 |
| `MoleculeAnalyzer` / `MoleculeNamer`（Core） | 分子结构分析 / 命名 |
| `AtomicStructureSolver` | 原子结构（电子排布等） |

### C-5 方程 / 产物书写（Gateway）
| 对象 | 作用 |
|---|---|
| `ProductWriterSolver` | 限量离子方程式 / 产物书写（窄锚点） |
| `KnownEquationWriterSolver` | 已知方程式书写 |
| `ReactionKbWriterSolver` | 反应知识库方程书写 |
| `AcidFirstIonizationSolver` | 弱酸第一步电离方程式 |
| `HydrolysisEquationSolver` | 水解方程式（除杂语境） |
| `EsterificationEquationSolver` / `EsterificationProductSolver` | 酯化方程式 / 产物 |

### C-6 平衡残留 / pH（Gateway + Core）
| 对象 | 作用 |
|---|---|
| `KspPhResidualSolver` | Ksp-pH 残留离子浓度 |
| `HydrolysisPhCalculator` | 水解 pH 计算 |
| `PhOrderingSolver` | pH 排序 |

### C-7 曲线 / 图像数据（Gateway）
| 对象 | 作用 |
|---|---|
| `CurveChoiceValidator`（数据提取部分） | 滴定/溶解度曲线读图 |
| `GraphSolver` | 图/曲线求解 |
| `CurveIntentExtractor` | 曲线意图提取（从结构化曲线资产取数据） |

### C-8 其他窄域
| 对象 | 作用 |
|---|---|
| `CatalystSolver` | 多步反应中催化剂识别（圈编号反应步） |
| `ConceptFactSolver` | 概念事实求解（事实库） |

---

## D. 校验器 / 匹配器 / Judge — 判定范式（非真值源）

**是什么**：不产出新化学事实，只对**已有陈述/选项**做真伪判定，或在**事实库**中匹配。结论依赖真值源（A/B/C）或静态事实 JSON。

| 对象 | 作用 |
|---|---|
| `ChemicalCheckerService`（Core） | **统一符号校验入口**：配平/守恒/离子规则的确定性校验，产 `CheckResult`（方程正误判定的真值源，虽名 Service） |
| `EquationCorrectnessJudge`（Gateway） | 方程式书写正误判定（调用 `ChemicalCheckerService`） |
| `ConceptChoiceValidator` | 概念选择题逐选项校验（concept-facts.json） |
| `FactChoiceMatcher` | 事实选择题通用匹配器（候选唯一契约） |
| `NaChoiceValidator` | NA 粒子计数选择题校验 |
| `EquilibriumChoiceValidator` | 电离平衡/胶体/电解质概念选择校验（equilibrium-rules.json） |
| `ExperimentChoiceValidator` | 实验选择题校验 |
| `MoleculeChoiceValidator` / `OrganicChoiceValidator` | 分子/有机选择题校验 |
| `CurveChoiceValidator`（校验部分） | 曲线选择题校验 |
| `ClassificationValidator` | 物质分类选择题校验 |
| `MultiSelectChoiceValidator` | 多选校验 |
| `CountingChoiceValidator`（判定部分） | 计数选择题判定 |
| `ChoiceVerifier` | 选择校验内核 |
| `IStatementJudge` 家族（`CompositeStatementJudge` / `FactBaseStatementJudge` / `CountingStatementJudge` / `RedoxElectronStatementJudge`） | 陈述真伪判定原语链 |
| `JudgmentStatementAggregator` / `MultiSubCorrectnessSolver` | 判分聚合 |
| `ImpurityTrapSolver` | 除杂陷阱判定（规则） |
| `CorrectnessContextRules` / `InfeasiblePattern` / `IonConflictRule` / `IonSolutionContext` | 正确性上下文 / 不可行模式 / 离子冲突 / 离子溶液上下文（规则数据与判定） |
| `ConceptVerdictSolver` | 概念判定聚合 |
| Core: `ConflictScanner` / `RuleSetValidator` / `WhitelistGuard` / `IonicEquationChecker` | 规则冲突扫描 / 规则集校验 / 白名单护栏 / 离子方程式校验 |

---

## E. 编排服务（Service）— 组合与采纳（非真值源）

| 对象 | 作用 |
|---|---|
| `LlmGatewayService`（Gateway） | **网关主编排**：LLM 输入标准化 → 组装 `EngineRequest` → 调引擎 → fallback 求解器链 → 采纳门 → LLM 生成讲解。**化学真值由引擎/求解器产出，LLM 只读** |
| `ChemicalInferenceService`（Chemistry） | 轻量推理服务：颜色/溶解性/共存/相关反应查询（内部调 KB，偏查询非推演） |
| `ProblemQueryCompiler`（Chemistry） | 题干 → 内部 `ChemicalQuery` 编译 |
| `KnowledgeQueryService`（Core） | 知识查询 |
| `TutorService` / `AdaptiveTeachingOrchestrator` / `AbilityGuidedTeachingService` / `ErrorPracticeLoopService` | 导学编排（非化学真值源） |
| `LlmProxyService` / `LlmNarrativeAgent` / `RuleBasedNarrativeAgent` | LLM 代理与讲解文案生成（**只读，不判定化学真值**） |

---

## F. 知识库 / 数据加载

| 对象 | 作用 |
|---|---|
| `ChemicalKnowledgeBase`（Api/Chemistry） | Api 侧知识库：加载 `data/chemistry/*.json`，建物质/反应/产物/离子索引 |
| `KnowledgeBase`（Core/Knowledge） | Core 侧知识库 |
| `ChemDataLoader`（Core/Data） | Core 数据加载（rules/knowledge 等） |
| `RuleStore`（Core/Rules） | 规则存储与索引 |
| `ReactionLibrary`（Core/Reactions） | 反应库 |
| `Tier2FactData` / `EquilibriumRuleData`（Core/Rules） | Tier2 事实 / 平衡规则数据加载 |

---

## G. 领域模型 / DTO（纯数据，无逻辑）

| 集合 | 内容 |
|---|---|
| `ChemicalEntities`（Api/Domain） | `Substance` / `Reaction` / `ChemicalIon` / `ReactionType` / `IonInteraction` / `SolubilityTable` / `ChemicalQuery` 等 |
| `InferenceModels`（Core） | 各引擎的 Request/Result（`ProductInferenceRequest` 等） |
| `GatewayContracts`（Api） | `EngineRequest` / `EngineResponse` / `EngineReaction` / `GatewayResult` 等网关契约 |
| `ReasoningContracts`（Api） | `ReasoningNode` / `AnswerNormalizer` |
| `ProblemModels`（Core/Benchmark） | 基准题目模型（`ProblemSpec` / `GivenSpec` / `ExpectedSpec` 等） |
| `Species` / `ChemicalEquation`（Core/Chemistry） | Core 侧物种与方程式模型 |
| 各模拟器/求解器内嵌 Request/Result | 如 `SimulationRequest` / `ReverseInferenceRequest` 等（将被统一契约收敛） |

---

## H. 控制器 / 入口（HTTP 路由）

`GatewayController`（主入口 `/api/gateway/process|solve|parse`）、`ChemistryController`（化学 API，注入模拟器/推理引擎）、`TutorController`、`KnowledgeController`、`ProblemController`、`StudentController`、`AbilityController`、`DiagnosticController`、`EvaluationController`、`LlmController`。

---

## 关键结论（供架构设计用）

1. **"模拟器"范式（A）当前只有 `ChemicalSimulationEngine` + `ChemicalReverseInferenceEngine` 两个**，且未接入网关 live 路径。重构将其演进为 `ChemicalSimulator` + `ChemicalMultiStepReasoningSimulator` + `EquilibriumStateSimulator` 三个主体类，是低回归风险的。
2. **B 类引擎中，反应/多步类（`ProcessFlowEngine` / `ProcessInferenceEngine` / `ExperimentEngine` / `OrganicInferenceEngine` / `ProductInferenceEngine` / `ReactionMatcher`）可被新模拟器承载或作为其上层查询**；平衡/水溶液类的数值由本期 `EquilibriumStateSimulator` 承载（电化学数值仍待后续评估）。
3. **C 类计算器/求解器不属于"反应状态推演"范式**，不能塞进化学模拟器：配平是守恒代数、计数是离散结构枚举、热化学是能量计算、曲线读图是数据提取。这些应保持为独立计算器，或归到"分子结构模拟器""晶胞模拟器""方程配平器"等独立主体。
4. **D 类校验/匹配不是真值源**，保持现状，不纳入模拟器体系。
5. **统一输入 `ChemicalSystemInput` 的适用范围**：A 类全部、B 类的反应/多步部分；C/D 类有各自的输入形态（方程、分子式、曲线资产等），不强制套用。
