# 多部分大题子问分发（Multi-Part Sub-Question Dispatch）- 产品需求文档

## Overview
- **Summary**：为 `pdf_questions.json` 中「一条题录含 (1)(2)(3)… 异构子问（方程书写 / 选字母 / 计算 / 概念填空混合）」的大题，新建「拆子问 → 逐子问确定性路由 → 逐子问独立求解 → 全对才聚合采纳」的分发架构，让引擎能解出原本因单一 taskType 整体调度而返回 `null` 的多部分大题。
- **Purpose**：当前 27 道多部分大题（台账 [data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md) K.1 簇）全部 `gatewayCorrect=false`，根因不是数据缺陷，而是引擎无子问分发——整题被当作单一 taskType 调度，异构子问无法各归其求解器。本 spec 在不破坏 `precision=1 / harm=0 / LOST=0` 红线的前提下，补齐这层架构，并为后续 P1-3（域计算器）、P2-3（图像融合）解锁多部分大题上的增益创造前置条件。
- **Target Users**：化学题库与求解引擎维护者；本 spec 是 [engine-capability-gap-fix](file:///d:/github/CogniThink-Chem/.trae/specs/engine-capability-gap-fix/spec.md) 的后继（K.1 簇的接续）。

## Goals
1. 建立多部分大题的子问分发链路：检测 → 按 `(1)(2)…` / 答题空拆子问 → 逐子问 `ClassifyTaskDeterministic` 路由 → 逐子问复用现有求解器求解 → 聚合。
2. **precision=1 构造保证**：多部分大题仅当**全部子问都得出确定且与答案键一致的结论**时才采纳为正确（全对才聚合，任一子问不确定 → 整题返回 `null`），杜绝「答中一个子问就判整题对」的假阳性。
3. 复用现有求解器，**不新建**化学域计算器（纯计算子问留待 P1-3，图像子问留待 P2-3）；用现有求解器能全覆盖子问的多部分大题翻转为正确。
4. 全库零回归：757 题 eval 对绿色基线 `base_p0`（629 正确）LOST=0、precision=1、harm=0；2150 测试 0 failed。
5. 台账闭环：27 道多部分大题逐题有明确处置结论（解出 / 因子问缺计算器留痕 P1-3 / 因子问缺图像留痕 P2-3 / 保持 null），0 静默遗漏。

## Non-Goals
- **不新建纯计算域计算器**：滴定质量分数、键焓、pH 排序、Ksp 定量、盖斯数值等子问的计算求解器属 P1-3，本 spec 不建；含这类子问的多部分大题保持 `null` 并登记。
- **不做图像读数融合**：依赖装置/曲线图的子问属 P2-3；本 spec 不读图，含图像依赖子问的多部分大题保持 `null` 并登记。
- **不做自由文本简答/解释子问的求解**：「用平衡原理解释…」「目的是…」等开放解释子问无确定性求解器，本 spec 不蒙数，含这类子问的多部分大题保持 `null`。
- **不给部分分**：eval 判分对多部分大题采用「全对才判正」，不引入部分答对计数（precision=1 优先于覆盖率）。
- **不改题库数据**：不修改 `pdf_questions.json` 的题面 / 答案 / answerKeywords / options（数据已在 `verify-fix-all-suspects` 修对）。
- **不重开 LLM**：eval 始终 `ASPNETCORE_ENVIRONMENT=Production`（LLM 关闭），引擎是唯一真值源；子问拆分与路由均确定性（规则），不依赖 LLM。
- **不改 `MultiSelectChoiceValidator` / `FactChoiceMatcher` 的 WRONG 正则**；不把守恒近似接入 choice。

## Background & Context

### 基线证据（已验证，不可动摇）
- 绿色基线：`_evalscratch/checkpoint_base_p0.jsonl`（757 题，`ASPNETCORE_ENVIRONMENT=Production`，LLM 关闭）→ 解对 **629** / 未解对 **128**；**precision=1 / harmCount=0**。证据：[checkpoint_base_p0_summary.txt](file:///d:/github/CogniThink-Chem/_evalscratch/checkpoint_base_p0_summary.txt)。
- 单元测试基线：`dotnet test CogniThink.Chem.sln -c Release --no-restore` → **0 failed / 2150 passed**（TutorBrain 1237 + Chem 913）。任何修改不得破此基线。
- 当前真实基线 = `base_p0` = `t5_kb`（工作树已含 P1-1 改动，629 正确）。本 spec 的所有 diff 以 `base_p0` 为对照。

### 27 道多部分大题清单（台账 K.1）
逐题题面/子问构成见 [data-defect-register.md K.1](file:///d:/github/CogniThink-Chem/data-defect-register.md)：pdf-024, 025, 070, 098, 146, 156, 166, 167, 168, 169, 190, 207, 228, 244, 258, 260, 261, 262, 272, 275, 328, 418, 461, 464, 484, 506, 532。

共同根因（K.1 已诊断）：一条题录含多子问，各子问分属**方程书写 / 化学计算 / 概念解释 / 选字母 / 流程推断**等不同求解域，但引擎以单一 taskType 整体调度，无法把每个空格分发到对应求解器并聚合；部分子问还依赖流程/装置图（文本外数据）。**当前处置：保持 `null`。**

### 当前架构事实（逐文件核实）
- **主链路**：[LlmGatewayService.ProcessStudentMessageAsync](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) → `ParseStudentInputAsync` → `ClassifyTaskDeterministic` → `CallEngineAsync`。整段题干一次性传入，**无通用子问拆分**。
- **唯一现存的子问拆分**：[MultiSubCorrectnessSolver](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/MultiSubCorrectnessSolver.cs) 的 `SplitSubQuestions` 按答题空 `( )`/`（ ）` 切分，但被 `LooksLikeJudgmentQuestion` 严格门控——**仅处理「判断方程式正误填 √/×」题**（pdf-051/134/480 等），其余题直接返回空，由后续分支接管。其「聚合铁律」（任一小题不确定 → 整题 `null`，不输出部分答案）正是本 spec 要复用的 precision 范式。
- **判分语义**（[GatewayVsLlmEvaluationService.IsAnswerCorrect](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L309)）：非选择题对 `AnswerKeywords` 做 **OR 语义**（任一关键词子串命中即判正）。
- **答案拆分**（[SplitPdfAnswerKeywords](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L1818)）：多空答案按「换行 / 2+ 连续空格 / 逗号分号顿号」拆成多个关键词（如 pdf-024 `answer="A, 0.2, 19"` → `["A","0.2","19"]`）。
- **采纳门控**（[BuildGatewayAnswerAsync](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L631)）：按 taskType 逐分支采纳，无多部分分支；多部分大题通常落到 `IsConcreteConclusion`（需含「能/正确/共存」等判断词）→ 不命中 → 返回 `null`。

### 核心 precision 风险（本 spec 必须解决）
当前多部分大题 `gatewayAnswer` 为空（`null`），故 OR 语义不触发、无假阳性。**一旦子问分发开始产出答案**，OR 语义会成为假阳性来源：若引擎只答对子问 (2)（输出 `0.2`），`IsAnswerCorrect` 在 `["A","0.2","19"]` 中命中 `0.2` → 整题被判正（而子问 (1)(3) 未答对）→ **precision 跌破 1**。因此本 spec 强制：多部分大题的采纳与判分必须走 **AND 语义 / 全对聚合**，与 `MultiSubCorrectnessSolver` 的聚合铁律一致。

### 冻结红线（从父 spec 继承，不可破）
- `precision=1`：不得把原本正确的题判错；新增分发不得让任何题产生「部分答对被判整题对」的假阳性。
- `harm=0`：不删除/覆盖正确数据；引擎不采纳任何错误结论；不得清空正确 options。
- `LOST=0`：27 道多部分大题每题有明确处置结论，不静默遗漏。
- `不蒙数`：任一子问求解器无唯一解 → 该子问 `null` → 整题 `null`（全对才聚合），绝不输出部分答案。
- `answer` 字段永不当真值；真值 = 答案键 + 化学真值收敛。
- 化学事实**只存 JSON**（`data/highschool/*.json`），**不硬编码进 `.cs`**；分发/路由代码不含化学判定真值。
- 千问 API key 不进提交；用户未要求不主动 commit。

## Functional Requirements
- **FR-1（多部分检测）**：在网关主链路中，确定性检测一条题录是否为多部分大题（题干含 ≥2 个 `(N)` 子问编号，或 `SplitPdfAnswerKeywords` 拆分后 ≥2 个答案关键词，或 ≥2 个答题空）。检测为正则进入分发链路；**检测为非时，行为与当前 `base_p0` 字节级一致**（零回归保证）。
- **FR-2（子问拆分）**：将题干按 `(1)(2)(3)…` 编号（半角/全角括号，兼容 `(2024·北京卷)` 不被误切）拆为有序子问片段；每个子问片段携带其序号与对应答案关键词（按答案拆分顺序对齐）。拆分结果可追溯（输出子问数与 `(N)` 编号一致）。
- **FR-3（逐子问路由与求解）**：对每个子问片段独立调用 `ClassifyTaskDeterministic` 得到 taskType，并复用现有求解链路（与单题 `CallEngineAsync` 相同的求解器瀑布）得到该子问的确定结论或 `null`。**不新建求解器**；子问求解完全复用现有能力。
- **FR-4（全对聚合）**：仅当**全部子问**都得出非 `null` 且经答案键校验通过的结论时，聚合成整题结论（形如 `(1)… (2)… (3)…`）并进入采纳；任一子问为 `null` 或未通过校验 → 整题返回 `null`（不输出部分答案，不蒙数）。
- **FR-5（多部分判分 AND 语义）**：对多部分大题，`gatewayCorrect` 的判定要求**全部子问答案关键词均被命中**（AND），不得因单个子关键词命中而判正。该 AND 判定**仅作用于检测为多部分的题**，不改变非多部分题的现有 OR 判分（零回归）。
- **FR-6（采纳门控接入）**：在 `BuildGatewayAnswerAsync` 中为多部分结论增设采纳分支：仅当聚合结论通过 FR-5 的 AND 校验时采纳；否则 `null`。凡采纳必判正（precision=1 构造保证）。
- **FR-7（子问可追溯）**：整题结论中保留每个子问的来源（序号 + taskType + 结论），便于诊断与台账登记；运行时返回给学生的话术不泄露未采纳/不确定的子问结论。
- **FR-8（台账同步）**：27 道多部分大题逐题登记处置结论到 [data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md)（解出 / 因子问缺计算器→P1-3 / 因子问缺图像→P2-3 / 因子问为开放解释→保持 null），与 K.1 清单一一对应，0 静默遗漏。

## Non-Functional Requirements
- **NFR-1（precision=1）**：多部分大题无假阳性（无「部分答对判整题对」）；无已解对题被改判错。
- **NFR-2（harm=0）**：不删除/覆盖正确数据；引擎不采纳任何错误结论；不得清空正确 options。
- **NFR-3（LOST=0 / 零回归）**：757 题 eval 对 `base_p0` 逐案一致，LOST=0；非多部分题行为字节级不变。
- **NFR-4（不蒙数）**：子问无唯一解 → 整题 `null`；探针/校验不通过的子问不强行产出结论。
- **NFR-5（事实只存 JSON）**：分发/路由/聚合代码不含化学事实硬编码；不新增 `.cs` 化学真值。
- **NFR-6（opt-in 零回归）**：多部分分发仅对检测为多部分的题激活；非多部分题走原链路，确保 `base_p0` 的 629 道正确题一题不丢。
- **NFR-7（密钥不入库）**：千问 API key 不进入任何提交文件。

## Constraints
- **Technical**：Windows PowerShell 5（无 `&&`，用 `;`）；dotnet 8；含中文脚本/TSV/TXT 须 UTF-8 BOM 读取；eval 读数从 `AppContext.BaseDirectory/data/pdf_questions.json`（bin 输出），跑前 `Get-FileHash` 确认 bin 副本与源文件 hash 一致；Python 解释器在 `C:\Users\Administrator\AppData\Local\Programs\Python\Python312\python.exe`（PATH 中的 `python` 为 Windows Store 桩，不可用），权威 diff 用 `_evalscratch/verify/diff_ckpt.py`。
- **Business**：`precision=1 / harm=0 / LOST=0 / 不蒙数` 红线不可破；破则回退对应改动，任务退回 `in_progress`。
- **Dependencies**：现有求解器（equation_writer / choice 事实链 / judgment / balance / reaction_check 等）；`ClassifyTaskDeterministic` 规则链；eval 管线（EvalRunner + diff_eval.ps1 / diff_ckpt.py）；P1-1 已完成（避免并发改路由）。
- **Risks**：① 子问拆分误切（把 `(2024·北京卷)`、`[Cu(NH3)2]` 当子问）→ 必须复用 `MultiSubCorrectnessSolver.SubMarker` 的「编号后紧跟非空白非右括号不匹配」口径，并用 27 题拆分准确率回归；② AND 判分若误作用于非多部分题 → 回归；③ 逐子问调用求解器可能改变已解对题（若某已解题被误判为多部分）→ opt-in 检测 + 全库 757 diff 兜底。

## Assumptions
- 27 道 K.1 多部分大题的题面 `(N)` 编号与答案关键词顺序一一对应（数据已在 `verify-fix-all-suspects` 修对）。
- 现有求解器对部分多部分大题的子问可独立产出确定结论（方程书写 / choice 事实 / judgment 等已验证域）；含纯计算 / 图像 / 开放解释子问的题无法全对聚合，将诚实保持 `null` 并登记。
- eval 可在本环境以 `ASPNETCORE_ENVIRONMENT=Production`（LLM 关闭）运行。
- `MultiSubCorrectnessSolver` 的聚合铁律（全对才输出）与 `SubMarker` 拆分口径可复用/抽取为多部分分发的共享基础设施。

## Acceptance Criteria

### AC-1：多部分检测与子问拆分准确
- **Type**: `rule`
- **Given**: 27 道 K.1 多部分大题题干
- **When**: 运行多部分检测器与拆分器
- **Then**: 27 题均被识别为多部分；每题拆出的子问数与题干 `(N)` 最大编号一致（允许末尾无编号的连续子问按序补号），无误切出处分句（如 `(2024·北京卷)`、化学式括号）
- **Pass Condition**: 27/27 识别正确；子问数与 `(N)` 编号一致的题 ≥ 25/27（允许 ≤2 题因题面噪声存在边界情况，登记留痕）；无误切导致的假子问
- **Evidence**: 拆分器单元测试输出 + 27 题拆分清单（`_evalscratch/verify/mp_split_audit.txt`）

### AC-2：全对聚合与 AND 判分，零假阳性
- **Type**: `rule`
- **Given**: 多部分分发产出的整题结论
- **When**: 经 `BuildGatewayAnswerAsync` 采纳 + `IsAnswerCorrect` 判分
- **Then**: 一道多部分大题被判正 **当且仅当** 其全部子问结论均命中对应答案关键词（AND）；不存在「仅答中一个子问即判整题正」的情况
- **Pass Condition**: 对所有多部分大题，`gatewayCorrect=true` ⟹ 全部子问关键词命中（可用逐子问校验脚本证明）；新增采纳的多部分题 0 假阳性
- **Evidence**: 逐子问校验脚本（`_evalscratch/verify/mp_and_audit.py`）输出 + 探针

### AC-3：零回归（非多部分题字节级不变）
- **Type**: `rule`
- **Given**: 绿色基线 `base_p0`（629 正确）
- **When**: 全量 757 题 eval 产新 tag，与 `base_p0` 权威 diff
- **Then**: 非多部分题的 `gatewayCorrect` 状态逐案不变（LOST=0）；已解对的 629 题一题不丢；precision=1、harm=0
- **Pass Condition**: `diff_ckpt.py base_p0 <new>` 非多部分子集 LOST=0；全库 LOST=0；summary precision=1 / harm=0
- **Evidence**: `checkpoint_<tag>_summary.txt` + `diff_ckpt.py` 输出

### AC-4：分发骨架交付（据实校准：≥0 即时增益）
- **Type**: `rule`
- **Given**: 27 道多部分大题
- **When**: 分发链路激活，逐子问复用现有求解器求解并全对聚合
- **Then**: 含纯计算/图像/开放解释子问、无法全对聚合的题保持 `null` 并登记，不蒙数；现有求解器可全覆盖子问的题（若有）翻转为 `correct=true && truthAdopted=true`，且每题全部子问均经答案键校验通过
- **Pass Condition**: 无法全对的题 0 道被强行判正（precision=1）；diff GAINED ≥ 0（**据实校准为 ≥0**：T0 + P1-3 就位后重审均确认 0/27 可由现有求解器全覆盖，见 CQ-2）；若后续 P1-4/P2-3 解锁子问，分发骨架可在不动判分门的前提下产出增益
- **Evidence**: checkpoint 对应记录 + [p12_reaudit.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p12_reaudit.txt) + [p12_check_states.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p12_check_states.py)
- **校准记录**：T0 审计 0/27 全覆盖 → CQ-2；P1-3（水解 pH / 氧还比例计算器）就位后重审仍 0/27（无一子问命中水解 pH / 氧还比例签名，阻塞子问为自由文本解释 / 图像依赖 / 热化学·Ksp·滴定等其他计算域）→ AC-4 据实校准为 ≥0；本项交付纯基础设施，零即时增益、零回归。

### AC-5：台账闭环与 LOST=0 留痕
- **Type**: `rubric`
- **Dimension**: 27 道多部分大题处置可追溯性与无遗漏
- **Scale**: 1-5
- **Anchors**: 1 = 仅说「解不了」无明细；3 = 有分类但缺逐题登记；5 = 27 道逐题有结论（解出 / 缺计算器→P1-3 / 缺图像→P2-3 / 开放解释→保持 null），与 K.1 清单一一对应，0 静默遗漏
- **Pass Threshold**: >= 4
- **Evidence**: `data-defect-register.md` 多部分大题处置更新 + 处置清单与 K.1 id 集合比对脚本输出

### AC-6：红线与代码纪律
- **Type**: `rule`
- **Given**: 本 spec 全部改动
- **When**: `dotnet test` + `git diff` 审查
- **Then**: 测试 0 failed；无千问 API key 明文；无化学事实硬编码进 `.cs`；未改 WRONG 正则；未把守恒近似接入 choice；分发代码 opt-in（非多部分题不激活）
- **Pass Condition**: `dotnet test` failed=0；grep 密钥片段 0 命中（本 spec 改动文件）；新增 `.cs` 仅含拆分/路由/聚合/判分逻辑，无化学方程/真值硬编码
- **Evidence**: 测试汇总 + grep 输出 + git diff 审查

## Open Questions
- [x] AC-4 的增益目标：T0 + P1-3 就位后重审均确认 **0/27** 可由现有求解器全覆盖子问 → AC-4 据实校准为 **≥0**（CQ-2 选 A，已用户确认）。分发骨架为 P1-4/P2-3 解锁增益的前置，本身零即时增益。
- [ ] 子问与答案关键词的对齐方式：按 `(N)` 编号对齐，还是按答案拆分顺序对齐？题面 `(N)` 与答案 `(N)` 是否恒一致？规划阶段需抽样核实（如 pdf-024 答案无编号，靠顺序对齐）。
- [ ] 多部分检测的 opt-in 条件（≥2 `(N)` 编号 / ≥2 答案关键词 / ≥2 答题空，三者满足其一）是否会误捕获已解对的非多部分题？规划阶段需全库扫描确认 0 误捕获，否则收紧条件。
- [ ] 是否将 `MultiSubCorrectnessSolver` 的 `SubMarker` / `SplitSubQuestions` 抽取为共享组件供新分发器复用，避免两份拆分逻辑漂移？（建议抽取，但属实现细节，不影响 AC。）
