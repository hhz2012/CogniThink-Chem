# 全题型思维链 DAG 统一规范 - Implementation Plan

> **前置门**：用户已批准 `spec.md`（5 个 OQ 均按推荐默认值裁决），实现 + 独立评审均已完成，全部任务 `completed`。完成证据见文末「完成证据汇总」。
> **依赖顺序**：Task 1 为类型基座，Task 2 校验函数先于 DAG 定义（以便边写边校验），Task 8 集成在各原子任务之后，Task 10 验证在集成之后，Task 11 独立评审收尾。

## Task 1: 定义统一思维链 DAG 类型契约
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 新建 `miniapp/src/types/thoughtChain.ts`，定义 `ThoughtLayer`、`ThoughtChainNode`、`ThoughtChainDAG`（字段与 spec 设计 A 一致）。
  - 纯加性：不修改 `types/chem.ts` / `types/scaffold.ts` / `types/reaction.ts` 的现有类型签名。
  - `ThoughtChainNode.dependsOn` 为 `string[]`（引用节点 `id`）；`interaction?` 复用 `AtomicTaskInteraction` 的字面量联合（直接引用该类型以避免双份维护）。
- **Acceptance Criteria Addressed**: AC-1
- **Test Requirements**:
  - `rule` TR-1.1: `thoughtChain.ts` 导出 `ThoughtChainNode` 与 `ThoughtChainDAG`，且 `ThoughtChainNode` 含 spec 设计 A 列出的全部字段；`GetDiagnostics` 对该文件为空。Evidence: 源码 + GetDiagnostics。
  - `rule` TR-1.2: `git diff` 显示 `types/chem.ts`、`types/scaffold.ts`、`types/reaction.ts` 无删除/改签名改动（仅可能加性引用）。Evidence: `git diff --stat`。
- **Notes**: 基座任务，所有后续任务依赖。

## Task 2: DAG 不变量校验纯函数
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 新建 `miniapp/src/engine/thoughtChainValidation.ts`，导出校验函数（返回 `{ ok: boolean; errors: string[] }`）：
    1. 所有 `dependsOn` 引用的 `id` 均在 `nodes` 中存在；
    2. 有向无环（拓扑排序存在）；
    3. 至少一个根节点（`dependsOn` 为空）；
    4. `index` 线性化不破坏依赖序（若 A dependsOn B，则 `index(B) < index(A)`）。
  - 纯函数：不就地修改、不抛错；非法输入返回带错误的 `{ ok:false }`。
- **Acceptance Criteria Addressed**: AC-2（校验支撑）, AC-5
- **Test Requirements**:
  - `rule` TR-2.1: 喂入 5 类样本——合法 DAG / 缺依赖引用 / 有环 / 无根 / index 破坏依赖序——校验结果分别符合预期（合法 ok=true，其余 ok=false 且 errors 非空并可读）。Evidence: 单测。
  - `rule` TR-2.2: 空 nodes / null 输入返回 `ok=false` 且不抛错。Evidence: 单测。
- **Notes**: 先于 DAG 定义，以便 Task 3/4 编写时即时校验。

## Task 3: 定义 6 题型标准推导 DAG
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1, Task 2
- **Description**:
  - 新建 `miniapp/src/engine/thoughtChainDAGs.ts`，导出 `CATEGORY_DAGS: Record<ChemCategory, ThoughtChainDAG>`。
  - 6 张 DAG 的节点结构与 spec 设计 B 六张表**逐字段一致**（id/title/goal/dependsOn/skipWhen/branchKey/stage/layer）。
  - `analyze` 文案**复用**现有 `solveModels.ts` 对应步骤（不重写、不编造），仅补结构字段。
  - 用 Task 2 校验函数断言 6 张 DAG 全部合法（写入文件级守卫或单测）。
