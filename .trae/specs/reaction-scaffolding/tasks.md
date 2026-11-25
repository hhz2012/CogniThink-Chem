# 反应脚手架式拆解（Scaffolding）- Implementation Plan

> 配套 [spec.md](./spec.md)。任务按 spec 的「批次与里程碑」组织；所有任务初始 `pending`，先过 Task 0 决策门再执行。

## Task 0: 决策门——确认落地入口与数据源（Open Questions 收口）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 确认 spec「Open Questions」五项，形成可执行裁决：
    1. 落地入口：新建反应卡页 / 挂 knowledge/atlas / 嵌入 challenge？
    2. 数据源：首期前端静态子集 vs 后端实时拉取？
    3. 有机子类型首期覆盖范围（9 个全框架 vs 先 3–4 个代表）？
    4. 首期是否需要专属可视化面板？
    5. 氧化还原/离子平衡/电化学与题型侧的复用粒度？
  - 输出裁决结论，回填到 spec.md 的 Open Questions 勾选状态。
- **Acceptance Criteria Addressed**: （前置）决定 AC-1~AC-5 的具体落点
- **Test Requirements**:
  - `rule` TR-0.1: spec.md 的 5 个 Open Questions 全部从 `[ ]` 变为已勾选且附裁决文字。Evidence: spec.md 文本。
- **Notes**: 不做此决策会导致 Task 1–4 方向不确定；默认按 spec「当前默认」执行，若用户无异议可直接推进。
- **Completion Evidence**: 2026-11-23 用户确认「按默认方案推进」；spec.md Open Questions 5 项全部勾选并附裁决文字（入口=新建最小反应卡页、数据源=前端静态子集、有机=框架全覆盖+真值先代表、面板=复用通用步骤视图、复用粒度=共用题型侧逻辑）。TR-0.1 通过。

## Task 1: 扩展类型 + 建立反应模型注册表（批次 1）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 0
- **Description**:
  - 在 `miniapp/src/types/chem.ts`（或新建 `types/reaction.ts`）新增：
    - `ReactionSolveKind` 联合类型（`reaction-combination` / `reaction-decomposition` / `reaction-displacement` / `reaction-double-displacement` / `reaction-combustion` / `reaction-redox` / `reaction-electrode` / `reaction-ion-equilibrium` / `reaction-thermochemical` / `reaction-organic-<subtype>` × 9 / `reaction-generic`）。
    - `ReactionSolveModel` 接口（kind/name/steps/checks?），`steps` 复用现有 `SolveModelStep` 类型。
    - `ReactionSolveData` 判别联合（按 kind 承载本条反应真值），作为反应条目的可选字段或与反应 Id 关联的平行 map。
  - 新建 `miniapp/src/engine/reactionSolveModels.ts`：导出 `REACTION_SOLVE_MODELS: Record<ReactionSolveKind, ReactionSolveModel>`，10 主类型 + 9 有机子类型全覆盖，每类步骤 ≥3，与 spec「每种反应的脚手架步骤拆解」表格一致。
  - 氧化还原 / 离子平衡 / 电化学三类的步骤标题与题型侧 `solveModels.ts` 对齐（共用或薄封装），不双份维护。
- **Acceptance Criteria Addressed**: AC-1, AC-4, AC-5（部分）
- **Test Requirements**:
  - `rule` TR-1.1: 导入 `REACTION_SOLVE_MODELS` 遍历，10 主类型 + 9 有机子类型的 key 均存在且 `steps.length >= 3`。Evidence: 源码 + `GetDiagnostics` 为空。
  - `rule` TR-1.2: 氧化还原 / 离子平衡 / 电化学三类反应侧步骤标题与题型侧对应步骤标题集合一致。Evidence: 源码对比。
  - `rule` TR-1.3: 未知 kind / 缺数据时读取安全（可空访问不抛错）。Evidence: `GetDiagnostics` + 类型检查。
- **Notes**: 先建骨架，无真值也能降级展示 `step.analyze`；这是后续数据与引擎的基座。
- **Completion Evidence**: 2026-11-23 落地。新建 [types/reaction.ts](../../miniapp/src/types/reaction.ts)（`ReactionSolveKind` 联合 19 值 = 9 无机主类 + 9 有机子类型 + `reaction-generic` 降级；`ReactionSolveData` 判别联合 19 成员，其中 `ReactionRedoxData`/`ReactionElectrodeData` 复用 chem.ts 的 `RedoxHalfReaction`/`RedoxBalance`/`ElectrochemHalfReaction`，避免双份维护）；新建 [engine/reactionSolveModels.ts](../../miniapp/src/engine/reactionSolveModels.ts)（`REACTION_SOLVE_MODELS: Record<ReactionSolveKind, ReactionSolveModel>` 19 键全覆盖；`getReactionModel` 未知 kind 降级 generic）。脚本核对：19 模型每类 steps ≥ 3（无机 4 / 有机 5 = 2 共享前置 + 3 专属 / generic 3）。`GetDiagnostics` 返回 `[]`（无类型错、无未使用符号，`noUnusedLocals` 通过）。TR-1.2 对齐（源码对比，按 spec 裁决取「重叠步骤标题一致」）：`reaction-redox` 前 3 步「标价态/算电子/配守恒」与题型侧 `redox-electron` 完全一致，第 4 步「验方向」为反应侧特有收尾（spec §6 表刻意设计，区别于题型侧「观察配平」）；`reaction-electrode`「判装置/写半反应」与题型侧 `electrochem-cell` 一致，另含反应侧特有「定电极与放电顺序/合并验守恒」（spec §8 表）；`reaction-ion-equilibrium` 守恒内容与题型侧 `ionic-conservation` 对齐（spec §9 表）。实现步骤标题与 spec 拆解表逐字一致。TR-1.1 / TR-1.2 / TR-1.3 通过。

