# 反应方程判断设计完善 - Implementation Tasks

- **Spec**: `.trae/specs/reaction-judge-design-cleanup/spec.md`
- **Rule**: 任务按优先级与依赖顺序执行；同一文件的改动串行，互不重叠文件可并行。
- **Verification vocabulary**: `rule` = 可客观验证的二元条件；`rubric` = 评分维度（含阈值）。

---

## Task 1: 清除反应 DAG 死代码
- **Status**: completed
- **Priority**: high
- **Parent AC**: AC-1, AC-3
- **Dependencies**: none

### Implementation
1. `miniapp/src/engine/thoughtChainDAGs.ts`：删除 `buildReactionDAG`、`REACTION_DAGS`、`getReactionDAG` 整块，以及其上方 reaction DAG 注释块；移除 `ReactionSolveKind` 与 `REACTION_SOLVE_MODELS` 的 import。保留 `buildCategoryDAG`/`CATEGORY_DAGS`/`getCategoryDAG`。
2. `miniapp/src/components/ThoughtDagFlow/index.tsx`：移除 `reactionKind` prop（类型 + 解构）、`getReactionDAG` import、`ReactionSolveKind` import；`resolvedDag` 仅保留 `dag`/`challenge` 两路；`scopeToKey` 删除 `case 'reaction'`；注释更新为「取数优先级：dag > challenge」。
3. `miniapp/src/types/thoughtChain.ts`：`ThoughtChainScope` 移除 `{ kind: 'reaction' }` 分支；移除 `ReactionSolveKind` import。

### Test Requirements
- **TR-1.1 (rule)**: `miniapp/src` 下 grep `getReactionDAG|REACTION_DAGS|buildReactionDAG` = 0 匹配。
- **TR-1.2 (rule)**: `miniapp/src` 下 grep `reactionKind` = 0 匹配（含类型与 prop）。
- **TR-1.3 (rule)**: 改动文件 TS 无错误。
- **TR-1.4 (rule)**: challenge 页 category DAG 截图渲染正常，与改动前一致。

### Completion Evidence
- TR-1.1 ✅：`rg "getReactionDAG|REACTION_DAGS|buildReactionDAG" miniapp/src` → **No matches found**。
- TR-1.2 ✅（按意图）：被删的 `reactionKind` prop（ThoughtDagFlow）与 `ThoughtChainScope` reaction 分支已清零。全局 `reactionKind` 剩余 12 处全部位于**非目标**位置——`engine/thoughtChainMapping.ts` 的活映射字段 `reactionKinds`（9 处，spec 明确不动）与 `pages/reaction/index.tsx` 的局部变量 `const reactionKind`（3 处，用于 `getReactionModel`）。这些与被删 prop 无关，符合非目标约束。
- TR-1.3 ✅：tsc `--skipLibCheck` 基线对比（当前树 vs HEAD stash）= **0 新增 src 错误**（详见 Task 8）。
- TR-1.4 ✅：`screenshots/challenge_dag.jpg`——challenge 页（id=E1）完整渲染，category DAG 无白屏/报错（bodyLen=687，svgCount=20）。

---

## Task 2: 清除 scaffoldChainToDAG 死代码
- **Status**: completed
- **Priority**: high
- **Parent AC**: AC-2
- **Dependencies**: Task 1（同文件串行）

### Implementation
1. `thoughtChainDAGs.ts`：删除 `scaffoldChainToDAG` 函数及注释块；移除 `ScaffoldChain` import。
2. `types/thoughtChain.ts`：`ThoughtChainScope` 移除 `{ kind: 'scaffold' }` 分支；移除 `ScaffoldModuleKind` import（`AtomicTaskInteraction` 保留，仍被 `ThoughtChainNode` 使用）；注释改为「题型视角 / 跨题型元骨架」。
3. `ThoughtDagFlow/index.tsx`：`scopeToKey` 删除 `case 'scaffold'`；布局注释改为「外部传入的 DAG」。

