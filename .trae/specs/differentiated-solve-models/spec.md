# 题型差异化解题模型与讲解 UI - Product Requirements Document

## Overview
- **Summary**: 为化学伴思小程序按题型（ChemCategory）建立差异化的「解题思维模型」，并为不同模型匹配专属可视化讲解 UI，接入到题目页（challenge）的讲解区。首批落地 3 个代表性题型：氧化还原、离子平衡、化学平衡，验证「模型 → UI → 讲解」的完整模式；R2 把电化学推广为第 4 个专属面板，无机推断/有机仍占位 generic。本轮（R3）将氧化还原从「纯电子守恒配平专项」重构为**两阶段思维模型**：阶段一「反应可行性 & 产物推理」（主链路，化学伴思核心——先判断能不能反应、介质是什么、产物是什么），阶段二「电子守恒配平」（子专项，可展开面板，不强制）。
- **Purpose**: 现状中 6 大题型共用同一套「审题→建模→调知识→应用→求解→校验」六步解题模型与同一套对话式讲解 UI，无法体现各题型本质的思维差异（如氧化还原的电子守恒配平、离子平衡的粒子与三大守恒、化学平衡的条件-移动-K 推演）。学生看不到「这一类题到底该怎么想」的专属框架，讲解沦为千篇一律的文本。本规格让每类题拥有自己的解题模型与可视化讲解。
- **Target Users**: 使用化学伴思小程序刷题、需要「一类题一套思维方法」的高中学生。

## Goals
- 建立按 `ChemCategory` 索引的题型解题模型注册表，每类题定义：专属模型名、标准解题步骤、专属可视化面板类型、专属校验项。
- 为 3 个代表性题型各实现一个专属讲解 UI 组件（可视化面板），把抽象思维变成可视结构。
- 把专属模型接入题目页讲解区：答题前展示「题型思维框架」（不泄答案），答题后展示「完整解题可视化」（含该题结构化数据）。
- 保持向后兼容：无结构化数据的题目降级到通用面板，不崩溃，现有 6 步引导/诊断/守恒校验保留。
- 模型注册表与面板做成引擎层/组件层共享资产，便于后续 solve/tutor 讲解页与其余 3 类题型复用。
- （R3）把氧化还原题型升级为两阶段模型：阶段一默认展示「粒子→变价物种→介质→（可选）反应先后→产物推理」，阶段二配平折叠在【展开电子守恒配平推导】按钮之后，纠正「看到物质就默认反应、产物固定不变」的误区。

## Non-Goals
- 本轮不重写全部 84 道题的 `guide` 字段（现有 hand-authored guide 保留）。
- 本轮不改后端驱动的 `solve` / `tutor` 页（预览无后端，无法验证）；仅保证模型层可被它们后续复用。
- 本轮不为无机推断 / 有机化学 实现专属面板（注册表预留 `panelKind`，默认走通用面板）；电化学已在本轮推广为专属面板。
- 不接入真实后端 AI 推理；所有模型与数据为前端确定性资产。
- （R3）不引入自动配平算法：配平系数、产物、价态等均为基于题目已验证 `explanation`/`guide` 手填的结构化数据，引擎不做化学推理计算。
- （R3）不改动其余 5 类题型（离子/平衡/电化学/无机/有机）的模型与面板；不改 `solve` / `tutor` 页。

## Background & Context
- 代码位置：`miniapp/src/`。讲解链路现状：
  - `types/chem.ts`：`Challenge` 含 `guide: GuideLevel[]`（6 步）、`category`、`thought` 等；所有题目结构相同。
  - `engine/tutorEngine.ts`：`getTutorScript` 用通用 `STAGE_GOALS`（六步）构建阶段，仅 `outcomes` 探问按分类差异化（`CORRECT_PROBES`/`WRONG_PROBES`），但只用于答后 2 条追问。
  - `engine/brainEngine.ts`：通用题目分析/诊断/完整讲解，`QuestionType` 仅 choice/fill/calculation，`explainFully` 输出结构对所有分类一致。
  - `pages/challenge/index.tsx`：所有分类共用同一套对话式 6 步引导 + 守恒校验 + 路径回顾 UI，无题型差异化渲染。
  - `data/challenges/*.ts`：每道题的 `explanation` 在分类内部模式较规整（如氧化还原含「X: +7→+2 得 5e⁻」），但自由文本抽取仍脆弱，故采用结构化 `solveData`（代表题手填）+ 降级通用面板的策略。