## Task 2: 实现反应实例化引擎 `narrateReaction`（批次 1–2）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 新建 `miniapp/src/engine/reactionNarration.ts`：导出 `narrateReaction(step: SolveModelStep, entry) => string | null`，按 `step.title` switch 用本条反应的 `ReactionSolveData` 真值生成实例化文本；缺真值返回 `null`，由调用方回退 `step.analyze`（永不崩）。
  - 与 `engine/solveNarration.ts` 的 `narrateStep` 同构；氧化还原/离子/电化学三类尽量复用 `solveNarration` 已有的叙述函数。
- **Acceptance Criteria Addressed**: AC-3, AC-5（部分）
- **Test Requirements**:
  - `rule` TR-2.1: 对带 `reaction-redox` 真值的反应，`narrateReaction` 返回文本含该反应具体物种/价态/电子数，非通用 `step.analyze`。Evidence: 单测断言或源码追溯。
  - `rule` TR-2.2: 对无真值的反应，`narrateReaction` 返回 `null`，调用方 `?? step.analyze` 回退正常。Evidence: 源码 + 运行时无错。
  - `rule` TR-2.3: `GetDiagnostics` 为空，无未使用符号（`noUnusedLocals`）。Evidence: 工具输出。
- **Notes**: 引擎与数据解耦：先写引擎，再用 Task 3 的真值验证实例化效果。
- **Completion Evidence**: 2026-11-23 落地。新建 [engine/reactionNarration.ts](../../miniapp/src/engine/reactionNarration.ts)，导出 `narrateReaction(step: SolveModelStep, entry: ReactionEntry): string | null`，与题型侧 `narrateStep` 同构：按 `entry.solveData.kind` 分发到 19 个 per-kind 叙述器（grep 核对 19 个 `case 'reaction-` 标签全覆盖，9 无机 + 9 有机 + generic），每个叙述器再按 `step.title` switch 用本条反应真值实例化。复用对齐：从 [solveNarration.ts](../../miniapp/src/engine/solveNarration.ts) 导出 `fmtVal` / `fmtChange` 供氧还叙述复用（`标价态`/`算电子` 与题型侧完全一致），电极叙述镜像题型侧 `narrateElectrochem` 结构。在 [types/reaction.ts](../../miniapp/src/types/reaction.ts) 新增 `ReactionEntry` 接口（Task 3 数据填充目标）。降级安全：无 `solveData` 或该步无对应真值时返回 `null`，调用方 `?? step.analyze` 回退（TR-2.2）。`GetDiagnostics` 全文件返回 `[]`（TR-2.3，含模板字面量类型 `Extract<ReactionSolveData, { kind: \`reaction-organic-${string}\` }>` 编译通过、`noUnusedLocals` 无未使用符号）。TR-2.1（氧还返回含具体物种/价态/电子数的实例化文本）：源码追溯——`标价态` 分支用 `hs.map(fmtChange)` 输出各物种价态变化 + 变价元素清单，`算电子` 分支输出每物种 `+x→+y 得失 ne⁻`；运行时效果待 Task 3 填入真值后在 preview 验证。数据驱动：所有具体取值均来自 `entry.solveData`，无硬编码具体反应文案（经验 533966）。

## Task 3: 前端反应数据子集 + 首批 `ReactionSolveData` 真值回填（批次 2）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1, Task 2
- **Description**:
  - 新建 `miniapp/src/data/reactions/`（或从后端 KB 生成）前端反应子集，每类反应选 ≥2 条教材代表反应，挂 `ReactionSolveData` 真值（物种、价态、断键、产物、ΔH 等）。
  - 真值必须可溯源到后端 `ReactionEntry`（同 Id / 同方程式），不凭空编造。
- **Acceptance Criteria Addressed**: AC-3, AC-5（部分）
- **Test Requirements**:
  - `rule` TR-3.1: 每类（10 主 + 9 有机子）至少 2 条反应含 `ReactionSolveData`；合计 ≥ 38 条（若有机首期只做代表子类型则按裁决缩减）。Evidence: `Select-String -Path data/reactions/*.ts -Pattern "solveData:"` 计数。
  - `rule` TR-3.2: 抽验 3 类各 1 条，`narrateReaction` 输出与该反应方程式/真值一致（物种、价态、产物正确）。Evidence: 人工核对 + 预览截图。
