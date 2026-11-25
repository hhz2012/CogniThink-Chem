# 反应方程判断（讲题脚手架）设计完善 - Product Requirements Document

## Overview
- **Summary**: 完善 miniapp 反应侧「反应方程判断」讲题脚手架的设计：清理已失效的死代码与冗余字段，消除模型层与叙述层之间的脆弱字符串耦合，让已维护但未展示的校验要点（checks）对用户可见。
- **Purpose**: 上一轮设计评审发现该模块存在 2 处死代码/冗余（反应 DAG 双套建模、`reactionType`/`checks` 死字段）和 2 处脆弱耦合（`switch(step.title)` 中文分发、有机步 index 硬编码）。本次将其收敛为干净、可维护的单一结构，避免后续漂移与静默失效。
- **Target Users**: 化学伴思的前端维护者与使用反应推理卡的学生。

## Goals
- 移除无消费方的死代码（反应 DAG、scaffold DAG 派生函数），消除与 `REACTION_SOLVE_MODELS` 并行的第二套反应结构。
- 让 `ReactionSolveModel.checks`（每类反应的校验自查清单）在反应详情页可见，补全产品价值。
- 删除 `ReactionEntry.reactionType` 死字段，统一以 `solveData.kind` 作为反应类型的唯一标识。
- 为步骤引入稳定 `key`，叙述层按 `key` 分发，消除「改标题即静默回退」的脆弱耦合。
- 有机反应步骤 index 改为拼接后自动编号，消除硬编码 3/4/5。

## Non-Goals
- **不引入运行时化学方程校验引擎**（判断反应能否发生/配平/守恒的实时计算）——这是 P2 新功能，本次不做；当前「判断」仍为声明式真值数据。
- **不修改题型侧**（`SOLVE_MODELS` / `solveNarration` / challenge 页）的行为，仅在 `SolveModelStep` 类型上做向后兼容的可选字段扩展。
- **不修改后端**（C# ScaffoldGenerator 与本题无关）。
- **不改动** `thoughtChainMapping.ts`（ChemCategory→ReactionKind→ScaffoldModule 映射，题型侧在用，活的）。
- **不改动** `validateDAG`（纯函数，无 DAG 依赖）。

## Background & Context
证据（均来自代码核查）：
- `thoughtChainDAGs.ts` 的 `buildReactionDAG` / `REACTION_DAGS` / `getReactionDAG`（L314-L351）为全部 19 个 `ReactionSolveKind` 派生 DAG，但反应页已改用 `StepProgress`，`ThoughtDagFlow` 仅在 `pages/challenge` 以 `challenge` prop 渲染 category DAG（`challenge/index.tsx:559`、`:741`），**从不传 `reactionKind`**——故反应 DAG 无消费方。
- `scaffoldChainToDAG`（`thoughtChainDAGs.ts:357`）同样无任何 import 调用方（ScaffoldPractice 已改用 StepProgress）。
- `ReactionEntry.reactionType`（`types/reaction.ts:204`）在 5 个数据文件 + `branchExamples.ts` 中被赋值，但全局无任何 `.reactionType` 读取方，与 `solveData.kind` 重复。
- `ReactionSolveModel.checks` 为 19 个 kind 均已维护，但反应页 `pages/reaction` 零引用（消费点全在题型侧），是死字段。
- `narrateReaction`（`reactionNarration.ts`）各分支用 `switch(step.title)` 中文字符串匹配模型 step 标题，改标题措辞即静默回退 `step.analyze`，无编译期保障。
- 有机 9 子类型用 `...ORGANIC_SHARED_STEPS`（硬编码 index 1/2）+ 专属步（硬编码 index 3/4/5），增减步数会 index 冲突。

## Functional Requirements
- **FR-1**: 移除反应 DAG 与 scaffold DAG 派生死代码后，`ThoughtDagFlow` 的 category DAG 渲染能力保持不变。
- **FR-2**: 反应详情页在分步脚手架之后展示「本类反应自查清单」区块，内容为 `model.checks`。
- **FR-3**: `ReactionEntry` 类型不再含 `reactionType` 字段；所有数据条目同步移除该字段。
- **FR-4**: `SolveModelStep` 增加可选稳定 `key`；反应侧 `REACTION_SOLVE_MODELS` 的每个 step 均带 `key`。
- **FR-5**: `narrateReaction` 及其子叙述函数按 `step.key`（而非 `step.title`）分发。
- **FR-6**: 有机模型的 steps 由共享步与专属步拼接后自动顺序编号，无硬编码 index。

## Non-Functional Requirements
- **NFR-1**: 改动后 `miniapp` TypeScript 诊断为空（GetDiagnostics 无错误）。
- **NFR-2**: 反应页列表与详情功能回归正常（截图验证：列表分组、详情步骤展开、提示揭示、checks 展示）。
- **NFR-3**: 不引入新依赖；纯前端 TS 改动。
- **NFR-4**: 向后兼容——题型侧 `SolveModelStep` 消费者不受 `key?: string` 可选字段影响。

## Constraints
- **Technical**: Taro + React + TypeScript 前端；Windows PowerShell 环境；dev server 端口 10086。
- **Business**: 不编造化学事实；checks 展示的内容必须来自已维护的 `model.checks`，不新增未校验文案。
- **Dependencies**: 无外部依赖变更。