- 现有数据约束：preview 环境无 `localhost:5080` 后端，故讲解入口必须是 challenge 页（纯前端可运行）。
- 上一轮已确认首页「连续反应推理」「电化学推理」禁用，物质速查/错题本/我的页已完成；本轮聚焦讲解模型。

## Functional Requirements
- **FR-1**: 新增题型解题模型注册表 `engine/solveModels.ts`，以 `ChemCategory` 为键，至少为 6 个分类提供元数据（模型名、图标、主题色、标准解题步骤、面板类型 `panelKind`、专属校验项生成）；其中氧化还原/离子平衡/化学平衡/电化学有专属 `panelKind`，无机推断/有机为 `generic`。
- **FR-2**: 扩展 `Challenge` 类型，新增可选字段 `solveData?: SolveData`，为带判别联合的结构化解题数据（氧化还原：半反应与电子守恒；离子平衡：粒子清单与守恒式；化学平衡：条件-移动推演）。缺省不影响既有逻辑。
- **FR-3**: 新增组件 `components/SolvePanels/`，含 `RedoxElectronPanel`、`IonicConservationPanel`、`EquilibriumShiftPanel`、`ElectrochemCellPanel`、`GenericSolvePanel` 及调度组件 `SolveModelPanel`（按 `panelKind` 选择渲染）。
- **FR-4**: `SolveModelPanel` 支持两种模式：`preview`（答题前，仅展示题型标准步骤与框架，不泄答案）与 `full`（答题后，展示含 `solveData` 的完整可视化）。
- **FR-5**: `pages/challenge/index.tsx` 讲解区接入 `SolveModelPanel`：题干下方、选项上方放置可折叠「🧩 这类题怎么想」（preview 模式）；答题结果区后放置「完整解题模型」（full 模式）。
- **FR-6**: 为每个有专属面板的题型各录入 ≥4 道题的结构化 `solveData`（氧化还原 6 / 离子平衡 4 / 化学平衡 4 / 电化学 14，合计 28 道），使预览可见富可视化。
- **FR-7**: 氧化还原面板采用两阶段渲染：**阶段一（默认可见，主链路）**渲染「体系粒子清单（区分大量/微量）→ 候选氧化剂/还原剂 → 介质判定 + 介质→产物规则（如 MnO₄⁻ 酸性→Mn²⁺）→ 推导产物及依据 → 共存/沉淀判断」；**阶段二（子专项，折叠）**默认收起，仅当存在配平数据时出现【展开电子守恒配平推导】按钮，展开后渲染半反应表（物种 / 起始价→终态价 / 得失 / 电子数）与电子守恒汇总（失电子总数 = 得电子总数、最小公倍数、系数比）。
- **FR-8**: 离子平衡面板渲染：粒子来源清单（电离/水解产生的粒子）+ 三大守恒式卡片（电荷/物料/质子，含表达式）。
- **FR-9**: 化学平衡面板渲染：推演表，每行一个条件改变因素（因素 / 改变 / 平衡移动方向 / K 变化 / 转化率变化）。
- **FR-10**: 无 `solveData` 时，`SolveModelPanel` 降级为 `GenericSolvePanel`，展示该分类的标准步骤与通用校验项，不崩溃。
- **FR-11**: 模型注册表导出 `getSolveModel(challenge)` / `getPanelKind(challenge)` / `getModelChecks(challenge)`，供 challenge 页及未来 solve/tutor 页复用。
- **FR-12**: 电化学面板 `ElectrochemCellPanel` 渲染：装置判别卡（装置类型徽标 / 电解质 / 电子流向 / 电极材料）+ 电极反应表（电极 / 反应式 / 氧化或还原，含总反应式）+ 现象与结论；含 `calc` 的计算类题额外渲染「已知 → 求解步骤 → 结果」。各字段缺失时优雅降级不崩溃。
- **FR-13**: `engine/solveModels.ts` 中 `RedoxReaction.steps` 重写为两阶段 11 步：阶段一 5 步（①列体系粒子 ②标变价物种、选候选氧化剂/还原剂 ③判介质 ④[可选]多剂判反应先后 ⑤推产物 + 共存判断），阶段二 6 步（⑥标价态 ⑦算单原子得失电子 ⑧求最小公倍数 ⑨配平变价物质 ⑩电荷守恒补 H⁺/OH⁻、原子守恒补 H₂O ⑪全局三重校验）；`SolveModelStep` 新增可选 `stage?: 'stage1' | 'stage2'`，供 `StepsView` 按阶段分组渲染；氧化还原模型名更新为体现两阶段（如「氧还反应推理模型」），`checks` 同步覆盖两阶段校验点。
- **FR-14**: `types/chem.ts` 的 `RedoxSolveData` 新增可选子对象 `stage1?`：含 `particles`（formula / source / abundance 大量·微量）、`medium`（酸性/碱性/中性）、`mediumRules?`（物种在酸/中/碱下的还原产物映射）、`candidates`（oxidants / reductants）、`order?`（多剂反应先后）、`products`（species / basis）、`coexistence?`（是否共存 / 沉淀）。全部字段可选，缺失不影响既有 14 道题的编译与运行（向后兼容）。
- **FR-15**: `RedoxElectronPanel` 重写为动态 section 路由：按 `solveData` 字段存在性决定渲染哪些 section——阶段一各段仅在对应字段存在时渲染；阶段二展开按钮仅在 `halfReactions.length > 0` 时出现（有 `balance` 时按钮文案为「展开电子守恒配平推导」并渲染电子守恒汇总卡；无 `balance` 时文案为「展开电子转移分析」、不渲染汇总卡——覆盖 R8/R11/R12 等有半反应无配平的变体，避免阶段二消失），展开状态由组件本地 state 控制（不强制学生走配平）；同时按字段存在性渲染既有 subtype 专属段（`calc` 计算过程、`comparison` 性质比较、`order` 反应先后、`valenceRule` 价态规律、`reactions` 多反应），使现有结构化数据不再是死字段。任一字段缺失均优雅降级不崩溃。
- **FR-16**: 对含 `stage1.mediumRules` 的题，面板展示「介质 → 还原产物」对照表（如 MnO₄⁻：酸性 Mn²⁺ / 中性 MnO₂ / 碱性 MnO₄²⁻），并高亮本题介质对应的产物；介质判定与产物推导必须与题目 `explanation`/`guide` 一致，不编造化学事实（precision=1、harmCount=0）。