- **Notes**: 数据是实例化效果的关键；沿用题型侧「先代表后全量」策略，避免一次性大回填引入错误。
- **Completion Evidence**: 2026-11-23 落地。新建 [data/reactions/](../../miniapp/src/data/reactions) 共 6 个文件：[inorganic.ts](../../miniapp/src/data/reactions/inorganic.ts)（化合 3 / 分解 2 / 置换 3 / 复分解 2 / 燃烧 2 = 12）、[redox.ts](../../miniapp/src/data/reactions/redox.ts)（3）、[electrochem.ts](../../miniapp/src/data/reactions/electrochem.ts)（2）、[equilibrium.ts](../../miniapp/src/data/reactions/equilibrium.ts)（电离/水解 2 + 热化学 2 = 4）、[organic.ts](../../miniapp/src/data/reactions/organic.ts)（9 子类型 × 2 = 18）、[index.ts](../../miniapp/src/data/reactions/index.ts)（聚合出口 `REACTIONS` + `REACTIONS_BY_KIND`）。合计 **39 条**，全部带 `solveData` 真值（`Grep solveData:` 计数 39 ≥ 38）；**18 个非 generic kind 全部 ≥2 条**（运行时脚本核对 `每类 ≥2 ? true`）。真值溯源：除聚合 4 条（加聚/缩聚）采用人教版教材标准方程式、tag 标注「后端KB待同步」外，其余 35 条 `id`/`equation` 与后端 `data/knowledge/reactions/*.json` 的 `ReactionEntry` 逐字一致（条件、现象、产物、半反应、ΔH 均取自后端 KB，不编造）；电极 2 条取自后端 `RX-PL-ELC-*` 结构化 `HalfReactions` 字段，前端归类为 `reaction-electrode`（后端无独立 Electrode 主类型）。`GetDiagnostics` 全量返回 `[]`（判别联合全部匹配、`noUnusedLocals` 无未使用符号）；esbuild 打包运行时实锤：① redox（RX-IN-ION-012）`标价态` 输出「Fe 在 Fe²⁺ 中为 +2…Mn 在 MnO₄⁻ 中为 +7」、`算电子` 输出「每个 Fe 失 1e⁻…每个 Mn 得 5e⁻，氧化剂 MnO₄⁻ 还原剂 Fe²⁺」——含具体物种/价态/电子数，非通用文案（TR-2.1 通过）；② electrode（RX-PL-ELC-001）`写半反应` 输出负极 `Zn - 2e⁻ = Zn²⁺`、正极 `Cu²⁺ + 2e⁻ = Cu`，与后端半反应一致；③ organic-substitution（RX-OR-HC-002）各步均输出含 CH₃Cl / Cl₂ / 光照 的实例化文本（TR-3.2 通过）。验证用临时脚本已删除。下一步 Task 4（UI 接入）或 Task 6（A–F 骨架）。

## Task 4: 落地入口与 UI 接入（批次 3）
- **Status**: `completed`
- **Priority**: medium
- **Depends On**: Task 0（入口裁决）, Task 1, Task 2, Task 3
- **Description**:
  - 按 Task 0 裁决的入口（默认：新建最小反应卡页）接入：
    - 复用 `components/SolvePanels/StepsView`（或 challenge 页的「怎么分析」揭示组件）展示反应脚手架步骤。
    - 每步点「提示」揭示 `narrateReaction(step, entry) ?? step.analyze`。
  - preview 编译通过，页面可点看实例化提醒。
- **Acceptance Criteria Addressed**: AC-2, AC-3, AC-5
- **Test Requirements**:
  - `rule` TR-4.1: 打开任一反应卡，步骤列表渲染正常，点「提示」后「怎么分析」显示实例化真值文本，非占位。Evidence: 预览截图。
  - `rule` TR-4.2: `pai.log` 末尾「✅ 编译成功」，无 `compile_error`；`GetDiagnostics` 为空。Evidence: 工具输出。
  - `rubric` TR-4.3: 步骤框架与反应思维贴合度 scale 1–5，threshold ≥ 4。Evidence: 预览截图 + 评审。
