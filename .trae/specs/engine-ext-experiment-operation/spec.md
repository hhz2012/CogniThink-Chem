# 引擎能力扩展·实验操作规范判定事实（pdf-416 / pdf-531）- Product Requirements Document

## Overview
- **Summary**：向 `data/highschool/experiment-facts.json` 追加 8 条**通用实验操作规范**事实（粗盐精制结晶方法选择 4 条 + 滴定分析操作正误 4 条），使 `pdf-416`（单选，经 `ExperimentChoiceValidator` 单库逐选项唯一求解）与 `pdf-531`（多选、无「双选」标记，经 `MultiSelectChoiceValidator` 全库遍历求解）在不蒙数、不做单题策展的前提下被引擎判正。**零 C# 行为改动**，仅数据 + 测试真值表同步。
- **Purpose**：在 55 道未解（`bothWrong`）中，分诊确认仅 `pdf-416`、`pdf-531` 两道为「题面与选项文本完整、answer 键自洽、无图像依赖」的真引擎缺口；解出这两道并保持全库零误伤，同时把可复用的实验操作真值沉淀进既有 `experiment-facts` 库，为后续同域题解锁打基础。
- **Target Users**：网关评估（`GatewayVsLlmEvaluationService`）与下游导学求解链路——拿到正确选项字母 + 可溯源推理节点。

## Goals
- `pdf-416` 由未解翻转为判正，引擎结论含独立字母 `C`。
- `pdf-531` 由未解翻转为判正，引擎结论含 `BC`。
- 零回归：相对基线 `snapshot_after_structperiodic`，`LOST=[]`、`harm=0`、`precision=1`，已判正题答案字节级不变。
- 新增事实均为**通用教材真值**（非只为匹配单题选项文本的硬凑条目）。

## Non-Goals
- 不改 `pdf_questions.json` 任何字段（`answer`/`answerKeywords`/`note`/`type`/`question` 等）。
- 不改任何 C# 校验器源码与 `CallChoice` 瀑布链顺序；既有单选/多选判定行为字节级不变。
- 不修复其余 53 道数据缺陷（answer 错配/图像依赖/填空非引擎域）——本批纯引擎数据扩展。
- 不做图像恢复轨道（pdf-255/303/325/406 等需读图的题不在范围）。
- 不把 `pdf-416`/`pdf-531` 之外的题强行拉入范围；探针若发现新事实误伤其它题立即回退该条。

## Background & Context
- 基线快照 `_evalscratch/snapshot_after_structperiodic.json`（上一批结构/周期律扩展完成后）：`gatewayCorrect` 含 701+ 判正题，`bothWrong=55`，`harm=0`，`precision=1`，`LOST=[]`。
- 55 道未解系统分诊（`_evalscratch/triage_full.py` + `probe_exp.py`，证据见 `_evalscratch/`）：
  - 真引擎缺口 2 道：`pdf-416`、`pdf-531`（题面文本完整、answer 自洽、无图依赖）。
  - 数据缺陷 ~12 道（answer 错配/冲突，如 pdf-075、pdf-263、pdf-180）。
  - 图像依赖 ~28 道（装置/曲线/结构图必需）。
  - 非引擎域 ~13 道（填空/判断/计算，无字母选项）。
- `pdf-416` 精确数据：choice，`answer=C`，题干已含文本化流程图 + 子问 (5)「一系列操作是指___(填字母)」；选项 A/B/C/D 为四种结晶操作组合；note 与独立化学推导一致（NaCl 溶解度随温度变化小→蒸发至大量晶体析出后趁热过滤，防 K 盐冷却结晶混入）。`images` 中的溶解度曲线图是给其它子问的，子问 (5) 为定性操作选择、不依赖读图。checkpoint：`gatewayCorrect=false`、`gatewayConclusionState=Insufficient`。
- `pdf-531` 精确数据：choice，`answer=BC`，题干为 Fe₃O₄ 制取 + 绿矾含量滴定背景，子问「下列滴定分析操作正确的是」；选项 A/B/C/D 为四条滴定操作陈述；note 与独立化学推导一致（KMnO₄ 用酸式滴定管、待测液酸式滴定管量取、接近终点冲洗内壁正确；变色后立即读数错误）。checkpoint：`gatewayCorrect=false`、`gatewayConclusionState=Insufficient`。
- 路由与判定语义（已核源码）：
  - `ExperimentChoiceValidator.TryDecide` → `FactChoiceMatcher.TryDecide(req, "experiment-facts", ...)`，**仅加载 experiment-facts 单库**：每选项须恰好命中 1 条事实（0 或 ≥2 → 整题 null）；问法方向可判；未标「双选/多选」按单选处理（候选须唯一）。pdf-416 走此路径解出。
  - `MultiSelectChoiceValidator.TryDecide` → `CompositeStatementJudge`（含 `FactBaseStatementJudge` → `FactChoiceMatcher.JudgeStatementWithIds`，**遍历全部 6 个事实库**）：每选项须被确定判定（任一 `Undecided` 或 verdict 冲突 → 整题 null）；满足问法方向的候选 ≥2 → 返回拼接字母。pdf-531（无「双选」标记、候选 BC=2）走此路径解出；其 `ExperimentChoiceValidator` 单库路径因候选不唯一安全返回 null，不抢道。
  - 瀑布链顺序（`LlmGatewayService.cs` L1338→L1424）：`ExperimentChoiceValidator` 先于 `MultiSelectChoiceValidator`，首个非空胜出 → pdf-416 被前者采纳、pdf-531 被后者采纳，互不干扰。