- **Acceptance Criteria Addressed**: AC-2, AC-9（rubric 基础）
- **Test Requirements**:
  - `rule` TR-3.1: 遍历 `CATEGORY_DAGS`，6 个 category 均非空；每张 `nodes.length >= 3`；校验函数返回 `ok=true`。Evidence: 单测。
  - `rule` TR-3.2: 氧化还原 DAG 含 `stage` 分组（stage1/stage2 节点均存在）；无机推断/有机 DAG 节点 id 不为 generic 占位且含 `dependsOn` 边；6 张中至少 4 张含 `skipWhen` 或 `branchKey`（体现非纯线性）。Evidence: 源码 + 单测。
  - `rubric` TR-3.3: 6 张 DAG 结构清晰度与教学贴合度；scale 1-5；anchors 1=线性无依赖分支 / 3=有依赖但表达不清 / 5=精准映射推导逻辑；threshold >= 4；evidence spec 设计 B 表 + 源码。
- **Notes**: 本任务是本规格的核心交付物（6 张推导图）。

## Task 4: 派生反应侧 18 个 kind 的 DAG
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1, Task 2
- **Description**:
  - 在 `thoughtChainDAGs.ts` 追加导出 `REACTION_DAGS: Record<ReactionSolveKind, ThoughtChainDAG>`。
  - 由现有 `REACTION_SOLVE_MODELS`（`engine/reactionSolveModels.ts`）派生：每步 → 节点（补 `id`、`dependsOn` 默认接前一步、`layer` 直填）；有机 9 子类型的 `ORGANIC_SHARED_STEPS` 前两步 `dependsOn` 正确（step2 dependsOn step1），子类型步骤 dependsOn 接共享第二步。
  - `reaction-generic` 也派生一张（3 步），作为降级兜底。
  - 用 Task 2 校验函数断言全部合法。
- **Acceptance Criteria Addressed**: AC-3
- **Test Requirements**:
  - `rule` TR-4.1: 19 个 `ReactionSolveKind` 均有条目；18 个非通用 kind 的 DAG 校验 `ok=true`；`reaction-generic` 存在。Evidence: 单测。
  - `rule` TR-4.2: 任一有机子类型 DAG 的前两个节点 title 为「认官能团」「定反应类型」，且第二个 `dependsOn` 含第一个 id。Evidence: 单测。
- **Notes**: 与 Task 3 可并行（都依赖 Task 1/2），但写在同一文件，需协调不冲突。

## Task 5: 映射对齐表 + 补齐 3 个脚手架模块缺口
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 新建 `miniapp/src/engine/thoughtChainMapping.ts`，导出 `CATEGORY_REACTION_SCAFFOLD_MAP`（spec 设计 C 的表）+ 查询函数 `getReactionKinds(category)` / `getScaffoldModules(category)`。
  - 在 `types/scaffold.ts` 的 `ScaffoldModuleKind` 联合**加性新增**：`'chemical-equilibrium' | 'inorganic-inference' | 'organic-reaction'`。
  - 在 `engine/scaffoldChains.ts` 的 `MODULE_CHAINS` 中为 3 个新模块各补一条 A–F 链（复用 `TASK_TEMPLATES`，按模块微调 `checks`，与现有 5 模块风格一致），保持 `Record<ScaffoldModuleKind, ScaffoldChain>` exhaustive。
- **Acceptance Criteria Addressed**: AC-4
- **Test Requirements**:
  - `rule` TR-5.1: `CATEGORY_REACTION_SCAFFOLD_MAP` 覆盖 6 个 category，每个映射 ≥1 reactionKind 且 ≥1 scaffoldModule；`ScaffoldModuleKind` 含 3 个新值。Evidence: 源码 + 单测。
  - `rule` TR-5.2: `MODULE_CHAINS` 对 8 个模块（5 旧 + 3 新）均可取到非空 A–F 链（每链 tasks.length ≥ 5）；`GetDiagnostics` 为空（Record exhaustive 编译通过）。Evidence: 单测 + GetDiagnostics。