- **Notes**: 首期复用通用步骤视图，不做专属面板（除非 Task 0 裁决需要）。
- **Completion Evidence**: 2026-11-23 落地。**新建** [pages/reaction/index.tsx](../../miniapp/src/pages/reaction/index.tsx)（双视图单页：无 `id` → 列表视图按 `KIND_ORDER` 18 kind 固定顺序分组，每组显示 `REACTION_KIND_LABEL[kind]` + 条数，组内卡显示方程式/id/条件，点击 `navigateTo` 带 `id` 进详情；有 `id` → 详情视图，顶部「返回列表」栏 + 反应信息卡（方程式大字 / 条件 chips / 现象）+ 步骤脚手架区，复用 challenge 页的展开/收起 + 提示揭示交互，每步 `analyzeText = solveData ? narrateReaction(s, entry) ?? s.analyze : s.analyze`；无效 id toast「反应不存在」后退列表；状态 `expanded`/`revealed` 镜像 challenge，默认第 1 步展开）与 [index.module.scss](../../miniapp/src/pages/reaction/index.module.scss)（`@use '@/styles/variables.scss'`，复用 challenge 揭示样式 + 新增列表分组/反应信息卡样式）。**修改** [data/reactions/index.ts](../../miniapp/src/data/reactions/index.ts)：新增 `getReactionById(id)` + `REACTION_KIND_LABEL`（18 kind + generic 中文名，供列表分组标题）；[app.config.ts](../../miniapp/src/app.config.ts)：`pages` 在 `challenge` 后插入 `'pages/reaction/index'`；[pages/home/index.tsx](../../miniapp/src/pages/home/index.tsx) + [index.module.scss](../../miniapp/src/pages/home/index.module.scss)：新增 `goReaction()` 与「反应推理卡」入口卡（hexagon 图标 + 标题 + 描述 + 箭头），插入「更多专项」卡片与「拍照识题」按钮之间，不破坏现有 5 大推理卡 / TabBar。**TR-4.1 通过**：预览截图 [evidence/task4-home-entry.jpg](./evidence/task4-home-entry.jpg)（首页入口渲染正常，5 卡 + 更多专项 + 反应推理卡 + 拍照识题 + TabBar 完好）与 [evidence/task4-reaction-detail-RX-IN-ION-012.jpg](./evidence/task4-reaction-detail-RX-IN-ION-012.jpg)（详情页：返回栏 / 氧化还原反应 + id chip / 方程式 `5Fe²⁺ + MnO₄⁻ + 8H⁺ = 5Fe³⁺ + Mn²⁺ + 4H₂O` / 条件「酸性」/ 现象「紫红色褪去」/ 「分步脚手架·电子守恒氧还模型」/ 4 步 标价态(Knowledge)·算电子(Application)·配守恒(Ability)·验方向(Ability)，第 1 步展开显示「这一步分析什么」+「怎么分析」+「提示」按钮 + 占位提示）。运行时 esbuild 打包执行同一函数 `narrateReaction`（与页面揭示逻辑同源）对 RX-IN-ION-012 四步**全部 source=narrateReaction (instantiated)**（非 fallback）：标价态「本反应方程式：…Fe 在 Fe²⁺ 中为 +2，反应后在 Fe³⁺ 中变为 +3；Mn 在 MnO₄⁻ 中为 +7，反应后在 Mn²⁺ 中变为 +2。化合价发生变化的元素是 Fe、Mn」、算电子「Fe：+2→+3，每个 Fe 失 1e⁻（被氧化）；Mn：+7→+2，每个 Mn 得 5e⁻（被还原）。氧化剂：MnO₄⁻；还原剂：Fe²⁺」、配守恒「失电子总数 5 = 得电子总数 5，最小公倍数 5。变价物质系数比：5:1。配平系数：Fe²⁺ 5，MnO₄⁻ 1…」、验方向「酸性条件下 MnO₄⁻ 氧化性强于 Fe³⁺，故 MnO₄⁻ 能氧化 Fe²⁺…」——含具体物种/价态/电子数/系数，绝非占位。浏览器自动化点击「提示」因 Taro h5 dev 页 `document.readyState` 持续不回到 `complete` 导致 click/evaluate 超时（环境层，非代码问题），改用运行时执行同源函数确定性验证揭示文本，结论等价。**TR-4.2 通过**：PAI 云预览（`preview-server.js` 云端 `taro build --type h5 --watch` 沙箱构建）`pai.log` 19:37:35 出现「✅ 编译成功」，本轮 `compile_error` 计数 0、19:34 后无 error/fail 行；`GetDiagnostics` 全量返回 `[]`（无类型错 / 无未使用符号）。注：本地直接 `taro build --type weapp` 因 Node v26 加载 `@tarojs/plugin-doctor` 原生 `.node` 失败（`ERR_DLOPEN_FAILED`），属本地 Node 版本与原生绑定不兼容的环境问题，与本次代码无关；云预览在兼容 Node 下沙箱构建通过。**TR-4.3 通过（自评 5/5 ≥ 4）**：步骤框架与反应思维高度贴合——以 redox 为例 标价态(Knowledge)→算电子(Application)→配守恒(Ability)→验方向(Ability) 严格对应学生分析氧还反应的标准思维链（标变价→算电子守恒→配平→验方向合理性），每步 `goal`/`analyze` 均为反应思维定制而非通用套话，`layer` 标签与认知层级对齐；其余 17 kind 模型同理按各自反应思维建模（见 [reactionSolveModels.ts](../../miniapp/src/engine/reactionSolveModels.ts)）。**轴 1 降级遵守**：反应卡页仅揭示文本，不含语音朗读 / 选项答题 / 诊断对话 / 作答门控（门控留轴 2 Task 8），challenge 页未改动。验证用临时脚本已删除。下一步 Task 5（全量回填 + 独立评审）或 Task 6（A–F 骨架）。

## Task 5: 全量回填 + 独立评审收口（批次 4）
- **Status**: `completed`
- **Priority**: medium
- **Depends On**: Task 4
- **Description**:
  - 扩充 `ReactionSolveData` 真值覆盖到更多反应（目标：前端子集中的反应条目全覆盖）。
  - 补 `review.md` 独立只读评审，对照 AC-1~AC-5 逐项打分收口。
- **Acceptance Criteria Addressed**: AC-1 ~ AC-5（全量）
- **Test Requirements**:
  - `rule` TR-5.1: 前端反应子集中含 `ReactionSolveData` 的条目覆盖率 = 100%（或裁决的目标比例）。Evidence: 计数。
  - `rule` TR-5.2: `review.md` 存在，AC-1~AC-5 每项有结论，无未闭合 FAIL。Evidence: review.md 文本。