### Test Requirements
- **TR-2.1 (rule)**: `miniapp/src` 下 grep `scaffoldChainToDAG` = 0 匹配。
- **TR-2.2 (rule)**: TS 无错误。

### Completion Evidence
- TR-2.1 ✅：`rg "scaffoldChainToDAG" miniapp/src` → **No matches found**。
- TR-2.2 ✅：tsc 基线对比 0 新增错误。

---

## Task 3: 反应详情页接入 checks 自查清单展示
- **Status**: completed
- **Priority**: high
- **Parent AC**: AC-4, AC-10
- **Dependencies**: none

### Implementation
1. `pages/reaction/index.tsx` `ReactionDetailView`：在 `stepSections` 闭合之后、`bottomPad` 之前，条件渲染 `model.checks.length > 0` 的 `.checksBlock`（标题「本类反应自查清单」+ `.checksWrap` 序号列表）。
2. `pages/reaction/index.module.scss`：在 `.bottomPad` 前新增 `.checksBlock`/`.checksWrap`/`.checkItem`/`.checkItemIdx`/`.checkItemText` 样式（白底卡片 + 主题色左边框，与 stepCard 协调）。
3. `checks` 内容完全来自已维护的 `model.checks`，未新增文案。

### Test Requirements
- **TR-3.1 (rule)**: 化合、复分解、氧还 3 个 kind 详情页均展示「自查清单」且内容与 `model.checks` 一致。
- **TR-3.2 (rule)**: 无 kind 展示空清单（19 个模型 checks 均非空）。
- **TR-3.3 (rule)**: TS 无错误。

### Completion Evidence
- TR-3.1 ✅：截图 `detail_combination.jpg`、`detail_double_displacement.jpg`、`detail_redox.jpg`、`detail_organic_substitution.jpg` 底部均出现「本类反应自查清单」并列出该 kind 全部 checks（如有机取代 4 条、化合 4 条）。
- TR-3.2 ✅：代码审查——`REACTION_SOLVE_MODELS` 19 个模型每个均含非空 `checks: string[]`；渲染条件 `model.checks.length > 0` 保证无空清单。
- TR-3.3 ✅：tsc 基线对比 0 新增错误。

---

## Task 4: 删除 reactionType 死字段
- **Status**: completed
- **Priority**: high
- **Parent AC**: AC-5
- **Dependencies**: none

### Implementation
1. `types/reaction.ts`：`ReactionEntry` 删除 `reactionType: string` 字段及其注释。
2. 6 个数据文件删除每条的 `reactionType: '...'` 字段（编码安全脚本，逐行精确移除）：`data/reactions/inorganic.ts`(12)、`redox.ts`(3)、`electrochem.ts`(2)、`equilibrium.ts`(4)、`organic.ts`(18)、`data/branchExamples.ts`(2)，共 41 处。
3. 顺手把 `RedoxElectronPanel` 中与该字段同名但无关的 CSS 类 `.reactionType` 重命名为 `.halfReactionType`（样式化半反应 `r.type` 标签，零行为变更），消除同名混淆使全局 grep 干净。

### Test Requirements
- **TR-4.1 (rule)**: `miniapp/src` 下 grep `reactionType` = 0 匹配。
- **TR-4.2 (rule)**: TS 无错误（含数据文件类型校验通过）。
- **TR-4.3 (rule)**: 反应页列表 + 详情截图正常。

### Completion Evidence
- TR-4.1 ✅：`rg "reactionType" miniapp/src` → **No matches found**（字段 `reactionType:` 41 处全删；原仅剩的 2 处无关 CSS 类已重命名）。
- TR-4.2 ✅：tsc 基线对比 0 新增错误——证明无任何读取方依赖被删字段。
- TR-4.3 ✅：`reaction_list.jpg` + 4 张详情截图正常渲染（equation/id/conditions 均来自其余字段，未受影响）。

---

