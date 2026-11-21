# 剩余 148 题：可靠性加固与引擎能力扩展 — 产品需求文档（PRD）

## Overview
- **Summary**：在 `precision=1 / harmCount=0 / LOSS=0` 红线下，针对权威基线 `checkpoint_ie_final`（gatewayCorrect=**609**，bothWrong=**148**，coverage=0.8045）中剩余的 148 道未解，优先落地**零假阳性风险的可靠性加固**（判分归一化、反应知识库覆盖补全），并完成已在工作树中建好但尚未验证的**晶胞求解闭环**，再评估新增**半反应配平器**等引擎能力。所有改动逐批经全题库护栏 + EvalRunner diff 验证；达不到红线的批次回退，不合并。
- **Purpose**：把"化学上已正确但因书写记号/知识库缺口未被识别"的假阴性转化为正确解；完成已建能力的验证兑现；严格区分"可系统化解"与"数据缺陷/建模域外"，诚实留痕。
- **Target Users**：CogniThink TutorBrain 高中化学求解引擎的维护者与使用者。

## Goals
- 扩展 `NormalizeChemicalNotation`：把 `==`/`→`/`⇌`/`\rightleftharpoons`/`─→` 等连接符与 LaTeX 化学记号**语义保持**地归一为可比较形态，让已有正确产出被正确识别。
- 补全 `data/knowledge/reactions/*.json` 覆盖：新增高中口径下产物唯一确定的反应（沉淀/置换/常见氧化还原等），每条经全题库唯一命中审计，由已建 `ReactionKbWriterSolver` 唯一命中门控消费。
- 完成晶胞求解闭环：`CrystalCellValidator`（已实现）+ 4 条 `type=crystal` 资产（已入库）→ 订正 pdf-170 答案键（官方源 page_059）、坐标核对、FullBankGuard、EvalRunner 验证，兑现 GAIN。
- 评估并实现 `HalfReactionBalancer`（半反应配平器）：对题干明示氧化/还原物种对与介质的电化学题，确定性配平电极半反应。
- 每批变更：全量测试 0 failed + EvalRunner 新 tag 全量 757，`precision=1 / harm=0 / LOSS=0`；产出终态覆盖报告与缺陷登记更新。

## Non-Goals
- **不**建装置图 / 表格 / 微观示意图 / 球棍模型消费引擎（假阳性风险高，继承前序结论）。
- **不**建通用产物推断配平器（产物不唯一时推断会假阳性）。
- **不**扩展 graph / curve / molecule 既有校验器（前序已审计无安全可扩展项）。
- **不**为追求覆盖率放宽"唯一判定才返回结论"的契约。
- **不**在本轮做多段填空题面/答案的批量重切片（高风险数据管道工作，列为后续单独 spike，不在 MVP 实现）。
- **不**做分子结构（官能团）资产抽取的批量落地（列为后续 spike，不在 MVP 实现）。
- **不**改动无官方源留痕的 `answer`；答案键订正必须附 `pdfs/_extract/answer/page_*.json` 溯源。

## Background & Context
- **权威基线（已逐 checkpoint 核实）**：`_evalscratch/checkpoint_ie_final_summary.txt`：total=757，gatewayCorrect=609，gatewayTruthCorrect=609，coverage=0.8045，precision=1，harmCount=0，bothWrong=148。全仓无 >609 的 checkpoint（`gatewayCorrect=6xx` 扫描确认）。
- **已落地（前序 `image-equation-engine-151` spec，本 spec 不重复）**：
  - `ReactionKbWriterSolver` 已实现并接线，交付 GAIN +3（pdf-038 / pdf-417 / pdf-477）。
  - `CrystalCellValidator` 已实现、`pdf_questions.json` 已录入 4 条 `type=crystal` 资产，但 **T7 eval 验证未完成**（无 >609 checkpoint），GAIN 尚未兑现；独立 spec 见 `.trae/specs/crystal-cell-validator/`。