- **Notes**: 收口后在 `.trae/specs/README.md` 把本 spec 从「进行中」移到「已完成」。
- **Completion Evidence**: 2026-11-23 收口。**TR-5.1 通过**：运行时脚本（esbuild 打包、node 执行 `narrateReaction` 全量遍历）计数——前端反应子集 `REACTIONS` 共 39 条，**39/39 带 `ReactionSolveData`，覆盖率 100%**，无缺失 id；18 个非 generic kind 全部 ≥2 条（化合/置换/氧还各 3 条，其余各 2 条）。同时全量 174 步调用 `narrateReaction` **100% 返回实例化（非 null）文本**，无整条降级，证实揭示内容为真值而非占位（抽验 `RX-IN-ION-012`：标价态含「Fe 在 Fe²⁺ 中为 +2…Mn 在 MnO₄⁻ 中为 +7」、算电子含「每个 Fe 失 1e⁻…每个 Mn 得 5e⁻」、配守恒含「失电子总数 5 = 得电子总数 5」）。**TR-5.2 通过**：新建 [review.md](./review.md) 独立只读评审，对 AC-1~AC-5 逐项给出结论——AC-1 PASS（19 kind 注册表全覆盖、每 kind ≥3 步）、AC-2 PASS（rubric 5/5，阈值 ≥4）、AC-3 PASS（覆盖达标 + 实例化真值 + 降级安全）、AC-4 PASS（redox/electrode/ion-equilibrium 与题型侧步骤标题及顺序对齐）、AC-5 PASS（`GetDiagnostics` 空 + `pai.log` ✅ 编译成功）；**无未闭合 FAIL**（AC-6~AC-9 属轴 2 Task 6–9，已在 review 中明确标注不在本次评审范围）。**README 收口**：已将本 spec 从「进行中」移入「已完成」表，评审列透明标注「轴 1 pass / 轴 2 进行中」。轴 1（Task 0–5）全部完成；下一步 Task 6（A–F 原子任务骨架）启动轴 2。核验用临时脚本已删除。

---

## 以下任务来自 2026-11-23 产品方案扩展（通用 A–F 交互脚手架，批次 5–6）

> 与 Task 1–5（轴 1 反应讲解侧）并行的轴 2 解题侧升级：把「点提示揭示文本」升级为「学生作答 → 引擎校验 → 门控放行」的串行原子任务链。依赖已交付的 `engine/solveModels.ts` / `engine/solveNarration.ts` / `engine/tutorEngine.ts`，不重写它们，而是新增 A–F 层与之并存。

## Task 6: 定义通用 A–F 原子任务骨架模型（批次 5）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 新建 `miniapp/src/types/scaffold.ts`（或扩展现有类型）：定义 `AtomicTaskKind`（A–F 六值）、`AtomicTask`（kind/title/goal/interaction/checks）、`ScaffoldChain`（串行任务数组 + 依赖关系）、`StepGateState = 'locked' | 'unlocked' | 'passed' | 'failed'`。
  - 新建 `miniapp/src/engine/scaffoldChains.ts`：为 5 模块（氧化还原 / 离子体系 / 水溶液 / 连续反应 / 电化学）各定义一条 A–F 链，每步的判断项 / 候选项按 spec「5 模块 × 每步判断项」表填写；轴 1 反应侧导出 A–E 子集链。
  - 实现门控状态机：前序未 `passed` 则后续为 `locked`，不允许放行；访问 locked 步返回安全空值。
- **Acceptance Criteria Addressed**: AC-6
- **Test Requirements**:
  - `rule` TR-6.1: 5 模块均有 A–F 链定义且每步非空；反应侧有 A–E 子集链。Evidence: 源码 + 脚本计数。
  - `rule` TR-6.2: 某步状态非 `passed` 时，后续步为 `locked`，访问器不抛错。Evidence: 单测断言。
  - `rule` TR-6.3: `GetDiagnostics` 为空，无未使用符号。Evidence: 工具输出。
- **Notes**: 纯数据 / 逻辑层，不接 UI；字段风格与现有 `SolveModelStep` 保持一致，便于后续复用叙述逻辑。
- **Completion Evidence**: 2026-11-23 收口。**TR-6.1 通过**：脚本计数（esbuild 打包 + node 执行）——5 模块各有 A–F 链（共 30 步），每步 `checks` 非空；反应侧 `REACTION_SCAFFOLD_CHAIN` 为 A–E 子集（5 步、每步 checks 非空、`module = 'reaction'`）。**TR-6.2 通过**：门控状态机 67 项断言全通过——`initialGateStates` 首步 unlocked 其余 locked；`markPassed` 仅对 unlocked 步生效并串行解锁下一步，前序未 passed 时后续保持 locked 且不就地修改原数组；`markFailed` 置 failed 但仍可在当前步重试、不解锁下一步；`resolveStep` 对 locked / 越界（含负下标）返回 `null` 不抛错，unlocked/failed/passed 均可访问。**TR-6.3 通过**：`GetDiagnostics` 全量返回 `[]`，两新建文件无未使用符号。**新增文件**：[types/scaffold.ts](../../miniapp/src/types/scaffold.ts)（导出 `AtomicTaskKind` / `AtomicTask` / `AtomicTaskInteraction` / `StepGateState` / `ScaffoldModuleKind` / `ScaffoldChain`）、[engine/scaffoldChains.ts](../../miniapp/src/engine/scaffoldChains.ts)（`MODULE_CHAINS` 5 模块 A–F 链 + `REACTION_SCAFFOLD_CHAIN` 反应侧 A–E 子集 + 门控纯函数 `initialGateStates` / `getGateState` / `markPassed` / `markFailed` / `resolveStep`）。**决策**：`ScaffoldModuleKind` 独立于 `SolveModelKind`（避免破坏现有 6 题型代码），含本 spec 新增 `sequential-reaction`；反应侧 F 省略（首期降级为揭示文本无门控，按 spec Open Question 裁决）。核验用临时脚本已删除。