- **Notes**: 改动 `types/scaffold.ts` 为加性新增联合成员，不影响现有 5 模块消费方。

## Task 6: DAG 门控纯函数（从线性泛化到 DAG）
- **Status**: `completed`
- **Priority**: medium
- **Depends On**: Task 1
- **Description**:
  - 新建 `miniapp/src/engine/thoughtGate.ts`，复用 `StepGateState`，导出：
    - `initialDagGateStates(dag)`：根节点 unlocked，其余 locked；
    - `isUnlocked(dag, states, nodeId, skipResolver?)`：全部 dependsOn passed（或 skipWhen 命中视为 passed）时 true；
    - `markDagPassed` / `markDagFailed` / `resetDagStep`：沿用线性门控语义，标记后按依赖更新解锁。
  - 纯函数纪律（不就地修改、不抛错、越界安全空值），与 `scaffoldChains.ts` 线性门控**并存不替换**。
- **Acceptance Criteria Addressed**: AC-6
- **Test Requirements**:
  - `rule` TR-6.1: 取一张含分支依赖的 DAG（如氧化还原），逐节点 passed，断言解锁顺序符合拓扑；根节点初始 unlocked；访问 locked/越界节点返回安全空值不抛错。Evidence: 单测。
  - `rule` TR-6.2: `skipWhen` 命中时，依赖该被跳节点的下游节点能正确解锁（跳过视为 passed）。Evidence: 单测。
- **Notes**: 可与 Task 3/4/5 并行（只依赖 Task 1）。

## Task 7: 题目实例化解析（NodeDataBinding）+ 缺失降级
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 3, Task 5
- **Description**:
  - 新建 `miniapp/src/engine/thoughtChainBindings.ts`，按 category 定义 `NodeDataBinding` 表（节点 id → 从 `Challenge.solveData`/`guide` 取实例结论的读取路径），例：`redox.list-particles` ← `solveData.stage1?.particles`、`redox.balance` ← `solveData.balance`、`ec.compute` ← `solveData.calc`。
  - 导出 `resolveNodeConclusion(challenge, nodeId)`：返回该节点本题实例结论；字段缺失返回 `null`（不抛错、不编造）。
  - `preview` 模式调用方不解析（由 UI 层控制，本函数提供 `previewSafe` 选项返回空）。
- **Acceptance Criteria Addressed**: AC-7
- **Test Requirements**:
  - `rule` TR-7.1: 取一道带 solveData 的题（如 R2），解析 `redox.balance` 返回非空且与 `solveData.balance` 一致；取一道无 solveData 的题，所有节点解析返回 null 且不抛错。Evidence: 单测。
  - `rule` TR-7.2: `previewSafe=true` 时解析恒返回空（不读 solveData），保证零答案泄露。Evidence: 单测。
- **Notes**: 绑定表是「DAG ↔ 题目数据」的桥梁，需覆盖 6 题型主要节点；次要节点可留空（只读骨架）。

## Task 8: 向后兼容集成 + 可选派生导出
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 3, Task 4, Task 5, Task 6, Task 7
- **Description**:
  - 不改动现有 `solveModels.ts` / `reactionSolveModels.ts` / 面板组件的运行行为；可选择性新增 `deriveStepsFromDAG(dag)`（把 DAG 线性化为 `SolveModelStep[]`），供未来 `solveModels` 逐步切换，但**本轮不强制切换消费方**。
  - 确保现有 `StepsView`、`SolvePanels/index.tsx`、各专属面板、`pages/challenge/index.tsx` 编译通过、行为不变。
  - 若 `ScaffoldModuleKind` 新增导致某处 `switch` 非 exhaustive 编译失败，补全分支（加性）。
- **Acceptance Criteria Addressed**: AC-8
- **Test Requirements**:
  - `rule` TR-8.1: `GetDiagnostics` 全工程为空；`npm run build:h5` 成功输出到 `miniapp/dist/`。Evidence: GetDiagnostics + 构建日志。
  - `rule` TR-8.2: 现有 R1–R3 面板（氧化还原两阶段、离子/平衡/电化学）与 challenge 页行为无回归（无运行时错误、无视觉变化）。Evidence: 本地预览抽样截图。
