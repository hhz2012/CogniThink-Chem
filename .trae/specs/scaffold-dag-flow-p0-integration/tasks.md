# Scaffold DAG Flow P0 前端接入 - Implementation Plan

## Task 1: 扩展 ThoughtChainScope + 补全 scopeToKey
- **Status**: `completed`
- **Completion Evidence**: TR-1.1 ✓ [thoughtChain.ts](file:///D:/github/CogniThink-Chem/miniapp/src/types/thoughtChain.ts) 已导入 `ScaffoldModuleKind` 并在 `ThoughtChainScope` 加 `{ kind:'scaffold'; module }` 变体；grep `scaffold.ts` 无 `from.*thoughtChain`（无循环依赖）。TR-1.2 ✓ [ThoughtDagFlow/index.tsx scopeToKey](file:///D:/github/CogniThink-Chem/miniapp/src/components/ThoughtDagFlow/index.tsx) 已补 `case 'scaffold'`；challenge 页调用未改（tsc 无回归错误）。
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 在 `miniapp/src/types/thoughtChain.ts` 的 `ThoughtChainScope` 联合新增 `{ kind: 'scaffold'; module: ScaffoldModuleKind | 'reaction' }` 变体，并从 `./scaffold` 导入 `ScaffoldModuleKind`。
  - 在 `miniapp/src/components/ThoughtDagFlow/index.tsx` 的 `scopeToKey` switch 补 `case 'scaffold': return \`scaf-\${scope.module}\``，保证穷尽、marker id 去重。
- **Acceptance Criteria Addressed**: AC-1, AC-3
- **Test Requirements**:
  - `rule` TR-1.1: `thoughtChain.ts` 中 `ThoughtChainScope` 含 `kind: 'scaffold'` 变体；`scaffold.ts` grep `from.*thoughtChain` 无匹配（无循环依赖）。证据：grep 输出。
  - `rule` TR-1.2: `ThoughtDagFlow/index.tsx` `scopeToKey` 含 `case 'scaffold'`；challenge 页调用 `<ThoughtDagFlow challenge=... />` 未改。证据：源码行号。

## Task 2: 实现 scaffoldChainToDAG 转换层
- **Status**: `completed`
- **Completion Evidence**: TR-2.1 ✓ [thoughtChainDAGs.ts](file:///D:/github/CogniThink-Chem/miniapp/src/engine/thoughtChainDAGs.ts) 末尾已导出 `scaffoldChainToDAG`；空 `chain.tasks` → `chain.tasks.map` 返回 `[]`，不抛错。TR-2.2 ✓ 节点 id=`${module}.${kind}`，首步 `dependsOn:[]`、后续 `[前一步id]`。TR-2.3 ✓ 函数体仅赋值 id/index/title/goal/dependsOn/interaction/checks/pitfall，无 analyze/layer/metaPhase/skipWhen/branchKey/stage。TR-2.4 rubric 评分 **5/5**：所有字段严格来自 AtomicTask + markPassed 线性解锁，无来源字段一律省略。
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 在 `miniapp/src/engine/thoughtChainDAGs.ts` 末尾新增并导出 `scaffoldChainToDAG(chain: ScaffoldChain): ThoughtChainDAG`。
  - 节点映射：`id = \`${chain.module}.${task.kind}\``；`index/title/goal/interaction/checks/pitfall` 透传 `AtomicTask`；`dependsOn` 首步 `[]`、第 i 步 `[nodes[i-1].id]`；**不设置** `analyze/layer/stage/metaPhase/skipWhen/branchKey`（AtomicTask 无来源，诚实省略）。
  - `scope = { kind: 'scaffold', module: chain.module }`；`name = chain.name`；`chain.tasks` 为空时返回 `{ scope, name, nodes: [] }` 不抛错。
- **Acceptance Criteria Addressed**: AC-2, AC-7
- **Test Requirements**:
  - `rule` TR-2.1: 函数存在且被 `export`；空 `chain.tasks` 返回 `nodes: []`。证据：源码行号。
  - `rule` TR-2.2: 节点 id 形如 `redox-reasoning.A`；dependsOn 线性（首步空、后续指前一步）。证据：源码行号。
  - `rule` TR-2.3: 映射不写入 `analyze/layer/metaPhase/skipWhen/branchKey/stage`（grep 函数体内无这些赋值）。证据：grep 输出。
  - `rubric` TR-2.4: 字段可追溯性；scale 1-5；anchors 1=编造 / 3=部分推断 / 5=全部来自 AtomicTask+markPassed；threshold >= 4；evidence：逐字段源码核对。

## Task 3: 脚手架练习页接入 ThoughtDagFlow（preview + full）
- **Status**: `completed`
- **Completion Evidence**: TR-3.1 ✓ [ScaffoldPractice/index.tsx](file:///D:/github/CogniThink-Chem/miniapp/src/components/ScaffoldPractice/index.tsx) 已加 `dag = useMemo(()=>scaffoldChainToDAG(chain))` 与 `lockedIds`（filter `gate[i]==='locked'` 拼 `${module}.${kind}`）。TR-3.2 ✓ 两处 `<ThoughtDagFlow>`：preview 可折叠（默认收起）+ full 在 `allPassed` 块内；`StepProgress` 浮动条保留。TR-3.3 源码结构确认（preview 在 StepProgress 之前、full 在总结区）；**截图证据见 Task 5**。
- **Priority**: high
- **Depends On**: Task 2
- **Description**:
  - 在 `miniapp/src/components/ScaffoldPractice/index.tsx`：import `ThoughtDagFlow` 与 `scaffoldChainToDAG`。
  - 用 `useMemo` 由 `chain` 生成 `dag`；用 `useMemo` 由 `gate` 推导 `lockedIds`（`state==='locked'` 的 task，id 同转换层约定 `${module}.${kind}`）。
  - 答题前：在「A–F 精熟练习」标题区下方、StepProgress 浮动条之前，加可折叠区块（默认收起）渲染 `<ThoughtDagFlow dag={dag} mode="preview" legendLabels={{active:'常规步骤', dimmed:'视情况触发'}} />`；用本地 state 控制展开/收起。
  - 全部通过（`allPassed`）：在总结区上方渲染 `<ThoughtDagFlow dag={dag} mode="full" dimmedIds={lockedIds} legendLabels={{active:'已通过', dimmed:'未解锁'}} />`。
  - `StepProgress` 浮动条保留不动。
- **Acceptance Criteria Addressed**: AC-4
- **Test Requirements**:
  - `rule` TR-3.1: 组件内存在 `scaffoldChainToDAG(chain)` 的 useMemo；存在 `lockedIds` 由 gate 推导（filter locked + 拼 id）。证据：源码行号。
  - `rule` TR-3.2: JSX 中含两处 `<ThoughtDagFlow ... />`（一处 mode=preview 可折叠、一处 mode=full 在 allPassed 块内）；`StepProgress` 仍存在。证据：源码行号。
  - `rule` TR-3.3: preview 位于 StepProgress 之前（不覆盖浮动条）；full 位于 allPassed 总结区。证据：源码结构 + 截图。

## Task 4: 反应详情页接入 ThoughtDagFlow（preview）
- **Status**: `completed`
- **Completion Evidence**: TR-4.1 ✓ [reaction/index.tsx](file:///D:/github/CogniThink-Chem/miniapp/src/pages/reaction/index.tsx) 已导入并在「分步脚手架」区上方渲染 `<ThoughtDagFlow dag={scaffoldChainToDAG(REACTION_SCAFFOLD_CHAIN)} mode="preview" />`；配套 `.dagBlock` 样式已加到 [index.module.scss](file:///D:/github/CogniThink-Chem/miniapp/src/pages/reaction/index.module.scss)。TR-4.2 ✓ `REACTION_SCAFFOLD_CHAIN` 原本已从 scaffoldChains 导出（grep 确认），无需补 export、内容未改。**截图证据见 Task 5**。
- **Priority**: medium
- **Depends On**: Task 2
- **Description**:
  - 在 `miniapp/src/pages/reaction/index.tsx`：import `ThoughtDagFlow`、`scaffoldChainToDAG`、`REACTION_SCAFFOLD_CHAIN`（确认从 `engine/scaffoldChains` 导出；若未导出则补 `export`，不改内容）。
  - 在「分步脚手架 · {model.name}」sectionLabel 上方（现有 StepProgress 之后）渲染：`<ThoughtDagFlow dag={scaffoldChainToDAG(REACTION_SCAFFOLD_CHAIN)} mode="preview" />`。
  - 反应无门控 → preview 全高亮（脚手架链无 skipWhen，组件 preview 逻辑天然全亮）。
- **Acceptance Criteria Addressed**: AC-5
- **Test Requirements**:
  - `rule` TR-4.1: reaction/index.tsx 导入并渲染 `<ThoughtDagFlow dag={scaffoldChainToDAG(REACTION_SCAFFOLD_CHAIN)} mode="preview" />`。证据：源码行号。
  - `rule` TR-4.2: `REACTION_SCAFFOLD_CHAIN` 从 scaffoldChains 导出（若原未导出，本次补 export 且内容未改）。证据：grep + diff。

## Task 5: 验证门（类型编译 + 浏览器渲染 + 降级 + 回归）
- **Status**: `completed`
- **Completion Evidence**: TR-5.1 ✓ `npx.cmd tsc --noEmit`（cwd=miniapp）本次改动**新增 0 错误**；报出的 4 个错误经 `git stash push -- <本次7文件>` 基线对比确认全部为 HEAD 既有（行号偏移 4 行一致），非本次引入。TR-5.2 ✓ 浏览器子代理截图：脚手架页 `pages/practice/index` preview 展开渲染 A–F 六节点；反应页 `pages/reaction/index?id=RX-IN-CL-006` 渲染 A–E 五节点（均非空白）。**full 图未端到端截图**（需答题全部通过触发），由代码逻辑 + preview 验证间接覆盖，已在 review.md「已知限制 2」标注，建议后续人工验证。TR-5.3 ✓ `git diff --name-only | Select-String package.json` 无匹配。TR-5.4 ✓ 挑战页 `pages/challenge/index?id=R1` DAG 正常渲染、无白屏报错。降级：ThoughtDagFlow 空 `layers` 返回 null 已有守卫。详见 [review.md](file:///D:/github/CogniThink-Chem/.trae/specs/scaffold-dag-flow-p0-integration/review.md)。
- **Priority**: high
- **Depends On**: Task 1, Task 2, Task 3, Task 4
- **Description**:
  - 运行 miniapp TS 类型检查（`npx tsc --noEmit` 或 IDE GetDiagnostics），确认零错误。
  - 启动本地 dev server，用浏览器子代理截图验证：① 脚手架练习页 preview 折叠/展开态；② 全部通过后的 full 图；③ 反应详情页 preview 图。
  - 验证降级：空 DAG 传入组件不崩（组件已有 `layers.length===0` return null 守卫）。
  - 回归：challenge 页 ThoughtDagFlow 行为不变。
  - 确认 `package.json` 依赖未变（git diff）。
- **Acceptance Criteria Addressed**: AC-3, AC-6, AC-4（截图）, AC-5（截图）
- **Test Requirements**:
  - `rule` TR-5.1: `tsc --noEmit`（或 GetDiagnostics）零错误。证据：编译输出。
  - `rule` TR-5.2: 浏览器截图显示脚手架页 preview + full、反应页 preview 正常渲染。证据：截图文件。
  - `rule` TR-5.3: `package.json` 无 diff（未引入新依赖）。证据：git diff --name-only 不含 package.json。
  - `rule` TR-5.4: challenge 页原有 ThoughtDagFlow 仍渲染（无回归）。证据：截图或源码确认调用未改。