- 预飞探针（`_evalscratch/probe_exp2.py`，字节级复刻 C# `NormOpt`/`OptContains`/`WRONG`）结论：
  - 8 条 draft 事实在**全库**仅命中各自目标 (qid, option)，**零 stray-hit**（零误伤的构造保证）。
  - pdf-416：单库路径 → 候选 `[C]`，answer=C ✓。
  - pdf-531：单库路径安全返回 null → 全库多路径 → 候选 `[B,C]`，answer=BC ✓。
- 红线（用户确认，不变）：`precision=1`、`harm=0`、`LOST=[]`、不蒙数、不做单题策展、不改 `pdf_questions.json`。

## Functional Requirements
- **FR-1（追加实验操作事实）**：向 `data/highschool/experiment-facts.json` 追加 8 条事实（`EF-416-A/B/C/D` + `EF-531-A/B/C/D`），每条含 `id`/`match`/`stemMatch`/`verdict`/`reason`；`match` 为归一化后必须全部出现在选项中的子串，`stemMatch` 为必须全部出现在题干中的子串（用于限定到本题、防跨题误命中）；`verdict` 为独立化学核对后的选项陈述真伪。落库格式遵循既有约定（4 空格缩进单行、末条无尾逗号、插入前给原末条补尾逗号）。
- **FR-2（同步测试真值表）**：因 `ExperimentChoiceValidator` 将新采纳 `pdf-416`，须把 `["pdf-416"] = "C"` 加入 `Batch2ChoiceValidatorTests.ExperimentTruth`（满足 `Experiment_ZeroFalsePositive` 与 `Experiment_Coverage_MatchesTruthTable` 两门）；`pdf-531` 由 `MultiSelectChoiceValidator` 采纳、不进 `ExperimentTruth`，但在 `MultiSelectChoiceValidatorTests.NoMarkerVariant_SolvesMultiSelect_AndGradedCorrect` 增加 `[InlineData("pdf-531", "BC")]` 回归断言。
- **FR-3（构建与单测）**：`dotnet build` 通过；`Batch2ChoiceValidatorTests`、`Batch3ChoiceValidatorTests`、`MultiSelectChoiceMatcherTests`、`MultiSelectChoiceValidatorTests`、`tests\CogniThink.Chem.Tests` 全绿（已知与本批无关的 2 个 `MixedAnswer_*` 红测除外，见 playbook 第 13 条）。
- **FR-4（评估与回归 diff）**：跑 `run_eval_resume.ps1 -Tag <新tag> -Env Production` 生成新快照；与 `snapshot_after_structperiodic` diff，确认 `GAINED ⊇ {pdf-416, pdf-531}`、`LOST=[]`、`harm=0`、`precision=1`。
- **FR-5（登记册维护）**：更正 `data-defect-register.md` 中 `pdf-416` 的过时「子题无上下文 NO-GO」登记（重新评估为已解）；把本批分诊新发现、尚未登记的数据缺陷（pdf-180/198/333/377/528 等）补登进册。

## Non-Functional Requirements
- **NFR-1（零假阳性构造保证）**：每条新事实经探针验证在全库仅命中目标选项；若全量测试或 eval 发现任一新事实导致其它题被错误采纳，立即回退该事实。
- **NFR-2（零回归）**：`snapshot_after_structperiodic` 中已判正题的 `gatewayAnswer` 字节级不变；`LlmGatewayService.CallChoice` 及各校验器源码无行为性 diff。
- **NFR-3（无单题策展）**：新增事实的 `reason` 须写清通用化学原理；`match`/`stemMatch` 关键词为实验操作通用词汇（如「趁热过滤」「冷却结晶」「酸式滴定管」「变色后立即记录」），可跨题复用，而非硬编码 pdf-416/531 的题面唯一标识。
- **NFR-4（不破坏既有计数护栏）**：`FullBank_MultiSelect_Assertions_AreExactlySeven`（仅统计四个事实校验器、不含 `MultiSelectChoiceValidator`）保持 7；`FullBank_SingleSelect_Assertions_StaySingleLetter` 不被破坏；如全量测试出现因合法新增导致的计数断言变化，须以证据更新并说明。

## Constraints
- **Technical**：仅编辑 `data/highschool/experiment-facts.json`（增量 Edit 追加，禁止 `json.dump` 重写导致格式漂移）与两个测试文件；`match`/`stemMatch` 关键词须先经探针 `norm()` 核对码点（`℃`/`·`/全角等 NFKC 假象会致 C# 失配）。
- **Business**：不蒙数、不迎合未核答案键；两题 answer 均经独立化学推导核对（不盲信 note）。
- **Dependencies**：复用 `_evalscratch/probe_exp2.py`（落库前 TOTAL 误伤=0）、`_evalscratch/structperiodic_diff.py` 同方法做基线 diff、`_evalscratch/run_eval_resume.ps1` 评估入口；基线快照 `_evalscratch/snapshot_after_structperiodic.json`。