## Non-Functional Requirements
- **NFR-1**: TypeScript 零诊断错误（`GetDiagnostics` 为空）。
- **NFR-2**: 预览编译成功（`pai.log` 出现「✅ 编译成功」，无 `compile_error`）。
- **NFR-3**: 所有新 `.module.scss` 顶部 `@use '@/styles/variables.scss' as *;`；条件类名用 `classnames`；仅用 `@tarojs/components` 组件。
- **NFR-4**: 不引入新依赖；不修改 `app.config.ts` 页面注册（无新页面）。
- **NFR-5 (rubric)**: 题型差异化讲解的视觉清晰度与思维贴合度高，学生能一眼看出「这类题的思考骨架」。

## Constraints
- **Technical**: Taro 4 / React 18 / TS / SCSS Modules；preview 地址 `https://trae.mobile.volcapp.com/preview/?ws=ws://localhost:51456`；不得使用原生 Taro CLI/npm，只用技能脚本。
- **Business**: 不破坏现有题库做题、错题本、物质速查、我的 等已完成功能。
- **Dependencies**: 依赖现有 `data/challenges`、`data/knowledge`、`engine/tutorEngine`、`types/chem`。

## Assumptions
- 第二问（落地入口）用户选「其他」但未返回具体文字，本规格按「优先接入 challenge 讲解区，模型层做成共享资产便于后续复用」处理；如用户希望同时改 solve/tutor，可在批准时调整。
- 结构化 `solveData` 由人工为代表题录入；同分类其余题目降级通用面板，后续可逐步补全。
- 电化学已在本轮推广为专属面板（面板组件 + 调度器 + 注册表 `panelKind`）；无机推断 / 有机化学仍只在注册表占位（`generic` 面板 + 专属步骤），专属面板留作后续迭代。
- （R3）`stage1` 结构化数据由人工基于每道题已验证的 `explanation`/`guide` 回填，价态与产物（如 MnO₄⁻ 酸性→Mn²⁺、NO₃⁻ 酸性→NO）须与高中化学定论及题目表述一致，严禁编造；为全部 14 道氧化还原题回填，其中纯概念判断类（无具体反应体系）仅在有化学依据时填入相关字段、其余留空由动态路由优雅降级。

## Acceptance Criteria

### AC-1: 题型模型注册表完整
- **Type**: `rule`
- **Given**: 代码库存在 `miniapp/src/engine/solveModels.ts`
- **When**: 导入并遍历 `SOLVE_MODELS`（或等价注册表）
- **Then**: 6 个 `ChemCategory` 均有条目；氧化还原/离子平衡/化学平衡/电化学的 `panelKind` 分别为专属值（非 `generic`），且各有 ≥3 条标准解题步骤
- **Pass Condition**: 6 类全覆盖且 4 个专属类非 generic、步骤非空
- **Evidence**: 源码检查 + `GetDiagnostics` 无错