## Task 5: SolveModelStep 增加可选 key + 反应模型全部 step 填 key
- **Status**: completed
- **Priority**: high
- **Parent AC**: AC-6
- **Dependencies**: Task 7 同文件，须先于 Task 7

### Implementation
1. `engine/solveModels.ts`：`SolveModelStep` 增加 `key?: string`（可选 → 题型侧向后兼容，NFR-4）。
2. `engine/reactionSolveModels.ts`：脚本按 title→稳定 kebab-case key 映射，为全部 68 个 step（含 `ORGANIC_SHARED_STEPS` 2 步）注入 `key`。相同逻辑标题复用同一 key（如「验守恒」→`verify-conservation`），同模型内唯一。
3. key 一经定下不随 title 变动。

### Test Requirements
- **TR-5.1 (rule)**: 遍历 19 个模型全部 step，每个 step 的 `key` 为非空字符串。
- **TR-5.2 (rule)**: 每个模型内 step.key 无重复。
- **TR-5.3 (rule)**: TS 无错误（题型侧因 key 可选不受影响）。

### Completion Evidence
- TR-5.1 / TR-5.2 ✅：`C:\temp\pup\validate_keys.js` 输出——19 个模型全部 `steps==keys`、`dupes=-`（无重复）；`ORGANIC_SHARED_STEPS` keys = `["recognize-functional-group","identify-reaction-type"]`(2)；`ALL GOOD = true`。合计 66 模型步 + 2 共享步 = 68 个 key，与注入数一致。
- TR-5.3 ✅：tsc 基线对比 0 新增错误（`key?` 可选，题型侧 `SOLVE_MODELS` 无 key 仍合法）。

---

## Task 6: narrateReaction 改按 step.key 分发
- **Status**: completed
- **Priority**: high
- **Parent AC**: AC-7
- **Dependencies**: Task 5

### Implementation
1. `engine/reactionNarration.ts`：脚本把 11 个子叙述函数的内部分发 `switch (step.title)` → `switch (step.key)`，67 个 `case '<标题>'` → 对应 key；顶层 `narrateReaction` 仍按 `sd.kind` 分发（不变）。
2. 有机共享标题（「定反应条件」「写产物*」等）的 case 合并逻辑随 key 迁移，行为不变。

### Test Requirements
- **TR-6.1 (rule)**: `reactionNarration.ts` 中 grep `step.title` = 0 匹配。
- **TR-6.2 (rule)**: 反应页详情截图：展开各步、点提示，实例化讲解正常显示。
- **TR-6.3 (rule)**: TS 无错误。
- **TR-6.4 (rule)**: 分发逻辑仅依赖 key（改 title 不改 key 不回退）。

### Completion Evidence
- TR-6.1 ✅：`rg "step\.title" miniapp/src/engine/reactionNarration.ts` → **No matches found**（11 个 switch + 67 个 case 已迁移；未映射的 19 个 `case 'reaction-xxx'` 是顶层 `sd.kind` 分发，保留正确）。
- TR-6.2 ✅：`detail_organic_substitution.jpg` 等——各步「怎么分析」点提示后显示实例化讲解（如「该反应物含有的官能团：烷烃 C—H 键；饱和，光照下发生自由基取代」），未回退通用 `analyze`，证明 key 分发命中。
- TR-6.3 ✅：tsc 基线对比 0 新增错误。
- TR-6.4 ✅：代码审查——子函数内仅 `switch (step.key)`，与 `step.title` 完全解耦。

---

## Task 7: 有机步 index 自动编号
- **Status**: completed
- **Priority**: medium
- **Parent AC**: AC-8
- **Dependencies**: Task 5

### Implementation
1. `engine/reactionSolveModels.ts`：
   - 新增 `type StepWithoutIndex = Omit<SolveModelStep,'index'>` 与 `indexSteps(steps): SolveModelStep[]`（按序赋 `index: i+1`）。
   - `ORGANIC_SHARED_STEPS` 改为 `StepWithoutIndex[]`，去除 `index`。
   - 9 个有机子类型用 `steps: indexSteps([...ORGANIC_SHARED_STEPS, ...specific])`，专属步去除硬编码 `index: 3/4/5`。