## Assumptions
- 前端 `src` 下无单测覆盖（glob 确认仅 node_modules 中有 test 文件），验证以 TS 诊断 + 浏览器截图为准。
- `reactionType` 无后端运行时依赖（miniapp 前端独立，与 C# 后端不共享类型），删除安全。
- `ThoughtChainScope` 的 `{ kind: 'reaction' }` 分支仅由 `buildReactionDAG` 构造，删除后可一并收敛类型。

## Acceptance Criteria

### AC-1: 反应 DAG 死代码已清除
- **Type**: `rule`
- **Given**: `thoughtChainDAGs.ts` 与 `ThoughtDagFlow` 已修改
- **When**: 全局搜索 `getReactionDAG`、`REACTION_DAGS`、`buildReactionDAG`
- **Then**: 无任何定义或引用残留（`ThoughtChainScope` 的 reaction 分支同步收敛）
- **Pass Condition**: grep 上述符号在 `miniapp/src` 下 0 匹配，且 TS 诊断为空
- **Evidence**: grep 输出 + GetDiagnostics

### AC-2: scaffold DAG 派生死代码已清除
- **Type**: `rule`
- **Given**: `thoughtChainDAGs.ts` 已修改
- **When**: 全局搜索 `scaffoldChainToDAG`
- **Then**: 无任何定义或引用残留
- **Pass Condition**: grep 0 匹配，TS 诊断为空
- **Evidence**: grep 输出 + GetDiagnostics

### AC-3: ThoughtDagFlow category DAG 功能不受影响
- **Type**: `rule`
- **Given**: 移除 `reactionKind` prop 与 reaction 分支后
- **When**: challenge 页渲染 category DAG（preview / full 两种模式）
- **Then**: DAG 正常绘制，节点/边/图例与改动前一致
- **Pass Condition**: challenge 页截图对比无回归
- **Evidence**: 浏览器截图

### AC-4: 反应详情页展示 checks 自查清单
- **Type**: `rule`
- **Given**: 进入任一反应详情页
- **When**: 页面渲染分步脚手架区块之后
- **Then**: 出现「本类反应自查清单」区块，列出该 kind 的全部 `model.checks`
- **Pass Condition**: 19 个 kind 中任取 3 个不同 kind（如化合、复分解、氧还）详情页均正确展示对应 checks，且无 kind 展示空清单
- **Evidence**: 浏览器截图（至少 3 个 kind）

### AC-5: reactionType 死字段已清除
- **Type**: `rule`
- **Given**: `types/reaction.ts` 与 5 个数据文件 + `branchExamples.ts` 已修改
- **When**: 全局搜索 `reactionType`
- **Then**: `miniapp/src` 下 0 匹配
- **Pass Condition**: grep 0 匹配，TS 诊断为空，反应页列表/详情正常
- **Evidence**: grep 输出 + GetDiagnostics + 截图

### AC-6: 步骤稳定 key 已引入
- **Type**: `rule`
- **Given**: `SolveModelStep` 增加 `key?: string`，`REACTION_SOLVE_MODELS` 全部 step 已填 key
- **When**: 检查反应模型每个 step
- **Then**: 每个 step 含非空 `key`，且同一模型内 key 唯一
- **Pass Condition**: 遍历 19 个模型的全部 step，`key` 均存在且模型内唯一
- **Evidence**: 代码审查 + 脚本校验输出

### AC-7: 叙述层按 key 分发
- **Type**: `rule`
- **Given**: `narrateReaction` 子函数已改按 `step.key` 匹配
- **When**: 故意把某模型 step 的 `title` 改名（key 不变）
- **Then**: 该步叙述仍正确实例化（不回退通用 analyze）
- **Pass Condition**: 代码中无 `switch(step.title)` 分发（改用 `step.key`）；反应页截图显示实例化讲解正常
- **Evidence**: 代码审查（grep `switch(step.title)` 0 匹配）+ 截图

### AC-8: 有机步 index 自动编号
- **Type**: `rule`
- **Given**: 有机模型 steps 拼接后自动赋 index
- **When**: 检查任一有机子类型模型的 steps
- **Then**: index 为 1..N 连续，无硬编码 3/4/5 残留，共享步在前、专属步在后
- **Pass Condition**: 代码中有机专属步不再写死 index，9 个子类型 index 均连续正确
- **Evidence**: 代码审查

### AC-9: 设计内聚性（无死字段/死代码、单一真值源无漂移）
- **Type**: `rubric`
- **Dimension**: 反应侧模块的内聚与去冗余程度
- **Scale**: 1-5
- **Anchors**: 1 = 仍有死字段/双套结构并存；3 = 死代码已清但仍有可消除的重复；5 = 单一真值源、无死代码、无冗余字段、耦合清晰
- **Pass Threshold**: >= 4
- **Evidence**: 代码审查结论

### AC-10: 反应页功能回归
- **Type**: `rule`
- **Given**: 全部改动完成并构建通过
- **When**: 浏览反应列表页 → 进入详情 → 展开步骤 → 点提示
- **Then**: 列表分组正常、详情步骤可展开、提示揭示实例化讲解、checks 清单可见、StepProgress 浮动条正常
- **Pass Condition**: 截图覆盖列表 + 详情（含展开+提示+checks）无异常
- **Evidence**: 浏览器截图

## Open Questions
- [ ] 无（范围与非目标已明确；P2 运行时校验引擎列入后续，不在本次）
