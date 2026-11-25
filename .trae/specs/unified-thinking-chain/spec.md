# 全题型思维链 DAG 统一规范 - Product Requirements Document

> **状态**：**已完成**（Specify → Plan → Approve → Implement → Review 五阶段全部完成）。用户已批准（5 个 OQ 按推荐默认值裁决）；实现产物见 `tasks.md` 完成证据汇总；独立评审结论见 `review.md`（AC-9 评分 6/6 ≥ 4，3 项发现均已修复复核通过，验收关闭）。
> **基线**：R3 已完成（氧化还原两阶段模型 pass）。本规格 R4 不回退 R1–R3 任何已验证行为。
> **红线（不可破）**：precision=1 / harmCount=0 / 不编造化学事实（价态、产物、守恒式须与高中化学定论及题目 `explanation`/`guide` 一致）/ 不主动 commit / 回复中文。

## Overview
- **Summary**：把化学伴思小程序里**三套并行、分类不对齐**的「解题思维链」——题型侧（`engine/solveModels.ts`，6 个 `ChemCategory` 线性步骤）、反应侧（`engine/reactionSolveModels.ts`，19 个 `ReactionSolveKind` 线性步骤）、脚手架侧（`engine/scaffoldChains.ts` + `scaffoldBranches.ts`，5 个 `ScaffoldModuleKind` 的 A–F 串行门控链）——**统一为一套「题型级标准推导图 DAG」规范**：每个题型定义「步骤节点 + 显式依赖边（哪步依赖哪步的输出）+ 分支条件 + 阶段分组」，具体题目实例化时套用这张图，逐步门控展开给学生。
- **Purpose**：现状三套链各讲各的、互不对齐（6 题型 ≠ 19 反应 kind ≠ 5 脚手架模块；无机推断/有机/化学平衡在脚手架侧缺模块；所有步骤都是线性列表，无显式依赖边与分支结构），导致「这类题怎么一步步想、哪步依赖哪步、条件一变结论怎么分叉」无法被结构化表达，学生看到的是扁平步骤串而非可追溯的推导网络。本规格定义统一的 DAG 契约 + 6 题型标准推导图 + 对齐映射 + 实例化与逐步展开机制，让思维链从「线性文案」升级为「有依赖、有分支、可门控」的推导图。
- **Target Users**：① 使用小程序刷题、需要「一类题一套可展开推导图」的高中学生；② 后续维护/扩充题库与讲解的开发者（统一契约降低新增题型/反应的成本）。

## Goals
- 定义统一的思维链 DAG 数据契约（`ThoughtChainNode` / `ThoughtChainDAG`），**复用并迁移**现有 `SolveModelStep`、`AtomicTask`、`ScaffoldBranch` 三类资产，不另起炉灶。
- 为 6 个题型各产出一张**标准推导 DAG**：节点 + `dependsOn` 依赖边 + `skipWhen` 可跳过条件 + `branch` 条件分支 + `stage` 阶段分组 + 认知层级。
- 建立 `ChemCategory → ReactionSolveKind → ScaffoldModuleKind` 的对齐映射表，**填补脚手架侧 3 个缺口**（化学平衡、无机推断、有机反应无对应模块）。
- 定义题目实例化机制：DAG 中哪些字段是题型通用、哪些来自本题 `solveData`/`guide`；preview 不泄答案、full 逐步展开。
- 定义逐步展开机制：把现有 A–F 线性门控（locked/unlocked/passed/failed）**泛化到 DAG**（节点在全部前置 passed 后解锁），并统一接入分支解析与阶梯释放（hint/probe/scaffold/reveal）。
- 保持向后兼容：现有 `solveModels` / `reactionSolveModels` / `scaffold*` 在过渡期并存，R1–R3 已验证行为零回归；无 `solveData` 的题优雅降级不崩。

## Non-Goals
- 本轮（规划阶段）**不实现任何代码**；实现须待用户评审批准 `spec.md` + `tasks.md` 后进行。
- 不改动后端 C# 引擎（`src/CogniThink.*`）的推理节点契约——那是另一套已批准的 `unified-reasoning-dag` spec，与本前端思维链是不同栈、不重叠。
- 不重写全部 84 道题的 `guide` 字段（现有 hand-authored guide 保留；DAG 实例化优先消费已有的 `solveData`，`guide` 作为叙述补充）。
- 不引入自动配平/自动化学推理算法；节点结论仍来自基于题目已验证 `explanation`/`guide` 手填的结构化 `solveData`，引擎不做化学计算（precision=1）。
- 不在本轮把 `InorganicInference` / `OrganicChemistry` 的 `panelKind` 从 `generic` 升级为专属可视化面板——专属面板是后续迭代（本规格只把它们的**推导 DAG** 从占位提升为完整定义，并补齐脚手架模块；专属面板 UI 留作后继 spec）。
- 不接真实后端 AI 推理；所有模型与数据为前端确定性资产。

## Background & Context（现状审计，精确到文件）

代码位置：`miniapp/src/`。当前存在**三套并行**的思维链资产，分类互不对齐：

