# 化学模拟器统一架构 — 设计规格（本轮：设计交付，不编码）

## Overview
- **Summary**：对化学伴思求解能力进行整体重构，建立三个**主体类**——**化学模拟器**（`ChemicalSimulator`）、**化学多步推理模拟器**（`ChemicalMultiStepReasoningSimulator`）与**平衡态数值模拟器**（`EquilibriumStateSimulator`），并定义一套**统一输入格式**。本轮（本 spec）**仅产出设计文档，不写代码、不改数据**：交付①三个主体类的详细设计（含 C# 签名级契约、吸收合并现有 `ChemicalSimulationEngine` 的方案、向后兼容策略、零回归论证）；②对其余求解过程的分类分析报告。设计批准后，下一轮直接按设计编码实现。
- **Purpose**：把目前散落在 `ChemicalSimulationEngine` / `ChemicalReverseInferenceEngine` / Core 各 Inference 引擎 / 71 个 Gateway 专用求解器中的"反应推演"能力收敛到一套统一、可组合、带证据与优先级的模拟器契约。本轮先把"主体类长什么样、怎么合并、怎么兼容、零回归怎么保证"设计到可直接照抄编码的粒度，再落地。
- **Target Users**：求解引擎开发者（下一轮按设计编码与迁移）、导学/讲解层、前端。

## 本轮交付物（Deliverables）
1. `chemical-simulator-design.md`：详细设计文档。必须包含 C# 签名级的类/方法/DTO 定义、状态机、合并方案、兼容适配器、零回归论证。
2. `solver-landscape-analysis.md`：其余求解器分类分析报告（A 可组合 / B 需独立模拟器 / C 非模拟器）。

> 本轮**不修改任何 `.cs` 代码与任何 JSON 数据**。实现留待设计批准后的下一轮。

## Goals
- 统一输入契约 `ChemicalSystemInput`（含 `ChemicalConditions`），作为三个模拟器共用的**唯一**输入真相。
- `ChemicalSimulator` 成为**唯一**正向模拟主体类：**吸收合并**现有 `ChemicalSimulationEngine`（旧类退化为薄兼容适配器，委托新类），输出多步反应，每步含参与物质、化学依据（结构化溯源）、优先级（分数+排名）、定量（反应量/产物量/剩余量）。
- `ChemicalMultiStepReasoningSimulator` 成为多步推理主体类：多步条件设定 → 状态轨迹；支持状态查询、what-if 分支（纯函数、不污染原轨迹）、目标导向候选搜索（复用逆向回放验证，不蒙数）。
- `EquilibriumStateSimulator` 成为平衡态数值主体类：基于 `acid-constants.json`（Ka/Kb/Ksp/Kw，人教版教材数据，精确有理 `ConstantSpec`）计算 pH、离子浓度、Ksp 残留浓度、沉淀转化平衡常数、粒子浓度排序与三大守恒；只在常数与输入齐备时输出，缺数据返回欠定（不蒙数）；可被多步推理模拟器按需挂载到状态快照上。
- 明确向后兼容策略：现有 `ChemistryController`、旧端点、现有测试在下一轮实现后保持行为不变。
- 明确零回归论证：不触 Gateway live 路径、不改 JSON 数据 → 757 题 eval 红线（precision=1 / harm=0 / LOST=0 / WORSEN=0）保持不变。
- 产出其余求解器分类与迁移路线，供后续 spec 承接。

## Non-Goals（本轮明确不做）
- 不写任何 C# 代码（`src/` 下文件不动）。
- 不修改任何 JSON 数据文件。
- 不接入 Gateway live 判分路径。
- 不迁移 71 个 Gateway 求解器（仅分类与建议）。
- 不做电离能/键能/晶格能等结构能的数值绝对值求解（热化学与分子结构属 B 类独立模拟器，不在本期反应体系范围）。平衡常数/pH/离子浓度数值本期纳入 `EquilibriumStateSimulator`。
- 不做前端展示。

