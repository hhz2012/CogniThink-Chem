# Scaffold DAG Flow P0 前端接入 - Product Requirements Document

## Overview
- **Summary**: 把已泛化的 `ThoughtDagFlow` 思路递进图组件接入「脚手架练习页（A–F 精熟练习）」与「反应详情页」，并补齐其所需的类型变体与 `ScaffoldChain → ThoughtChainDAG` 转换层。
- **Purpose**: 让学生在脚手架练习前看到完整 A–F 推导骨架（不泄答案）、练习后看到已通过的执行路径；让反应详情页也能展示推导路径可视化。当前组件已就绪但这两页都还没用上（脚手架页用 `StepProgress` 迷你条替代、反应页也只用 `StepProgress`）。
- **Target Users**: 在 miniapp 中进行 A–F 精熟练习与查看反应推理卡的学生用户。

## Goals
- 脚手架练习页：答题前可查看 A–F 思路骨架（preview，不泄答案）；全部通过后展示完整执行路径（full）。
- 反应详情页：展示反应推理的通用 A–E 脚手架骨架（preview，全执行态）。
- 转换层 `scaffoldChainToDAG` 可复用、不编造化学事实、空链不崩。
- `StepProgress` 浮动迷你条保留，与 `ThoughtDagFlow` 并存（职责不同：迷你条=答题中导航；思路图=完整推导可视化）。
- challenge 页现有 `ThoughtDagFlow` 调用零回归。

## Non-Goals
- 不实现后端 ScaffoldGenerator 的 8 模块推广（P1，单独 spec）。
- 不引入反应专属 DAG（`getReactionDAG` 当前不存在；反应专属步骤 DAG 属 P1+，避免编造）。
- 不改 `StepProgress` 组件、不改 `app.config.ts`、不新增 npm 依赖。
- 不改脚手架门控/校验/阶梯释放的业务逻辑（仅加可视化层）。

