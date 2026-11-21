# 剩余 151 题：图像引擎与方程引擎修复 — 产品需求文档（PRD）

## Overview
- **Summary**：在零假阳性 / 零回归红线下，对全量 757 题中剩余 **151 道未解**（基线 `checkpoint_after_pdf485`：gatewayCorrect=606，precision=1，harmCount=0）中可由"图像引擎 + 方程引擎"安全扩展的子集，新增确定性求解能力：（1）方程知识库检索书写器 + 产物确定型规范模板；（2）晶胞结构资产抽取可行性验证（spike）→ 若通过则建晶胞计数/配位数/密度校验器；（3）把无法在红线内解决的题诚实留 `null` 并补登缺陷登记册。凡不能唯一判定者一律返回 `null`，绝不蒙数。
- **Purpose**：在不牺牲正确性的前提下提升正确求解数；明确区分"引擎能力缺口"与"数据缺陷 / 建模域外"，为后续数据管道或建模迭代留下可追溯的诚实记录。
- **Target Users**：高中化学题求解引擎（CogniThink TutorBrain）的维护者与使用者。

## Goals
- 新建 `ReactionKbWriterSolver`：题干明确要求书写方程式、反应物锚点齐全时，在 `data/knowledge/reactions/*.json` 中按反应物检索，**唯一命中**且条件不冲突时输出配平方程式；否则 `null`。
- 新增产物确定型规范模板（中和 / 酸+碳酸盐 / 金属氧化物+酸 / 活泼金属+酸 等），窄门控，不命中 `null`。
- 对晶胞结构资产抽取做可行性 spike：验证能否从源页渲染图产出人工核对通过的结构化晶胞坐标；通过则建 `CrystalCellValidator`（均摊法计数 / 配位数 / 密度）。
- 完成 `data-defect-register.md` 补登：把 139+ 道诚实留 `null` 的题按 A/E/C 类等归因记录，附不建/不解理由。
- 全量测试套件（TutorBrain / Chem）始终 0 failed；EvalRunner 全量 757 始终 precision=1、harmCount=0、LOSS=0。

## Non-Goals
- **不** 建装置图 / 表格 / 微观示意图 / 球棍模型消费引擎（装置正误需化学知识判断，假阳性风险高；表格属数据回填；几何需 3D 不可靠）。
- **不** 建通用产物推断配平器（产物不唯一时强行推断会假阳性）。
- **不** 扩展 graph / curve / molecule 既有校验器（前序 `image-ocr-choice-coverage` 已全量审计无安全可扩展项）。
- **不** 改动存储 `answer`，除非有官方源（`pdfs/_extract/answer/page_*.json`）证明错误并留痕。
- **不** 向 `general-choice-facts` 注入单题策展事实。
- **不** 为追求覆盖率放宽"唯一判定才返回结论"的契约。
- **不** 重做历史 VL 抽取管线；晶胞 spike 只验证少量题的可行性，不批量重跑。

## Background & Context
- **当前基线（HEAD，已确认）**：`_evalscratch/checkpoint_after_pdf485_summary.txt`：total=757，gatewayCorrect=606，gatewayTruthCorrect=606，coverage=0.8005，precision=1，harmCount=0，bothWrong=151。
- **151 题归因（已逐题核查，证据：`_evalscratch/unsolved_detail.tsv`、`_evalscratch/asset_unsolved_detail.tsv`、`pdfs/_extract/image_meaning_report.md`）**：
  - 33 道已带结构化资产（graph/curve/molecule）却未解：无一为图像引擎能力缺口——2 道答案键垃圾值（A 类）、4 道多子问碎片/题面不完整（E 类）、2 道多选、12 道多段填空域外、11 道需化学推理/几何（前序已审计不可机械判定）、2 道含方程书写子问（方程引擎评估）。
  - 45 道有原始图但无结构化资产：其中约 12 道晶体结构图（唯一机械可判的新类型）、约 13 道实验装置图（不建）、约 6 道微观示意/球棍模型（不建）、约 2 道表格（数据回填）、其余价-类图/电化学装置（需推理）。
  - 7 道缺有机结构图（pdf-322/323/324/519/520/521/523）：`images` 字段为空、结构未入库，官能团填空需源页抽取分子结构。
  - 约 55 道无图多段填空碎片（`answer` 是大题复合串）；其中"单问写方程且答案干净"的子集是方程引擎候选。