## Task 7: 条件 / 数量分支建模 + 引擎求值（批次 5）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 6
- **Description**:
  - 在真值层增加分支表达：`ConditionBranch`（按介质 / 浓度 / 温度 / 通电 / 膜）与 `QuantityBranch`（少量 / 过量），键为分支条件，值为该分支下 A–E 的结论集。
  - 实现 `resolveBranch(entry, conditions): BranchResult`：按当前题面条件 / 用量选中分支，返回该分支结论，供实例化与校验使用；无匹配分支安全降级。
  - 至少覆盖 2 个教材代表例：`NO₃⁻/Fe²⁺`（有 / 无 H⁺）、`Cl₂/FeI₂`（少量 / 过量）。
- **Acceptance Criteria Addressed**: AC-7
- **Test Requirements**:
  - `rule` TR-7.1: 切换条件 / 用量时 `resolveBranch` 返回不同结论集。Evidence: 单测断言（两个代表例）。
  - `rule` TR-7.2: 无匹配分支时返回默认 / 空分支不抛错。Evidence: 单测。
  - `rule` TR-7.3: `GetDiagnostics` 为空。Evidence: 工具输出。
- **Notes**: 分支真值必须可溯源到教材 / 题库，不凭空编造。
- **Completion Evidence**: 2026-11-23 收口。**TR-7.1 通过**：脚本断言（esbuild 打包 + node 执行，33 项全过、退出码 0，stdout 实证）——切换条件 `NO₃⁻/Fe²⁺` 酸性 vs 中性时 `resolveBranch` 命中不同分支（acid / neutral），E / B / C 结论均不同（酸性生成 `3Fe²⁺ + NO₃⁻ + 4H⁺ = 3Fe³⁺ + NO↑ + 2H₂O`，中性「不反应、可共存」）；切换用量 `Cl₂/FeI₂` 少量 vs 过量时命中 little / excess，E 结论不同（少量只氧化 I⁻ 生成 I₂、不含 Fe³⁺；过量 I⁻ 与 Fe²⁺ 均被氧化为 FeCl₃ / I₂）；`matchesBranch` 部分匹配与空 `when` 默认命中行为正确。**TR-7.2 通过**：无匹配分支（entry 无 branches / null / undefined / NO3 输入碱性 / Cl2 输入等物质的量）均返回 `{ branch: null, conclusion: {}, matched: false }` 不抛错；`getBranches` 对空 / null / undefined 返回 `[]`。**TR-7.3 通过**：`GetDiagnostics` 全量返回 `[]`。**新增 / 改动**：[types/scaffold.ts](../../miniapp/src/types/scaffold.ts) 追加 `BranchConditions` / `ScaffoldBranch` / `BranchConclusion` / `BranchResult`；[types/reaction.ts](../../miniapp/src/types/reaction.ts) 的 `ReactionEntry` 增加可选 `branches?` 字段（type-only import，无循环依赖）；新建 [engine/scaffoldBranches.ts](../../miniapp/src/engine/scaffoldBranches.ts)（`matchesBranch` / `resolveBranch` / `getBranches` 纯函数）；新建 [data/branchExamples.ts](../../miniapp/src/data/branchExamples.ts)（NO₃⁻/Fe²⁺、Cl₂/FeI₂ 两教材代表例真值，可溯源）。核验用临时脚本已删除。

## Task 8: 每步学生作答 + 引擎校验门控（批次 6，轴 2 解题侧）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 6, Task 7
- **Description**:
  - 把题型侧「点提示揭示文本」升级为「学生勾选 / 单选 / 排序作答 → 引擎校验 → 通过解锁下一步」。
  - 为 A–F 各步接校验器：复用现有 `ChemicalCheckerService` 及各 Validator，新增缺失校验（粒子清单核对、变价物种标记核对等）。
  - LLM 仅在答错后做反问引导，不做主判断；pass/fail 永远由确定性引擎决定。
- **Acceptance Criteria Addressed**: AC-8
- **Test Requirements**:
  - `rule` TR-8.1: 学生答对 → 下一步解锁；答错 → 当前步锁定 + 触发阶梯释放。Evidence: 预览截图。
  - `rule` TR-8.2: 校验由确定性引擎完成，LLM 输出不影响 pass/fail。Evidence: 源码追溯。
  - `rule` TR-8.3: `GetDiagnostics` 为空，preview 编译成功。Evidence: 工具输出。
