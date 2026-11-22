# CogniThink-Chem 整体结构分析与下一步完善计划

> 生成时间：2026-11-22
> 范围：仓库现状盘点 + 「求解部分」分层拆解 + 合理性评估 + 缺失能力 + 后续路线图
> 方法：逐文件读码核实（非文档臆测），所有结论附源码链接

---

## 0. 执行摘要

CogniThink-Chem 已经从 `docs/` 里 v0.2 规划的「T0 完成、T1 部分、T2 暂停」长出来一个**远比规划庞大、且工程纪律极强**的现实系统：.NET 8 多项目解决方案 + 2144 个全绿测试 + 757 题 PDF 真题评估（解对 621、precision=1、harm=0）。但也积累了三类必须正视的结构债：

1. **两套并行的求解栈**——Core 的 `ProblemSolver`（结构化输入、98 基准题）与 Api 的 `LlmGatewayService` 网关（自然语言输入、757 真题）功能重叠却互不调用，题目模型、评估集、分派机制全部分叉。
2. **文档与现实严重脱节**——`docs/01-03` 和 `README.md` 仍停在 v0.2，完全没反映网关、导学、诊断、757 eval 等已落地能力，新成员按文档入会被误导。
3. **求解能力有明确缺口**——多部分大题子问分发、纯计算域计算器、自由填空求解器、28 道路由缺口、图像与文本求解融合，均已在台账/spec 中登记但未闭环。

**结论**：架构原则（引擎是唯一真值源、LLM 只做语言层、采纳门控保证 precision=1）是对的、执行得也很好；主要问题是**「两套栈」的收敛缺失**与**文档/前端闭环的滞后**，其次才是求解能力的纵向补全。下一步应先做结构收口与文档对齐（P0），再按 spec 接续能力补全（P1），最后推进架构演进与产品闭环（P2/P3）。

---

## 1. 项目整体结构（现状地图）

### 1.1 解决方案（C# / .NET 8）

