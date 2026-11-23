# 题型差异化解题模型与讲解 UI - Implementation Plan

## Task 1: 扩展类型 + 建立题型模型注册表
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 在 `miniapp/src/types/chem.ts` 新增 `SolveModelKind` 联合类型与 `SolveData` 判别联合（`RedoxSolveData` / `IonicSolveData` / `EquilibriumSolveData`），并给 `Challenge` 增加可选字段 `solveData?: SolveData`。
  - 新建 `miniapp/src/engine/solveModels.ts`：定义 `SolveModel` 接口（category/name/icon/color/steps/panelKind/buildChecks），导出 `SOLVE_MODELS: Record<ChemCategory, SolveModel>`、`getSolveModel(challenge)`、`getPanelKind(challenge)`、`getModelChecks(challenge)`。
  - 6 类全覆盖：氧化还原 `redox-electron`、离子平衡 `ionic-conservation`、化学平衡 `equilibrium-shift` 三类有专属 panelKind 与专属标准步骤（≥3 步）；电化学/无机/有机为 `generic` 面板但给出专属步骤骨架。
- **Acceptance Criteria Addressed**: AC-1, AC-9（部分）, FR-1, FR-2, FR-11
- **Test Requirements**:
  - `rule` TR-1.1: 导入 `SOLVE_MODELS` 遍历，6 个 `ChemCategory` 键均存在且值非空；3 个代表类的 `panelKind` 不等于 `'generic'` 且 `steps.length >= 3`。Evidence: 源码 + GetDiagnostics。
  - `rule` TR-1.2: `Challenge` 新增 `solveData?` 可选字段，既有 84 道题无该字段时 TS 不报错、运行时读取安全（`challenge.solveData?.kind` 可空）。Evidence: GetDiagnostics 为空。
  - `rule` TR-1.3: `getPanelKind` 对无 solveData 的题返回该分类默认 panelKind；`getModelChecks` 返回非空字符串数组。Evidence: 源码。
- **Notes**: 先建类型与注册表，是后续组件与数据的基座。
- **Completion Evidence**: `types/chem.ts` 新增 `SolveModelKind`/`SolveData` 判别联合（Redox/Ionic/Equilibrium）与 `Challenge.solveData?` 可选字段；新建 `engine/solveModels.ts` 导出 `SOLVE_MODELS`（6 类全覆盖，3 代表类 panelKind 非 generic 且 steps≥3）、`getSolveModel`/`getPanelKind`/`getModelChecks`；`GetDiagnostics` 为空。TR-1.1/1.2/1.3 均通过。

## Task 2: 实现题型专属可视化面板组件
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 新建目录 `miniapp/src/components/SolvePanels/`：
    - `RedoxElectronPanel/index.tsx` + `.module.scss`：渲染半反应表（物种 / 起始价→终态价 / 得失 / 电子数）+ 电子守恒汇总卡（失电子总数、得电子总数、最小公倍数、系数比）。
    - `IonicConservationPanel/index.tsx` + `.module.scss`：渲染粒子来源清单 + 三大守恒式卡片（电荷/物料/质子）。
    - `EquilibriumShiftPanel/index.tsx` + `.module.scss`：渲染条件-移动推演表（因素 / 改变 / 移动方向 / K 变化 / 转化率）。
    - `GenericSolvePanel/index.tsx`：展示分类标准步骤与通用校验项（降级用）。
    - `index.tsx` 调度组件 `SolveModelPanel`：props `{ challenge: Challenge; mode: 'preview' | 'full' }`；preview 模式只渲染该分类 `steps`（不读 solveData 结论），full 模式按 `getPanelKind` + `challenge.solveData` 选择专属面板，无数据降级 Generic。
  - 各专属面板在 `solveData` 缺失对应字段时优雅降级（不崩）。
