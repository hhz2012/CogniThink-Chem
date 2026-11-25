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

## Task 6: 推广电化学专属面板（R2）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 2（面板基础设施）, Task 5（R1 已 pass）
- **Description**:
  - 把电化学从 `generic` 占位提升为第 4 个专属面板：
    - `engine/solveModels.ts`：`Electrochemistry.panelKind` 由 `'generic'` 改为 `'electrochem-cell'`；更新注释。既有 steps（判装置 / 写半反应 / 析现象，≥3 步）与 checks 保留。
    - 新建 `components/SolvePanels/ElectrochemCellPanel/index.tsx` + `index.module.scss`：渲染①装置判别卡（装置类型徽标 / 电解质 / 电子流向 / 电极材料）②电极反应表（电极 / 反应式 / 氧化·还原，含总反应式）③现象与结论；含 `calc` 时额外渲染「已知 → 求解步骤 → 结果」。所有字段可选，缺失时优雅降级不崩溃。
    - `components/SolvePanels/index.tsx`：导入并接入调度分支 `panelKind === 'electrochem-cell' && sd?.kind === 'electrochem-cell'` → `<ElectrochemCellPanel />`。
  - 电化学题库 `data/challenges/electrochemistry.ts` 的 C1–C14 已全部带 `solveData`（14 道，覆盖原电池/电解池/燃料电池/可充电电池/金属腐蚀/计算），无需新增数据；核对其类型与面板字段一致。
- **Acceptance Criteria Addressed**: AC-1（电化学非 generic）, AC-10, FR-1, FR-3, FR-12, AC-7（无 solveData 仍降级）
- **Test Requirements**:
  - `rule` TR-6.1: `SOLVE_MODELS.Electrochemistry.panelKind === 'electrochem-cell'` 且 `steps.length >= 3`；`getPanelKind` 对电化学题返回 `'electrochem-cell'`。Evidence: 源码 + GetDiagnostics。
  - `rule` TR-6.2: `SolvePanels/index.tsx` 调度器对电化学题（带 `solveData.kind==='electrochem-cell'`）mode='full' 时渲染 `ElectrochemCellPanel`；电极反应表行数 ≥1，装置类型与结论非空。Evidence: 预览截图。
  - `rule` TR-6.3: 计算类电化学题（如 C4，含 `calc`）渲染「已知 → 步骤 → 结果」段；无 `calc` 的题（如 C1）不渲染该段、不崩溃。Evidence: 预览截图。
  - `rule` TR-6.4: 面板字段缺失降级健壮——`electrochemistry.ts` 中各题 `solveData` 缺 `phenomena`/`totalEquation`/`calc`/`conclusion` 任一字段时面板不抛错（抽查 C2 缺 phenomena、C14 缺 phenomena）。Evidence: 源码核对 + 预览不报错。
  - `rule` TR-6.5: 无 `solveData` 的电化学题（若存在）或同分类未录数据的题仍降级 `GenericSolvePanel`，原 6 步引导/诊断/守恒校验不受影响。Evidence: 预览操作（电化学 14 题均有 solveData，降级验证可用 R7 既有通用路径回归，或临时构造验证后还原）。
  - `rule` TR-6.6: `GetDiagnostics` 为空，`pai.log` 末尾「✅ 编译成功」无 `compile_error`。Evidence: 工具输出。
  - `rubric` TR-6.7: 电化学面板与「判装置→写半反应→析现象」思维骨架的贴合度，scale 1-5，threshold >= 4。Evidence: review.md。