## Assumptions
- `pdf-416` 子问 (5) 的定性操作选择不依赖 `images` 中的溶解度曲线图（已逐字核对：选项为操作陈述，无需从图读数值）。
- `pdf-531` 选项的操作判断不依赖题干背景中的流程图（子问为通用滴定操作规范，已独立核对）。
- 追加事实若在 55 道未解中额外解出其它题，只要 `precision=1`、`harm=0` 即为合法收益；若引入 harm 立即回退。

## Acceptance Criteria

### AC-1: pdf-416 判正（单选路径）
- **Type**: `rule`
- **Given**: 基线 `snapshot_after_structperiodic`（pdf-416 ∈ bothWrong，`gatewayCorrect=false`）。
- **When**: 追加 `EF-416-A/B/C/D` 并 build，跑 eval 生成新快照。
- **Then**: 新快照中 pdf-416 `gatewayCorrect=true`，`gatewayAnswer`/`finalResult` 含独立字母 `C`，由 `ExperimentChoiceValidator` 采纳（路由首个非空为 Experiment）。
- **Pass Condition**: pdf-416 判正且引擎结论为 `C`。
- **Evidence**: 新快照 pdf-416 条目；`checkpoint_<tag>.jsonl` 中 `gatewayConclusion`/`gatewayAnswer`/`gatewayResultProvenance`。

### AC-2: pdf-531 判正（多选路径）
- **Type**: `rule`
- **Given**: 基线（pdf-531 ∈ bothWrong）。
- **When**: 追加 `EF-531-A/B/C/D` 并 build，跑 eval。
- **Then**: 新快照中 pdf-531 `gatewayCorrect=true`，引擎结论含 `BC`，由 `MultiSelectChoiceValidator` 采纳（`ExperimentChoiceValidator` 对其安全返回 null）。
- **Pass Condition**: pdf-531 判正且引擎结论为 `BC`。
- **Evidence**: 新快照 pdf-531 条目；checkpoint provenance。

### AC-3: 零回归红线
- **Type**: `rule`
- **Given**: 基线与新快照两份。
- **When**: 对比 `bothCorrectCases ∪ gatewayOnlyCorrectCases` 集合。
- **Then**: `LOST=[]`、`harm=0`、`precision=1`；`GAINED ⊇ {pdf-416, pdf-531}`；已判正题 `gatewayAnswer` 字节级不变。
- **Pass Condition**: LOST/harm 为空，precision 保持 1，收益含两题。
- **Evidence**: diff 脚本输出；新快照 `harmCount`/`enginePrecision` 字段。

### AC-4: 全库零假阳性与无路由二义
- **Type**: `rule`
- **Given**: 落库后的事实库。
- **When**: 跑 `Batch2ChoiceValidatorTests`（`Experiment_ZeroFalsePositive`/`Experiment_Coverage`/`Experiment_NoOverlap`）、`Batch3ChoiceValidatorTests`（`Concept_NoOverlap`）、`MultiSelectChoiceMatcherTests`（`FullBank_AllFactOutcomes_GradedCorrect_NoHarm` 等）、`MultiSelectChoiceValidatorTests`（`FullBank_AnyMultiSelectConclusion_GradedCorrect_NoHarm`）。
- **Then**: 全部通过；无任何题因新事实被错误采纳或产生路由二义。
- **Pass Condition**: 上述测试全绿。
- **Evidence**: `dotnet test` 输出。

### AC-5: 新事实通用性（非单题策展）
- **Type**: `rubric`
- **Dimension**: 新增事实是否为可跨题复用的教材通用化学真值，而非硬匹配单题的凑数条目。
- **Scale**: 1-5
- **Anchors**: 1 = 条目 match/stemMatch 直接复制单题整句选项、无化学原理；3 = 关键词取自选项但 reason 化学自洽、可想象同域题复用；5 = 通用规则表（操作词汇+判定方向），明显可跨题复用且 stemMatch 仅作题域限定。
- **Pass Threshold**: ≥ 4
- **Evidence**: `experiment-facts.json` 新增 8 条的 `match`/`reason` 人工审计；探针零 stray-hit 输出。

### AC-6: 登记册维护
- **Type**: `rule`
- **Given**: `data-defect-register.md`。
- **When**: 本批完成后。
- **Then**: pdf-416 过时「NO-GO」登记更正为「已解（实验操作事实）」；本批新发现未登记的数据缺陷补登（含 id、类别、不可解原因）。
- **Pass Condition**: 登记册与当前真实可解状态一致，无遗漏。
- **Evidence**: `data-defect-register.md` diff。

## Open Questions
- 无。方向已与用户确认（解 pdf-416+pdf-531，纯数据扩展）。
