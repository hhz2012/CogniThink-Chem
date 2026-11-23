# 题型差异化解题模型与讲解 UI - Product Requirements Document

## Overview
- **Summary**: 为化学伴思小程序按题型（ChemCategory）建立差异化的「解题思维模型」，并为不同模型匹配专属可视化讲解 UI，接入到题目页（challenge）的讲解区。首批落地 3 个代表性题型：氧化还原、离子平衡、化学平衡，验证「模型 → UI → 讲解」的完整模式，为其余 3 类（电化学、无机推断、有机）预留扩展接口。
- **Purpose**: 现状中 6 大题型共用同一套「审题→建模→调知识→应用→求解→校验」六步解题模型与同一套对话式讲解 UI，无法体现各题型本质的思维差异（如氧化还原的电子守恒配平、离子平衡的粒子与三大守恒、化学平衡的条件-移动-K 推演）。学生看不到「这一类题到底该怎么想」的专属框架，讲解沦为千篇一律的文本。本规格让每类题拥有自己的解题模型与可视化讲解。
- **Target Users**: 使用化学伴思小程序刷题、需要「一类题一套思维方法」的高中学生。

## Goals
- 建立按 `ChemCategory` 索引的题型解题模型注册表，每类题定义：专属模型名、标准解题步骤、专属可视化面板类型、专属校验项。
- 为 3 个代表性题型各实现一个专属讲解 UI 组件（可视化面板），把抽象思维变成可视结构。
- 把专属模型接入题目页讲解区：答题前展示「题型思维框架」（不泄答案），答题后展示「完整解题可视化」（含该题结构化数据）。
- 保持向后兼容：无结构化数据的题目降级到通用面板，不崩溃，现有 6 步引导/诊断/守恒校验保留。
- 模型注册表与面板做成引擎层/组件层共享资产，便于后续 solve/tutor 讲解页与其余 3 类题型复用。

## Non-Goals
- 本轮不重写全部 84 道题的 `guide` 字段（现有 hand-authored guide 保留）。
- 本轮不改后端驱动的 `solve` / `tutor` 页（预览无后端，无法验证）；仅保证模型层可被它们后续复用。
- 本轮不为电化学 / 无机推断 / 有机化学 实现专属面板（注册表预留 `panelKind`，默认走通用面板）。
- 不接入真实后端 AI 推理；所有模型与数据为前端确定性资产。

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
- **FR-1**: 新增题型解题模型注册表 `engine/solveModels.ts`，以 `ChemCategory` 为键，至少为 6 个分类提供元数据（模型名、图标、主题色、标准解题步骤、面板类型 `panelKind`、专属校验项生成）；其中氧化还原/离子平衡/化学平衡有专属 `panelKind`，其余 3 类为 `generic`。
- **FR-2**: 扩展 `Challenge` 类型，新增可选字段 `solveData?: SolveData`，为带判别联合的结构化解题数据（氧化还原：半反应与电子守恒；离子平衡：粒子清单与守恒式；化学平衡：条件-移动推演）。缺省不影响既有逻辑。
- **FR-3**: 新增组件 `components/SolvePanels/`，含 `RedoxElectronPanel`、`IonicConservationPanel`、`EquilibriumShiftPanel`、`GenericSolvePanel` 及调度组件 `SolveModelPanel`（按 `panelKind` 选择渲染）。
- **FR-4**: `SolveModelPanel` 支持两种模式：`preview`（答题前，仅展示题型标准步骤与框架，不泄答案）与 `full`（答题后，展示含 `solveData` 的完整可视化）。
- **FR-5**: `pages/challenge/index.tsx` 讲解区接入 `SolveModelPanel`：题干下方、选项上方放置可折叠「🧩 这类题怎么想」（preview 模式）；答题结果区后放置「完整解题模型」（full 模式）。
- **FR-6**: 为 3 个代表性题型各录入 ≥4 道题的结构化 `solveData`（合计 ≥12 道），使预览可见富可视化。
- **FR-7**: 氧化还原面板渲染：每个半反应一行（物种 / 起始价 → 终态价 / 失或得 / 电子数），底部电子守恒汇总（失电子总数 = 得电子总数、最小公倍数、系数比）。
- **FR-8**: 离子平衡面板渲染：粒子来源清单（电离/水解产生的粒子）+ 三大守恒式卡片（电荷/物料/质子，含表达式）。
- **FR-9**: 化学平衡面板渲染：推演表，每行一个条件改变因素（因素 / 改变 / 平衡移动方向 / K 变化 / 转化率变化）。
- **FR-10**: 无 `solveData` 时，`SolveModelPanel` 降级为 `GenericSolvePanel`，展示该分类的标准步骤与通用校验项，不崩溃。
- **FR-11**: 模型注册表导出 `getSolveModel(challenge)` / `getPanelKind(challenge)` / `getModelChecks(challenge)`，供 challenge 页及未来 solve/tutor 页复用。

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
- 电化学 / 无机推断 / 有机化学 3 类本轮只在注册表占位（`generic` 面板 + 专属步骤），专属面板留作后续迭代。

## Acceptance Criteria

### AC-1: 题型模型注册表完整
- **Type**: `rule`
- **Given**: 代码库存在 `miniapp/src/engine/solveModels.ts`
- **When**: 导入并遍历 `SOLVE_MODELS`（或等价注册表）
- **Then**: 6 个 `ChemCategory` 均有条目；氧化还原/离子平衡/化学平衡的 `panelKind` 分别为专属值（非 `generic`），且各有 ≥3 条标准解题步骤
- **Pass Condition**: 6 类全覆盖且 3 个代表类非 generic、步骤非空
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

## Open Questions
- [ ] 落地入口：用户对第二问选「其他」的具体意图？（当前默认：优先 challenge 讲解区 + 共享模型层）
- [ ] 电化学/无机推断/有机 3 类是否在下一轮立即推广专属面板？（当前默认：否，先验证 3 类模式）