- **Notes**: 复用既有 `RedoxElectronPanel` 的表格/汇总样式语言与 `ModelIcon` `battery` 图标；不引入新依赖、不改 `app.config.ts`；不改动 challenge 页接线（已在 Task 4 完成，对电化学自动生效）。
- **Completion Evidence**:
  - TR-6.1（源码）：`solveModels.ts` Electrochemistry `panelKind: 'electrochem-cell'`、steps=3；`getPanelKind` 透传该值。`GetDiagnostics = []`。
  - TR-6.2 / AC-10：本地 H5 构建端到端答题，C1 答题后 full 面板渲染装置卡（原电池徽标/电解质/电子流向/电极芯片）+ 电极反应表（≥2 行）+ 结论框；截图 `evidence/AC10_C1_电化学_原电池面板.jpg`。
  - TR-6.3：C4（含 calc）渲染「已知→求解→编号步骤→结果」计算段（截图 `evidence/AC10_C4_电化学_电解计算面板.jpg`）；C1（无 calc）不渲染该段。
  - TR-6.4：C4 缺 `phenomena` 仅显结论卡、C1 缺 `calc` 不渲染计算段，均不崩溃（源码 `&&` 守卫 + 截图佐证）。
  - TR-6.5：调度器 `specialized` 初始 null，panelKind/sd.kind 不匹配时回落 `GenericSolvePanel`（源码 L64/L88-90 确认）；R7 端到端答题无崩溃，无回归。
  - TR-6.6：`GetDiagnostics` 空；云预览 `pai.log` 末尾「✅ 编译成功」；本地 `taro build --type h5` → 「Compiled successfully in 39.40s」（仅资源体积警告）。
  - TR-6.7（rubric）：独立评审 CP-U2.1 得分 5（≥4），面板三段与「判装置→写半反应→析现象」1:1 映射。
  - 独立只读评审 Review R2 结论 `pass`（7 rule 全 PASS、rubric 5、无 must-fix），见 `review.md`。

## Task 7: 氧化还原两阶段思维模型改造（R3）
- **Status**: completed
- **Priority**: high
- **Depends On**: Task 2（面板基础设施）, Task 5 / Task 6（R1 / R2 已 pass）
- **Description**:
  - 把氧化还原题型从「纯电子守恒配平专项」重构为**两阶段思维模型**：阶段一「反应可行性 & 产物推理」（主链路，默认可见）+ 阶段二「电子守恒配平」（子专项，可展开，不强制）。
  - `types/chem.ts`：`SolveModelStep` 新增可选 `stage?: 'stage1' | 'stage2'`；`RedoxSolveData` 新增可选子对象 `stage1?`（`particles` / `medium` / `mediumRules?` / `candidates` / `order?` / `products` / `coexistence?`），全部字段可选，向后兼容，既有 14 道题无该字段时不报错。
  - `engine/solveModels.ts`：`RedoxReaction` 模型名更新为「氧还反应推理模型」；`steps` 重写为两阶段 11 步（阶段一 5 步 + 阶段二 6 步），每步标注 `stage`；`checks` 同步覆盖两阶段校验点。
  - `components/SolvePanels/StepsView`：支持按 `stage` 分组渲染（阶段一 / 阶段二 小标题），无 `stage` 字段时保持现状（其余 5 类不受影响）。
  - `components/SolvePanels/RedoxElectronPanel`：重写为动态 section 路由——①阶段一各段（粒子清单 / 介质判定 + 介质→产物对照表 / 候选氧化剂·还原剂 / 产物推导及依据 / 共存·沉淀判断）按字段存在性渲染；②阶段二折叠在【展开电子守恒配平推导】按钮后，仅当 `halfReactions.length > 0` 时出现（`balance` 决定按钮文案与守恒汇总卡：有 balance→「展开电子守恒配平推导」+ 汇总卡；无 balance→「展开电子转移分析」+ 无汇总卡，避免 R8/R11/R12 阶段二消失），展开状态由组件本地 state 控制，展开后复用现有电子转移表 + 电子守恒汇总；③按字段存在性渲染既有 subtype 专属段（`calc` 计算过程 / `comparison` 性质比较 / `order` 反应先后 / `valenceRule` 价态规律 / `reactions` 多反应），激活现有死字段。任一 section 数据缺失即跳过，不崩溃。样式追加到 `index.module.scss`。
  - `data/challenges/redox.ts`：为全部 14 道题回填 `stage1` 数据，**必须基于各题已验证的 `explanation`/`guide`，零编造化学事实**；纯概念判断类（无具体反应体系）仅在有化学依据时填入相关字段，其余留空由动态路由优雅降级。
