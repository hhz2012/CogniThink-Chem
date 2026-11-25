# 按题动态生成脚手架链路 - Product Requirements Document

## Overview
- **Summary**: 为化学伴思小程序建立一个「按题动态生成脚手架」的生成器 `buildScaffoldForQuestion(question)`：给定一道题，由**确定性化学引擎产出 A–F 每一步的真值（候选项 + 正确答案）**，LLM 只负责撰写每步的阶梯释放文案，经**校验门**核验「生成的正确答案与引擎真值一致」后才发布为一条 `ScaffoldPractice`，供首页脚手架练习 UI（[ScaffoldPractice](file:///d:/github/CogniThink-Chem/miniapp/src/components/ScaffoldPractice/index.tsx)）消费。把当前「全手工录入、仅 1 条练习」的状态升级为「引擎批量生成、按题真值可靠、零 precision 风险」。
- **Purpose**: 现状中 A–F 原子任务骨架、门控状态机、校验器、阶梯释放 UI 均已完成（[reaction-scaffolding spec](file:///d:/github/CogniThink-Chem/.trae/specs/reaction-scaffolding/spec.md)，Tasks 0–9 全部 completed），但**每一步的候选项 / 正确答案 / 释放文案全是静态预定义 + 手工录入**——[scaffoldPractices.ts](file:///d:/github/CogniThink-Chem/miniapp/src/data/scaffoldPractices.ts) 仅有 1 条练习（`CL2_FEI2_PRACTICE`）。后端化学引擎其实已经能为一道题产出结构化中间真值（物种清单、价态变化、粒子浓度排序、电极反应式等），却没有被回填到脚手架每一步。学生只能练 1 道手工题，脚手架能力被严重浪费。本规格让引擎真值「流」到脚手架，实现按题生成、批量覆盖。
- **Target Users**: 使用化学伴思小程序进行 A–F 精熟练习、需要「每道题都有可交互门控脚手架」的高中学生；以及后续运营/教研批量生成练习资产的内部角色。

## Goals
- 建立生成器 `buildScaffoldForQuestion`：输入一道题 → 输出一条经校验门通过的 `ScaffoldPractice`（或降级标记）。
- 打通「引擎结构化真值 → A–F 各步 `truth.correct`」的映射：A 粒子、B 角色、E 产物/结果优先由引擎产出；C 条件、D 排序在引擎可提供时产出，否则该步降级。
- LLM 严格只写文案（每步 `releases`：hint / probe / scaffold / reveal），**不参与、不触碰任何一步的正确答案**。
- 建立发布前校验门：每步 `correct` 与引擎对应字段重算一致、F 步 `correct` 与原题 answer / 引擎 `FinalResult` 一致、干扰项与正确项不相交；任一失败则该步降级或整题不发布。
- 生成产物落入前端可消费的数据层（与 [scaffoldPractices.ts](file:///d:/github/CogniThink-Chem/miniapp/src/data/scaffoldPractices.ts) 同模式），preview 无后端即可验证 UI 走通。
- MVP 端到端跑通至少 1 个模块（建议 `redox-reasoning`）的 ≥3 道题，证明「引擎真值 → 生成 → 校验 → UI 门控走通」闭环。
- 全程守住全局红线：precision=1、harmCount=0、LOST=0、不蒙数。

## Non-Goals
- **不改变引擎判分链路**：生成器只读引擎求解结果，不修改 `LlmGatewayService` 采纳门、`BuildGatewayAnswerAsync`、eval 判分逻辑；不影响 `gatewayCorrect` 计数与 `precision`。
- **不碰当前仍未解的 ~110 道题**：MVP 只在引擎已解出的题（eval `gatewayCorrect=true`，当前 647 道）上生成；未解的题引擎无可靠 `FinalResult`，不在本轮生成范围内（走人工在环，后续迭代）。
- **不要求前端运行时连后端**：生成是离线批量步骤，产物导出为前端静态资产；preview 沙箱 `localhost:5080` 不可达不影响 UI 验证。
- **本轮不实现「按物质数量自动定单选/多选」的题面级智能**（那是题面理解层，属后续迭代）；本轮 F 步交互沿用题目的存储类型（choice→single-select，填空→按既有约定）。
- **不做 C 步 / D 步的全新引擎建模**：若现有引擎无法可靠产出 C（条件抽取）/ D（竞争排序）的结构化真值，这两步降级为「只读揭示」（沿用 `narrate` 文本模式），不硬上不可靠的门控。是否扩展引擎留作 Open Question。
- **不重写已有手工练习**：`CL2_FEI2_PRACTICE` 保留；生成产物与之并存，不覆盖。
- 不引入新依赖、不改 `app.config.ts` 页面注册、不动 `miniapp/` 下与本任务无关的预存改动。

## Background & Context

### 现有三层资产（均已交付，本 spec 只做「组装 + 生成」）
1. **骨架与门控（静态，已完成）**：[scaffoldChains.ts](file:///d:/github/CogniThink-Chem/miniapp/src/engine/scaffoldChains.ts) 定义 `TASK_TEMPLATES`（A–F）、`MODULE_CHAINS`（5 模块各一条 A–F 链）、`REACTION_SCAFFOLD_CHAIN`（A–E 子集），以及门控纯函数 `initialGateStates` / `markPassed` / `markFailed` / `resetStep` / `resolveStep`。**全部静态预定义**。
2. **校验与释放（静态，已完成）**：[scaffoldValidators.ts](file:///d:/github/CogniThink-Chem/miniapp/src/engine/scaffoldValidators.ts) 的 `validateStep`（multi-check 集合相等 / single-select 等值 / order 顺序全等，纯函数）与 `releaseTier`（错 1/2/3 次 → 三档释放）。
3. **数据（手工，仅 1 条）**：[scaffoldPractices.ts](file:///d:/github/CogniThink-Chem/miniapp/src/data/scaffoldPractices.ts) 的 `CL2_FEI2_PRACTICE`，每步含 `truth.{type,options,correct}` + `releases.{hint,probe,scaffold,reveal}`，全手工录入。

### 目标产物格式（已存在，生成器产出它）
[types/scaffold.ts](file:///d:/github/CogniThink-Chem/miniapp/src/types/scaffold.ts) 的 `ScaffoldPractice`：
- `module: ScaffoldModuleKind`（选哪条 A–F 链）
- `steps: StepPractice[]`，每步：`kind`（A–F）+ `truth: StepGroundTruth`（`{type, options:[{key,label}], correct}`）+ `releases: StepScaffoldReleases`（文案）。

### 后端已有的结构化真值来源（生成器的「原料」）
后端网关 `EngineResponse`（[LlmGatewayService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs)）已暴露结构化字段：`FinalResult`、`ReasoningNodes`、`ExplanationNodes`、`DerivationSteps`、`ConstraintNote`、`ResultProvenance`、`ConclusionState`。各核心引擎产出可溯源的中间结果：

| 引擎 | 公开入口 | 结构化输出 | 可喂给 A–F 哪一步 |
|---|---|---|---|
| [EquationParser](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Parsing/EquationParser.cs) | `Parse` | `ParsedEquation.AllSpecies` / `Left` / `Right` / `Condition` | **A** 粒子清单、**E** 产物物种 |
| [FormulaParser](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Parsing/FormulaParser.cs) | `Parse` | `Species` | A 粒子解析 |
| [OxidationStateAnalyzer](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Balancing/OxidationStateAnalyzer.cs) | `Assign` | `OxidationAssignment.States`（元素→氧化数） | **B** 变价物种识别 |
| [RedoxBalancer](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Balancing/RedoxBalancer.cs) | `SplitHalfReactions` | `RedoxAnalysis.Changes`（价态变化）、`ElectronsTransferred`、`ElectronConservation`、`OxidationHalf` / `ReductionHalf` | **B** 变价 + 电子、**E** 半反应/产物 |
| [AqueousEquilibriumEngine](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Inference/AqueousEquilibriumEngine.cs) | `Infer` | `AqueousResult.Order`（粒子浓度排序）、`ConservationEquations`、`QuantitativeConclusion`、`Steps` | **E** 浓度排序/守恒；A 粒子来源 |
| [CoexistenceEngine](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Inference/CoexistenceEngine.cs) | `Judge` | 共存判定 + `Steps` | **E** 能否共存 |
| [ElectrochemEngine](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Inference/ElectrochemEngine.cs) | `Infer` | `ElectrochemResult.Electrodes`（电极反应式）、`OverallEquation`、`Steps` | **E** 电极产物；B 电极角色 |

此外 [ReasoningContracts.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/ReasoningContracts.cs) 的 `ReasoningNode`（`Step` / `Description` / `Premises` / `rule_id` / `EvidenceLevel`）构成叙述性推理链 DAG，已被 [TutorSolveChainProvider](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/TutorSolveChainProvider.cs) 导学流消费——可作为 LLM 撰写 `releases` 文案的**上下文输入**，但不直接作为 `correct` 来源（它是叙述文本，不是可校验的真值集合）。

### 缺口（本 spec 要补的）
- 没有任何代码把「引擎结构化字段」映射成「A–F 每步 `truth.correct`」。
- 没有「候选项（含干扰项）生成」逻辑：引擎只给正确项，干扰项从哪来、如何保证不撞正确项，未定义。
- 没有「LLM 写文案」的接入点与约束（LLM 不能碰 correct）。
- 没有「发布前校验门」：如何保证生成的 correct 与引擎真值一致、不合入错误产物。
- 没有生成产物的落盘 / 前端消费通路。

## 核心架构：生成器数据流（7 层管线 + 校验门）

```
题干 case (question / options / answer / type)
  │
  ▼
[1] 引擎求解  ──► EngineResponse { FinalResult, ReasoningNodes, ResultProvenance,
 │                                   ConclusionState, 各引擎结构化结果 (AllSpecies/Changes/Order/...) }
 │
 ▼
[2] 链选择    ──► 按 case.category / 路由 → ScaffoldModuleKind → MODULE_CHAINS[module]（A–F 链骨架）
 │
 ▼
[3] 真值提取（按步，correct 只来自引擎）
 │     A 粒子   ← ParsedEquation.AllSpecies / 题面物种抽取
 │     B 角色   ← RedoxAnalysis.Changes / OxidationAssignment.States
 │     C 条件   ← ConstraintNote / 条件抽取器（缺失 → 该步降级）
 │     D 排序   ← 排序真值源（缺失 → 该步降级）
 │     E 产物   ← AqueousResult.Order / ElectrochemResult.Electrodes / RedoxAnalysis
 │     F 作答   ← 存储 answer / EngineResponse.FinalResult
 │
 ▼
[4] 候选项生成  ──► correct（引擎）+ distractors（确定性干扰库 / LLM 文案，校验不撞 correct）
 │                   → 组装为 StepGroundTruth {type, options:[{key,label}], correct}
 │
 ▼
[5] 文案生成（LLM）──► 输入：题干 + 该步 correct + ReasoningNodes；输出：releases {hint,probe,scaffold,reveal}
 │                       【LLM 不读 correct 的值来"判断"，只把它当已知事实写讲解；不产 correct】
 │
 ▼
[6] 校验门（发布铁律，全部通过才发布）
 │     ✔ 每步 correct 与引擎对应字段重算一致（防漂移）
 │     ✔ F 步 correct 与存储 answer / FinalResult 一致
 │     ✔ 每步 distractors ∩ correct == ∅
 │     ✔ options 非空、correct 非空、releases 四档齐全
 │     失败 → 该步降级为「只读揭示」（无 truth，不门控）；关键步(A/E/F)失败 → 整题 unsuitable（不发布）
 │
 ▼
[7] 产物落盘  ──► GeneratedScaffold（ScaffoldPractice 超集，带 provenance/校验摘要）
                   → 导出前端静态资产 → ScaffoldPractice 组件消费
```

### 降级策略（守住 precision 的关键）
不是每道题都能生成完整 A–F 门控链。生成器按「能确定几步就发布几步门控」：
- **完整门控**：A/B/E/F 四步引擎真值齐全且校验通过 → 发布完整 A–F 练习，C/D 若缺则该步显示为「只读揭示」（沿用 `narrate` 文本，学生不可作答但可看讲解）。
- **部分门控**：仅 A/E/F 齐全 → 发布部分链，B/C/D 只读揭示。
- **不发布**：A 或 E 或 F 任一关键步引擎无可靠真值（如引擎 `FinalResult` 为 null、或物种抽取失败）→ 整题返回 `unsuitable`，不生成任何产物（绝不发布猜测的 correct）。
- 所有降级都在 `GeneratedScaffold` 上留 `degradedReason` 字段，便于审计与后续补引擎。

## Functional Requirements

- **FR-1**: 新增生成器入口 `buildScaffoldForQuestion(@case)`（后端 C#，离线批量调用），返回 `GeneratedScaffold | null`（null = 整题不发布）。`@case` 形状对齐 eval 的 `case.{id,type,question,options,answer,...}`。
- **FR-2**: 新增「链选择器」`selectModule(case)`：按题目的 `category` / `taskType` / `ResultProvenance` 映射到 `ScaffoldModuleKind`（redox-reasoning / ionic-system / aqueous-equilibrium / sequential-reaction / electrochem-reasoning）；无法映射时返回 null（整题不发布）。映射表须可单测、可扩展。
- **FR-3**: 新增「真值提取器」`extractStepTruth(stepKind, engineArtifacts)`：为 A/B/E/F（必做）与 C/D（可选）各实现一个提取函数，从引擎结构化结果中取出该步的 `correct` 值（集合或单值）。提取失败返回 `undefined`（触发该步降级），**绝不返回猜测值**。
- **FR-4**: 新增「候选项生成器」`buildOptions(correct, stepKind, distractors)`：把引擎 `correct` 包装成 `StepOption[]`，并混入干扰项。干扰项来源优先级：(a) 确定性干扰库（按 stepKind 的常见混淆项，如同族元素、相邻价态、常见错误离子）；(b) 库不足时由 LLM 生成，但**必须校验生成的干扰项与 correct 集合不相交**，相交则丢弃重生成或回退 (a)。
- **FR-5**: 新增「文案生成器」`generateReleases(step, context, llm)`：调用 LLM 为每步生成 `hint` / `probe` / `scaffold` / `reveal` 四档。LLM 输入包含题干、该步 `correct`（作为已知事实）、`ReasoningNodes`（叙述参照）；**LLM 输出只含文案字段，不含 correct**。LLM 不可用或输出不合规时，回退到该 stepKind 的通用模板文案（`TASK_TEMPLATES` 的 `goal` / `pitfall` + 引擎 `Steps` 文本），保证永不崩。
- **FR-6**: 新增「校验门」`validateScaffold(scaffold, engineArtifacts)`：发布前对整条 `GeneratedScaffold` 跑 FR-3~FR-5 的反向核对——每步 `correct` 与引擎字段重算一致、F 步与 answer/FinalResult 一致、distractors 不撞 correct、字段齐全。返回 `{ok, errors[]}`；`ok=false` 时按降级策略处理（步级降级 / 整题 null）。
- **FR-7**: 新增「生成产物类型」`GeneratedScaffold`（`ScaffoldPractice` 超集）：在 `ScaffoldPractice` 基础上增加 `provenance`（每步 correct 来源引擎字段，如 `RedoxAnalysis.Changes`）、`degradedSteps`（哪些步降级为只读及原因）、`validationSummary`（校验门结果）、`sourceQuestionId`。便于审计与回放。
- **FR-8**: 生成产物落盘为前端可消费的静态资产：批量生成脚本输出 `miniapp/src/data/scaffolds/generated/index.ts`（或 `.json`），导出 `GeneratedScaffold[]`；前端 [scaffoldPractices.ts](file:///d:/github/CogniThink-Chem/miniapp/src/data/scaffoldPractices.ts) 的 `SCAFFOLD_PRACTICES` 与之合并（手工 + 生成并存），`getScaffoldPractice(id)` 同时查两份。
- **FR-9**: 前端 ScaffoldPractice 组件支持「降级步」渲染：当某步 `truth` 缺失（只读揭示）时，该步显示为可展开的讲解文本（来自 `releases.reveal` 或引擎 `Steps`），不渲染作答控件、不参与门控（直接视为 passed 解锁下一步，或按设计锁定——见 Open Questions）。
- **FR-10**: 批量生成命令：提供一个可重复运行的入口（如 `dotnet run --project <Generator> -- --out <path> --module redox-reasoning --limit 50`），只生成引擎已解（`gatewayCorrect=true`）的题；幂等（同输入同输出），可审计（打印每题的发布/降级/不发布及原因）。
- **FR-11**: 护栏测试：生成器对全题库（或 MVP 限定题库）运行后，断言「凡发布的脚手架，其每步 correct 与引擎字段一致」「F 步 correct 与存储 answer 一致」「distractors 不撞 correct」，0 违例。与 eval 一样作为 precision 护栏。

## Non-Functional Requirements
- **NFR-1**: 生成器是**只读**消费者：不修改任何引擎求解逻辑、不修改题干/选项/answer/事实库；`dotnet test` 全绿，eval `gatewayCorrect` / `precision` 字节级不变（零回归）。
- **NFR-2**: TypeScript / C# 零诊断错误（`GetDiagnostics` 为空）；前端 preview 编译成功。
- **NFR-3**: 不引入新依赖；不改 `app.config.ts` 页面注册。
- **NFR-4**: 生成产物可追溯：每条 `GeneratedScaffold` 带 `sourceQuestionId` 与每步 `provenance`，任一发布的 correct 都能回查到引擎字段。
- **NFR-5**: 降级安全：任何引擎异常 / LLM 异常 / 字段缺失 → 返回 null 或降级步，不抛错、不发布半成品。
- **NFR-6 (rubric)**: 生成的脚手架在「不泄露答案的前提下引导思维」与「交互门控能暴露学生认知漏洞」两方面贴合度高。

## Constraints
- **Technical**: 后端 dotnet 8（生成器 + 引擎）；前端 Taro 4 / React 18 / TS / SCSS Modules；preview 无后端，产物须静态可消费。
- **Global Red Lines（全程不可逾越）**:
  - precision=1：发布的每步 `correct` 必须与引擎真值一致；引擎不确定的步降级，**绝不发布猜测 correct**。
  - harmCount=0：生成的脚手架不得包含化学错误的真值或会误导学生的干扰项；校验门是最后防线。
  - LOST=0：不改判分链路，已判正题不受影响。
  - 不蒙数：引擎 `FinalResult=null` 或关键步提取失败 → 整题不发布。
  - LLM 只写文案，不产 `correct`；LLM 输出不参与 pass/fail 判定（判定仍由 [validateStep](file:///d:/github/CogniThink-Chem/miniapp/src/engine/scaffoldValidators.ts) 纯函数完成）。
- **Business**: 不破坏现有题库做题、错题本、物质速查、首页脚手架入口等已完成功能；不碰 `miniapp/` 下与本任务无关的预存改动。
- **Dependencies**: 依赖现有 [scaffoldChains](file:///d:/github/CogniThink-Chem/miniapp/src/engine/scaffoldChains.ts) / [scaffoldValidators](file:///d:/github/CogniThink-Chem/miniapp/src/engine/scaffoldValidators.ts) / [types/scaffold](file:///d:/github/CogniThink-Chem/miniapp/src/types/scaffold.ts)；依赖后端 `AqueousEquilibriumEngine` / `RedoxBalancer` / `ElectrochemEngine` / `EquationParser` / `OxidationStateAnalyzer` 与 `LlmGatewayService`。

## Assumptions
- 生成器在**后端 C# 离线批量**运行（可复用强引擎 + LLM API key），产物导出为前端静态资产；不要求前端运行时调后端。如用户希望改为前端运行时生成，见 Open Questions。
- MVP 先做 `redox-reasoning` 模块（引擎真值最齐全：EquationParser + OxidationStateAnalyzer + RedoxBalancer 覆盖 A/B/E），C/D 步先降级，验证闭环后再推广到其余 4 模块。
- LLM API 在生成环境可用（eval 环境 `llmAvailable=False`，但生成脚本是独立运行环境，可配 key）；LLM 不可用时文案回退模板，不阻塞真值生成。
- F 步 `correct` 以**存储 answer**为准（eval 已验证 answer 可靠的题），引擎 `FinalResult` 作交叉校验；二者冲突时该题不发布（标 conflict，人工复核）。
- 现有 `ReasoningNodes` DAG 作为文案上下文，不作为 correct 来源；若后续 `ReasoningNode` 增加结构化 `correct` 字段，可升级为真值来源（后续迭代）。

## Acceptance Criteria

### AC-1: 生成器入口与产物类型
- **Type**: `rule`
- **Given**: 存在生成器 `buildScaffoldForQuestion` 与 `GeneratedScaffold` 类型
- **When**: 对一道引擎已解的氧化还原题调用
- **Then**: 返回非 null 的 `GeneratedScaffold`，含 `module`、`steps[]`（每步 kind/truth/releases）、`sourceQuestionId`、`provenance`
- **Pass Condition**: 产物结构完整、类型检查通过
- **Evidence**: 源码 + 单元测试

### AC-2: 引擎真值 → A/B/E/F 步 correct 映射
- **Type**: `rule`
- **Given**: 一道引擎已解的氧化还原题（如 `CL2_FEI2` 同类）
- **When**: 生成器提取各步 correct
- **Then**: A 步 correct = 引擎 `AllSpecies` 中的体系粒子；B 步 correct = `RedoxAnalysis.Changes` 中的变价物种；E 步 correct = 引擎产物/半反应结论；F 步 correct = 存储 answer
- **Pass Condition**: 四步 correct 均与对应引擎字段重算一致（单测断言）
- **Evidence**: 单元测试 + 生成日志

### AC-3: 校验门拦截错误产物
- **Type**: `rule`
- **Given**: 构造一个 correct 与引擎字段不一致的脚手架（模拟漂移）
- **When**: 过校验门
- **Then**: `ok=false`，该步降级或整题 null，不发布
- **Pass Condition**: 0 条错误 correct 流入产物
- **Evidence**: 护栏单元测试（正例通过 + 反例拦截）

### AC-4: LLM 只写文案不碰 correct
- **Type**: `rule`
- **Given**: 生成器运行（LLM 可用与不可用两种场景）
- **When**: 检查产物
- **Then**: 所有 `correct` 均来自引擎字段（`provenance` 非空且指向引擎）；LLM 输出仅出现在 `releases` 文案字段；LLM 不可用时 releases 回退模板且不崩
- **Pass Condition**: 无任何 correct 由 LLM 产出；文案四档齐全或回退齐全
- **Evidence**: 源码审查 + 单测（mock LLM）

### AC-5: 干扰项不撞正确项
- **Type**: `rule`
- **Given**: 任一步生成的 options
- **When**: 校验 distractors 与 correct
- **Then**: `distractors ∩ correct == ∅`
- **Pass Condition**: 全量生成产物 0 违例
- **Evidence**: 护栏测试扫描全部产物

### AC-6: MVP 端到端走通
- **Type**: `rule`
- **Given**: `redox-reasoning` 模块 ≥3 道引擎已解题生成产物，已落入前端静态资产
- **When**: 在 preview 打开 ScaffoldPractice 练习其中一题
- **Then**: A 步可勾选作答、答对解锁 B（或降级步）、答错锁定并释放 hint；完整走到 F 步；门控状态机行为与手工 `CL2_FEI2_PRACTICE` 一致
- **Pass Condition**: 一条生成的脚手架在 preview 完整走通，无运行时错误
- **Evidence**: 预览截图 + 操作录屏/步骤

### AC-7: 零回归（不碰判分链路）
- **Type**: `rule`
- **Given**: 本 spec 全部改动完成
- **When**: 跑 `dotnet test --nologo -c Debug` 与全量 eval（`--tag regen_check`）
- **Then**: 测试全绿（预期 2415 通过 0 失败）；eval `gatewayCorrect` / `precision` / `harm` 与改动前字节级一致
- **Pass Condition**: 测试全绿 + eval 无回归
- **Evidence**: 测试日志 + eval summary 对比

### AC-8: 降级与不发布审计
- **Type**: `rule`
- **Given**: 对一批题（含引擎未解的题）运行生成器
- **When**: 查看生成日志
- **Then**: 引擎未解的题 → 整题不发布（null），日志标 `unsuitable` 及原因；关键步缺失的题 → 降级步标 `degradedReason`；不发布任何猜测 correct
- **Pass Condition**: 所有未发布/降级均有原因，无静默丢弃
- **Evidence**: 生成日志

### AC-9: 生成脚手架的教学贴合度
- **Type**: `rubric`
- **Dimension**: 生成的脚手架是否在不泄答案的前提下有效引导 A–F 思维，且门控能暴露学生认知漏洞
- **Scale**: 1-5；Pass Threshold ≥ 4
- **Evidence**: 预览操作 + 独立评审

## Open Questions
- [x] **生成器运行位置**：后端 C# 离线批量生成 + 前端静态资产交付（当前默认），还是要求前端运行时调后端 API 生成？→ 已采纳默认前者（preview 可验证、与 data/challenges 同模式）。
- [x] **MVP 模块与题量**：先做 `redox-reasoning` ≥3 题（默认），还是先做覆盖面更广的 `aqueous-equilibrium`？→ 已采纳默认氧化还原（引擎真值最齐全）；已生成 4 条（pdf-104/110/112/298）。
- [x] **C 条件 / D 排序步**：本轮降级为只读揭示，还是同步扩展引擎产出这两步的结构化真值？→ 已采纳默认降级（守住 precision，不硬上不可靠门控）；引擎扩展留后续迭代（Task 8）。
- [x] **降级步的门控行为**：某步无 truth（只读揭示）时，是「自动视为 passed 解锁下一步」还是「学生需点『已理解』才解锁」？→ 已采纳默认后者（需显式确认，保持认知节奏）；**已通过 Task 7 preview UI 验证**。
- [x] **F 步交互形态**：选择题 → single-select 已明确；填空题 / 计算题的 F 步是否纳入门控（输入框校验），还是 F 步只对选择题生效、其余题型 F 步降级？→ 已采纳默认 F 步仅选择题门控，填空/计算 F 步只读揭示。
- [x] **产物存储格式**：`.ts`（类型安全、与 scaffoldPractices 同模式）还是 `.json`（后端直出、免翻译）？→ 已采纳默认 `.ts`（前端 import 零摩擦，生成脚本负责翻译）。
- [x] **与 Task 7（hard-bones 结项）的优先级**：用户已选「方案 B 先写 spec」，Task 7 结项报告稍后收尾；本 spec 获批后是否立即进入实现，还是等 Task 7 结项后再开工？→ 已采纳默认 spec 批准即可开工 MVP，与 Task 7 收尾并行不冲突（本 spec 不碰判分链路）。

## 与现有 spec 的关系
- **继承** [reaction-scaffolding](file:///d:/github/CogniThink-Chem/.trae/specs/reaction-scaffolding/spec.md)：A–F 骨架、门控、校验器、阶梯释放全部复用，不重定义；本 spec 只补「按题生成真值与文案」。
- **平行** [differentiated-solve-models](file:///d:/github/CogniThink-Chem/.trae/specs/differentiated-solve-models/spec.md)：题型侧 `solveData`（手工 84 题）与本 spec 的 `GeneratedScaffold`（引擎生成）是两套真值资产——前者服务 challenge 讲解面板，后者服务首页脚手架练习；二者可共享引擎真值来源但产物不同，互不覆盖。
- **不冲突** [hard-bones-coverage-push](file:///d:/github/CogniThink-Chem/.trae/specs/hard-bones-coverage-push/spec.md)：本 spec 不改判分链路，eval 覆盖率无回归；生成范围限定在已解 647 题，不碰未解 110 题。