### AC-2: 题目页讲解区渲染专属面板
- **Type**: `rule`
- **Given**: 打开任意一道氧化还原/离子平衡/化学平衡题
- **When**: 查看页面
- **Then**: 题干下方出现可折叠「这类题怎么想」面板（preview 模式，仅步骤框架），答题后结果区出现完整解题模型面板（full 模式）
- **Pass Condition**: 两个位置均渲染且不泄露答案（preview 模式不含 solveData 结论）
- **Evidence**: 预览截图

### AC-3: 氧化还原专属面板可视化
- **Type**: `rule`
- **Given**: 一道配有 `solveData.kind==='redox-electron'` 的氧化还原题（如 R1/R2/R3）
- **When**: 答题后查看完整模型面板
- **Then**: 渲染半反应表（物种/起始价→终态价/得失/电子数）与电子守恒汇总（失电子总数、得电子总数、最小公倍数、系数比）
- **Pass Condition**: 半反应行数 ≥1，汇总字段完整且数值与题目解析一致
- **Evidence**: 预览截图 + 数据核对

### AC-4: 离子平衡专属面板可视化
- **Type**: `rule`
- **Given**: 一道配有 `solveData.kind==='ionic-conservation'` 的离子平衡题
- **When**: 答题后查看完整模型面板
- **Then**: 渲染粒子来源清单与三大守恒式卡片（电荷/物料/质子）
- **Pass Condition**: 至少渲染守恒式卡片，表达式非空
- **Evidence**: 预览截图

### AC-5: 化学平衡专属面板可视化
- **Type**: `rule`
- **Given**: 一道配有 `solveData.kind==='equilibrium-shift'` 的化学平衡题
- **When**: 答题后查看完整模型面板
- **Then**: 渲染条件-移动推演表（因素/改变/移动方向/K变化/转化率）
- **Pass Condition**: 推演表行数 ≥1，方向与 K 字段非空
- **Evidence**: 预览截图

### AC-6: 代表题结构化数据覆盖
- **Type**: `rule`
- **Given**: redox / ionic / equilibrium 三个数据文件
- **When**: 统计含 `solveData` 的题目数
- **Then**: 每类 ≥4 道，合计 ≥12 道
- **Pass Condition**: 三类计数均 ≥4
- **Evidence**: 源码 grep 计数

### AC-7: 向后兼容与降级
- **Type**: `rule`
- **Given**: 一道无 `solveData` 的题（如电化学/无机/有机，或同分类未录数据的题）
- **When**: 打开并答题
- **Then**: 面板降级为通用面板（展示该分类标准步骤与通用校验），页面不报错，原有 6 步引导/诊断/守恒校验仍可用
- **Pass Condition**: 无运行时错误，功能保留
- **Evidence**: 预览操作验证

### AC-8: 题型差异化讲解的清晰度与贴合度
- **Type**: `rubric`
- **Dimension**: 专属面板是否让学生一眼看出该题型的思考骨架，且可视化与题型思维本质一致
- **Scale**: 1-5
- **Anchors**: 1 = 仍是纯文本、无差异化；3 = 有结构化展示但贴合度一般；5 = 可视化精准映射题型思维（如氧化还原的电子转移表），清晰易用
- **Pass Threshold**: >= 4
- **Evidence**: 预览截图 + 独立评审

### AC-9: 构建与类型质量
- **Type**: `rule`
- **Given**: 本轮所有改动完成
- **When**: 运行 `GetDiagnostics` 并查看 `pai.log`
- **Then**: 诊断为空，日志末尾「✅ 编译成功」无编译错误
- **Pass Condition**: 零诊断 + 编译成功
- **Evidence**: 工具输出

### AC-10: 电化学专属面板可视化
- **Type**: `rule`
- **Given**: 一道配有 `solveData.kind==='electrochem-cell'` 的电化学题（如 C1 原电池 / C4 电解计算 / C7 可充电电池）
- **When**: 答题后查看完整模型面板
- **Then**: 渲染装置判别卡（装置类型 / 电解质 / 电子流向）+ 电极反应表（电极 / 反应式 / 氧化或还原，含总反应式）+ 现象与结论；计算类题额外渲染「已知 → 求解步骤 → 结果」
- **Pass Condition**: 电极反应表行数 ≥1，装置类型与结论非空；无 `calc` 时不渲染计算段、不崩溃
- **Evidence**: 预览截图 + 数据核对