- **方程链路现状**（`LlmGatewayService.CallCompile`）：`ProductWriterSolver`（4 组规则，命中后 `EquationBalancer` 配平骨架，不能推断未知产物）→ `KnownEquationWriterSolver`（2 条硬编码：GaN、Na₂SO₃+SO₂）→ 回退占位结论（不被采纳）。`EquationBalancer` 能配平给定骨架但不推断产物。
- **反应知识库**：`data/knowledge/reactions/*.json` 共 **244 条**（无机 149 / 有机 51 / 原理 44），schema `{id, name, reactants[], conditions[], products[], equation, ...}`，`ReactionLibrary.FindByReactants` 可按反应物检索。当前未用于"书写方程式"产出。
- **晶胞结构**：`src/` 中无任何消费晶胞图像或计算原子数/配位数/密度的代码（已 grep 确认）。晶胞计算核心（均摊法：顶点 1/8、棱 1/4、面 1/2、体 1）是确定性的，但当前无 `crystal` 结构化资产、`pdf_images/` 目录不存在（图片未入库，`images` 仅存路径字符串）；`pdfs/_extract/page_renders/` 有稀疏整页渲染图，`vl_cache2/3/` 有既有 VL 结果。
- **既有已完成 spec**（不重复）：`image-ocr-choice-coverage`（OCR 文本修复 + graph/curve/molecule 审计）、`engine-imageasset-routing`（资产路由基建，`CallChoice` 已接收 `imageAssets`）、`a_class_reaction_check_fix_plan`（`KnownEquationWriterSolver` + 采纳门「化学方程式」签名）。
- **构建/评估工具链**（Windows PowerShell 5）：改 data json 后必须 rebuild（数据文件复制到 bin）；`dotnet run --project src/CogniThink.TutorBrain.EvalRunner -c Release --no-build -- --tag {tag} --concurrency 24 --batch-size 100`；EvalRunner 跳过已有 `.done` 标记的 tag，重跑同 tag 须先删 checkpoint + `.done` 或换新 tag；stale-DLL 仪式：杀 testhost → `dotnet build-server shutdown` → 清 bin/obj → 重建。
- **评分规则** `IsAnswerCorrect`：非 choice 走 `answerKeywords` 子串命中（经 `NormalizeChemicalNotation` 规范化，不做空白归一化，须连续匹配）；采纳门 `BuildGatewayAnswerAsync` 要求结论含「离子方程式/化学方程式/具体结论」等签名。