## Background & Context
- 现有正向引擎 [ChemicalSimulationEngine.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Chemistry/ChemicalSimulationEngine.cs) 已实现：知识库检索 → 按类型优先级/金属活动性择一 → 计量算限量试剂与剩余量 → 输出事件与最终状态。其 `SimulationRequest` 接近统一输入，但 `SimulationEvent` **未暴露优先级/分数**，化学依据仅为拼接字符串 `Evidence`。按用户决策，该引擎将被 `ChemicalSimulator` **吸收合并**，旧类仅留薄适配器。
- 现有逆向引擎 [ChemicalReverseInferenceEngine.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Chemistry/ChemicalReverseInferenceEngine.cs) 的"反查 + 正向回放验证"可被多步推理模拟器的目标导向搜索复用（设计为委托/映射，不重复实现）。
- **关键事实（零回归依据）**：grep 全 `src/`，`ChemicalSimulationEngine` / `ChemicalReverseInferenceEngine` / `SimulationRequest` / `ReverseInferenceRequest` 仅在 [Program.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Program.cs) 与 [ChemistryController.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Api/Controllers/ChemistryController.cs) 中引用，**未接入 `LlmGatewayService` live 求解路径**。因此在该命名空间内合并/演进模拟器，对 757 题 eval 基线零影响。
- 领域实体在 [ChemicalEntities.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Domain/Chemistry/ChemicalEntities.cs)：`Substance` / `Reaction`（含 `KnowledgeIds`）/ `ReactionType`，新模拟器直接复用。
- 知识库 [ChemicalKnowledgeBase.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Chemistry/ChemicalKnowledgeBase.cs) 从 `data/chemistry/*.json` 加载。
- 已完成的 [world-model-minimal spec](file:///d:/github/CogniThink-Chem/.trae/specs/world-model-minimal/spec.md) 是微观机理/趋势级解释（EvidenceLevel=Trend，不作判定依据），与本 spec 的反应状态模拟正交。
- 红线（不可破）：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 事实只存 JSON / 不注入答案。

## 用户已确认决策（resolved）
- **D-1（重构力度）**：吸收合并为唯一主体类——`ChemicalSimulator` 吸收 `ChemicalSimulationEngine`；旧类退化为薄兼容适配器；`ChemicalSystemInput` 为唯一输入真相。✅
- **D-2（推进节奏）**：本轮仅产出设计，不编码；设计批准后下一轮直接实现。✅
- **D-3（多步推理范围）**：状态轨迹 + what-if 分支 + 目标导向搜索。✅
- **D-4（分析产物）**：其余求解器本轮仅分类分析，不实际迁移；迁移由后续 spec 承接。✅
- **D-5（平衡态数值范围·追加）**：用户要求将平衡态数值（pH / 离子浓度 / Ksp 残留 / 沉淀转化 K / 粒子排序与守恒）纳入本期设计范围，由第三个主体类 `EquilibriumStateSimulator` 承载，数据取自已落地的 `acid-constants.json`（人教版，Ka/Kb/Ksp/Kw）。✅

## Functional Requirements（设计文档必须覆盖）
- **FR-1（统一输入契约）**：定义 `ChemicalSystemInput`（物种用量 `SpeciesInput{Substance, Moles, Concentration?, Volume?, Role?}` + `ChemicalConditions`）。`ChemicalConditions` 集中所有环境开关（Aqueous/Heating/Ignition/Catalyst/Electricity/Light/Concentrated/Dilute/Air/Acidic/Alkaline/MaxSteps），并含 `ReturnConsidered` 开关。三个模拟器共用此输入（`Moles` 供反应模拟，`Concentration`/`Volume` 供平衡数值）。
- **FR-2（ChemicalSimulator 主类）**：`Simulate(ChemicalSystemInput)` → `SimulationTrace`。返回多步 `ReactionEvent`。
- **FR-3（每步结构化输出）**：`ReactionEvent` 含 `Consumed`/`Produced`（参与物质 → mol）、`Basis`（`ReactionId` + `KnowledgeIds` + 选择理由）、`Priority`（`Score` + `Rank`）、`Extent`（反应进度）与各物质剩余量。
- **FR-4（候选可观测）**：`ReturnConsidered=true` 时返回本步未发生候选及分数/原因，默认关闭。
- **FR-5（最终状态）**：`SimulationTrace` 含最终体系状态（物种初始/剩余量、颜色、沉淀、气体、现象）。
- **FR-6（多步推理主类）**：`Reason(ReasoningPlan, ChemicalSystemInput initial)` → `ReasoningResult`，产出 `StateSnapshot[]` 状态轨迹。`ReasoningPlan` 步骤类型：`AddSpecies` / `SetConditions` / `Assert`。
- **FR-7（状态查询）**：可查询任一快照的物种量/颜色/沉淀/气体/是否存在某物质。
- **FR-8（what-if 分支）**：`WhatIf(StateSnapshot from, ChemicalConditions alternative)` → 独立分支 `SimulationTrace`，不修改原轨迹（纯函数）。
- **FR-9（目标导向搜索）**：`Search(ChemicalSystemInput initial, ReasoningGoal goal)` → 排序候选 `{Steps, Trace, Verification}`，候选来自知识库、经正向回放验证，未通过如实标注。
- **FR-9A（平衡态数值主类）**：`EquilibriumStateSimulator.Compute(ChemicalSystemInput, EquilibriumQuery)` → `EquilibriumNumericalResult`，基于 `acid-constants.json` 计算：弱酸/弱碱 pH、盐水解 pH、缓冲 pH（Henderson–Hasselbalch）、Ksp 残留离子浓度（`c=Ksp/[OH⁻]ⁿ` 或题给 Ksp）、沉淀转化平衡常数 `K=Ksp1/Ksp2`（精确有理 `ConstantSpec.Ratio`）、粒子浓度排序与三大守恒（委托 Core `AqueousEquilibriumEngine`）。所有常数来自 JSON；缺常数/缺输入/歧义 → 返回欠定，不蒙数。
- **FR-9B（数值与状态的组合）**：`ChemicalMultiStepReasoningSimulator` 可按需在状态快照上挂载 `EquilibriumNumericalResult`（如"该步 pH / 残留浓度"），使多步推理能对不同状态做数值推理；`ChemicalSimulator`（反应量，mol）与 `EquilibriumStateSimulator`（浓度/pH）解耦，按需组合。
- **FR-10（吸收合并方案）**：设计文档必须说明 `ChemicalSimulationEngine` 的哪些逻辑迁入 `ChemicalSimulator`、旧类如何退化为薄适配器（方法签名不变，内部委托新类并映射返回类型），以及 `ChemistryController` 与现有测试如何保持绿色。
- **FR-11（零回归论证）**：设计文档必须论证下一轮实现不修改 `Application/Gateway/` 与 `data/`，并列出实现阶段需核验的红线清单。
- **FR-12（分析报告）**：`solver-landscape-analysis.md` 将 Gateway（71 文件）与 Core Inference/Solving/Rules/Molecules 能力分为 A/B/C 三类，每类 ≥3 代表 + 理由 + 推荐迁移顺序与风险。

## Non-Functional Requirements
- **NFR-1（不蒙数）**：设计中所有反应/产物/状态必须来自知识库；无命中返回空并说明。
- **NFR-2（证据可溯源）**：`Basis.KnowledgeIds` 指向 `Reaction.KnowledgeIds` 真实条目。
- **NFR-3（设计可直接实现）**：设计文档的 C# 签名/字段/状态机粒度，应使下一轮编码无需再做架构级决策。
- **NFR-4（本轮零改动）**：本 spec 实现阶段（写文档）不触碰 `src/*.cs` 与 `data/*.json`。

## Constraints
- **Technical**：.NET 8 / C# 12；目标落点 `src/CogniThink.TutorBrain.Api/Application/Chemistry/`；复用 `ChemicalEntities` 与 `ChemicalKnowledgeBase`；不引新包。
- **Business**：红线 precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 事实只存 JSON。

## Acceptance Criteria

### AC-1：设计文档定义统一输入且被三模拟器共用
- **Type**: `rule`
- **Given**: `chemical-simulator-design.md`
- **When**: 检查文档内容
- **Then**: 定义了 `ChemicalSystemInput` + `ChemicalConditions`（集中全部环境开关）；`ChemicalSimulator`、`ChemicalMultiStepReasoningSimulator`、`EquilibriumStateSimulator` 的主方法签名均以 `ChemicalSystemInput` 为体系输入
- **Pass Condition**: 文档中三类主方法签名包含 `ChemicalSystemInput`
- **Evidence**: 文档章节

### AC-2：设计文档规定 ChemicalSimulator 吸收合并为唯一主体类且输出含优先级/依据/定量
- **Type**: `rule`
- **Given**: `chemical-simulator-design.md`
- **When**: 检查文档
- **Then**: 明确 `ChemicalSimulator` 吸收 `ChemicalSimulationEngine`（旧类退化为薄适配器，附合并映射）；`ReactionEvent` 字段含 `Consumed`/`Produced`/`Basis`/`Priority`/`Extent`/剩余量
- **Pass Condition**: 文档含合并方案 + 完整 `ReactionEvent` 字段表
- **Evidence**: 文档章节

### AC-3：设计文档规定多步推理 API（轨迹/分支/搜索）与纯函数分支语义
- **Type**: `rule`
- **Given**: 设计文档
- **When**: 检查
- **Then**: 定义 `Reason`（轨迹）/`QueryState`/`WhatIf`（标注纯函数、不改原轨迹）/`Search`（复用逆向回放验证、不蒙数），含 `StateSnapshot`/`ReasoningPlan`/`ReasoningGoal` 模型
- **Pass Condition**: 四类方法与三类模型均在文档中定义
- **Evidence**: 文档章节

### AC-4：设计文档含 C# 签名级契约，粒度可直接实现
- **Type**: `rubric`
- **Dimension**: 设计的具体可实现程度
- **Scale**: 1-5
- **Anchors**: 1 = 仅文字描述无签名；3 = 有类名/方法名但字段不全；5 = 所有 DTO 字段、方法签名、关键状态机/算法步骤均以 C# 代码块或表格给出，下一轮编码无需架构级决策
- **Pass Threshold**: >= 4
- **Evidence**: 文档

### AC-5：求解器分类分析报告完整可操作
- **Type**: `rubric`
- **Dimension**: 报告完整性与可操作性
- **Scale**: 1-5
- **Anchors**: 1 = 仅罗列文件名；3 = 有分类但代表不足；5 = A/B/C 三类各 ≥3 代表、理由具体到引擎职责、含推荐迁移顺序与风险
- **Pass Threshold**: >= 4
- **Evidence**: `solver-landscape-analysis.md`

### AC-6：设计文档含零回归论证与实现期红线核验清单
- **Type**: `rule`
- **Given**: 设计文档
- **When**: 检查
- **Then**: 含"不修改 Gateway / 不改 JSON"的论证，并列出实现期需核验的红线（precision/harm/LOST/WORSEN）
- **Pass Condition**: 文档有专节论证 + 核验清单
- **Evidence**: 文档章节

### AC-7：本轮零代码零数据改动
- **Type**: `rule`
- **Given**: 本 spec 实现阶段完成后的工作树
- **When**: `git status --short`
- **Then**: 改动仅限 `.trae/specs/chemical-simulator-architecture/` 下的 `.md` 文件；`src/` 与 `data/` 无改动
- **Pass Condition**: 无 `src/`、`data/` 路径出现在改动清单
- **Evidence**: git status 输出

### AC-8：设计文档含平衡态数值模拟器设计（数据真实、不蒙数）
- **Type**: `rule`
- **Given**: 设计文档
- **When**: 检查
- **Then**: 定义 `EquilibriumStateSimulator` 主类与 `Compute` 签名；明确数据来源为 `acid-constants.json`（Ka/Kb/Ksp/Kw）；列出可计算项（pH/离子浓度/Ksp 残留/转化 K/排序守恒）及每项的数据需求；明确缺数据返回欠定（不蒙数）；定义与多步推理模拟器的挂载方式
- **Pass Condition**: 上述要素均在文档中出现
- **Evidence**: 文档章节

## Open Questions
- 无（D-1 ~ D-5 已由用户确认）。设计批准后下一轮直接编码实现。