### AC-11: 氧化还原两阶段思考骨架 preview 不泄答案
- **Type**: `rule`
- **Given**: 一道带 `solveData.stage1` 的氧化还原题（如 R2），未答题
- **When**: 查看题干下「这类题怎么想」（preview 模式）
- **Then**: 展示两阶段思考骨架（阶段一 5 步 + 阶段二 6 步的标题/目标，按阶段分组），不展示该题具体粒子/产物/系数等结论性数据
- **Pass Condition**: preview 不含 stage1 具体数据与阶段二系数；两阶段步骤框架非空
- **Evidence**: 预览截图

### AC-12: 氧化还原阶段二折叠在按钮之后，数据驱动出现
- **Type**: `rule`
- **Given**: 一道带 `halfReactions` + `balance` 的配平类氧化还原题（如 R2），答题后
- **When**: 查看完整解题模型（full 模式）
- **Then**: 阶段一内容默认展开可见；出现【展开电子守恒配平推导】按钮，点击后才渲染半反应表与电子守恒汇总；未点击时不渲染配平表格
- **Pass Condition**: 展开按钮存在且默认折叠；点击后半反应表行数 ≥1、汇总四字段（失/得电子总数、最小公倍数、系数比）非空且与解析一致
- **Evidence**: 预览截图（折叠态 + 展开态）+ 数据核对

### AC-13: 动态 section 路由——按字段条件渲染，缺失不崩溃
- **Type**: `rule`
- **Given**: 覆盖不同 subtype 的 14 道氧化还原题（配平 R2/R7、计算 R4/R8/R12/R13、概念判断 R1/R3/R6/R9/R14、性质比较 R10、反应先后 R11、复杂综合 R5）
- **When**: 逐题答题后查看完整模型面板
- **Then**: 仅当对应 `solveData` 字段存在时渲染该 section：有 `stage1` 渲染阶段一各段、有 `halfReactions` 时出现阶段二展开按钮（`balance` 决定按钮文案与是否渲染守恒汇总卡）、有 `calc`/`comparison`/`order`/`valenceRule`/`reactions` 渲染对应段；字段缺失的 section 不渲染，面板不崩溃
- **Pass Condition**: 14 道题答题后面板均无运行时错误；各 section 与字段存在性一一对应
- **Evidence**: 预览逐题验证 + 源码守卫核对

### AC-14: 介质→产物规则展示且与题目一致
- **Type**: `rule`
- **Given**: 一道带 `stage1.mediumRules` 的酸性介质氧化还原题（如 R2：MnO₄⁻ 酸性→Mn²⁺）
- **When**: 答题后查看阶段一「介质判定」段
- **Then**: 展示介质→产物对照表（MnO₄⁻ 酸性 Mn²⁺ / 中性 MnO₂ / 碱性 MnO₄²⁻），高亮本题介质（酸性）对应的产物；产物推导与 `explanation`/`guide` 一致，无编造化学事实
- **Pass Condition**: 对照表非空，本题介质对应产物高亮，与解析一致
- **Evidence**: 预览截图 + 数据核对

### AC-15: 向后兼容——无 stage1 数据时不回归
- **Type**: `rule`
- **Given**: 一道带 `halfReactions`/`balance` 但**无** `stage1` 的氧化还原题，或任一字段缺失的题
- **When**: 答题后查看面板
- **Then**: 阶段一不渲染（无数据）、不报错；阶段二展开按钮与既有半反应表/汇总仍正常；既有 6 步引导/诊断/守恒校验不受影响
- **Pass Condition**: 无运行时错误，旧数据题配平可视化不丢失
- **Evidence**: 源码守卫 + 预览验证

## Open Questions
- [ ] 落地入口：用户对第二问选「其他」的具体意图？（当前默认：优先 challenge 讲解区 + 共享模型层）
- [x] 电化学/无机推断/有机 3 类是否在下一轮立即推广专属面板？→ 本轮先推广电化学（用户指令「先推广电化学专属面板」），无机推断/有机留待后续迭代。
- [x] 氧化还原是否升级为两阶段模型、阶段二是否强制？→ R3 确认：阶段一「反应可行性 & 产物推理」默认可见（主链路），阶段二「电子守恒配平」折叠在【展开电子守恒配平推导】按钮后，不强制学生走配平，选择题可直接进入选答案。
