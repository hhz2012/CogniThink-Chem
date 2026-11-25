# 思路递进图接入反应页 & 脚手架练习 实现计划

## Repository Research（结论均来自真实代码，非想象）

**组件现状（紧耦合题型侧）** — [ThoughtDagFlow/index.tsx](file:///d:/github/CogniThink-Chem/miniapp/src/components/ThoughtDagFlow/index.tsx)：
- props 仅 `{ challenge, mode? }`，内部只调 `getCategoryDAG(challenge.category)`。
- 布局按 `metaPhase` 分层：每层节点横向并排、层间垂直；无 `metaPhase` 的节点全部归入末尾 `'none'` 层 → 横向排成一行。
- 这对反应侧 / 脚手架侧是错的：两者都是纯线性链（无 `metaPhase`），应**单列垂直**布局。

**反应侧** — [thoughtChainDAGs.ts L314-351](file:///d:/github/CogniThink-Chem/miniapp/src/engine/thoughtChainDAGs.ts#L314-L351)：
- `getReactionDAG(kind)` 已存在，`buildReactionDAG` 派生纯线性链：`id=${kind}.step-${index}`，`dependsOn` 接前一步；**无 `metaPhase`、无 `skipWhen`、无 S1/S5 元骨架**。
- [pages/reaction/index.tsx](file:///d:/github/CogniThink-Chem/miniapp/src/pages/reaction/index.tsx)：`ReactionDetailView` 从 `entry.solveData.kind` 取 `ReactionSolveKind`（L115），渲染「分步脚手架 · {model.name}」（L149）。当前无任何思路图。
- 反应无答题对错概念，图恒为全执行态（青绿），用 `mode="preview"` 即可。

**脚手架侧** — 关键判断（遵守经验 100029238：不凭想象补连线）：
- [components/ScaffoldPractice](file:///d:/github/CogniThink-Chem/miniapp/src/components/ScaffoldPractice/index.tsx) 学生实际走的是 `MODULE_CHAINS[practice.module]` 的 A–F 原子任务链（[scaffoldChains.ts](file:///d:/github/CogniThink-Chem/miniapp/src/engine/scaffoldChains.ts)），靠 `markPassed` 线性解锁下一步。
- 它**不是** ThoughtChainDAG，也**不能**映射到题型 solve DAG——那会渲染出与页面实际步骤不一致的节点（属编造）。
- [types/thoughtChain.ts L1-5](file:///d:/github/CogniThink-Chem/miniapp/src/types/thoughtChain.ts#L1-L5) 明确：ThoughtChainDAG 契约**本来就设计为统摄题型侧、反应侧、脚手架侧三套资产**，且 `ThoughtChainNode` 已带 `checks?`/`pitfall?`/`interaction?` 对齐 `AtomicTask`。所以正确做法是新增 `ScaffoldChain → ThoughtChainDAG` 转换层：节点取 `task.title`，边取自真实线性门控依赖（前一步 passed 才解锁后一步）。
- grep 确认：当前**没有**任何 scaffold→DAG 转换器；`ThoughtChainScope` 联合无 `scaffold` 变体，但**无消费方穷举此联合**（仅类型声明处出现），`validateDAG` 也不读 scope → 加变体安全。

**可选性安全**：`ThoughtChainNode.analyze` / `layer` 当前必填，但 `AtomicTask` 没有这两个字段。grep 确认所有 `.layer`/`.analyze` 读取都发生在 `SolveModelStep` 或知识点上，**没有任何消费方从 ThoughtChainNode 读 `layer`/`analyze`** → 把它们改为可选是安全的，且避免为脚手架节点编造无来源的文案。

## Files and Modules

1. `miniapp/src/types/thoughtChain.ts`
   - `ThoughtChainScope` 联合新增 `{ kind: 'scaffold'; module: ScaffoldModuleKind | 'reaction' }`（需从 `./scaffold` 导入 `ScaffoldModuleKind`；该文件已导入 `AtomicTaskInteraction`，无新增循环依赖）。
   - `ThoughtChainNode.analyze`、`layer` 改为可选（`analyze?: string`、`layer?: ThoughtLayer`）。
2. `miniapp/src/engine/thoughtChainDAGs.ts`
   - 新增并导出 `scaffoldChainToDAG(chain: ScaffoldChain): ThoughtChainDAG`：线性节点 `id=${module}.${kind}`，`dependsOn` 接前一 task 的 kind（首步为空），`scope={kind:'scaffold', module}`，`name=chain.name`；复用 `task` 的 `title/goal/interaction/checks/pitfall`，不写无来源的 `analyze/layer`。
3. `miniapp/src/components/ThoughtDagFlow/index.tsx`
   - 泛化 props：`{ challenge?, reactionKind?, dag?, mode?, dimmedIds?, legendLabels? }`；取数优先级 `dag > reactionKind ? getReactionDAG : challenge ? getCategoryDAG`。
   - 自适应布局：`nodes.some(n => n.metaPhase)` 为真 → 现有按阶段横向分层；为假 → 每个节点独占一行（复用现有渲染循环，自动单列垂直堆叠 + 竖直连线）。
   - full 模式置灰：`dimmedIds`（若传）优先，否则 `challenge ? resolveSkippedNodeIds(challenge) : ∅`；preview 模式仍按 `skipWhen`。
   - arrow id 改为由 scope 推导的稳定 key（兼容无 challenge 的实例）。
   - `legendLabels` 覆盖图例文案（缺省沿用 challenge 文案，保证 challenge 页零回归）。
4. `miniapp/src/pages/reaction/index.tsx` + `.module.scss`
   - `ReactionDetailView` 在「分步脚手架」区上方插入思路递进图卡片：`<ThoughtDagFlow reactionKind={kind} mode="preview" />`。
5. `miniapp/src/components/ScaffoldPractice/index.tsx` + `.module.scss`
   - `useMemo` 由 `chain` 生成 dag；在练习标题下方、步骤流上方渲染 `<ThoughtDagFlow dag={dag} mode="full" dimmedIds={lockedIds} legendLabels={{active:'已解锁', dimmed:'未解锁'}} />`，`lockedIds` 取 `gate` 中 `state==='locked'` 的 task id。

## Implementation Steps（依赖序）

1. `types/thoughtChain.ts`：加 scaffold scope 变体 + `analyze`/`layer` 改可选。
2. `engine/thoughtChainDAGs.ts`：实现并导出 `scaffoldChainToDAG`。
3. `components/ThoughtDagFlow`：泛化 props + 自适应单列布局 + `dimmedIds`/`legendLabels`。
4. `pages/reaction`：接入图 + scss。
5. `components/ScaffoldPractice`：接入图 + scss。
6. 验证（见下）。

## Dependencies and Considerations

- 纯加性：challenge 页现有 `<ThoughtDagFlow challenge={...} mode=... />` 调用签名不变，props 全部缺省兼容，**零回归**。
- 线性布局不新写布局路径，而是把 `layers` 拆成单节点行，复用现有居中/堆叠/连线逻辑——改动面最小。
- 不编造化学事实：脚手架每条边都可回指到 `scaffoldChains.ts` 的 `markPassed` 解锁关系；节点文案来自 `AtomicTask.title`；无来源的 `analyze`/`layer` 诚实省略。
- dev server 上一任务在端口 10086 运行（后台命令），验证前确认存活，不在则重启 `npm.cmd run dev:h5`。

## Validation

1. `GetDiagnostics` 返回空数组（含 4 个改动文件）。
2. dev server 编译无报错。
3. 浏览器截图：
   - 反应详情页（任选一条，如 `#/pages/reaction/index?id=...`）：思路递进图**单列垂直**渲染，节点青绿、连线竖直向下，无控制台错误。
   - 首页脚手架练习（`#/pages/home/index`，滚动到 A–F 区）：A–F 链渲染，初始 A 高亮、B–F 灰虚（未解锁）；答过 A 后 B 变高亮。无控制台错误。
4. 回归：challenge 页 6 题型图仍正常（props 未变，仅内部加分支）。
5. 截图复制到 `d:\github\CogniThink-Chem\screenshots\`。

## Risks

- 线性布局 `NODE_W=172` 可能截断较长 title：反应/脚手架 title 与题型侧长度相近，先按现状验证；若明显截断再统一调宽（属后续微调，不阻塞接入）。
- `ScaffoldModuleKind` 导入 thoughtChain.ts：该文件已从 `./scaffold` 导入，`scaffold.ts` 不反向导入 thoughtChain → 无循环依赖。
- 后台 dev server 若已退出：验证前 `npm.cmd run dev:h5` 重启（PowerShell 用 `.cmd` 绕过 `.ps1` 执行策略）。