- **Notes**: 集成任务，重点是「不破坏」；如发现回归立即回退到改动前。

## Task 9: 代表题 solveData 回填以验证绑定通路（条件于 OQ-3）
- **Status**: `completed`
- **Priority**: medium
- **Depends On**: Task 7（且 OQ-3 批准为「补代表性题」时启动；若 OQ-3 选「全量 84 题」则扩展范围）
- **Description**:
  - 为 6 题型各选 1–2 道已有 solveData 的代表题，核对 `NodeDataBinding` 能正确解析出实例结论；对解析为空但应有结论的节点，基于题目 `explanation`/`guide` 回填 `solveData`（不编造化学事实）。
  - 若 OQ-3 批准全量回填，则扩展到 84 题（分批，每批校验 precision=1）。
- **Acceptance Criteria Addressed**: AC-7（数据前提部分）
- **Test Requirements**:
  - `rule` TR-9.1: 代表题经 Task 7 解析后，关键节点（每题型 ≥1）实例结论非空且与 `explanation` 一致。Evidence: 源码核对 + 单测。
  - `rule` TR-9.2: 回填的 solveData 无化学事实错误（价态/产物/守恒式与高中定论一致）。Evidence: 人工核对 + 抽样。
- **Notes**: 范围取决于 OQ-3；化学事实零编造是硬约束，拿不准的宁可不填（节点安全降级）。

## Task 10: 单测 + 构建 + 预览回归验证
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 8, Task 9
- **Description**:
  - 汇总 Task 2/3/4/5/6/7 的单测全部通过。
  - `GetDiagnostics` 全工程为空；`npm run build:h5` 成功。
  - 本地起静态服务器预览 `miniapp/dist/`，抽样 6 题型各 1 题：preview 不泄答案、full 面板无回归、DAG 相关新增不报错。
  - 验证后删除临时静态服务器文件（`.pai/serve-dist.js` 等，gitignore）。
- **Acceptance Criteria Addressed**: AC-8
- **Test Requirements**:
  - `rule` TR-10.1: 全部单测通过；GetDiagnostics 为空；build:h5 成功。Evidence: 命令输出。
  - `rule` TR-10.2: 6 题型抽样预览无运行时错误、preview 无答案泄露。Evidence: 截图 + 操作记录。
- **Notes**: 沿用 R3 的本地构建+临时静态服务器验证方式（云预览页不可交互）。

## Task 11: 独立评审
- **Status**: `completed`
- **Priority**: medium
- **Depends On**: Task 10
- **Description**:
  - 委托一个**只读**独立子代理（fresh context）按 `review.md` 的 checkpoint 评审：6 张 DAG 结构清晰度/教学贴合度（AC-9）、向后兼容（AC-8）、化学事实零编造（红线）。
  - 评审通过 → 关闭本规格；有 actionable finding → 转 Issue 修复后复评。
- **Acceptance Criteria Addressed**: AC-9, AC-8, 红线
- **Test Requirements**:
  - `rubric` TR-11.1: 6 张推导 DAG 的结构清晰度与教学贴合度；scale 1-5；anchors 1/3/5 同 AC-9；threshold >= 4；evidence 独立评审报告。
  - `rule` TR-11.2: 独立评审确认无化学事实编造、无 R1–R3 回归。Evidence: 评审报告。
- **Notes**: Review 阶段才写 `review.md`；评审者不得是实现者本人。

## 完成证据汇总（Completion Evidence）

全部 11 个任务已完成。统一证据来源：`miniapp/scripts/verify-thoughtchain.ts`（ts-node 复跑，exit 0「全部通过 PASS」）、`GetDiagnostics` 全工程 → `[]`、`npm run build:h5` → exit 0。