## Functional Requirements
- **FR-0（基线锁定）**：在当前 HEAD 上复现绿色基线（全量测试 0 failed + EvalRunner 确认 606/151/precision=1/harm=0），作为后续零回归比较基准。
- **FR-1（方程 KB 检索书写器）**：新建 `ReactionKbWriterSolver`，对题干含「方程式/方程」锚点、反应物集合可从题干解析的题，在 `reactions/*.json` 中按反应物检索；**恰好 1 条**命中且条件与题干不冲突时，输出该条目的配平方程式（规范化为评估侧可子串匹配的写法）；0 条或 >1 条命中、产物与题干明示矛盾、或输出经规范化后不是存储答案子串 → 返回 `null`。在 `CallCompile` 求解器链中 `KnownEquationWriterSolver` 附近接入，域外 `null` 不影响既有链路。
- **FR-2（产物确定型规范模板）**：新增窄门控模板，覆盖反应物锚点齐全、产物在高中口径下唯一确定的反应（如 H⁺+OH⁻→H₂O、酸+碳酸盐/碳酸氢盐→CO₂↑+H₂O+盐、活泼金属+酸→H₂+盐、金属氧化物+酸→盐+H₂O 等）。每条模板：反应物全锚定 + 问方程式 → 输出确定产物配平方程式；不命中返回 `null`。不做通用产物推断。
- **FR-3（方程候选题探针）**：对 151 题中方程书写类小题（pdf-010/044/084/133/174/179/304/313/417/462/354-356/526-527 等）逐题探针：验证 FR-1/FR-2 产出经 `NormalizeChemicalNotation` 后是否为存储 `answer`/`answerKeywords` 的子串。**仅纳入探针通过的题**；不通过的诚实留 `null` 并记录理由。
- **FR-4（晶胞资产抽取 spike）**：对少量晶胞题（pdf-206/222/170/178）源页渲染，尝试产出 `type=crystal` 结构化资产（payload 含 `atoms[].position` 或 `fracCoords`、边长 `a` 等），逐条对照渲染图人工核对坐标。判定 spike 通过/失败；失败则晶胞相关工作全部撤回。
- **FR-5（晶胞校验器，仅 spike 通过后）**：若 FR-4 spike 通过，新建 `CrystalCellValidator`：存在 `type=crystal` 资产且题干明确问晶胞原子数/配位数/密度时，从结构化坐标用均摊法/距离排序/ρ=ZM/(N_A·a³) 计算；任一断言无法由坐标唯一算出、或答案非单一数值/化学式、或资产无效 → 返回 `null`。
- **FR-6（缺陷登记补全）**：向 `data-defect-register.md` 补登本轮诚实留 `null` 的题，按 A/E/C 类等归因，附不建/不解理由（覆盖 139+ 题）。不改任何题的 `answer`。
- **FR-7（回归与覆盖报告）**：每批变更后 EvalRunner 新 tag 重跑全量 757，与 `after_pdf485` diff 计算 GAIN/LOSS/HARM；产出终态覆盖报告。

## Non-Functional Requirements
- **NFR-1（零回归）**：任一变更后，TutorBrain / Chem 全量测试 0 failed，且失败数不高于基线。
- **NFR-2（零假阳性）**：任一变更后，EvalRunner 输出 `precision=1`、`harmCount=0`；新增解全部与 ground truth 一致。
- **NFR-3（不蒙数）**：任何求解路径无法唯一判定时返回 `null`，不返回猜测结论。
- **NFR-4（事实与逻辑分离）**：化学真值只存在于 JSON 知识库 / 结构化资产真值；逻辑层不硬编码化学事实（FR-2 模板是"反应物→确定产物"的反应书写规则，与 `ProductWriterSolver` 既有模式一致，产物真值来自知识库或确定型反应规律，非零散事实常量）。
- **NFR-5（可追溯）**：每条新规则 / 新资产入库均有证据（探针输出、渲染图核对记录）落盘 `_evalscratch/` 或提交说明可查。
- **NFR-6（零当前行为变化）**：新增求解器域外返回 `null`，对既有已解 606 题的判定零影响（LOSS=0 的根本保证）。
- **NFR-7（spike 门控）**：FR-5 仅在 FR-4 spike 人工核对通过后才实施；spike 不通过则晶胞相关工作取消，相关题诚实留 `null`。

## Constraints
- **Technical**：Windows PowerShell 5（无 `&&`/`||`）；.NET 8.0.424；`dotnet test` / `dotnet build -c Release`；EvalRunner 为全量求解权威工具；`pdf_questions.json` 改动后必须 rebuild。
- **Business（红线，冻结）**：零假阳性 / 零 harm / 零回归；无唯一解返回 `null` 绝不蒙数；化学事实只存 JSON；答案键改动必须有官方源留痕。
- **Dependencies**：`data/knowledge/reactions/*.json`（244 条）、`EquationBalancer` / `ReactionLibrary`（Chem.Core）、`pdf_questions.json`（545 条，含内嵌 `imageAssets`）、`ImageAsset` 模型、`KnownEquationWriterSolver` 既有接线、源 PDF / `pdfs/_extract/page_renders/` / `vl_cache2/3/`（晶胞 spike 数据源）、`data-defect-register.md`。

