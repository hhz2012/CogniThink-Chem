# 思路递进图组件 + 5 题型推广 实现计划

## 仓库研究结论

### 1. DAG 资产已完备，但未被 challenge 页消费
- [thoughtChainDAGs.ts](file:///d:/github/CogniThink-Chem/miniapp/src/engine/thoughtChainDAGs.ts) 已定义全部 6 个题型的标准推导 DAG：每个节点含 `id / dependsOn / skipWhen / branchKey / metaPhase`，并通过 `buildCategoryDAG` 自动前置 S1（读题定向）、追加 S5（定答案）。导出 `getCategoryDAG(category)`（[L312](file:///d:/github/CogniThink-Chem/miniapp/src/engine/thoughtChainDAGs.ts#L312)）。
- 但 Grep 确认：**该文件目前未被任何页面/组件导入**（仅 `thoughtChainBranchResolver.ts` 内部取 DAG 做分支解析）。challenge 页的"思考过程启发"区消费的是 `solveModels.steps`（线性步骤），不是 DAG。
- 6 个题型 DAG 的 `metaPhase` 分布（[NODE_META_PHASE L43-94](file:///d:/github/CogniThink-Chem/miniapp/src/engine/thoughtChainDAGs.ts#L43-L94)）天然构成递进分层，适合做分层布局图。

### 2. skipWhen 运行时求值尚不存在，但有不编造的判定基础
- 4 个 `skipWhen` 键仅在 DAG 定义中出现，**无任何运行时求值实现**（Grep 确认）。`thoughtGate.ts` 只定义了 `SkipResolver` 类型（[L13](file:///d:/github/CogniThink-Chem/miniapp/src/engine/thoughtGate.ts#L13)），无构造方。
- 但 [thoughtChainBindings.ts](file:///d:/github/CogniThink-Chem/miniapp/src/engine/thoughtChainBindings.ts) 已有 `hasNodeConclusion(challenge, nodeId)`（[L143](file:///d:/github/CogniThink-Chem/miniapp/src/engine/thoughtChainBindings.ts#L143)）：从 `Challenge.solveData` 取该节点的实例结论，缺失返回 null。
- **关键对齐**：4 个 skipWhen 节点的结论恰好都取自 solveData 的某字段（见 bindings 表），字段缺失 = 本题不需该步：
  | 节点 | skipWhen | 结论取自 | 缺失语义 |
  |---|---|---|---|
  | redox.judge-order | single-pair | stage1.order / order | 单一氧还对，无需判先后 |
  | redox.patch-ho | no-ionic-equation | reaction.ionicEquation | 非离子方程式，无需补氢氧 |
  | ionic.verify | only-concentration-rank | equations | 只比浓度，无守恒式可校验 |
  | ec.compute | no-calc | calc | 非计算题，无需计算段 |
- 因此可用 **`skipResolver(node) = !!node.skipWhen && !hasNodeConclusion(challenge, node.id)`** 作为通用、不编造的跳过判定——完全复用现有 bindings 单一真值源，不引入新化学推理。

### 3. challenge 页现状（氧化还原已改，5 题型待推广）
- 答题前：读题区已**通用**渲染 S1 数据（`questionTarget` 问题行 + `problemConditions` 条件清单，[L514-531](file:///d:/github/CogniThink-Chem/miniapp/src/pages/challenge/index.tsx#L514-L531)），对 6 题型生效。
- 答题前思考区（[L534-662](file:///d:/github/CogniThink-Chem/miniapp/src/pages/challenge/index.tsx#L534-L662)）：`guideSteps = steps.filter(s => s.stage !== 'stage2')` 默认展开，`stage2GuideSteps` 折叠为选做按钮。**仅氧化还原 steps 带 `stage` 字段**，其余 5 题型 steps 无 stage → 全部默认展开、不显示折叠按钮（行为正常，只是没有"选做折叠"交互）。
- `STAGE_LABEL`（[L30-33](file:///d:/github/CogniThink-Chem/miniapp/src/pages/challenge/index.tsx#L30-L33)）和阶段二 toggle 文案（[L636](file:///d:/github/CogniThink-Chem/miniapp/src/pages/challenge/index.tsx#L636)）**写死了氧化还原文案**（"电子守恒配平"）。因其余题型无 stage 字段、`showStageHead` 恒 false，当前不触发渲染，故无可见 bug；但健壮性差，将来给其他题型加 stage 会文案错配。
- 答题后：`SolveModelPanel mode="full"`（[L720](file:///d:/github/CogniThink-Chem/miniapp/src/pages/challenge/index.tsx#L720)）。调度器 [SolvePanels/index.tsx](file:///d:/github/CogniThink-Chem/miniapp/src/components/SolvePanels/index.tsx) 已统一：有专属面板（氧化还原/离子/平衡/电化学）→ 只渲染专属可视化（无 StepsView 重复）；无机/有机 → `GenericSolvePanel`（带 StepsView + 校验要点，作为无专属面板的合理降级）。**#2 答后去重已正确实现，无需改动。**

### 4. S1 数据已全题库覆盖
- Grep 确认：6 个题库文件各 14 题，每题都有 `questionTarget` 和 `problemConditions`（各 84 匹配）。**#4 S1 接入已完成，无需补数据。**

### 5. emoji 残留（本任务范围）
- challenge 解题链路内仅 1 处彩色 emoji：[challenge/index.tsx L727](file:///d:/github/CogniThink-Chem/miniapp/src/pages/challenge/index.tsx#L727) `🧭 一步一步找到你的问题`（诊断标题）→ 替换为 `ModelIcon`。
- 其余 emoji 分布在 `solveModels.ts`（fallback icon 字符串 + 注释，UI 已用 `CATEGORY_ICON` 渲染 ModelIcon，fallback 不显示）、`data/`（章节 icon 字段，challenge 页 metaBar 用 `CATEGORY_ICON` 渲染不用此字段）、其他页面（mine/atlas/knowledge/reaction/TabBar）——**不在 challenge 解题链路内，本轮不动，避免范围蔓延。**

## 改动文件与模块

| 文件 | 改动 |
|---|---|
| `engine/thoughtChainSkip.ts`（新建） | 导出 `resolveSkippedNodeIds(challenge): Set<string>`：基于 `hasNodeConclusion` 的通用 skip 判定；纯函数、不读 solveData 以外的东西、不抛错。 |
| `components/ThoughtDagFlow/index.tsx`（新建） | 关系图组件：SVG 渲染，按 `metaPhase`（S1→S6）垂直分层布局，节点卡片 + 依赖箭头；props `{ challenge, mode: 'preview'\|'full' }`。preview=完整骨架（带 skipWhen 的节点标"视情况触发"灰虚，不读 solveData）；full=高亮本题实际执行路径（绿=执行，灰虚=跳过）。 |
| `components/ThoughtDagFlow/index.module.scss`（新建） | 容器样式；节点/连线样式用 CSS 变量支持亮/暗。 |
| `pages/challenge/index.tsx` | ①答题前思考区顶部加折叠「思路递进图」(preview)；②答题后「完整解题模型」区加 full 高亮图；③L727 🧭 → ModelIcon；④`STAGE_LABEL`/阶段二文案改为通用健壮（无 stage2 时不渲染，已有 stage2 时文案从模型取或用通用措辞）。 |
| `pages/challenge/index.module.scss` | 追加图容器、折叠头等样式。 |

**不改动**：`solveModels.ts`（不给其他题型强行加 stage，避免无化学依据的内容决策）、`thoughtChainDAGs.ts`（DAG 定义已是单一真值源，只读不写）、其他页面的 emoji、`app.config.ts`、`package.json`。

## 实现步骤（依赖有序）

1. **新建 skip 判定** `engine/thoughtChainSkip.ts`
   - `resolveSkippedNodeIds(challenge)`：取 `getCategoryDAG(challenge.category)`，遍历节点，对带 `skipWhen` 的节点用 `hasNodeConclusion` 判定；返回被跳过的 nodeId 集合。
   - 防御：category 越界 / solveData 形状异常 → 返回空集（不抛错）。
   - 导出辅助 `isNodeSkipped(challenge, nodeId)`。

2. **新建关系图组件** `components/ThoughtDagFlow/`
   - 输入 `{ challenge, mode }`。
   - 布局：按 `metaPhase` 分组（用 `META_PHASE_ORDER` 排序），每层一个横向行；节点为圆角矩形（显示序号 + title）；层间与层内依赖用 SVG `<line>` + 箭头 marker 连接。
   - 视觉：执行节点 = 青绿（`MODEL_ACCENT #2EA7C4` 品牌色，与现有解题模型一致）实线；跳过/可选节点 = 灰虚线框 + 灰虚线；S1/S5/S6 元骨架节点用胶囊形区分。
   - preview 模式：不调用 `hasNodeConclusion`（不泄答案），带 `skipWhen` 的节点统一标为"条件触发"灰虚；full 模式：调用 `resolveSkippedNodeIds` 高亮实际跳过。
   - 空态：DAG 节点为空 → 不渲染（return null）。
   - 图例：底部小图例说明"实线=执行 / 灰虚=跳过或条件触发"。
   - 样式顶部 `@use '@/styles/variables.scss' as *;`；仅用 `@tarojs/components`（SVG 标签在 Taro H5 可直接用）。

3. **接入 challenge 页**
   - 答题前：在"二、思考过程启发"标题行下方、步骤列表上方，加一个可折叠区块「🧩→改图标」思路递进图（preview 模式），默认折叠（不干扰先看步骤）。
   - 答题后：在 `solveModelFull` 区块内、`<SolveModelPanel mode="full" />` 上方，渲染 full 模式高亮图。
   - L727：`🧭 一步一步找到你的问题` → `<ModelIcon name="compass" .../> 一步一步找到你的问题`（需确认 ModelIcon 有 compass；若无则用现有图标如 `navigation` 或 `lightbulb`）。
   - `STAGE_LABEL`：改为仅对有 stage2 的模型生效；阶段二 toggle 文案用通用措辞"展开选做推导（选做）"替代写死的"电子守恒配平推导"，或从模型读取。**最小改动**：保留 `STAGE_LABEL` 但把 stage2 文案泛化为"阶段二 · 选做推导"，并确保无 stage2 时不渲染 toggle（已由 `stage2GuideSteps.length > 0` 守卫）。

4. **验证**（见下）。

## 依赖与注意事项
- **不编造化学事实红线**：skip 判定完全复用 `thoughtChainBindings` 的现有字段映射，不新增任何化学推理；若某题该步数据缺失，图上显示"跳过"——这是数据状态的真实反映，不假装执行。
- **preview 不泄答案**：preview 模式绝不读 solveData（`hasNodeConclusion` 不在 preview 调用）；只展示题型通用 DAG 骨架。
- **Taro H5 SVG**：Taro 4 React H5 支持 JSX 内联 SVG 标签；若编译异常，降级为 View 绝对定位布局（备选）。验证步骤会确认。
- **ModelIcon 图标名**：需先读 `ModelIcon.tsx` 确认可用图标名（compass/navigation 等），避免引用不存在的图标。
- **不引入新依赖、不改 app.config.ts**（NFR 约束）。

## 验证
1. `GetDiagnostics` 返回空数组（零 TS 错误）。
2. dev server 编译成功（`npm.cmd run dev:h5`，端口 10086），无 `compile_error`。
3. 浏览器截图验证（6 题型各一张答题后 full 图）：
   - 氧化还原 R1：④判先后、⑩补氢氧灰虚跳过，其余青绿高亮执行。
   - 电化学某非计算题（如 C1）：`ec.compute` 灰虚跳过。
   - 电化学计算题（如 C4）：`ec.compute` 青绿执行。
   - 离子/平衡/无机/有机：图正常渲染，节点与依赖边与 DAG 定义一致。
4. 答题前 preview 图：显示完整骨架不泄答案，带 skipWhen 的节点标"条件触发"。
5. 回归：氧化还原答题前阶段一展开/阶段二折叠、答后 RedoxElectronPanel 不受影响；其余 5 题型思考区全展开行为不变。

## 风险与处理
- **风险 1：SVG 在 Taro H5 编译/渲染异常** → 先用最小 SVG 验证；若不行，降级为 View 分层布局（节点卡片 + CSS 伪元素连线），视觉等价。
- **风险 2：DAG 同层多节点布局重叠**（如氧化还原 S2 有 ①③ 两个节点）→ 同层节点横向均分，连线按节点中心计算起止点，避开重叠。
- **风险 3：给其他题型加 stage 划分的诉求未满足** → 本轮明确不做（无化学依据，属内容决策）；计划中标注，用户如需可单独提需求。
- **风险 4：skip 判定误判（数据缺失但该步应执行）** → 可接受：图上灰显提示数据缺口，且与"不编造"一致；后续补数据后自动变绿。