- **归一化缺口（已核实代码）**：[NormalizeChemicalNotation](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L563-L588) 仅处理上下标字符与全角字符，**不**归一 `==`/`→`/`⇌`/`△`/`↑`/`↓`/LaTeX。缺陷登记册 G 类②（[data-defect-register.md L121](file:///d:/github/CogniThink-Chem/data-defect-register.md#L121)）精确锁定 5 道因此漏解：pdf-044（`==`）、pdf-532（`→`）、pdf-481（`→`）、pdf-484（`→`）、pdf-010（`⇌`+胶体）；登记册末尾（L130）已预判"统一归一化后方程引擎应能自解（奖励）"。
- **KB 覆盖缺口（已核实候选清单）**：[equation_unsolved_eqkw.tsv](file:///d:/github/CogniThink-Chem/_evalscratch/equation_unsolved_eqkw.tsv) 显示反应物锚定齐全、产物确定但 KB 缺失的题：pdf-161（CaS 沉淀）、pdf-174（MnO₂ 氧化亚硫酸盐）、pdf-278（CN⁻+CO₂）、pdf-532（卤素置换/高锰酸盐氧化）、pdf-164（Fe+Cu²⁺ 置换）等。
- **评分与采纳**：非 choice 走 `answerKeywords` 子串命中（经 `NormalizeChemicalNotation`，[L313/L349](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L309-L367)）；`concept_fact` 采纳门（[L636](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L636)）凡采纳必 `IsAnswerCorrect`，是新能力 precision=1 的根本保证。
- **构建/评估工具链（Windows PowerShell 5）**：改 data json 后必须 rebuild（数据复制到 bin）；EvalRunner 跳过已有 `.done` 标记的 tag，重跑须删 checkpoint + `.done` 或换新 tag；stale-DLL 仪式：杀 testhost → `dotnet build-server shutdown` → 清 src/tests bin/obj → clean rebuild。

## Functional Requirements
- **FR-0（基线锁定）**：在当前工作树复现绿色基线（全量测试 0 failed + EvalRunner 确认 609/148/precision=1/harm=0/LOSS=0），作为零回归比较基准。
- **FR-1（判分归一化扩展）**：扩展 `NormalizeChemicalNotation`，语义保持地把 `==`/`→`/`⇌`/`\rightleftharpoons`/`─→` 等连接符归一为 `=`；规范化 `△`/`↑`/`↓`/`催化剂` 等条件标记；展开常见 LaTeX 化学记号（`^{3+}`→`3+`、`_2`→`2`、`\rightleftharpoons`→`=`、`\uparrow`/`\downarrow`）。不得影响 √/× 序列、选项字母 A–D、数字的既有匹配。
- **FR-2（归一化零回归护栏）**：归一化改动后，全量 EvalRunner 重跑与 `ie_final` diff：`LOSS=0`（无 true→false 翻转）；全题库护栏测试断言凡非 null 引擎输出仍 `IsAnswerCorrect=true`。
- **FR-3（归一化学解锁探针）**：对 G 类②的 5 题 + 其他方程候选逐题探针，确认归一化后哪些题被已有方程引擎自解（输出为答案关键词子串）；仅解锁探针通过题，不通过的保持 `null` 并登记。
- **FR-4（反应知识库覆盖补全）**：向 `data/knowledge/reactions/*.json` 新增高中口径产物唯一确定的反应（每条含 `reactants[]`/`conditions[]`/`products[]`/配平 `equation`）。每条新增前必须全题库扫描：`ReactionKbWriterSolver` 对该条的命中在全 757 题中**唯一**且不破坏已判正题的触发集；不满足则不入库。
- **FR-5（KB 补全零回归护栏）**：KB 改动后，`ReactionLibraryTests`（含 `EveryEntry_PassesChemicalChecker`）0 failed；全题库护栏 + EvalRunner diff 确认 `precision=1 / harm=0 / LOSS=0`。
- **FR-6（晶胞闭环，接续已建工作）**：完成 `.trae/specs/crystal-cell-validator/` 未竟任务：订正 pdf-170 答案键（官方源 `page_059`，B→4）、坐标与渲染图核对、`FullBankGuard_Crystal` 全绿、EvalRunner 验证 pdf-206/222（+pdf-170）GAIN 且 `LOSS=0`；pdf-178 无官方源保持 0 GAIN。若验证中发现假阳性/坐标歧义，该题回退 `null`。
- **FR-7（半反应配平器，新能力）**：新建 `HalfReactionBalancer`：题干明示氧化/还原物种对 + 介质（酸/碱）可唯一解析时，按"原子→O 用 H₂O→H 用 H⁺/OH⁻→电荷用 e⁻"确定性配平；物种/介质不唯一 → `null`。接入走 `concept_fact` 采纳门。配 `FullBankGuard` 全题库护栏。
- **FR-8（缺陷登记 + 终态报告）**：更新 `data-defect-register.md`（本轮新解锁题移出未解、新排除题登记理由）；产出终态覆盖报告（基线 vs 终态 GAIN/LOSS/HARM、precision=1）。

## Non-Functional Requirements
- **NFR-1（零回归）**：任一变更后 TutorBrain / Chem 全量测试 0 failed，失败数不高于基线。
- **NFR-2（零假阳性）**：任一变更后 EvalRunner `precision=1`、`harmCount=0`；新增解全部与 ground truth 一致。
- **NFR-3（不蒙数）**：无法唯一判定时返回 `null`。
- **NFR-4（事实与逻辑分离）**：化学真值只在 JSON 知识库 / 结构化资产；逻辑层不硬编码零散化学事实。
- **NFR-5（零当前行为变化）**：域外返回 `null`，对既有已解 609 题判定零影响（LOSS=0 的根本保证）。
- **NFR-6（可追溯）**：每条新规则/新资产/每次归一化改动均有探针或 eval diff 证据落盘 `_evalscratch/`。
- **NFR-7（批次门控）**：每批改动若 EvalRunner `precision≠1` 或 `harm>0` 或 `LOSS>0` → 回退到上一绿色状态，该批不合并。

## Constraints
- **Technical**：Windows PowerShell 5（无 `&&`/`||`，用 `;` 或 `if ($LASTEXITCODE -eq 0)`）；.NET 8；`dotnet build -c Release` / `dotnet test`；EvalRunner 全量权威。
- **Business（红线，冻结）**：零假阳性 / 零 harm / 零回归；无唯一解返回 `null` 绝不蒙数；化学事实只存 JSON；答案键改动必须有官方源留痕。
- **Dependencies**：`GatewayVsLlmEvaluationService.NormalizeChemicalNotation`、`data/knowledge/reactions/*.json`、`ReactionKbWriterSolver`、`ReactionLibrary` / `EquationBalancer`（Chem.Core）、`CrystalCellValidator` + `pdf_questions.json` 晶胞资产、`data-defect-register.md`、`pdfs/_extract/answer/page_*.json`（官方源）。

## Assumptions
- `pdf_questions.json` 的 `answer`/`answerKeywords` 为 ground truth；`reactions/*.json` 条目已配平且产物正确。
- 归一化扩展是语义保持变换，只增匹配不减匹配；若 eval 显示 LOSS>0，则该改动回退（由 FR-2 护栏兜底，不依赖形式证明）。
- 晶胞 4 条资产坐标与渲染图一致（需 FR-6 核对确认；不一致则该题回退）。
- 半反应配平仅在题干明示物种对 + 介质时触发；电化学装置极性判断等推理不在范围。

## Acceptance Criteria

### AC-1: 基线锁定与零回归基准
- **Type**: `rule`
- **Given**: 当前工作树
- **When**: stale-DLL 仪式后 clean rebuild 全 sln，跑 TutorBrain / Chem 全量测试，再 EvalRunner 新 tag 重跑全量 757
- **Then**: 构建 0 error、测试 0 failed；EvalRunner summary `gatewayCorrect=609`、`precision=1`、`harmCount=0`
- **Pass Condition**: 测试退出码 0 且 failed=0；summary 数字与基线一致
- **Evidence**: 测试输出 + `_evalscratch/checkpoint_<tag>_summary.txt`

### AC-2: 归一化语义保持且零假阳性
- **Type**: `rule`
- **Given**: FR-1 扩展后的 `NormalizeChemicalNotation`
- **When**: 对全 757 题重跑 EvalRunner，并与 `ie_final` 做 id 级 `gatewayCorrect` diff
- **Then**: `LOSS=0`（无 true→false 翻转）；凡新增正确（false→true）其引擎输出经归一化后确为答案关键词子串；`precision=1`、`harmCount=0`
- **Pass Condition**: diff 报告 `LOST=0`、`precision=1`、`harm=0`
- **Evidence**: diff 报告 + 新增解清单与答案核对

### AC-3: KB 新增条目唯一命中安全
- **Type**: `rule`
- **Given**: FR-4 新增的反应条目
- **When**: 全题库扫描每条新增条目的 `ReactionKbWriterSolver` 命中情况
- **Then**: 每条新增在全 757 题中唯一命中且不破坏已判正题触发集；命中产出经归一化后为对应题答案子串
- **Pass Condition**: 全题库护栏测试通过；无条目在非目标题产生 adopted-but-wrong
- **Evidence**: 探针/护栏测试输出 + `ReactionLibraryTests` 0 failed

### AC-4: 晶胞闭环兑现 GAIN 且零回归（条件性）
- **Type**: `rule`
- **Given**: `CrystalCellValidator` 已建 + 4 条资产已入库
- **When**: 订正 pdf-170 键（page_059）、坐标核对后，FullBankGuard 全绿，EvalRunner 重跑
- **Then**: pdf-206 / pdf-222（及 pdf-170 若键订正通过）由 bothWrong 转正确；`LOSS=0`、`precision=1`、`harm=0`；pdf-178 保持 null（无官方源）
- **Pass Condition**: diff 显示上述题 GAIN 且无 LOSS；若任一题验证不通过则该题回退 null、不计 GAIN，但不得破坏红线
- **Evidence**: diff 报告 + 坐标核对记录 + FullBankGuard 输出

### AC-5: 半反应配平器窄门控与不蒙数
- **Type**: `rule`
- **Given**: FR-7 `HalfReactionBalancer`
- **When**: 题干物种对或介质无法唯一解析
- **Then**: 返回 `null`；可判时输出配平半反应且经归一化后为答案子串；全题库无 adopted-but-wrong
- **Pass Condition**: 守卫测试（不可判→null）+ FullBankGuard 全绿 + eval `precision=1/harm=0/LOSS=0`
- **Evidence**: 源码 + 守卫测试 + 护栏测试 + eval diff

### AC-6: 逻辑层不硬编码化学事实
- **Type**: `rule`
- **Given**: 本 spec 所有源码变更
- **When**: 审查新增/修改的求解器与归一化逻辑
- **Then**: 无硬编码零散化学事实常量；产物真值来自 `reactions/*.json` 或确定型反应规律；归一化仅做记号变换不含化学真值
- **Pass Condition**: grep/审查未发现逻辑层化学事实常量
- **Evidence**: 审查记录

### AC-7: 覆盖提升且全为正确解
- **Type**: `rubric`
- **Dimension**: 正确求解数净增量（仅计零假阳性的正确解）
- **Scale**: 1-5
- **Anchors**: 1 = 覆盖未提升或引入假阳性/回归；3 = 归一化 + KB 补全落地，净增 ≥ 3 且 precision=1/harm=0/LOSS=0；4 = 前述 + 晶胞闭环兑现，净增 ≥ 6 且无假阳性；5 = 前述 + 半反应配平器安全落地，净增 ≥ 8，全为正确解，排除理由登记完整
- **Pass Threshold**: >= 3
- **Evidence**: EvalRunner 基线 vs 终态 summary 对比（engineSolved 增量、precision=1、harm=0、LOSS=0）+ 覆盖报告

### AC-8: 缺陷登记与终态报告完整
- **Type**: `rule`
- **Given**: 本轮全部改动完成
- **When**: 收尾审查 `data-defect-register.md` 与终态覆盖报告
- **Then**: 新解锁题从未解移除；新排除题登记理由；终态报告含 GAIN/LOSS/HARM 与 precision=1 证据；`git diff` 确认无无官方源的 `answer` 改动
- **Pass Condition**: 登记册与报告存在且内容一致；diff 审查通过
- **Evidence**: 登记册 + 报告 + `git diff`

## Open Questions
- [ ] 归一化扩展是否会影响 √/× 序列或 choice 字母匹配？由 AC-2 全量 diff 实证裁决；若 LOSS>0 则回退该改动。
- [ ] 晶胞 pdf-170 官方源 page_059 是否确实给出 Ni²⁺ 个数=4？需在 FR-6 核对确认。
- [ ] 半反应配平器对 pdf-304/313/258 的介质解析是否可靠？由 AC-5 守卫 + FullBankGuard 裁决；不可靠则该题保持 null。
- [ ] 多段填空重切片、分子结构抽取是否纳入后续？本 spec 默认不在 MVP，列为后续 spike，需用户单独批准。