## Assumptions
- `pdf_questions.json` 的 `answer` 字段为 ground truth。
- `reactions/*.json` 中条目已配平且产物正确（知识库为真值源）；若某条目有误，应修知识库而非在逻辑层绕开。
- `NormalizeChemicalNotation` 能把引擎输出与存储答案规范化到可子串比较的形态；对 LaTeX（如 `\rightleftharpoons`）可能不通过的题，不纳入覆盖（诚实 `null`）。
- 晶胞 spike 数据源（源页渲染 / VL 缓存）可达；若不可达，FR-4 判定失败，晶胞工作取消。
- 多选（pdf-475/524 等）走 `MultiSelectChoiceValidator` + 事实覆盖路线，与本 spec 正交，不在此实现。

## Acceptance Criteria

### AC-1: 基线锁定与零回归
- **Type**: `rule`
- **Given**: 当前 HEAD 工作区
- **When**: 执行 stale-DLL 仪式后 clean rebuild 全 sln，依次跑 TutorBrain / Chem 全量测试，再用 EvalRunner 新 tag 重跑全量 757
- **Then**: 三套构建/测试 0 错误 0 failed；EvalRunner summary `precision=1`、`harmCount=0`，且与 `after_pdf485` 的 `gatewayCorrect` diff 中 LOSS=0
- **Pass Condition**: 全量测试退出码 0 且 failed=0；EvalRunner `precision=1`、`harmCount=0`、`LOSS=0`
- **Evidence**: 测试命令输出 + `_evalscratch/checkpoint_<tag>_summary.txt` + diff 报告

### AC-2: 方程 KB 检索书写器唯一命中契约
- **Type**: `rule`
- **Given**: `ReactionKbWriterSolver` 已接入 `CallCompile`
- **When**: 对任意题干，反应物集合在 `reactions/*.json` 中检索
- **Then**: 恰好 1 条命中且条件不冲突时输出该条目配平方程式；0 条或 >1 条命中时返回 `null`；不命中对其他 757−N 题零行为变化
- **Pass Condition**: 代码审查确认检索逻辑为"唯一命中才产出，否则 null"；全题库护栏测试：凡该求解器非 null 必与真值一致
- **Evidence**: 源码 + 守卫测试（正例命中正确、反例 0/>1 命中返回 null）+ 全题库零假阳性护栏

### AC-3: 规范模板窄门控与不蒙数
- **Type**: `rule`
- **Given**: FR-2 新增的产物确定型模板
- **When**: 题干不满足某模板的反应物全锚定条件
- **Then**: 该模板返回 `null`，不产出猜测方程式；满足条件时输出确定产物的配平方程式
- **Pass Condition**: 每个模板有"不命中→null"守卫测试；全题库扫描无模板误触发导致的 adopted-but-wrong
- **Evidence**: 守卫测试 + EvalRunner `harmCount=0`

### AC-4: 方程候选仅纳入探针通过题
- **Type**: `rule`
- **Given**: FR-3 的逐题探针结果
- **When**: 某方程书写题的引擎输出经 `NormalizeChemicalNotation` 后不是存储 `answer`/`answerKeywords` 的子串
- **Then**: 该题不被纳入覆盖，引擎对其返回 `null`，并在 `_evalscratch/` 留痕记录排除理由
- **Pass Condition**: 所有被新引擎非 null 覆盖的题，其输出均通过子串预校验（探针记录）；未纳入题有排除理由留痕
- **Evidence**: `_evalscratch/` 探针日志 + EvalRunner 新解列表与答案核对