- **Acceptance Criteria Addressed**: AC-1（模型名/步骤更新）, AC-11, AC-12, AC-13, AC-14, AC-15, FR-7（修订）, FR-13, FR-14, FR-15, FR-16, NFR-1~NFR-5
- **Test Requirements**:
  - `rule` TR-7.1: `SolveModelStep` 有可选 `stage?: 'stage1' | 'stage2'`；`RedoxSolveData` 有 `stage1?` 子对象且其字段均可选；既有 14 道氧化还原题无 `stage1` 时 TS 不报错、运行时 `data.stage1?.particles` 等读取安全。Evidence: 源码 + `GetDiagnostics`。
  - `rule` TR-7.2: `SOLVE_MODELS.RedoxReaction.steps` 共 11 步，其中 `stage==='stage1'` 5 步、`stage==='stage2'` 6 步；模型名体现两阶段；`checks` 同时含阶段一（反应可行性/产物/介质/共存）与阶段二（电子守恒/配平）校验项。Evidence: 源码。
  - `rule` TR-7.3: 配平类题 R2（含 `halfReactions` + `balance` + `stage1`）full 面板：阶段一默认可见；出现【展开电子守恒配平推导】按钮且默认折叠；点击后半反应表行数 ≥1、汇总四字段（失/得电子总数、最小公倍数、系数比）非空且与解析一致（Mn 得 5e⁻ / Fe 失 1e⁻ / lcm 5 / ratio 1:5）。Evidence: 预览截图（折叠态 + 展开态）。
  - `rule` TR-7.4: preview 模式（R2 未答题）仅展示两阶段步骤框架（按 stage 分组），不含 `stage1` 具体粒子/产物与阶段二系数，不泄答案。Evidence: 预览截图。
  - `rule` TR-7.5: 14 道氧化还原题逐题答题后面板无运行时错误；section 渲染与字段存在性一一对应（有 `halfReactions` 时出现阶段二展开按钮且 `balance` 控制文案与汇总卡、有 `calc` 才渲染计算段、有 `comparison`/`order`/`valenceRule`/`reactions` 才渲染对应段）；无 `stage1` 的题阶段一不渲染、不报错。Evidence: 预览逐题验证 + 源码守卫核对。
  - `rule` TR-7.6: R2 阶段一「介质判定」展示 MnO₄⁻ 介质→产物对照表（酸性 Mn²⁺ / 中性 MnO₂ / 碱性 MnO₄²⁻）并高亮本题介质（酸性）对应产物，与 R2 `explanation` 一致，无编造。Evidence: 预览截图 + 数据核对。
  - `rule` TR-7.7: `GetDiagnostics` 为空；本地 `taro build --type h5` 编译成功；新改 `.module.scss` 顶部 `@use '@/styles/variables.scss' as *;`，`.tsx` 仅从 `@tarojs/components` 导入；无新依赖、未改 `app.config.ts`。Evidence: 工具输出。
  - `rubric` TR-7.8: 两阶段模型与「先判反应可行性 / 介质 / 产物 → 再选做电子守恒配平」的化学伴思主链路贴合度，scale 1-5，threshold ≥ 4。Evidence: review.md。