- **Acceptance Criteria Addressed**: AC-3, AC-4, AC-5, AC-7（降级部分）, FR-3, FR-4, FR-7, FR-8, FR-9, FR-10
- **Test Requirements**:
  - `rule` TR-2.1: `SolveModelPanel` 传入氧化还原题（带 redok solveData）且 mode='full' 时，渲染半反应表行数 ≥1、汇总卡字段（失/得电子总数、最小公倍数、系数比）非空。Evidence: 组件单测/渲染检查或预览截图。
  - `rule` TR-2.2: 传入离子平衡题（带 ionic solveData）mode='full' 时，渲染守恒式卡片 ≥1 且表达式非空。Evidence: 预览截图。
  - `rule` TR-2.3: 传入化学平衡题（带 equilibrium solveData）mode='full' 时，渲染推演表行数 ≥1 且方向/K 字段非空。Evidence: 预览截图。
  - `rule` TR-2.4: mode='preview' 时不渲染 solveData 的结论性数据，仅展示步骤框架；传入无 solveData 的题 mode='full' 时降级 Generic 面板且不抛错。Evidence: 预览操作。
  - `rubric` TR-2.5: 可视化与题型思维贴合度；scale 1-5；anchors 1=纯文本无差异 / 3=有结构但贴合一般 / 5=精准映射思维骨架；threshold >= 4；evidence 预览截图。
- **Notes**: 面板为纯展示组件，数据全部来自 props，便于复用。
- **Completion Evidence**: 新建 `components/SolvePanels/`：`StepsView`、`RedoxElectronPanel`（半反应表+电子守恒汇总）、`IonicConservationPanel`（粒子清单+守恒式卡片）、`EquilibriumShiftPanel`（条件-移动推演表）、`GenericSolvePanel`（降级）、调度器 `SolveModelPanel`（preview/full 双模式）；各 `.module.scss` 顶部均 `@use '@/styles/variables.scss' as *;`；GetDiagnostics 为空。TR-2.1~2.4 待预览截图验证，TR-2.5 待评审打分。

## Task 3: 为 3 个代表性题型录入结构化 solveData
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 在 `data/challenges/redox.ts`、`ionic.ts`、`equilibrium.ts` 中，为每类至少 4 道题（优先基础/进阶/综合各档，覆盖 R1-R 至少 4 题、I1-I 至少 4 题、E1-E 至少 4 题）新增 `solveData` 字段，内容与该题 `explanation`/`guide` 一致。
  - 氧化还原：`halfReactions`（物种、from、to、electrons、role）+ `balance`（oxidized/reduced 原子数、ratio）。
  - 离子平衡：`species`（formula、source）+ `equations`（type、expression）。
  - 化学平衡：`factors`（factor、change、shift、kChanges、conversion）。
  - 合计 ≥12 道题带 solveData。
- **Acceptance Criteria Addressed**: AC-6, FR-6, AC-3/4/5（数据前提）
- **Test Requirements**:
  - `rule` TR-3.1: 在 redox/ionic/equilibrium 三个文件中 grep `solveData`，每文件匹配题数 ≥4，合计 ≥12。Evidence: grep 计数输出。
  - `rule` TR-3.2: 抽查 R2（配平）的 solveData 电子数与 explanation 一致（Mn 得 5e⁻、Fe 失 1e⁻、最小公倍数 5、系数比 1:5）。Evidence: 源码核对。
  - `rule` TR-3.3: 所有新增 solveData 通过 TS 类型校验（无诊断错误）。Evidence: GetDiagnostics。
- **Notes**: 只加可选字段，不动既有 guide/explanation，零回归风险。
- **Completion Evidence**: grep 计数 redox=6 / ionic=4 / equilibrium=4，合计 14（每类 ≥4）；R2 抽查 Mn 得 5e⁻/Fe 失 5e⁻/lcm 5/ratio 1:5 与 explanation 一致；GetDiagnostics 为空。TR-3.1/3.2/3.3 均通过。