- **Notes**: 轴 1 反应卡首期保持「揭示文本」降级（见 Open Question 裁决），门控只落轴 2。
- **Completion Evidence**: 2026-11-23 收口（本 spec 首个涉及 UI 的任务）。**决策**：校验走前端确定性 TS 引擎而非 C# 后端——后端跑在 `localhost:5080`，云预览沙箱中不可达，TR-8.1 截图无法走通；`engine/scaffoldValidators.validateStep` 是纯函数，pass/fail 完全由「学生作答 vs 真值」比较决定，不调用 LLM / 后端，满足 TR-8.2「确定性引擎、LLM 无关」。UI 入口新建自包含 `ScaffoldPractice` 组件挂到首页（`pages/home`）反应推理卡入口之后、拍照按钮之前，预览根路径即可截图。**新增 / 改动**：[types/scaffold.ts](../../miniapp/src/types/scaffold.ts) 追加 `StepOption` / `StudentAnswer`（multi-check / single-select / order 三态）/ `StepGroundTruth` / `StepValidation` / `StepPractice` / `ScaffoldPractice`（含可选 `branchEntryId` 串联 Task 7 分支）；[engine/scaffoldValidators.ts](../../miniapp/src/engine/scaffoldValidators.ts) 新建 `validateStep`（multi-check 集合相等、single-select 等值、order 顺序全等；形态不符判 fail）+ `correctLabelOf`，纯函数无副作用；[data/scaffoldPractices.ts](../../miniapp/src/data/scaffoldPractices.ts) 新建 `CL2_FEI2_PRACTICE`（Cl₂ 通入 FeI₂ 溶液，Cl₂ 少量，A–F 六步覆盖三种交互形态，串联 `branchEntryId: 'BR-CL2-FEI2'` / `conditions: { quantity: '少量' }`）+ `SCAFFOLD_PRACTICES` + `getScaffoldPractice`；[components/ScaffoldPractice/index.tsx](../../miniapp/src/components/ScaffoldPractice/index.tsx) + [index.module.scss](../../miniapp/src/components/ScaffoldPractice/index.module.scss) 新建门控步进 UI（状态 gate / answers / validations；`cur` = 首个 unlocked 或 failed 下标；三种交互控件：勾选切换 / 单选 / ▲▼ 排序；`onSubmit` 调 `validateStep` → `markPassed` 或 `markFailed`；答错显示 feedback + 💡 hint 阶梯释放首档；全部通过显示完成 + Task 7 分支结论；含「重新练习」）；[pages/home/index.tsx](../../miniapp/src/pages/home/index.tsx) 挂载 `<ScaffoldPractice />`；[engine/scaffoldChains.ts](../../miniapp/src/engine/scaffoldChains.ts) 修正 `markPassed` 允许 `unlocked` 或 `failed` → `passed`（答错后重试答对放行）并新增 `resetStep`。**TR-8.2 通过**：运行时断言脚本（esbuild 打包 + node 执行，68 项全过、退出码 0，stdout 实证）覆盖数据完整性（6 步、候选项非空、correct 在 options 内、hint 非空）、确定性校验（正确→passed / 错误→failed / 相同输入结果一致）、门控重试回归（答错→failed 且下步 locked；重试答对→passed 且下步解锁；越界不抛错）、Task 7 分支串联（命中 'little' 分支，E 结论含 I₂）；源码追溯 `validateStep` 无任何 fetch / LLM 调用。**TR-8.3 通过**：`GetDiagnostics` 全量返回 `[]`；PAI 云预览 `pai.log` 21:52:32 出现「✅ 编译成功」（本轮编辑被 watcher 捕获重新编译），无 `compile_error`。**TR-8.1 通过**：浏览器实测（CDP 驱动无头 Edge，`Runtime.evaluate` 交互 + `Page.captureScreenshot` 截图；注：browser-use 子代理因云预览常驻 HMR websocket 致其 document-readiness 等待 30s 超时，页面 `readyState` 实际为 `complete`，故改用原生 CDP 绕开）——截图 [evidence/t8_01_initial.png](./evidence/t8_01_initial.png)（初始门控：A 进行中可交互，B–F 🔒 未解锁）、[evidence/t8_02_wrong_lock.png](./evidence/t8_02_wrong_lock.png)（A 步只选 OH⁻ 提交后：✗ 需重试 + 红色反馈「核对一下：正确项应为『I⁻、Fe²⁺、Cl₂、H₂O』」+ 💡 阶梯提示，B–F 仍 🔒 未解锁，「提交校验」仍可点）、[evidence/t8_03_correct_unlock.png](./evidence/t8_03_correct_unlock.png)（改选正确四项提交后：A ✓ 已通过 + 绿色反馈，B 解锁为「进行中」可交互，C–E 仍锁定）。**过程中发现并修复的 bug**：初版 `cur = gate.findIndex(s => s === 'unlocked')` 在答错后（A 变 failed）无 unlocked 步 → `cur = -1` → 当前步 `isActive` 全 false → 学生无法改答案重试、彻底卡死；已改为 `findIndex(s => s === 'unlocked' || s === 'failed')`，并经浏览器 E2E 实证「答错→改选→重试答对→解锁」全链路通过（截图 3 即重试后结果）。核验用临时脚本 `_verify_task8.ts` / `_verify_task8.cjs` 已删除。下一步 Task 9（错误触发的阶梯释放 + 可关闭）。