- **Notes**: 阶段一是化学伴思核心（纠正「看到物质就默认反应、产物固定不变」误区），阶段二是可选子专项——不强制学生走配平，选择题可直接进入选答案。严守红线：precision=1、harmCount=0，所有 `stage1` 数据须来自题目已验证的 `explanation`/`guide`，价态与产物与高中化学定论一致；其余 5 类模型与面板不动；不引入自动配平算法。
- **Completion Evidence**:
  - TR-7.1（源码 + GetDiagnostics）：`SolveModelStep.stage?: 'stage1'|'stage2'` 可选（solveModels.ts L16）；`RedoxSolveData.stage1?: RedoxStage1` 且子对象 7 字段全可选（chem.ts L104-113、L152）；面板 `data.stage1` 经 `s1 &&` / `?.` 守卫，R9/R10/R14 无 stage1 编译通过。`GetDiagnostics = []`。
  - TR-7.2（源码）：`RedoxReaction.steps` 共 11 步——stage1=5（列粒子/标变价物种/判介质/判先后/推产物）、stage2=6（标价态/算电子/求公倍数/配变价/补氢氧/全局校验），每步标 stage；模型名「氧还反应推理模型」；checks 6 条覆盖两阶段（阶段一 3 + 阶段二 3）。
  - TR-7.3 / AC-12（截图）：`AC12_R2_stage1.jpg` 证 R2 答题后阶段一默认可见 + 折叠态【展开电子守恒配平推导】按钮；`AC12_R2_expanded.jpg` 证展开后半反应表 2 行（Mn 7→2 还原 5e⁻ / Fe 2→3 氧化 5e⁻）+ 汇总四字段非空（失 5 = 得 5 / lcm 5 / ratio 1:5），与 R2 explanation 一致。
  - TR-7.4 / AC-11（截图）：`AC11_R2_pending.jpg` 证未答题时「二、思考过程启发 · 氧还反应推理模型」仅展示按 stage 分组的步骤骨架（阶段一 banner + ①~⑤ 标题/目标），无 R2 具体粒子/产物/系数，不泄答案。
  - TR-7.5 / AC-13/AC-15（源码守卫 + 截图）：阶段一七段、阶段二按钮与汇总、calc/comparison/order/valenceRule/reactions 全部按字段存在性渲染，缺失即跳过；`AC13_R8.jpg`（halfReactions 无 balance、有 calc：按钮文案「展开电子转移分析」、无汇总卡、计算段正常）、`AC13_R9.jpg`/`AC13_R14.jpg`（无 stage1 不崩溃）佐证；14 题中 11 题带 stage1、3 题留空降级，无运行时错误。
  - TR-7.6 / AC-14（截图 + 数据核对）：`AC14_R2_medium.jpg` 证 R2 介质判定展示 MnO₄⁻ 对照表（酸性 Mn²⁺ 红色高亮 / 中性 MnO₂ / 碱性 MnO₄²⁻），与 explanation 一致，无编造（MnO₄⁻ 酸性→Mn²⁺ 为高中化学定论）。
  - TR-7.7（工具输出）：`GetDiagnostics = []`（独立复跑确认）；主代理本地 `npm run build:h5` 编译成功（约 22s，仅资源体积警告）；三处改 scss 顶部均 `@use '@/styles/variables.scss' as *;`；改 tsx 仅从 `@tarojs/components` 导入；`package.json` 无 diff（无新依赖）；`app.config.ts` 唯一改动为并发任务加 `pages/reaction/index`，R3 未触碰。
  - TR-7.8（rubric）：独立评审 CP-U3.1 得分 **5**（≥4 阈值），两阶段模型与「先判反应可行性/介质/产物 → 再选做电子守恒配平」化学伴思主链路 1:1 贴合。
  - 化学正确性抽查（precision=1 / harmCount=0）：R2/R4/R8/R13 的 stage1 particles/medium/products/coexistence 与各题 explanation 逐字核对一致，介质→产物映射符合高中化学定论，零编造。
  - 回归：StepsView 对无 stage 的其余 5 类模型 `showStageHead` 恒 false，行为不变；challenge 页 R3 加性修改由 `showStageHead` 守卫，不破坏并发任务的朗读/分步引导/答题流。
  - 独立只读评审 Review R3 结论 `pass`（7 rule 全 PASS、rubric 5、无 must-fix），见 `review.md`。