## Background & Context
- 设计依据：[.trae/documents/scaffold-generation-to-dag-flow-design.md](file:///D:/github/CogniThink-Chem/.trae/documents/scaffold-generation-to-dag-flow-design.md) §4（P0 前端接入），接口级契约已在该文档定义。
- 现状核实（读码，2026-11-25）：
  - [ThoughtChainScope](file:///D:/github/CogniThink-Chem/miniapp/src/types/thoughtChain.ts#L70-L72) 联合仅 `category` / `meta`，缺 `scaffold`。
  - [ThoughtChainNode](file:///D:/github/CogniThink-Chem/miniapp/src/types/thoughtChain.ts#L38-L67) 的 `analyze`/`layer` 已可选，脚手架节点无来源可诚实省略。
  - [ThoughtDagFlow](file:///D:/github/CogniThink-Chem/miniapp/src/components/ThoughtDagFlow/index.tsx) 组件 props 已支持 `dag?`/`mode?`/`dimmedIds?`/`legendLabels?`，布局已自适应（无 `metaPhase` → 单列垂直）；但其 [scopeToKey](file:///D:/github/CogniThink-Chem/miniapp/src/components/ThoughtDagFlow/index.tsx#L36-L43) switch 只覆盖 `category`/`meta`，加 `scaffold` 变体后必须补 case 否则 TS 报错。
  - [thoughtChainDAGs.ts](file:///D:/github/CogniThink-Chem/miniapp/src/engine/thoughtChainDAGs.ts) 仅有 category DAG，无 `scaffoldChainToDAG`、无 `getReactionDAG`。
  - [ScaffoldPractice](file:///D:/github/CogniThink-Chem/miniapp/src/components/ScaffoldPractice/index.tsx) 已有 `gate`/`allPassed`/`chain`，接入点清晰；L213 注释说 StepProgress「替代大块思路递进图」——本设计裁决为并存。
  - [reaction/index.tsx](file:///D:/github/CogniThink-Chem/miniapp/src/pages/reaction/index.tsx#L153-L161) 「思路递进图」区当前渲染的是 `StepProgress`，**不是** ThoughtDagFlow；[REACTION_SCAFFOLD_CHAIN](file:///D:/github/CogniThink-Chem/miniapp/src/engine/scaffoldChains.ts) 已存在（A–E 反应侧子集）。
  - `scaffold.ts` 不导入 `thoughtChain.ts` → 在 `thoughtChain.ts` 导入 `ScaffoldModuleKind` 无循环依赖。

## Functional Requirements
- **FR-1**: `ThoughtChainScope` 新增 `scaffold` 变体，携带 `module: ScaffoldModuleKind | 'reaction'`。
- **FR-2**: 新增导出函数 `scaffoldChainToDAG(chain)`，把 `ScaffoldChain`（A–F 或 A–E）转成 `ThoughtChainDAG`，节点文案取 `AtomicTask`，边取线性前序依赖。
- **FR-3**: `ScaffoldPractice` 答题前展示 ThoughtDagFlow preview（可折叠，默认收起避免干扰先看步骤），全部通过后展示 full（按门控置灰未解锁步）。
- **FR-4**: 反应详情页在「分步脚手架」区上方展示 ThoughtDagFlow preview（`scaffoldChainToDAG(REACTION_SCAFFOLD_CHAIN)`）。
- **FR-5**: 转换层与组件对空链/空节点/缺字段不抛错、不崩，自然降级为不渲染。

## Non-Functional Requirements
- **NFR-1（零回归）**: challenge 页 `<ThoughtDagFlow challenge={...} />` 现有调用行为不变；`thoughtChainValidation` 不读 scope，加变体不破坏校验。
- **NFR-2（不编造）**: 转换层所有字段可回指到 `AtomicTask` 或 `markPassed` 线性解锁关系；`AtomicTask` 没有的字段（analyze/layer/metaPhase/skipWhen/branchKey/stage）一律省略。
- **NFR-3（纯加性）**: 不引入新依赖、不改路由配置、不删现有 UI。
- **NFR-4（类型安全）**: miniapp TypeScript 编译零错误（`tsc --noEmit` 或 IDE GetDiagnostics）。

## Constraints
- **Technical**: Taro4 + React + TS；`ThoughtDagFlow` 为受控纯展示组件，取数优先级 `dag > challenge`。
- **Business**: 脚手架练习页 preview 不得泄露具体题目的答案/正确项（仅展示 A–F 通用骨架）——preview 模式组件本身不读实例数据，天然满足。
- **Dependencies**: `ScaffoldChain`/`AtomicTask`/`ScaffoldModuleKind` 来自 `types/scaffold`；`REACTION_SCAFFOLD_CHAIN` 来自 `engine/scaffoldChains`。

## Assumptions
- `REACTION_SCAFFOLD_CHAIN` 已从 `scaffoldChains.ts` 导出（实现时确认；若未导出则补导出，不改其内容）。
- 脚手架练习页 preview 图默认折叠不影响用户先看步骤；产品上可接受「需手动展开看图」。
- 反应详情页用通用 A–E 脚手架骨架（而非该反应专属步骤）是可接受的 P0 范围；反应专属 DAG 留待 P1。

## Acceptance Criteria

### AC-1: ThoughtChainScope 扩展且无循环依赖
- **Type**: `rule`
- **Given**: `types/thoughtChain.ts`
- **When**: 查看 `ThoughtChainScope` 联合定义
- **Then**: 存在 `{ kind: 'scaffold'; module: ScaffoldModuleKind | 'reaction' }` 变体；`scaffold.ts` 不导入 `thoughtChain.ts`（无循环依赖）
- **Pass Condition**: grep 确认变体存在 + grep `scaffold.ts` 无 `from.*thoughtChain`
- **Evidence**: 源码行号 + grep 结果

### AC-2: scaffoldChainToDAG 转换层落地且不编造
- **Type**: `rule`
- **Given**: `engine/thoughtChainDAGs.ts`
- **When**: 调用 `scaffoldChainToDAG(chain)`
- **Then**: 返回 `ThoughtChainDAG`，`scope.kind==='scaffold'`；每个节点 id 为 `${module}.${kind}`；首步 `dependsOn=[]`、后续步依赖前一步 id；节点仅含 `AtomicTask` 可提供的字段（id/index/title/goal/interaction/checks/pitfall/dependsOn），不含 analyze/layer/metaPhase/skipWhen/branchKey/stage
- **Pass Condition**: 读源码确认映射规则；空 `chain.tasks` 时返回 `nodes: []` 不抛错
- **Evidence**: 源码行号

### AC-3: scopeToKey 穷尽且 challenge 零回归
- **Type**: `rule`
- **Given**: `components/ThoughtDagFlow/index.tsx`
- **When**: 查看 `scopeToKey` switch
- **Then**: 含 `case 'scaffold'` 分支；TS 编译无「switch 未穷尽」错误；challenge 页调用签名未变
- **Pass Condition**: grep 确认 case 存在 + tsc/GetDiagnostics 零错误
- **Evidence**: 源码行号 + 编译结果

### AC-4: 脚手架练习页接入 preview + full
- **Type**: `rule`
- **Given**: `components/ScaffoldPractice/index.tsx`
- **When**: 进入练习页
- **Then**: 答题前存在可折叠的 ThoughtDagFlow preview（不泄答案）；`allPassed` 时存在 full 图；`dimmedIds` 由 `gate` 中 `state==='locked'` 的 task id 推导；`StepProgress` 浮动条仍保留
- **Pass Condition**: 读源码确认两个接入点 + dimmedIds 推导逻辑；浏览器截图验证两态渲染
- **Evidence**: 源码行号 + 截图

### AC-5: 反应详情页接入 preview
- **Type**: `rule`
- **Given**: `pages/reaction/index.tsx`
- **When**: 进入任意反应详情
- **Then**: 「分步脚手架」区上方渲染 ThoughtDagFlow，`dag={scaffoldChainToDAG(REACTION_SCAFFOLD_CHAIN)}`，`mode="preview"`；反应无门控故全高亮
- **Pass Condition**: 读源码确认接入；浏览器截图验证渲染
- **Evidence**: 源码行号 + 截图

### AC-6: 降级不崩 + 零类型错误
- **Type**: `rule`
- **Given**: 完整 P0 改动
- **When**: 空链 DAG 传入组件 / 整体 tsc 编译
- **Then**: 组件对空节点返回 null 不抛错；miniapp tsc --noEmit 零错误；不引入新依赖
- **Pass Condition**: 组件已有空节点守卫（`layers.length===0` return null）+ tsc 零错误 + `package.json` 依赖未变
- **Evidence**: 编译输出 + package.json diff

### AC-7: 不编造化学事实
- **Type**: `rubric`
- **Dimension**: 转换层字段可追溯性
- **Scale**: 1-5
- **Anchors**: 1 = 转换层含 AtomicTask 之外编造的文案/字段；3 = 大部分字段可追溯但有少量推断；5 = 所有字段严格来自 AtomicTask/markPassed，无来源字段一律省略
- **Pass Threshold**: >= 4
- **Evidence**: 源码逐字段核对

## Open Questions
- [ ] 脚手架练习页 preview 图默认折叠是否符合产品预期？（当前按「默认收起」实现，若产品要默认展开可一行调整）
- [ ] 反应详情页是否需要按 `reactionKind` 展示专属步骤 DAG？（P0 用通用 A–E 骨架；专属 DAG 需 P1 单独设计以避免编造）