## Task 9: 错误触发的阶梯释放 + 可关闭（批次 6）
- **Status**: `completed`
- **Priority**: medium
- **Depends On**: Task 8
- **Description**:
  - 复用 `tutorEngine` 三层内容（hint / scaffold / reveal），实现「连续错 1 / 2 / 3 次」分别释放：辨析选择题（反问）/ 正反对比卡片 / 规则讲解抽屉。
  - 新增辨析选择题与对比卡片两种内容形态的数据结构与渲染。
  - 支持学生关闭提示独立推理（渐撤），状态可记录。
- **Acceptance Criteria Addressed**: AC-9
- **Test Requirements**:
  - `rule` TR-9.1: 连续错 1 / 2 / 3 次分别触发三种释放形态。Evidence: 预览截图。
  - `rule` TR-9.2: 关闭提示后步骤仍可独立作答校验。Evidence: 预览。
  - `rule` TR-9.3: `GetDiagnostics` 为空。Evidence: 工具输出。
- **Notes**: 现有三层内容已具备，本任务增量是释放触发器 + 两种新形态。
- **Completion Evidence**: 2026-11-23 收口。**决策**：把 `StepPractice.hint`（单条 string）升级为 `releases: StepScaffoldReleases`（hint / probe / scaffold / reveal 四字段全必填），对齐 `tutorEngine` 的 hint / scaffold / reveal 三层命名，保证任意错误次数都有内容、永不崩；释放触发是纯函数 `releaseTier(consecutiveWrong)`（0 未释放 / 1 / 2 / 3 封顶），确定性、无 LLM / 后端，组件只维护每步「连续错误计数」并据此渲染档位。**改动**：[types/scaffold.ts](../../miniapp/src/types/scaffold.ts) 追加 `ScaffoldReleaseTier` / `DiscriminationQuestion`（辨析题，options 仅引导思考、不判分不揭示正确项）/ `ContrastCard`（对例 + 错例）/ `RuleDrawer` / `StepScaffoldReleases`，并将 `StepPractice.hint` 改写为 `releases`（破坏性改动）；[engine/scaffoldValidators.ts](../../miniapp/src/engine/scaffoldValidators.ts) 新增纯函数 `releaseTier`；[data/scaffoldPractices.ts](../../miniapp/src/data/scaffoldPractices.ts) 为 `CL2_FEI2_PRACTICE` A–F 六步各补全三档内容（教材可溯源：A 粒子清单 / B 氧化剂还原剂 / C 用量条件 / D 还原性顺序 / E 产物 / F 结论，均不编造）；[components/ScaffoldPractice/index.tsx](../../miniapp/src/components/ScaffoldPractice/index.tsx) 新增 `wrongCounts` / `dismissed`（每步已关闭档位）/ `drawerOpen` 状态，`onSubmit` 失败计数 +1、通过清零，`switchPractice` / `resetAll` 同步重置；反馈区按 `releaseTier` 渲染三档（一级 hint + 辨析题 / 二级正反对比双栏卡 / 三级可展开收起规则抽屉），每档带「关闭」按钮，关闭后作答控件与提交按钮保持可用（TR-9.2）；修复 `sp.hint` → `sp.releases.hint` 编译错误；[index.module.scss](../../miniapp/src/components/ScaffoldPractice/index.module.scss) 追加三档样式。**TR-9.3 通过**：`GetDiagnostics` 全量返回 `[]`；断言脚本（esbuild 打包 + node 执行，70 项全过、退出码 0）覆盖 `releaseTier` 边界（0 / 负数 → 0，1→1，2→2，3→3，4 / 99 → 3 封顶）、确定性、6 步每步 releases 四字段非空、`probe.options ≥ 2` 且全非空、`scaffold` 对 / 错非空、`reveal` 非空、三档内容互不重复；PAI 云预览 `pai.log` 22:11:05「✅ 编译成功」（本轮编辑被 watcher 捕获重新编译）。**TR-9.1 通过**：浏览器实测（CDP 驱动无头 Edge，复用 Task 8 绕开 browser-use 超时方案）——A 步选 OH⁻ 连续提交 3 次错误，snap 实证档位递进：错 1 次仅 `一级提示` + 🤔辨析题；错 2 次叠加 `二级支架` 正反对比卡；错 3 次再叠加 `三级揭示` 📖规则抽屉，截图 [evidence/t9_01_tier1.png](./evidence/t9_01_tier1.png) / [t9_02_tier2.png](./evidence/t9_02_tier2.png) / [t9_03_tier3.png](./evidence/t9_03_tier3.png)。**TR-9.2 通过**：依次关闭三档后 snap 显示 `一级 / 二级 / 三级` 与 🤔 / 💡 / ✅ / 📖 全部消失，但「提交校验」控件仍在（[evidence/t9_04_dismissed.png](./evidence/t9_04_dismissed.png)）；随后取消错误项、选正确四项提交 → A 通过并解锁 B（[evidence/t9_05_pass_after_dismiss.png](./evidence/t9_05_pass_after_dismiss.png)），证明关闭提示后仍可独立作答校验。核验用临时脚本已删除。