| Task | 对应 AC | 完成证据 |
|------|---------|----------|
| Task 1 类型契约 | AC-1 | 新建 [thoughtChain.ts](../miniapp/src/types/thoughtChain.ts)，导出 `ThoughtLayer`/`ThoughtChainNode`/`ThoughtChainDAG`/`DAGValidationResult`；`branchKey` 已放宽为 `string \| string[]`；现有 `SolveModelStep`/`AtomicTask`/`ScaffoldBranch` 零改签名（GetDiagnostics []）。 |
| Task 2 DAG 校验 | AC-2/5 | 新建 [thoughtChainValidation.ts](../miniapp/src/engine/thoughtChainValidation.ts)；verify 脚本 8 类负样本（合法/缺依赖/有环/无根/index 破坏序/null/undefined/空 nodes）全部判定符合预期。 |
| Task 3 6 题型 DAG | AC-2/9 | [thoughtChainDAGs.ts](../miniapp/src/engine/thoughtChainDAGs.ts) 导出 `CATEGORY_DAGS`，6 张共 30 节点（Redox 11 / 离子 4 / 平衡 4 / 电化学 4 / 无机 3 / 有机 4），全部 `validateDAG` ok；均含根节点、≥1 个 skipWhen/branchKey 体现非纯线性；氧化还原含 stage1/stage2；无机/有机非 generic 占位。AC-9 独立评审 6/6 ≥ 4（见 review.md）。 |
| Task 4 反应侧 DAG | AC-3 | `REACTION_DAGS` 覆盖 19 kind（18 非通用 + reaction-generic 降级），全部校验 ok；有机子类型 step1=认官能团/step2=定反应类型且 step2 dependsOn step1，子类型步骤数=5。 |
| Task 5 映射 + 3 模块 | AC-4 | [thoughtChainMapping.ts](../miniapp/src/engine/thoughtChainMapping.ts) 映射表覆盖 6 category，每类 ≥1 reactionKind 且 ≥1 scaffoldModule；`ScaffoldModuleKind` 加性新增 3 值；`MODULE_CHAINS` 8 模块均有非空 A–F 链（≥5 步），Record exhaustive（GetDiagnostics []）。 |
| Task 6 DAG 门控 | AC-6 | [thoughtGate.ts](../miniapp/src/engine/thoughtGate.ts)：初始根节点 unlocked、非根 locked；逐节点 passed 后按依赖拓扑解锁；skipWhen 命中视为 passed；越界/未知节点安全不抛错；failed/reset 语义正确（verify 11 项断言全通过）。与线性 `scaffoldChains` 门控并存不替换。 |
| Task 7 实例化绑定 | AC-7 | [thoughtChainBindings.ts](../miniapp/src/engine/thoughtChainBindings.ts)：6 题型节点→solveData 字段绑定；带数据题解析非空、无数据题全 null 不抛错；`previewSafe=true` 恒 null（零答案泄露）；未知节点 null。 |
| Task 8 向后兼容集成 | AC-8 | 未改任何现有消费方；新增文件零导入；GetDiagnostics []；build:h5 exit 0（仅预存 asset-size 警告）。R1–R3 面板行为零回归。 |
| Task 9 代表题绑定通路 | AC-7 | verify 脚本遍历真实题库：84 题全部有 solveData，命中 294 个节点结论（Redox 94 / 离子 33 / 平衡 36 / 电化学 47 / 无机 42 / 有机 42），每题型 ≥1 节点可解析。 |
| Task 10 单测 + 构建 + 回归 | AC-8 | verify 脚本 exit 0 全通过；GetDiagnostics []；build:h5 成功。 |
| Task 11 独立评审 | AC-8/9/红线 | 只读独立子代理评审：AC-9 评分 5/4/4/4/4/5 全部 ≥4；无化学事实编造；无 R1–R3 回归。发现 1 中 + 2 低，均已修复并复核（见 review.md「发现清单与处置」+「修复后复核证据」）。 |