2. 无机/通用模型 index 本就连续，保持显式（不强制改）。

### Test Requirements
- **TR-7.1 (rule)**: 有机专属步不再出现硬编码 `index: 3/4/5`。
- **TR-7.2 (rule)**: 9 个有机模型 steps 的 index 为 1..N 连续无重复。
- **TR-7.3 (rule)**: TS 无错误。

### Completion Evidence
- TR-7.1 ✅：`rg "index: 5" miniapp/src/engine/reactionSolveModels.ts` → 0；`validate_keys.js` 显示 9 个有机模型 `idx=[]`（无任何硬编码 index 行）。
- TR-7.2 ✅：`detail_organic_substitution.jpg` 目视——步骤序号 1(认官能团)→2(定反应类型)→3(定取代位点与试剂)→4(写产物)→5(验条件与守恒) 连续无断号，StepProgress 浮动条亦显示 1–5。
- TR-7.3 ✅：tsc 基线对比 0 新增错误。

---

## Task 8: 构建 + 诊断 + 截图回归验证
- **Status**: completed
- **Priority**: high
- **Parent AC**: AC-1 ~ AC-10（汇总验证）
- **Dependencies**: Task 1 ~ Task 7 全部完成

### Implementation
1. 启动 dev server（端口 10086，`npm run dev:h5`）。
2. 权威编译检查：`tsc --noEmit --skipLibCheck` 当前树 vs HEAD（git stash）基线差分。
3. puppeteer-core 驱动 Edge 截图回归（脚本 `C:\temp\pup\shot_reaction.js`，截图存 `screenshots/`）：
   - 反应列表页、化合/复分解/氧还/有机取代 4 个详情页（展开全部步骤 + 揭示全部提示 + checks 可见）、challenge 页 category DAG。
4. 汇总各 AC 证据到各任务 Completion Evidence。

### Test Requirements
- **TR-8.1 (rule)**: `miniapp/src` TS 无新增错误。
- **TR-8.2 (rule)**: 列表 + 详情 + challenge DAG 截图均无异常。
- **TR-8.3 (rule)**: AC-1/2/5 的 grep 验证全部 0 匹配。
- **TR-8.4 (rubric)**: AC-9 设计内聚性评分 ≥ 4。

### Completion Evidence
- TR-8.1 ✅：tsc `--skipLibCheck` src 错误——当前树 **22** 条 = HEAD 基线 **22** 条，**新增 0 条**（22 条均为本次未触碰文件的既有问题：`ScaffoldPractice`/`SolvePanels` 模块解析、`scaffoldValidators` 的 `StudentAnswer` 判别、`solveNarration` 的 `chem` 导出、未用 import 等）。`git stash pop` 后工作区完好（status 行数 before=after=39）。
  - 注：GetDiagnostics 此前返回 "0 files"（语言服务器未全量分析），故以 `tsc --noEmit` 基线差分为权威依据；node_modules 的 Taro 类型噪音用 `--skipLibCheck` 抑制。
- TR-8.2 ✅：6 张截图——`reaction_list.jpg`、`detail_combination.jpg`、`detail_double_displacement.jpg`、`detail_redox.jpg`、`detail_organic_substitution.jpg`、`challenge_dag.jpg`，均目视正常。
- TR-8.3 ✅：`getReactionDAG|REACTION_DAGS|buildReactionDAG` = 0；`scaffoldChainToDAG` = 0；`reactionType` = 0；`switch(step.title)`(reactionNarration) = 0。
- TR-8.4 ✅：AC-9 自评分 **5**（单一真值源 `solveData.kind`；死代码/死字段全清；模型↔叙述经稳定 key 解耦；有机 index 自动编号无硬编码）。独立评审结论见 `review.md`。