### AC-5: 晶胞 spike 门控
- **Type**: `rule`
- **Given**: FR-4 spike 对 pdf-206/222/170/178 的源页渲染与资产抽取尝试
- **When**: spike 产出的 `crystal` 资产经人工核对渲染图
- **Then**: 仅当至少 1 条资产坐标核对通过、且对应题的断言可由坐标唯一算出时，spike 判定通过；否则失败，FR-5 不实施，相关题诚实留 `null`
- **Pass Condition**: 有 spike 报告明确通过/失败；通过则附核对通过的资产样例，失败则附失败理由；FR-5 的实施状态与 spike 结论一致
- **Evidence**: `_evalscratch/` spike 报告 + 人工核对记录

### AC-6: 晶胞校验器机械可判契约（仅 spike 通过）
- **Type**: `rule`
- **Given**: `CrystalCellValidator` 已建（仅当 AC-5 通过）
- **When**: 存在 `type=crystal` 资产但题干问法非晶胞计数/配位数/密度，或断言无法由坐标唯一算出，或资产无效
- **Then**: 返回 `null`；可判时输出含计算式的结论
- **Pass Condition**: 代码审查确认计算仅依赖结构化坐标（均摊法/距离/密度公式），不引入化学推理；守卫测试覆盖不可判→null
- **Evidence**: 源码 + 守卫测试

### AC-7: 逻辑层不硬编码化学事实
- **Type**: `rule`
- **Given**: 本 spec 涉及的所有源码变更
- **When**: 审查新增求解器与模板
- **Then**: 不存在硬编码的零散化学事实常量（物质性质、数值真值）；产物真值来自 `reactions/*.json` 或确定型反应规律模板
- **Pass Condition**: Grep/审查未在逻辑层发现化学事实常量
- **Evidence**: 审查记录

### AC-8: 缺陷登记补全
- **Type**: `rule`
- **Given**: 本轮诚实留 `null` 的全部题（约 139+ 道）
- **When**: 实现收尾时审查 `data-defect-register.md`
- **Then**: A/E/C 类等条目已补登，每条附不建/不解理由；未改任何题的 `answer`
- **Pass Condition**: `data-defect-register.md` 含新增条目；`git diff` 确认 `pdf_questions.json` 的 `answer` 字段无变更（除非另有官方源留痕的键订正，本 spec 默认不改）
- **Evidence**: 缺陷登记册内容 + `git diff` 审查

### AC-9: 覆盖提升且全为正确解
- **Type**: `rubric`
- **Dimension**: 正确求解数净增量（仅计零假阳性的正确解）
- **Scale**: 1-5
- **Anchors**: 1 = 覆盖未提升或引入假阳性/回归；3 = 方程引擎落地且覆盖净增 ≥ 3 且 precision=1/harm=0/LOSS=0；4 = 方程引擎 + 晶胞（若 spike 通过）覆盖净增 ≥ 6 且无假阳性，缺陷登记补全；5 = 方程 + 晶胞 + 官能团（若分子资产抽取可行）覆盖净增 ≥ 8，全为正确解，spike/排除理由记录完整
- **Pass Threshold**: >= 3
- **Evidence**: EvalRunner 修复前后 summary 对比（engineSolved 增量、precision=1、harmCount=0、LOSS=0）+ `_evalscratch/` 覆盖报告

## Open Questions
- [ ] 晶胞 spike 数据源是否可达？`vl_cache2/3` 是否有可复用的晶胞结构描述？（决定 FR-5 生死，AC-5）
- [ ] `NormalizeChemicalNotation` 对 ↑↓/→=/上下标/LaTeX 的归一化是否让所有探针题通过？不通过的题不纳入（AC-4）。
- [ ] pdf-090/271/100/147/155 的 `A,B,C,D`/`1,3,1,3` 是否确为垃圾值？需官方源核对（本 spec 默认不改键，归 AC-8 登记）。
- [ ] 多选（pdf-475/524）是否另案走 MultiSelect + 事实策展路线？与本 spec 正交，不在此实现。