### 现状 1：题型侧 — `engine/solveModels.ts`（6 类，线性步骤）
- [solveModels.ts](file:///d:/github/CogniThink-Chem/miniapp/src/engine/solveModels.ts)：`SOLVE_MODELS: Record<ChemCategory, SolveModel>`，每个 `SolveModel` = `{ category, name, icon, color, panelKind, steps: SolveModelStep[], checks }`。
- `SolveModelStep`（L8-17）= `{ index, title, goal, layer, analyze, stage? }`——**线性列表，无依赖边字段**；只有氧化还原用 `stage?: 'stage1'|'stage2'` 做了两阶段分组（R3）。
- 6 类现状：氧化还原 11 步（两阶段）/ 离子平衡 3 步 / 化学平衡 4 步 / 电化学 3 步 / 无机推断 3 步 / 有机 3 步。
- **缺口**：`InorganicInference` 与 `OrganicChemistry` 的 `panelKind` 仍是 `'generic'`（占位），见 L279 / L315。
- 消费方：[SolvePanels/index.tsx](file:///d:/github/CogniThink-Chem/miniapp/src/components/SolvePanels/index.tsx) L38-92 按 `category` 取模型，preview 渲染 `StepsView`（只读 `model.steps`，不碰 `solveData`），full 按 `panelKind` + `solveData.kind` 选专属面板，否则 `GenericSolvePanel`。

### 现状 2：反应侧 — `engine/reactionSolveModels.ts`（19 kind，线性步骤）
- [reactionSolveModels.ts](file:///d:/github/CogniThink-Chem/miniapp/src/engine/reactionSolveModels.ts)：`REACTION_SOLVE_MODELS: Record<ReactionSolveKind, ReactionSolveModel>`，每个含 `steps: SolveModelStep[]`（**复用题型侧的 `SolveModelStep` 类型**）+ `checks`。
- 9 个无机主类型（化合/分解/置换/复分解/燃烧/氧还/电极/离子平衡/热化学）各 3–4 步；9 个有机子类型共享 `ORGANIC_SHARED_STEPS`（认官能团/定反应类型 2 步）+ 子类型专属 3 步；+ `reaction-generic` 降级。
- 真值数据：[types/reaction.ts](file:///d:/github/CogniThink-Chem/miniapp/src/types/reaction.ts) 的 `ReactionSolveData` 判别联合挂在 `ReactionEntry.solveData` 上，由 `narrateReaction` 实例化。

### 现状 3：脚手架侧 — A–F 原子任务链（5 模块，串行门控 + 条件分支）
- [scaffoldChains.ts](file:///d:/github/CogniThink-Chem/miniapp/src/engine/scaffoldChains.ts)：`MODULE_CHAINS: Record<ScaffoldModuleKind, ScaffoldChain>`，5 个模块 = `redox-reasoning` / `ionic-system` / `aqueous-equilibrium` / `sequential-reaction` / `electrochem-reasoning`，各一条 A→F 串行 `AtomicTask[]` + 门控纯函数（`initialGateStates` / `markPassed` / `markFailed` / `resetStep` / `resolveStep`）。
- [scaffoldBranches.ts](file:///d:/github/CogniThink-Chem/miniapp/src/engine/scaffoldBranches.ts)：`resolveBranch(entry, conditions)` 按 `BranchConditions`（medium/quantity/concentration/temperature/electrolyzed/membrane）从 `ReactionEntry.branches: ScaffoldBranch[]` 选中分支结论。
- [types/scaffold.ts](file:///d:/github/CogniThink-Chem/miniapp/src/types/scaffold.ts)：`AtomicTask`（L17-32）/ `StepGateState`（L41）/ `ScaffoldBranch`（L99-108）/ `StepScaffoldReleases`（L194-203，hint+probe+scaffold+reveal 三档必填）。
- **门控是严格线性的**（A 过→解锁 B），**不支持 DAG 多前置依赖**；分支只挂在反应侧 `ReactionEntry` 上，题型侧步骤无分支结构。

### 不对齐缺口（本规格要解决的核心问题）
| 维度 | 题型侧 | 反应侧 | 脚手架侧 | 缺口 |
|---|---|---|---|---|
| 分类数 | 6 `ChemCategory` | 19 `ReactionSolveKind` | 5 `ScaffoldModuleKind` | 三者无统一映射表 |
| 步骤结构 | 线性 `steps[]`，无依赖边 | 线性 `steps[]`，无依赖边 | A→F 串行 + 门控 | **全链路缺显式 DAG 依赖边** |
| 分支 | 无 | 无 | 有（`ScaffoldBranch`，仅反应侧） | 分支能力未复用到题型 DAG |
| 覆盖 | 6/6 | 19/19 | 5 模块 | 脚手架缺：**化学平衡、无机推断、有机反应** |
| 占位 | 无机/有机 `panelKind=generic` | — | — | 推导骨架有但面板/模块未对齐 |

## 核心设计 A：统一思维链 DAG 契约

新增类型（建议落点 `miniapp/src/types/thoughtChain.ts`，**纯加性**，不删改现有类型）：

```ts
// 认知层级：与 engine/solveModels.ModelLayer 一致
export type ThoughtLayer = 'Knowledge' | 'Application' | 'Ability'

// 推导图节点：统一 SolveModelStep（讲题骨架）+ AtomicTask（门控作答）的公共字段
export interface ThoughtChainNode {
  id: string              // 稳定 id，如 'redox.infer-products'（跨版本稳定，供 dependsOn 引用）
  index: number           // 展示序号（线性化视图 / 门控初始态用）
  title: string           // 复用 SolveModelStep.title
  goal: string            // 复用 SolveModelStep.goal
  analyze: string         // 通用讲题文案（不含本题答案）；复用 SolveModelStep.analyze
  layer: ThoughtLayer
  stage?: string          // 阶段分组（泛化 redox 的 'stage1'|'stage2'），如 '主链路'/'配平子专项'
  dependsOn: string[]     // 前置节点 id（DAG 依赖边）；空数组 = 根节点
  skipWhen?: string       // 可选：跳过条件键（如 'single-pair'），由条件求值决定是否渲染/门控
  branchKey?: string | string[]  // 可选：单键或多键数组（结论同时受多个分支控制，如电化学半反应 device-type+medium；复用 ScaffoldBranch 解析，见设计 E）
  interaction?: 'multi-check' | 'single-select' | 'order'  // 该步是否可门控作答（复用 AtomicTaskInteraction）
  checks?: string[]
  pitfall?: string
}

export interface ThoughtChainDAG {
  scope:
    | { kind: 'category'; category: ChemCategory }
    | { kind: 'reaction'; reactionKind: ReactionSolveKind }
  name: string
  nodes: ThoughtChainNode[]
  // 不变量（由 tasks 中的校验纯函数保证）：
  //   1) dependsOn 引用的 id 均在 nodes 中存在；
  //   2) 有向无环（拓扑排序存在）；
  //   3) 至少一个根节点（dependsOn 为空）；
  //   4) index 线性化顺序不破坏依赖序（若 A dependsOn B，则 index(B) < index(A)）。
}
```

**与现有三类资产的关系（迁移而非替换，过渡期并存）**：
- `SolveModelStep` → 是 `ThoughtChainNode` 的「讲题骨架子集」（缺 `id`/`dependsOn`/`skipWhen`/`branchKey`/`interaction`）。题型 DAG 定义时，现有 `solveModels.ts` 的每步可**派生**为一个 `ThoughtChainNode`（自动补 `id`、`dependsOn` 默认接前一步、`interaction` 默认空），保证现有 `StepsView` / 面板消费方零改动。
- `AtomicTask` → 是 `ThoughtChainNode` 的「门控作答子集」（`interaction` 必填、`analyze` 放 `goal`）。脚手架 A–F 链是 DAG 的一种**特殊形状**（严格串行、每步都可作答）；统一后 A–F 仍可由 DAG 线性化导出。
- `ScaffoldBranch` → 直接复用：`ThoughtChainNode.branchKey` 引用一组 `ScaffoldBranch`，由现有 `resolveBranch` 求值，不重写分支引擎。

## 核心设计 B：6 题型标准推导 DAG

每张图给出节点、依赖、可跳过条件、分支、阶段、层级。节点 `analyze` 文案沿用现有 `solveModels.ts`（不重写、不编造），仅新增结构字段。

### DAG-1 氧化还原（RedoxReaction）—— 两阶段（沿用 R3 骨架，补依赖边与分支）

| id | title | dependsOn | skipWhen | branchKey | stage | layer |
|----|-------|-----------|----------|-----------|-------|-------|
| redox.list-particles | 列粒子 | — | | | stage1 | Knowledge |
| redox.mark-valence-species | 标变价物种 | redox.list-particles | | | stage1 | Application |
| redox.judge-medium | 判介质 | redox.list-particles | | `medium` | stage1 | Application |
| redox.judge-order | 判先后 | redox.mark-valence-species, redox.judge-medium | `single-pair` | | stage1 | Ability |
| redox.infer-products | 推产物 | redox.mark-valence-species, redox.judge-medium, redox.judge-order* | | `medium` | stage1 | Ability |
| redox.mark-valence | 标价态 | redox.infer-products | | | stage2 | Knowledge |
| redox.count-electrons | 算电子 | redox.mark-valence | | | stage2 | Application |
| redox.lcm | 求公倍数 | redox.count-electrons | | | stage2 | Ability |
| redox.balance | 配变价 | redox.lcm | | | stage2 | Ability |
| redox.patch-ho | 补氢氧 | redox.balance | `no-ionic-equation` | `medium` | stage2 | Ability |
| redox.verify | 全局校验 | redox.patch-ho | | | stage2 | Ability |

\* `redox.judge-order` 被跳过时，`redox.infer-products` 的依赖退化为前两个节点（条件边，见设计 E 的门控泛化）。`branchKey=medium` 复用现有 `RedoxMediumRule`（如 MnO₄⁻ 酸性→Mn²⁺），与 R3 `stage1.mediumRules` 对齐。

### DAG-2 离子平衡（IonicEquilibrium）

| id | title | dependsOn | skipWhen | branchKey | layer |
|----|-------|-----------|----------|-----------|-------|
| ionic.list-species | 找粒子 | — | | | Knowledge |
| ionic.write-conservation | 写守恒 | ionic.list-species | | `solute-type` | Application |
| ionic.compare-concentration | 比浓度 | ionic.write-conservation | `only-equation-check` | `solute-type` | Ability |
| ionic.verify | 守恒校验 | ionic.write-conservation | `only-concentration-rank` | | Ability |

`branchKey=solute-type`（强电解质/弱电解质/混合溶液）决定写哪些守恒式、浓度比较的主导因素。

### DAG-3 化学平衡（ChemicalEquilibrium）

| id | title | dependsOn | skipWhen | branchKey | layer |
|----|-------|-----------|----------|-----------|-------|
| eq.write-reaction | 写平衡 | — | | | Knowledge |
| eq.identify-condition | 判条件 | eq.write-reaction | | | Knowledge |
| eq.infer-shift | 推移动 | eq.write-reaction, eq.identify-condition | | `condition-type` | Application |
| eq.analyze-k | 看 K 与转化率 | eq.infer-shift | | `temperature-changed` | Ability |

`branchKey=condition-type`（浓度/温度/压强/催化剂）决定推移动走哪条子规则；`temperature-changed` 表示仅温度改变时 K 才变化（浓度/压强/催化剂不改 K）。

### DAG-4 电化学（Electrochemistry）

| id | title | dependsOn | skipWhen | branchKey | layer |
|----|-------|-----------|----------|-----------|-------|
| ec.identify-device | 判装置 | — | | `device-type` | Knowledge |
| ec.write-half-reactions | 写半反应 | ec.identify-device | | `device-type`, `medium` | Application |
| ec.analyze-phenomena | 析现象 | ec.write-half-reactions | | | Ability |
| ec.compute | 计算 | ec.write-half-reactions | `no-calc` | | Ability |

`branchKey=device-type`（原电池/电解池/可充电/燃料电池/金属腐蚀）+ `medium`（酸/碱/熔融）决定半反应配平写法。`no-calc` 跳过计算节点（与现有 `ElectrochemSolveData.calc?` 存在性对齐）。

### DAG-5 无机推断（InorganicInference）—— 从 generic 占位提升为完整 DAG

| id | title | dependsOn | skipWhen | branchKey | layer |
|----|-------|-----------|----------|-----------|-------|
| inf.breakthrough | 找突破口 | — | | `subtype` | Knowledge |
| inf.forward-backward | 顺逆推 | inf.breakthrough | | `subtype` | Application |
| inf.verify | 代回验证 | inf.forward-backward | | | Ability |

`branchKey=subtype`（框图推断/元素周期律/工业流程/离子检验/物质鉴别/离子共存/一步转化），与现有 `InferenceSolveData.subtype?` 对齐。

### DAG-6 有机化学（OrganicChemistry）—— 从 generic 占位提升，对齐反应侧有机子类型

| id | title | dependsOn | skipWhen | branchKey | layer |
|----|-------|-----------|----------|-----------|-------|
| org.identify-fg | 认官能团 | — | | | Knowledge |
| org.determine-reaction | 定反应 | org.identify-fg | | `organic-subtype` | Application |
| org.write-product | 写产物 | org.determine-reaction | | `organic-subtype` | Ability |
| org.verify | 价键/不饱和度校验 | org.write-product | | | Ability |

`branchKey=organic-subtype`（9 种：取代/加成/消去/氧化/还原/酯化/水解/加聚/缩聚），与反应侧 `reactionSolveModels.ts` 的 `ORGANIC_SHARED_STEPS` + 子类型步骤对齐——题型 DAG 的有机节点是反应侧 9 个子类型模型在「题目视角」的统一投影。

## 核心设计 C：映射对齐（ChemCategory → ReactionSolveKind → ScaffoldModuleKind）

建立单一映射表（建议落点 `miniapp/src/engine/thoughtChainMapping.ts`），作为全栈唯一分类对齐源：

| ChemCategory | 对齐的 ReactionSolveKind | ScaffoldModuleKind | 现状 |
|---|---|---|---|
| RedoxReaction | reaction-redox, reaction-combustion, reaction-displacement | redox-reasoning | ✓ 模块已存在 |
| IonicEquilibrium | reaction-ion-equilibrium, reaction-double-displacement | ionic-system, aqueous-equilibrium | ✓ 模块已存在 |
| ChemicalEquilibrium | reaction-ion-equilibrium, reaction-thermochemical | **chemical-equilibrium（新）** | ✗ 缺口，本规格补齐 |
| Electrochemistry | reaction-electrode | electrochem-reasoning | ✓ 模块已存在 |
| InorganicInference | reaction-combination/decomposition/displacement/double-displacement（特征性质类） | **inorganic-inference（新）** | ✗ 缺口，本规格补齐；`sequential-reaction`（连续转化/工业流程）并入此 category 的 subtype |
| OrganicChemistry | 9 个 reaction-organic-* | **organic-reaction（新）** | ✗ 缺口，本规格补齐 |

> 用户口径「反应侧 16 类」：实际 `ReactionSolveKind` 去 `reaction-generic` 后为 18 个非通用 kind（9 无机 + 9 有机）。本规格对齐**全部 18 个非通用 kind**（见 Assumptions）。

`ScaffoldModuleKind` 联合类型需**加性新增** 3 个值：`'chemical-equilibrium' | 'inorganic-inference' | 'organic-reaction'`，并在 `MODULE_CHAINS` 中各补一条 A–F 链（复用 `TASK_TEMPLATES`，仅微调 `checks`），保证 `Record<ScaffoldModuleKind, ScaffoldChain>`  exhaustive 不破坏编译。

## 核心设计 D：题目实例化机制

一道具体 `Challenge` 套用其所属 category 的 DAG 时，节点内容分两层：

1. **题型通用层（来自 DAG 定义）**：`title` / `goal` / `analyze` / `layer` / `stage` / `dependsOn` / `skipWhen` / `branchKey` / `interaction` —— 对该 category 所有题相同。
2. **题目实例层（来自本题数据）**：每个节点的**具体结论**由 `NodeDataBinding` 解析：
   - 主数据源：`Challenge.solveData`（按 `kind` 判别联合，[types/chem.ts](file:///d:/github/CogniThink-Chem/miniapp/src/types/chem.ts) L115-289）；
   - 补充叙述：`Challenge.guide: GuideLevel[]`（L20-28）与 `Challenge.explanation`。
   - 绑定关系按 category 定义一张 `NodeDataBinding` 表（例：氧化还原 `redox.list-particles` ← `solveData.stage1?.particles`；`redox.balance` ← `solveData.balance`；`ec.compute` ← `solveData.calc`）。
   - 缺失字段 → 该节点「实例结论」为空，节点仍展示通用骨架（preview 行为），不崩（precision=1，不编造）。

**preview / full 双模式（沿用 R3，零行为回归）**：
- `preview`（答题前）：渲染 DAG 节点的 `title`/`goal`/`stage` 分组（思考骨架），**不解析 `NodeDataBinding`、不读 `solveData`** → 不泄答案。
- `full`（答题后）：渲染 DAG 节点 + 各节点经 `NodeDataBinding` 解析出的实例结论，按门控逐步解锁。

## 核心设计 E：逐步展开机制（门控 + 分支 + 阶梯释放统一）

把现有 A–F 线性门控**泛化到 DAG**，并复用分支与阶梯释放：

1. **DAG 门控**（新增纯函数，建议 `engine/thoughtGate.ts`，与 `scaffoldChains.ts` 线性门控并存）：
   - 节点状态沿用 `StepGateState = 'locked'|'unlocked'|'passed'|'failed'`。
   - 初始：所有根节点（`dependsOn` 为空）`unlocked`，其余 `locked`。
   - 解锁规则：节点在其 **`dependsOn` 全部为 `passed`**（或被 `skipWhen` 跳过时视为 passed）时变为 `unlocked`。
   - 保持现有纯函数纪律：不就地修改、不抛错、越界返回安全空值。
2. **分支解析**：节点 `branchKey` 非空时，用现有 `resolveBranch(entry, conditions)` 选中该分支的结论集；无匹配 → 节点实例结论为空（安全降级）。条件输入复用 `BranchConditions`。
3. **阶梯释放**：节点 `interaction` 非空（可作答）时，答错按连续错误次数递进释放 `StepScaffoldReleases`（hint→probe→scaffold→reveal），复用 `scaffoldValidators.releaseTier`。无 `interaction` 的节点为「只读推导节点」，不触发作答与释放。

## Functional Requirements
- **FR-1**: 新增 `miniapp/src/types/thoughtChain.ts`，定义 `ThoughtLayer` / `ThoughtChainNode` / `ThoughtChainDAG` 契约（加性，不改现有类型）。
- **FR-2**: 新增 `miniapp/src/engine/thoughtChainDAGs.ts`，导出 6 张题型标准推导 DAG（`CATEGORY_DAGS: Record<ChemCategory, ThoughtChainDAG>`），节点结构与设计 B 六张表一致；并导出 18 张反应 kind DAG（`REACTION_DAGS: Record<ReactionSolveKind, ThoughtChainDAG>`，由现有 `REACTION_SOLVE_MODELS` 派生 + 补依赖边）。
- **FR-3**: 新增 `miniapp/src/engine/thoughtChainMapping.ts`，导出 `CATEGORY_REACTION_SCAFFOLD_MAP`（设计 C 的映射表）与查询函数。
- **FR-4**: `ScaffoldModuleKind` 加性新增 3 值（chemical-equilibrium / inorganic-inference / organic-reaction），`MODULE_CHAINS` 补全对应 A–F 链，保持 `Record`  exhaustive。
- **FR-5**: 新增 `miniapp/src/engine/thoughtChainValidation.ts`：DAG 不变量校验纯函数（依赖存在 / 无环 / 有根 / index 不破坏依赖序），供发布前校验与单测。
- **FR-6**: 新增 `miniapp/src/engine/thoughtGate.ts`：DAG 门控纯函数（初始态 / 解锁判定 / markPassed / markFailed / resetStep），与线性 `scaffoldChains` 门控并存、不互相替换。
- **FR-7**: 新增按 category 的 `NodeDataBinding` 表 + 解析函数（节点 id → 本题 solveData/guide 中的实例结论），缺失字段安全降级。
- **FR-8**: 现有 `solveModels.ts` / `reactionSolveModels.ts` / `scaffoldChains.ts` / 面板组件在过渡期**保持编译与运行不变**；可选择性地让 `SolveModel.steps` 从对应 DAG 派生（线性化导出），但不得改变现有 `StepsView` 消费的字段形状。
- **FR-9**: 单测覆盖：DAG 不变量校验、6 题型 DAG 结构（节点数/依赖/根节点）、门控解锁规则、NodeDataBinding 缺失降级。

## Non-Functional Requirements
- **NFR-1**: TypeScript 零诊断错误（`GetDiagnostics` 为空）。
- **NFR-2**: 不引入新依赖；不新增页面（不改 `app.config.ts`）。
- **NFR-3**: 所有纯函数保持「不就地修改、不抛错、越界安全空值」纪律（与 `scaffoldChains.ts` / `scaffoldBranches.ts` 一致）。
- **NFR-4 (rubric)**: 6 张推导 DAG 让学生/开发者一眼看出「这类题的思考骨架 + 哪步依赖哪步 + 条件在哪分叉」，结构清晰无歧义。

## Constraints
- **Technical**: Taro 4 / React 18 / TS / SCSS Modules；本地构建验证走 `npm run build:h5` 输出到 `miniapp/dist/`（云预览页不可交互，沿用 R3 验证方式）。
- **Business**: 不破坏 R1–R3 已验证的氧化还原两阶段面板、离子/平衡/电化学面板、现有 6 步引导/诊断/守恒校验、做题/错题本/物质速查/我的等已完成功能。
- **Dependencies**: 依赖现有 [types/chem.ts](file:///d:/github/CogniThink-Chem/miniapp/src/types/chem.ts)、[types/scaffold.ts](file:///d:/github/CogniThink-Chem/miniapp/src/types/scaffold.ts)、[types/reaction.ts](file:///d:/github/CogniThink-Chem/miniapp/src/types/reaction.ts)、[engine/solveModels.ts](file:///d:/github/CogniThink-Chem/miniapp/src/engine/solveModels.ts)、[engine/reactionSolveModels.ts](file:///d:/github/CogniThink-Chem/miniapp/src/engine/reactionSolveModels.ts)、[engine/scaffoldChains.ts](file:///d:/github/CogniThink-Chem/miniapp/src/engine/scaffoldChains.ts)、[engine/scaffoldBranches.ts](file:///d:/github/CogniThink-Chem/miniapp/src/engine/scaffoldBranches.ts)。
- **红线**: precision=1、harmCount=0、不编造化学事实；不主动 commit。

## Assumptions
- 用户决策已确认（来自本轮 AskUserQuestion）：① 范围 = 全部 6 题型（84 题）+ 对齐反应侧；② 先出规划文档、评审通过后再实现；③ 把现有三套并存思维链统一为一套规范；④ 颗粒度 = 题型级标准推导图 DAG。
- 「反应侧 16 类」按实际 `ReactionSolveKind` 去 `reaction-generic` 后为 **18 个非通用 kind** 处理；如用户希望严格按 16（例如合并某两个有机子类型），在批准时调整（见 OQ-1）。
- 本轮只把无机推断/有机的**推导 DAG 与脚手架模块**补齐；其专属可视化面板（`panelKind` 从 generic 升级）留作后继 spec，不在本规格验收内（见 OQ-2）。
- 84 道题的 `solveData` 不强制本轮全部回填：实例化解析对缺失字段安全降级；是否在本轮补完全部 84 题的数据绑定，由用户批准时定（见 OQ-3）。
- DAG 门控的 UI 渲染（把 DAG 画成可交互的节点网络）属后继迭代；本轮先提供数据契约 + 纯函数引擎，现有 `StepsView` 线性展示作为线性化视图保留（见 OQ-4）。

## Acceptance Criteria

### AC-1: 统一 DAG 契约定义完整且加性
- **Type**: `rule`
- **Given**: 代码库存在 `miniapp/src/types/thoughtChain.ts`
- **When**: 检查类型定义与现有类型的关系
- **Then**: 定义了 `ThoughtChainNode`（含 id/index/title/goal/analyze/layer/stage?/dependsOn[]/skipWhen?/branchKey?/interaction?/checks?/pitfall?）与 `ThoughtChainDAG`；未删除或改签名现有 `SolveModelStep` / `AtomicTask` / `ScaffoldBranch` 类型
- **Pass Condition**: 类型存在且现有类型零改动（git diff 仅新增文件/加性字段）
- **Evidence**: 源码 + GetDiagnostics 为空

### AC-2: 6 题型标准推导 DAG 全覆盖且结构合法
- **Type**: `rule`
- **Given**: 存在 `CATEGORY_DAGS: Record<ChemCategory, ThoughtChainDAG>`
- **When**: 对 6 张 DAG 运行不变量校验（AC-5 的校验函数）
- **Then**: 每张 DAG 节点数 ≥3、存在根节点、无环、`dependsOn` 引用均存在、index 不破坏依赖序；且节点结构与设计 B 六张表一致（含 dependsOn 边、至少一个 skipWhen 或 branchKey 体现「非纯线性」）
- **Pass Condition**: 6/6 通过校验，且氧化还原含 stage1/stage2 分组、无机/有机不再是 generic 占位
- **Evidence**: 单测输出 + 源码

### AC-3: 反应侧 DAG 对齐全部 18 个非通用 kind
- **Type**: `rule`
- **Given**: 存在 `REACTION_DAGS`
- **When**: 遍历 18 个非通用 `ReactionSolveKind`
- **Then**: 每个 kind 都有对应 DAG；有机 9 个子类型共享「认官能团→定反应」前置节点（与 `ORGANIC_SHARED_STEPS` 一致）+ 子类型节点；`reaction-generic` 走降级
- **Pass Condition**: 18/18 全覆盖，有机共享前置结构正确
- **Evidence**: 单测 + 源码

### AC-4: 映射表对齐且 3 个脚手架缺口补齐
- **Type**: `rule`
- **Given**: 存在 `CATEGORY_REACTION_SCAFFOLD_MAP` 与 `MODULE_CHAINS`
- **When**: 检查映射表与 `ScaffoldModuleKind` 联合
- **Then**: 6 个 category 均映射到 ≥1 个 reactionKind 与 ≥1 个 scaffoldModule；`ScaffoldModuleKind` 含 chemical-equilibrium / inorganic-inference / organic-reaction 三个新值，且 `MODULE_CHAINS` 为每个新模块提供非空 A–F 链
- **Pass Condition**: 映射 exhaustive、`Record<ScaffoldModuleKind, ScaffoldChain>` 编译通过且无 undefined 模块
- **Evidence**: 源码 + GetDiagnostics + 单测

### AC-5: DAG 不变量校验纯函数正确
- **Type**: `rule`
- **Given**: `thoughtChainValidation.ts` 的校验函数
- **When**: 喂入（合法 DAG / 缺依赖引用 / 有环 / 无根 / index 破坏依赖序）各样本
- **Then**: 合法样本通过，四类非法样本各自被检出并返回可读错误
- **Pass Condition**: 5 类样本判定均符合预期
- **Evidence**: 单测

### AC-6: DAG 门控泛化正确
- **Type**: `rule`
- **Given**: `thoughtGate.ts` 的门控函数 + 一张含分支依赖的 DAG
- **When**: 逐节点 passed
- **Then**: 节点仅在全部 dependsOn passed（或 skipWhen 跳过视为 passed）时解锁；根节点初始 unlocked；locked 节点访问返回安全空值不抛错
- **Pass Condition**: 解锁顺序符合依赖拓扑，无越界崩溃
- **Evidence**: 单测

### AC-7: 题目实例化解析 + 缺失降级
- **Type**: `rule`
- **Given**: `NodeDataBinding` 解析函数 + 一道带 solveData 的题 + 一道不带的题
- **When**: 解析两题各节点的实例结论
- **Then**: 带数据题解析出非空结论（与 solveData 字段对应）；不带数据题所有节点实例结论为空但不抛错；preview 模式不解析（不泄答案）
- **Pass Condition**: 解析正确、降级安全、preview 零答案泄露
- **Evidence**: 单测 + 源码

### AC-8: 向后兼容——R1–R3 行为零回归
- **Type**: `rule`
- **Given**: 本轮改动完成
- **When**: 运行既有面板/题目页（氧化还原两阶段、离子/平衡/电化学面板、无 solveData 降级）
- **Then**: 现有 `solveModels` / 面板 / challenge 页行为与改动前一致；`GetDiagnostics` 为空；`npm run build:h5` 成功
- **Pass Condition**: 无运行时错误、无视觉回归、构建成功
- **Evidence**: GetDiagnostics + 构建日志 + 预览抽样

### AC-9: 推导 DAG 的结构清晰度与教学贴合度
- **Type**: `rubric`
- **Dimension**: 6 张 DAG 是否让读者一眼看清「这类题的思考骨架、步骤依赖关系、条件分叉点」，且与题型思维本质一致
- **Scale**: 1-5
- **Anchors**: 1 = 仍是线性步骤串、看不出依赖/分支；3 = 有依赖边但分支/阶段表达不清；5 = DAG 精准映射题型推导逻辑（如氧化还原两阶段依赖、有机子类型分支），清晰无歧义
- **Pass Threshold**: >= 4
- **Evidence**: spec 六张表 + 独立评审

## Open Questions（已裁决：用户批准 spec.md，5 项均采用推荐默认值）
- [x] **OQ-1**（已裁决）：反应侧对齐范围 → 采用推荐：18 个非通用 `ReactionSolveKind` 全对齐，`reaction-generic` 保留降级。
- [x] **OQ-2**（已裁决）：无机推断 / 有机化学的专属可视化面板 → 不纳入本轮，只补 DAG + 脚手架模块，专属面板留后继 spec。
- [x] **OQ-3**（已裁决）：84 题 solveData 回填 → 先补代表性题验证绑定通路，全量回填留后继批次。
- [x] **OQ-4**（已裁决）：DAG 图形化节点网络 UI → 本轮不做，先数据层 + 纯函数引擎，UI 留后继迭代。
- [x] **OQ-5**（已裁决）：迁移策略 → 并存过渡期，DAG 新增、现有 `solveModels.steps` 不动（可派生但不改消费方形状）。

---

## 后继扩展：核心设计 F — 跨题型元 DAG 与 metaPhase 打标

> **状态**：已实现并验证通过（纯加性，不改变 R4 已验收的任何契约与行为）。本节记录 R4 关闭后追加的「元骨架超结构」，使 6 张题型 DAG 共享一张公共方法论导航层。

### 动机
6 张题型 DAG 各成体系，但学生在跨题型迁移时缺少一张「通用求解骨架」来对齐思考节奏。把不同题型抽象为统一的六阶段模式：**读题定向 → 条件识别 → 反应建模 → 推导结论 → 问题关联 → 校验作答**，可为每张题型节点标注其所处的元阶段，并提供一张跨题型线性元 DAG 作为导航超结构。

### 契约变更（全部加性，零破坏）
- [types/thoughtChain.ts](file:///d:/github/CogniThink-Chem/miniapp/src/types/thoughtChain.ts)：
  - 新增 `MetaPhase = 'S1' | 'S2' | 'S3' | 'S4' | 'S5' | 'S6'` 与导出常量 `META_PHASE_ORDER`。
  - `ThoughtChainNode` 新增可选字段 `metaPhase?: MetaPhase`（缺省 = 未归类）。
  - `ThoughtChainScope` 联合新增 `{ kind: 'meta' }` 分支（grep 确认无消费方穷举此联合，安全）。
- [types/chem.ts](file:///d:/github/CogniThink-Chem/miniapp/src/types/chem.ts)：`Challenge` 接口加性新增两个可选字段——`questionTarget?: string`（S5「问题关联」输入，题目所求的量，如「配平系数比」「浓度排序」）与 `problemConditions?: string[]`（S1「读题定向」输入，题面条件清单）。两者皆缺省降级，不影响解题。

### 元 DAG（导航层超结构，不替换题型 DAG）
新增 [engine/thoughtChainMetaDAG.ts](file:///d:/github/CogniThink-Chem/miniapp/src/engine/thoughtChainMetaDAG.ts)：
- `META_PHASE_INFO`：6 阶段的 title/goal/layer/analyze 文案（单一真值源，纯通用方法论，不含任何题目答案）。
- `META_DAG`：6 节点线性骨架 `meta.S1 → meta.S2 → … → meta.S6`，`dependsOn` 依次指向前一节点，S1 为根；`scope = { kind: 'meta' }`，每节点自带对应 `metaPhase`。多步反应的「产物→新输入」回环是运行时迭代，**不构成图上的环**（避免破坏 DAG 无环校验）。
- 导出 `getMetaDAG`（单例）、`getMetaPhaseLabel`、`groupNodesByMetaPhase`、`getQuestionTarget`（空串/非串 → undefined 安全降级）、`resolveQuestionTargetNodes`（已落地：按 category 查 `QUESTION_TARGET_MAP` 关键词→nodeId 映射表，target 包含关键词即命中、合并去重；无命中 → []）。

### 题型节点打标 + S1/S5 实例闭合
[engine/thoughtChainDAGs.ts](file:///d:/github/CogniThink-Chem/miniapp/src/engine/thoughtChainDAGs.ts)：
- `NODE_META_PHASE` 映射表（单一真值源）覆盖全部 42 个题型节点；`withMetaPhase` 纯函数（命中则标注，不就地修改）在 `buildCategoryDAG` 返回处应用。
- `buildCategoryDAG` 自动为每张题型 DAG 前置 S1 节点（`<prefix>.read-problem`，新根，原根节点改依赖 S1），并在推导结论之后追加 S5 节点（`<prefix>.match-answer`，被 S6 校验节点依赖；无 S6 的题型为末端）；`topoIndex` 稳定拓扑排序重排 index 为 1..N，保证连续且不破坏依赖序。
- 分布：S1=6 / S2=7 / S3=8 / S4=11 / S5=6 / S6=4，合计 42，元骨架 S1–S6 全阶段在题型层闭合。
- [engine/thoughtChainBindings.ts](file:///d:/github/CogniThink-Chem/miniapp/src/engine/thoughtChainBindings.ts)：S1 解析器读 `Challenge.problemConditions`，S5 解析器读 `questionTarget` + 映射命中节点；preview 恒 null（守纪律，不泄答案）。

### 数据附着（84 题全覆盖）
- 题库 84 题（6 category × 14）已全量填充 `problemConditions`（2–5 条/题，从题面及选项客观摘录，零编造）与 `questionTarget`（归纳题目所求，全部命中所属 category 的映射关键词）。
- 端到端：`resolveQuestionTargetNodes` 对 84 题真实数据 84/84 命中，S1/S5 实例结论 84/84 非空。

### 已知缺口（诚实记录，非缺陷）
- ~~S1/S5 无题型实例节点~~ → 已闭合；~~`resolveQuestionTargetNodes` 是桩~~ → 已落地映射表。
- `questionTarget` 匹配为关键词子串包含（`includes`），当前词表覆盖 84 题全命中；后继若新增题型/新问法，需在 `QUESTION_TARGET_MAP` 补关键词，否则该题 S5 返回 [] 安全降级（不影响解题）。

### 验证
[scripts/verify-thoughtchain.ts](file:///d:/github/CogniThink-Chem/miniapp/scripts/verify-thoughtchain.ts) 元 DAG 断言区块覆盖：META_DAG 校验 / 6 节点线性依赖 / 42 题型节点全覆盖且值合法 / 分布精确（S1–S6）/ 每张 DAG 唯一根是 S1 / `questionTarget` 与 `problemConditions` 全覆盖 84/84 / 映射全命中 / 84 题 S1/S5 实例结论非空 / 安全降级。复跑脚本 exit 0「全部通过 PASS」，真实题库节点命中 294 → 462（+84 S1 + 84 S5），`GetDiagnostics` → `[]` 零回归。