## Task 4: 接入题目页（challenge）讲解区
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 2, Task 3
- **Description**:
  - 修改 `pages/challenge/index.tsx`：
    - 题干卡片下方、选项上方，新增可折叠「🧩 这类题怎么想」，内部 `<SolveModelPanel challenge={challenge} mode="preview" />`（答题前可见，不泄答案）。
    - 答题结果区（resultBox）之后，新增「完整解题模型」区块，渲染 `<SolveModelPanel challenge={challenge} mode="full" />`（仅 `result !== 'pending'` 时显示）。
    - 守恒校验区块保留；可将 `getModelChecks` 并入校验展示（可选，不破坏现有）。
  - 新增对应样式到 `pages/challenge/index.module.scss`（面板容器、折叠头）。
- **Acceptance Criteria Addressed**: AC-2, AC-7, FR-5
- **Test Requirements**:
  - `rule` TR-4.1: 打开任意代表题，题干下方存在「这类题怎么想」折叠面板且展开后仅显示步骤框架（无答案结论）。Evidence: 预览截图。
  - `rule` TR-4.2: 提交答案后，结果区下方出现「完整解题模型」并渲染对应专属面板。Evidence: 预览截图。
  - `rule` TR-4.3: 打开一道无 solveData 的题并答题，面板降级通用、不报错，原有提交/诊断/6 步引导仍正常。Evidence: 预览操作。
- **Notes**: 不改动选项与提交流程，只新增展示区块。
- **Completion Evidence**: `pages/challenge/index.tsx` 已接入：import `getModelChecks`(L12) + `SolveModelPanel`(L14)；state `showModel`(L67)；题干下折叠「🧩 这类题怎么想」→ `<SolveModelPanel mode="preview" />`(L464-469)；结果区后「📐 完整解题模型」→ `<SolveModelPanel mode="full" />`(L515-519，仅 `result!=='pending'` 显示)；守恒校验区并入 `getModelChecks`(L595)；`index.module.scss` 追加 `.modelBox*`/`.solveModelFull*`/`.conservationSubHead` 样式；`GetDiagnostics` 返回 `[]`；`pai.log` 末尾「✅ 编译成功」。TR-4.1/4.2 待预览截图验证，TR-4.3 待无 solveData 题降级验证。

## Task 5: 构建验证 + 独立评审
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 4
- **Description**:
  - 运行 `GetDiagnostics` 确认零 TS 错误。
  - 保存后确认预览 watch 编译成功（`pai.log` 末尾「✅ 编译成功」）；必要时重启预览服务（不删 lock）。
  - 用浏览器代理打开预览，逐题验证 AC-2~AC-7，截图留证。
  - 进入 Review 阶段，生成 `review.md`，委托独立只读评审。
- **Acceptance Criteria Addressed**: AC-8, AC-9, NFR-1~NFR-5
- **Test Requirements**:
  - `rule` TR-5.1: `GetDiagnostics` 返回空数组。Evidence: 工具输出。
  - `rule` TR-5.2: `pai.log` 末尾出现「✅ 编译成功」且无 `compile_error`。Evidence: 日志 tail。
  - `rubric` TR-5.3: 独立评审对 AC-8（差异化清晰度贴合度）打分，scale 1-5，threshold >= 4，附理由与截图。Evidence: review.md。
  - `rule` TR-5.4: 独立评审确认 AC-1~AC-7 全部有通过证据、无未解决 actionable finding。Evidence: review.md pass。
- **Notes**: 评审为最终验收门禁；不通过则回到 Implement 生成修复 issue。
- **Completion Evidence**: `GetDiagnostics` 返回 `[]`（TR-5.1）；`pai.log` 末尾「✅ 编译成功」无 `compile_error`（TR-5.2）；直连预览源站采集 5 张运行时截图（固化于 `.trae/specs/differentiated-solve-models/evidence/`）逐一验证 AC-2~AC-7：R2 preview 仅步骤框架不泄答案 / R2 半反应表+电子守恒汇总四字段非空 / I3 粒子清单+三大守恒式卡片 / E1 平衡移动推演表方向与 K 非空 / R7 无 solveData 降级通用不报错。独立评审见 `review.md`：7 个 rule 检查点全 pass、rubric AC-8 得分 5（≥4 阈值）、无 actionable finding，Review R1 结果 `pass`（TR-5.3/TR-5.4 通过）。临时调试代码已全部移除，grep 确认零残留，还原后编译成功。