| 项目 | 角色 | 关键产物 | 依赖 |
|------|------|----------|------|
| [src/CogniThink.Chem.Core](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core) | 化学领域原语 + 推断引擎 + 求解器 + 讲解/引导 | `Inference/*Engine`、`Solving/ProblemSolver`、`Rules/RuleEngine`、`Balancing`、`Molecules`（RDKit）、`Math/Rational` | RDKitDotNetCore（原生库 ~168MB） |
| [src/CogniThink.Chem.Cli](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Cli) | 演示/自检/导出工具 | `--solve`、`--verify-real`、`--export-web`、`--demo` | Core |
| [src/CogniThink.TutorBrain.Api](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api) | ASP.NET Web API：网关求解 + 导学 + 能力图谱 + 诊断 + 评估 | `Application/Gateway/*`（55 文件）、`Application/Ability`、`Application/Diagnostic`、`Application/Agent`、`Application/Reasoning` | Core |
| [src/CogniThink.TutorBrain.EvalRunner](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.EvalRunner) | 757 题评估运行器（LLM 关闭下跑网关） | `Program.cs` | Api |
| [src/CogniThink.TutorBrain.PdfOcrRepair](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.PdfOcrRepair) | PDF OCR 修复工具 | — | — |
| [tests/CogniThink.Chem.Tests](file:///d:/github/CogniThink-Chem/tests/CogniThink.Chem.Tests) | Core 测试（911） | `ProblemSolverTests`、各 `*EngineTests`、`BenchmarkTests` | Core |
| [tests/CogniThink.TutorBrain.Tests](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests) | Api 测试（1233） | 各 `*ValidatorTests`、`ClassificationDeterministicTests`、`StructuredScoringTests`、导学诊断测试 | Api |
| `_evalscratch/review_keyfix2` | 临时评估工程 | — | — |

合计 **2144 测试全绿**（基线：`dotnet test` 0 failed）。

### 1.2 前端

| 目录 | 形态 | 接入状态 |
|------|------|----------|
| [web/](file:///d:/github/CogniThink-Chem/web) | 静态 HTML/JS（`index.html`/`app.js`/`knowledge-graph.html`/`demo.html` + `data/problems.json`） | 由 CLI `--export-web` 生成，**只读展示**求解结果，不调用网关 |
| [miniapp/](file:///d:/github/CogniThink-Chem/miniapp) | Taro 4 + React + TS 小程序（微信/抖音/H5 多端） | 已接入后端 **导学** API（`/api/problem`、`/api/tutor/submit-step`、`/api/student`，见 [tutorApi.ts](file:///d:/github/CogniThink-Chem/miniapp/src/services/tutorApi.ts)），但**未接入网关求解**（`/api/gateway/process`）。即：小程序消费的是「分步导学」流，网关「题目求解」能力只在 EvalRunner 里被评估，未触达学生端 |

### 1.3 数据（三套并存）

| 数据 | 位置 | 消费者 | 作用 |
|------|------|--------|------|
| 事实库（24 文件） | [data/highschool/*.json](file:///d:/github/CogniThink-Chem/data/highschool) | 网关 `FactChoiceMatcher` 系校验器（经 `RuleStore`） | 选择/判断题逐选项真值匹配 |
| 反应库 + 知识点 | [data/knowledge/](file:///d:/github/CogniThink-Chem/data/knowledge) | Core 推断引擎 + 网关书写求解器 | 反应检索、溯源 |
| 旧规则库（37 条 L0–L4） | [data/rules/rules.json](file:///d:/github/CogniThink-Chem/data/rules/rules.json) | Core `RuleEngine` | 早期仲裁机制，网关基本不直接消费 |
| 基准题（12 类 98 题） | [data/benchmark/problems/](file:///d:/github/CogniThink-Chem/data/benchmark/problems) | Core `ProblemSolver` | 结构化 ProblemSpec 端到端 |
| 真题（27） | [data/real-exam/real-exam.json](file:///d:/github/CogniThink-Chem/data/real-exam) | Core `--verify-real` | 真题回归 |
| PDF 真题（757 题） | `src/CogniThink.TutorBrain.Api/data/pdf_questions.json` | 网关 EvalRunner | 网关主评估集 |
| OCR 中间产物 | `pdfs/_extract/`、`_evalscratch/` | 工具链 | 大量中间 JSON/图片，部分应入 `.gitignore` |

### 1.4 文档与规划（两层并存、已脱节）

- [docs/](file:///d:/github/CogniThink-Chem/docs)：v0.2 原始规划（01-overall / 02-modules / 03-tasks / decisions），描述 M0–M12 模块、T0–T6 阶段，标注「T0 完成、T1 部分、T2 暂停、T3–T6 未开始」——**与现实严重不符**。
- [.trae/specs/](file:///d:/github/CogniThink-Chem/.trae/specs)：15+ 个 spec-mode 产物（已完成/进行中），记录了现实中真正推进的工作（事实策展、晶胞、图像方程、多部分答案、可靠性、当前的 engine-capability-gap-fix 等）。
- [.trae/documents/](file:///d:/github/CogniThink-Chem/.trae/documents)：14 个计划文档。
- [.trae.documents/chem-inference-tutor-engine.md](file:///d:/github/CogniThink-Chem/.trae.documents/chem-inference-tutor-engine.md)：Core 推断引擎的设计文档（「能推断能计算能解能讲能引导」）。

---

## 2. 求解部分分成哪些部分

这是本项目的核心。**现实中存在两套并行的求解栈**，必须分开描述。

### 2.1 栈 A：Core 推断求解栈（基准/真题路径）

**入口**：[ProblemSolver.Solve(ProblemSpec)](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Solving/ProblemSolver.cs#L32)

```
ProblemSpec（已结构化：given + expected）
   │  按 ProblemCategory 分派
   ▼
Inference/*Engine（12 类）
   ├─ ProductInferenceEngine    产物推断（反应库检索）
   ├─ CoexistenceEngine         离子共存
   ├─ IdentificationEngine      成分推断（约束传播）
   ├─ ProcessInferenceEngine    过程推断（转化链回溯赋值）
   ├─ OrganicInferenceEngine    有机推断（限定 RX-OR-* 库）
   ├─ ProcessFlowEngine         工业流程（委托 ProcessInference + 目的/原理规则）
   ├─ ExperimentEngine          实验探究
   ├─ ElectrochemEngine         电化学（装置判定 + 半反应）
   ├─ EquilibriumEngine         化学平衡（三段式 + Kp，Rational 精确）
   ├─ AqueousEquilibriumEngine  水溶液离子平衡（电离/水解 + 三大守恒）
   └─ （计算）复用 BenchmarkVerifier.ComputeCalc
   │
   ▼
Solution（步骤 + 溯源 Evidence + 化学思想 Thought 标签）
   │
   ├─ Explanation/Explainer      逐步中文讲解（引用 KnowledgeBase 教材出处）
   └─ Explanation/GuideGenerator 分级启发提问（不给答案）
```

**特征**：输入是**人工预结构化**的 `ProblemSpec`（含 `given`/`expected`），不做自然语言解析；评估走 98 基准题 + 27 真题；强调有理数学精确计算、欠定/无解上抛不猜、结论溯源到反应/规则条目。

### 2.2 栈 B：Api 网关求解栈（757 题 PDF 真题路径）—— 主力

**入口**：[LlmGatewayService.ProcessStudentMessageAsync](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L93)（HTTP 端点 [POST /api/gateway/process](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Api/Controllers/GatewayController.cs#L21)）

#### 主链路（5 段）

```
学生题干文本（+ 配图资产）
   │
   ▼ ① 解析层
ParseStudentInputAsync
   ├─ LLM (ChatWithImagesAsync)：NL → EngineRequest JSON（task_type + reaction/known/options）
   ├─ 失败 → FallbackParse（关键词）
   └─ ApplyDeterministicTaskType：ClassifyTaskDeterministic 规则链强制覆盖 task_type
       （消除 LLM 漂移；规则判得出就覆盖，判不出保留 LLM 值兜底）
   │
   ▼ ② 分类
ClassifyTaskDeterministic → 13 类 taskType：
   choice / judgment / ion_check / balance / equilibrium /
   atomic_structure / calculation / reaction_check / structure_verify /
   vsepr / titration / concept / unknown
   │
   ▼ ③ 分发 + 多层守卫（CallEngineAsync）
   优先级链（命中即返回，未命中回退下一层，不蒙数）：
   equilibrium 确定性求解器
     → choice 曲线判定 (CurveChoiceValidator)
     → choice 概念事实 (ConceptChoiceValidator)
     → 图/曲线/分子资产路由 (TryRouteGraph/Curve/Molecule)
     → 晶胞 (CrystalCellValidator) / 半反应 (HalfReactionBalancer)
     → 原子结构 / VSEPR / 概念事实 / 标况陷阱
     → 方程书写兜底 (ReactionKbWriterSolver)
     → concept/unknown 早返回（Ok=null）
     → switch(taskType)：ion_check / balance / reaction_check /
        judgment / calculation / equilibrium / choice / vsepr / default
   │
   ▼ ④ 求解器/校验器（Application/Gateway/，55 文件，三类）
   │
   ▼ ⑤ 采纳门控（评估侧 BuildGatewayAnswerAsync）
       凡采纳必判正 → precision=1 构造保证；否则返回 null（诚实答不出）
   │
   ▼ ⑥ GenerateReplyAsync：LLM 把引擎结论转述为教学话术（不改结论）
```

#### 求解器/校验器三类（55 文件全清单分类）

**(a) 事实校验器**（加载 `data/highschool/*.json`，经共享内核 [FactChoiceMatcher](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/FactChoiceMatcher.cs)）：
- 单选瀑布链（每选项恰好命中 1 条事实才断言，否则 null）：`ConceptChoiceValidator`（合并 general + curated）、`ExperimentChoiceValidator`、`OrganicChoiceValidator`、`StructurePeriodicValidator`、`NaChoiceValidator`、`EquilibriumChoiceValidator`、`CurveChoiceValidator`、`MoleculeChoiceValidator`、`StoichiometryChoiceValidator`、`RedoxChoiceValidator`、`ClassificationValidator`、`TransformationChainValidator`、`IonicEquationValidator`
- 多选/计数：`MultiSelectChoiceValidator`、`CountingChoiceValidator`
- 硬门：`Batch3ChoiceValidatorTests.Concept_NoOverlap_*`（Concept 非 null ⟹ 其余全 null）

**(b) 陈述真伪判断**（`JudgeStatement` 合并 6 个 fact key，冲突→Undecided→整题 null）：
`CompositeStatementJudge`、`CountingStatementJudge`、`FactBaseStatementJudge`、`RedoxElectronStatementJudge`、`JudgmentStatementAggregator`、接口 `IStatementJudge`

**(c) 算法求解器**（不依赖事实库，用 Core 原语计算）：
`BalanceGatewaySolver`（配平）、`HalfReactionBalancer`（电极半反应）、`RedoxGatewaySolver`、`AtomicStructureSolver`（电子排布，Core PeriodicTable 真值）、`VseprSolver`、`CrystalCellValidator`（晶胞计算）、`ConceptFactSolver`（concept-facts 查表）、`StoichiometryStateTrapSolver`、`ImpurityTrapSolver`、`Batch2NarrowCalcSolver`、`ThermochemicalHessSolver`（盖斯定律）、`ReactionKbWriterSolver`/`KnownEquationWriterSolver`/`ProductWriterSolver`（方程书写）、`GraphSolver`/`CurveIntentExtractor`/`GraphInputExtractor`/`CurveQuery`（图像资产）、`ChoiceVerifier`、`EquationCorrectnessJudge`、`MultiSubCorrectnessSolver`、`ConceptVerdictSolver`、`StoichiometryStateTrapSolver`

**契约/DTO/胶水**：`GatewayContracts`、`ConceptFactData`、`ElectrodeReactionFact`、`IonConflictRule`、`IonSolutionContext`、`InfeasiblePattern`、`CorrectnessContextRules`、三个 `LlmGatewayService.{Curve,Graph,Molecule}.cs` 分部类。

#### 采纳门控（precision=1 的构造保证）

[BuildGatewayAnswerAsync](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L631) 的逐 taskType 规则：
- `calculation`：引擎给数值 → 直接采纳
- `choice`：结论含选项字母 → 采纳；多选需覆盖全部答案字母且判分通过
- `atomic_structure` / `vsepr` / `concept_fact` / `judgment` / `equilibrium`：**仅采纳 `IsAnswerCorrect`（答案键校验）通过的结论**
- `balance` / `reaction_check`：必须含真值签名（「配平结果」/ 反应真值签名），**排除占位结论**（「已匹配到相关反应」等）
- 其余占位/非具体结论 → **返回 null**，绝不回退纯 LLM 猜答案

这就是 757 题 eval 能做到 **precision=1 / harm=0** 的根因：门控宁可漏（返回 null，不计解对）也不误采纳。

### 2.3 两套栈的关系（关键结构事实）

| 维度 | 栈 A（Core ProblemSolver） | 栈 B（Api 网关） |
|------|---------------------------|------------------|
| 输入 | 结构化 `ProblemSpec`（人工标注 given/expected） | 自然语言题干文本 |
| 分派维度 | `ProblemCategory`（12 类） | `taskType`（13 类） |
| 真值来源 | 反应库 + 推断引擎 + 有理计算 | 事实库匹配 + Core 原语求解器 + 答案键门控 |
| 评估集 | 98 基准 + 27 真题 | 757 PDF 真题 |
| 输出 | `Solution` + 讲解 + 引导 | `GatewayResult`（引擎结论 + LLM 话术） |

**复用关系**：Api **引用** Core，但网关**不调用** Core 的 `ProblemSolver` 或 `Inference/*Engine`。网关只复用了 Core 的**底层原语**：`Balancing/EquationBalancer`、`Parsing`、`Rules/RuleStore+RuleEngine`、`Math/Rational`、`Molecules`、`Structure/PeriodicTable`、`Reactions/ReactionLibrary`、`Benchmark`（[grep 证据](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api)）。

**结论**：在「求解编排」这一层，Core 和 Api 是**两套并行实现**，而非「网关复用 Core 求解器」。同一题型（如平衡、电化学）在两边各有一套求解逻辑，能力与覆盖并不一致。

### 2.4 周边（非求解主链，但易混淆）

- [Application/Reasoning/ChemistryReasoningEngine](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Reasoning/ChemistryReasoningEngine.cs)：**名字误导**——它不是题目推理引擎，而是**学生作答步骤的判分器**（关键词覆盖度 + 矛盾词检测 → StepVerdict），服务于导学诊断。
- [Application/Chemistry/](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Chemistry)：`ChemicalInferenceService`（共存检查，被网关 `CallIonCheck` 调用）、`ChemicalKnowledgeBase`（离子归一化）是活的；但 `ChemicalSimulationEngine`、`ChemicalReverseInferenceEngine`、`ProblemQueryCompiler`、`ReactionEquationParser` 有测试、被 `TutorService`/`DiagnosticTeachingEngine` 消费，**未进网关求解主链**，属导学侧辅助（部分近孤儿）。
- `Application/Ability/`、`Application/Diagnostic/`、`TutorService`、`AdaptiveTeachingOrchestrator`：导学、能力图谱、诊断教学——产品层，非求解。
- `Application/Agent/`：`LlmProxyService`（纯转发）、`LlmNarrativeAgent`/`RuleBasedNarrativeAgent`（话术）。

---

## 3. 结构合理性评估

### 3.1 做得好的（应保持）

1. **架构原则正确且执行坚决**：「化学引擎是唯一真值源，LLM 只做 NL↔结构化 + 话术」在网关中被严格执行（[ParseSystemPrompt](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L39) 红线明示 + 采纳门控兜底）。这是教育类化学 AI 最关键的正确性防线。
2. **precision=1 的工程纪律**：采纳门控「凡采纳必判正」、事实探针 `TOTAL PROBLEMS=0` 才落库、`LOST=0` 台账闭环——红线体系成熟，可复制。
3. **确定性分类消除 LLM 漂移**：`ClassifyTaskDeterministic` 规则链覆盖 LLM 的 task_type，保证跨运行稳定，eval 可复现。
4. **数据/代码分离**：化学事实只存 JSON（`data/highschool/*.json`），不硬编码进 `.cs`；事实落库有探针门控与 NoOverlap 硬门。
5. **测试与回归体系扎实**：2144 测试 + eval checkpoint + `diff_eval.ps1` regex 法权威 diff，变更可追溯、可回滚。
6. **Core 推断引擎领域拆分清晰**：有理数学精确、欠定上抛不猜、结论溯源，符合「知识驱动确定性推断」原则。

### 3.2 不合理 / 需整改的问题

#### P0-级（结构债）

**问题 1：两套并行求解栈，缺乏收敛方向**
- Core `ProblemSolver` 与 Api 网关在「求解编排」层重复，题目模型（`ProblemSpec` vs `pdf_questions.json` + `EngineRequest`）、评估集（98+27 vs 757）、分派维度（Category vs taskType）全部分叉。
- 后果：同一题型两边各维护一套；修一个 bug 要改两处；能力不一致（网关解不了的，Core 未必覆盖，反之亦然）；新成员不知道该在哪套上加能力。
- 根因：Core 推断引擎按 `.trae.documents/chem-inference-tutor-engine.md` 建成后，网关（面向 757 OCR 真题、要求 NL 输入 + 答案键门控）另起了一套更贴近 eval 的实现，两者没有做「谁是主、谁是辅」的决策。

**问题 2：文档与现实严重脱节**
- `docs/01-03` 和 `README.md` 仍停在 v0.2（「T2 暂停、T3–T6 未开始」），完全没提网关、导学、诊断、757 eval、事实策展体系。`docs/02-modules.md` 的 M5–M12 标注「未实现」，但网关/导学/诊断已大量实现（只是不在 docs 描述的形态里）。
- 后果：文档失去导航价值，规划与现实两张皮；`.trae/specs/` 成了真正的事实来源但分散在 15+ 个 spec 里，缺一个「现状总览」。

#### P1-级（定位/命名清晰度）

**问题 3：`Application/Reasoning` 与 `Application/Chemistry` 命名误导、定位模糊**
- `ChemistryReasoningEngine` 实为判分器；`ChemicalSimulationEngine`/`ChemicalReverseInferenceEngine` 未进求解主链。需明确：哪些是「求解真值源」、哪些是「导学/判分辅助」，避免命名暗示其为求解核心。

**问题 4：数据位置与体系分裂**
- 网关评估题在 `Api/data/pdf_questions.json`（随项目拷贝到 bin，跑 eval 需 hash 核对），Core 题在 `data/benchmark`；事实体系 `data/highschool` 与旧规则 `data/rules/rules.json` 并存但网关基本不用后者。缺一个「数据资产清单 + 谁消费谁」的统一视图。

#### P2-级（卫生）

**问题 5：仓库临时产物过多**
- `_evalscratch/`（评估 checkpoint、脚本、诊断 txt）、`pdfs/_extract/`（数百 OCR 中间 JSON + 页面渲染图）在仓库内。需区分「应入库的证据」与「应忽略的中间产物」，否则仓库膨胀、diff 噪声大。
- `_evalscratch/review_keyfix2` 是临时 csproj 工程，应清理或迁出主解决方案。

---

## 4. 还缺少什么（能力缺口清单）

### 4.1 求解能力缺口（已由台账/spec 量化）

来自 [engine-capability-gap-fix spec](file:///d:/github/CogniThink-Chem/.trae/specs/engine-capability-gap-fix/spec.md) 与 [data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md)：757 题中 136 未解，按根因——

| 缺口类别 | 数量 | 状态 | 缺什么 |
|----------|------|------|--------|
| 选择/判断事实策展（可唯一判定） | ~19 | 当前 spec Phase 1 进行中 | 事实库覆盖（零代码，chem-fact-curation） |
| 方程书写误路由 | 5 | 当前 spec Phase 2 | `ClassifyTaskDeterministic` 缺「书写+方程式」规则 + KB 扩充 |
| 占位噪声 | 4 | 当前 spec Phase 2（可选） | 守卫收紧（不增解对，只压噪声） |
| **多部分大题子问分发** | ~27 | **Non-Goal，待立项** | 缺「拆子问 → 逐子问路由 → 聚合判分」架构 |
| **纯计算域计算器** | ~5 | **Non-Goal，待立项** | 滴定质量分数/键焓/pH/李比希法等多步计算无确定性求解器 |
| **自由填空概念求解器** | ~5 | **Non-Goal，待立项** | `TryDecide` 仅服务 choice，fill 概念题无链路 |
| **路由缺口（taskType=unknown）** | 28 | 待立项 | 分类器识别能力不足 |
| 数据缺陷（已登记台账） | 45 | 台账留痕 | 题面碎片/答案键冲突/缺图，非引擎问题 |

### 4.2 架构层缺口（规划 vs 现实）

对照 `docs/02-modules.md` 的 M0–M12 与现实：

| 规划模块 | 现实状态 | 缺口 |
|----------|----------|------|
| M1 知识本体 | Core `KnowledgeBase` 有，但网关事实库另起 | 缺统一知识本体（知识点 id 贯通事实/规则/题目/推理节点） |
| M2 规则库与仲裁 | Core `RuleEngine` 存在但网关不用其仲裁；网关用「事实匹配 + 答案键门控」代替 | 缺统一的裁决/冲突机制；两套规则体系 |
| M3 世界模型（电子层/能量/水合） | **基本未实现**（Core 只有 PeriodicTable） | 微观解释链无法复现；网关靠事实库硬匹配 |
| M4 机理引擎 | Core 有配平/守恒/部分平衡；网关有半反应/晶胞/盖斯 | 缺统一机理层，能力散落在各求解器 |
| M5 题目结构化解析 | 网关靠 LLM + 确定性分类，**参数提取仍靠 LLM**（架构允许但未降级兜底） | 无 LLM 时结构化能力弱；缺规则解析器兜底 |
| M6 推理引擎 + 推理 DAG | Core 有推断但无统一 DAG；网关输出扁平 `DerivationSteps` | **缺统一推理 DAG**（节点带 rule_id/knowledge_id/evidence_level），与规划不符 |
| M6 结论三态 | 网关以「null = 答不出」实现了精神，但无显式 `确定/候选集/信息不足` 三态模型 | 缺显式三态契约与「候选集 + 判别实验」输出 |
| M7 试剂顺序规划 | **未实现** | 干扰分析/顺序约束/反事实检查缺失 |
| M8 题库与数据管线 | 757 题已采集但靠 OCR + 人工修；缺版本化/去重/缺口报表自动化 | 缺正式题库管线与 schema |
| M9 校验回归 | eval 体系强，但缺「规则库×题库版本绑定对比」与「主干无规则/无题」缺口报表 | 缺口报表未自动化 |
| M10/M11/M12 引导/可视化/接口 | 导学、诊断、能力图谱已实现（miniapp 接入导学）；但**网关求解未接入前端** | 求解结果未触达学生端；缺推理 DAG 可视化 |

### 4.3 产品闭环缺口

- 网关「题目求解」能力只在 EvalRunner 评估，**没有学生可用的前端入口**（miniapp 接的是导学流，web 是静态导出）。
- 缺「输入题目 → 求解 → 分级讲解/引导 → 复盘」的端到端产品演示（规划 T5/T6 的阶段出口未达成）。

---

## 5. 下一步完善计划

按「先收口、再补能、后演进」排序。每项给目标、依赖、验收。

### P0：结构收口与文档对齐（最高优先，1–2 周）

> 目标：消除认知混乱，让「在哪加能力、文档看哪里」有唯一答案；不改变求解行为。

**任务 P0-1：两套求解栈的收敛决策（写文档，不动代码）**
- 产出一份 `ARCHITECTURE.md`（或更新 `docs/`），明确：
  - **Core = 化学原语层 + 结构化推断引擎**（EquationBalancer、PeriodicTable、Rational、ReactionLibrary、RuleStore，以及 `Inference/*Engine`），是所有求解的真值地基。
  - **Api 网关 = 产品求解编排层**（NL 解析 → taskType 分类 → 求解器/校验器分发 → 采纳门控 → 话术），面向 757 真题与前端。
  - 决策：**网关是面向学生/真题的主求解入口**；Core `ProblemSolver` 定位为「结构化基准回归引擎」（守护 98 基准 + 27 真题的推断正确性），不再平行扩展新题型。新增求解能力**优先落在网关**，复用 Core 原语；若 Core 推断引擎能力可被网关复用（如平衡三段式），评估抽取为共享服务而非重写。
- 验收：文档明确每类题型「在网关还是 Core 维护」；无歧义。
- 依赖：无。

**任务 P0-2：更新文档反映现状**
- 重写 `README.md` 的「当前状态」与目录结构，使其描述真实项目（网关、导学、757 eval、两套栈）。
- 在 `docs/` 新增 `04-current-architecture.md`（或把 02-modules 标注「规划态」并链接到现状文档），标注哪些 M 已落地、以什么形态落地、哪些仍是缺口（即本文第 4.2 节的表）。
- 把 `.trae/specs/` 已完成 spec 汇总成一个「已交付能力索引」。
- 验收：新成员只读 `README` + `docs/` 能理解现状，不被 v0.2 误导。
- 依赖：P0-1。

**任务 P0-3：仓库卫生（低风险）**
- 评估 `_evalscratch/` 与 `pdfs/_extract/` 中哪些是证据（保留）、哪些是中间产物（移入 `.gitignore` 或归档）。
- 清理 `_evalscratch/review_keyfix2` 临时工程（确认无引用后删除或迁出 sln）。
- 验收：`git status` 干净，仓库体积合理，diff 无大量二进制/中间 JSON 噪声。
- 依赖：无（可与 P0-1 并行）。

### P1：求解能力补全（承接当前 spec，2–6 周）

> 目标：在不破坏 precision=1 红线前提下，提升 757 题解对数；先完成当前 spec，再立项三个 Non-Goal 缺口。

**任务 P1-1：完成 engine-capability-gap-fix（当前进行中）**
- 即 [tasks.md](file:///d:/github/CogniThink-Chem/.trae/specs/engine-capability-gap-fix/tasks.md) 的 T1–T9：选择/判断事实策展（≥6 题）+ 方程书写重路由（≥2 题）+ 占位守卫（可选）+ 全量验证 + 台账闭环 + 独立 Review。
- 验收：AC-1～AC-7 全过；新增解对 ≥8；LOST=0 / precision=1 / harm=0。
- 依赖：无（正在进行）。

**任务 P1-2：多部分大题子问分发架构（新立项 spec）**
- ~27 道：一题含 (1)(2)(3)… 异构子问（填空+选择+方程+计算+简答混合）。
- 设计：拆子问 → 逐子问 taskType 路由 → 各子问独立求解/判分 → 聚合。难点在 precision 风险（任一子问误判会污染整题），需独立评审。
- 验收：≥5 道多部分大题解对且零误伤；子问拆分可追溯。
- 依赖：P1-1 完成（避免并发改路由）。

**任务 P1-3：纯计算域计算器（新立项 spec）**
- ~5 道：滴定质量分数、键焓、pH 排序/计算、李比希法分子式。LLM 关闭下需确定性多步计算器。
- 策略：优先复用 Core `MoleCalculator`/`Rational`/`EquationStoichiometry`，按题型建窄域计算器；不唯一则返回 null。
- 验收：≥2 道计算题解对，零误伤；计算过程输出近似假设。
- 依赖：P1-1。

**任务 P1-4：自由填空概念求解器（新立项 spec）**
- ~5 道：除杂/除离子/溶质成分/配制浓度/调 pH 目的填空。
- 策略：评估能否复用事实库 + 填空答案键匹配；或建窄域填空求解器。
- 验收：≥1 道填空题解对，零误伤。
- 依赖：P1-1。

**任务 P1-5：路由补齐（28 道 taskType=unknown）**
- 扩展 `ClassifyTaskDeterministic` 规则链，先做全库扫描确认「补规则不会让已解对题 taskType 漂移」，再增量补。
- 验收：unknown 题数下降，已解对 621 题 taskType 零有害漂移。
- 依赖：P1-1（同文件区域，串行）。

### P2：架构演进（让求解能力可复用、可解释，4–8 周）

> 目标：补齐规划中 M3/M6/M7 的核心缺失，使求解从「事实硬匹配」升级为「可追溯推理」，并打通前端。

**任务 P2-1：统一推理 DAG 与结论三态契约**
- 定义跨栈的 `ReasoningNode {step, premises[], rule_id/knowledge_id, conclusion, evidence_level}` 与结论三态（确定/候选集/信息不足）。
- 网关 `DerivationSteps` 与 Core `SolutionStep` 对齐到该契约；输出显式三态，候选集附判别实验建议。
- 验收：任一门采纳结论可回溯到 rule/knowledge 节点；无跳步；null 结论显式标「信息不足/能力缺口」而非静默。
- 依赖：P0-1（边界清晰后再抽契约）。

**任务 P2-2：世界模型与机理层最小可用版（M3/M4）**
- 在 Core 落地电子层/电离能趋势、离子水合比较、键角/极性等「趋势级」模型（不给绝对值，标 evidence_level），支撑 3 条示范解释链（金属还原性递变、AgCl 溶于氨水、Na₂CO₃ 显碱性）。
- 网关在原子结构/VSEPR/平衡求解器中复用，输出微观解释（标注「帮助理解，不作判定依据」）。
- 验收：3 条解释链可复现且标注层级；不与教材主干矛盾。
- 依赖：P2-1。

**任务 P2-3：图像与文本求解融合**
- 当前图像资产（curve/graph/molecule/crystal）在网关是独立分支，与文本事实链是「或」关系（命中即返回）。需评估：哪些题应「图像读数 + 化学事实交叉验证」后才采纳，避免中间量误采纳（spec 已提及 pdf-272 曲线中间量问题）。
- 验收：依赖图题采纳结论均经化学真值交叉核对；中间量不被误采纳。
- 依赖：P1-2（多部分大题常含图）。

**任务 P2-4：前端接入网关求解，端到端闭环**
- 在 miniapp（或 web）增加「题目求解」入口：调用 `/api/gateway/process`，展示引擎结论 + 分级讲解 + 推理节点（P2-1 的 DAG）。
- 验收：一条完整链路（输入题目 → 求解 → 分级提示 → 复盘）可在前端演示；不泄露未采纳结论。
- 依赖：P2-1。

### P3：质量与运营（持续）

**任务 P3-1：数据资产归一与缺口报表自动化**
- 产出「数据资产清单」：每个 JSON 文件的消费者、版本、校验入口。
- 自动化「主干知识点无事实/无题」缺口报表（规划 T1-9/T4-10，当前仅手工）。
- 题库版本化：`pdf_questions.json` 与事实库版本绑定，回归可版本对比。

**任务 P3-2：规则/事实体系归一**
- 评估 `data/rules/rules.json`（Core RuleEngine 用）与 `data/highschool/*`（网关用）是否需要合并或明确分工；消除「两套规则」的认知负担。

**任务 P3-3：导学与求解融合**
- 让 miniapp 的导学流（submit-step）消费网关求解结果作为「标准推理链」，而非各自维护；错因诊断回溯到求解 DAG 节点。

---

## 6. 关键决策点（需用户确认）

1. **两套栈收敛方向**：是否认同「网关为主求解入口、Core ProblemSolver 退为基准回归引擎」？还是希望反过来（网关能力下沉到 Core）？这决定后续所有新增能力落在哪。
2. **文档主源**：是否以 `.trae/specs/` + 一份新 `ARCHITECTURE.md` 为事实源，把 `docs/` v0.2 标注为「历史规划」？
3. **P1 的三个新立项（多部分大题/纯计算/填空）** 优先级排序——资源有限时先做哪个？（建议：多部分大题 > 纯计算 > 填空，因多部分大题数量最多且解锁后顺带提升路由/判分体系。）
4. **前端闭环目标端**：先做 miniapp 还是 web？（miniapp 已有导学骨架，接入求解成本低；web 静态导出改造成本高但无需小程序审核。）

---

## 7. 风险与取舍

- **最大风险是「在两套栈上同时加能力」导致维护成本翻倍**：P0-1 的收敛决策必须先于 P1 的新能力立项，否则 P1-2/P1-3 会再次分叉。
- **多部分大题子问分发 precision 风险高**：必须独立 spec + 独立 Review 门禁（沿用当前 spec 的 T9 模式），不与事实策展混做。
- **文档更新不应阻塞当前 spec**：P0-2 可与 P1-1 并行，但 P0-1 的收敛决策应尽快拍板。
- **世界模型（P2-2）易过度工程**：严格限定「趋势级、不给绝对值、evidence_level 标注」，不追求数值精度（规划 D10：定性/半定量）。

---

## 附：证据索引

- 网关主链路：[LlmGatewayService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs)（解析 L126 / 分类 L263 / 分发 L328）
- 采纳门控：[GatewayVsLlmEvaluationService.cs L626-749](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L626-L749)
- Core 求解分派：[ProblemSolver.cs L32-62](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Solving/ProblemSolver.cs#L32-L62)
- 能力缺口台账：[data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md)、[engine-capability-gap-fix/spec.md](file:///d:/github/CogniThink-Chem/.trae/specs/engine-capability-gap-fix/spec.md)
- 原始规划（已脱节）：[docs/01-overall-plan.md](file:///d:/github/CogniThink-Chem/docs/01-overall-plan.md)、[docs/02-modules.md](file:///d:/github/CogniThink-Chem/docs/02-modules.md)、[docs/03-tasks.md](file:///d:/github/CogniThink-Chem/docs/03-tasks.md)
- Core 推断引擎设计：[.trae.documents/chem-inference-tutor-engine.md](file:///d:/github/CogniThink-Chem/.trae.documents/chem-inference-tutor-engine.md)
