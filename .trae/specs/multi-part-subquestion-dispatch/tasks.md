# 多部分大题子问分发（Multi-Part Sub-Question Dispatch）- 实施任务清单

> **Baseline (不可动摇)**：`base_p0`（[summary](file:///d:/github/CogniThink-Chem/_evalscratch/checkpoint_base_p0_summary.txt)）：757 题，gatewayCorrect=629 / 未解对=128，precision=1 / harm=0。测试基线 0 failed / 2150 passed。
> **红线（冻结）**：precision=1 / harm=0 / LOST=0 / 不蒙数 / 事实只存 JSON / answer 不当真值 / 密钥不入库 / 分发 opt-in（非多部分题字节级不变）。
> **Spec 文档**：[spec.md](file:///d:/github/CogniThink-Chem/.trae/specs/multi-part-subquestion-dispatch/spec.md)。
> **范围来源**：台账 [data-defect-register.md K.1](file:///d:/github/CogniThink-Chem/data-defect-register.md)（27 道多部分大题）。

## Overview

### 任务表

| ID | Task | AC 映射 | 类型 | 优先级 | 依赖 | 状态 |
|----|------|---------|------|--------|------|------|
| T0 | 前置审计：现有求解器逐子问命中率 + 子问/答案对齐核实（只读，校准 AC-4 与拆分设计） | AC-1, AC-4 | 调研 | high | 无 | completed |
| T1 | 多部分检测 + 通用子问拆分器（抽取复用 MultiSubCorrectnessSolver 拆分口径） | AC-1 | 代码+测试 | high | T0 | completed |
| T2 | 逐子问路由与求解（复用 ClassifyTaskDeterministic + 现有求解链路，不新建求解器） | AC-1, AC-4 | 代码 | high | T1 | completed |
| T3 | 全对聚合 + 采纳门控分支（任一子问 null/未过校验 → 整题 null） | AC-2 | 代码 | high | T2 | completed |
| T4 | 多部分 AND 判分（仅作用于多部分题，非多部分题 OR 语义字节级不变） | AC-2, AC-3 | 代码+测试 | high | T3 | completed |
| T5 | 全量回归验证（757 eval vs base_p0：LOST=0 / precision=1 / harm=0；dotnet test 0 failed） | AC-3, AC-4, AC-6 | 验证 | high | T3, T4 | completed |
| T6 | 台账闭环（27 道逐题处置结论 + 与 K.1 id 集合比对，0 遗漏） | AC-5 | 文档 | medium | T5 | completed |
| T7 | 红线审查（密钥 / .cs 无化学硬编码 / WRONG 正则未改 / opt-in 无回归） | AC-6 | 验证 | medium | T5 | completed |
| T8 | 独立 Review 门禁（fresh-context 只读子代理） | 全部 | 评审 | high | T5, T6, T7 | completed |

### 执行顺序
1. **T0**（只读审计，校准目标与设计）→ 2. **T1**（拆分器，带单元测试）→ 3. **T2**（逐子问求解）→ 4. **T3**（全对聚合 + 采纳）→ 5. **T4**（AND 判分）→ 6. **T5**（全量回归，决定能否进入 Review）→ 7. **T6**（台账）+ **T7**（红线）→ 8. **T8**（独立 Review）。

---

## T0: 前置审计（只读，校准 AC-4 与拆分设计）

- **Status**: `completed`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 只读审计 27 道 K.1 多部分大题，回答两个决定设计与目标的问题：
    1. **现有求解器逐子问命中率**：把每题按 `(N)` 拆子问后，逐子问跑现有 `ClassifyTaskDeterministic` + 求解链路（可用现有诊断脚本或一次性只读脚本），统计「整题全部子问都能得出非 null 确定结论」的题有几道。这决定 AC-4 的真实可达目标（若 ≥2 则维持，若仅 1 则据实下调并登记，绝不蒙数）。
    2. **子问与答案关键词对齐**：核实题面 `(N)` 编号与 `SplitPdfAnswerKeywords` 拆分后的答案关键词是否一一对应（按编号 or 按顺序）；记录例外（如 pdf-024 答案无编号靠顺序对齐）。
    3. **opt-in 误捕获扫描**：全库 757 题跑多部分检测器（≥2 `(N)` / ≥2 答案关键词 / ≥2 答题空），确认 0 道已解对的非多部分题被误捕获（否则收紧检测条件）。
  - 不修改任何源码；产出审计报告供 T1–T4 设计与 AC-4 校准。
- **Acceptance Criteria Addressed**: AC-1, AC-4
- **Test Requirements**:
  - `rule` TR-0.1: 27 题逐子问命中率审计完成，输出「全子问可解」题数清单，作为 AC-4 目标校准依据。
  - `rule` TR-0.2: 全库 757 题多部分检测扫描，输出误捕获清单（应为空，或全部为未解对题）；若有已解对题被误捕获，记录并要求 T1 收紧条件。
  - `rule` TR-0.3: 子问/答案对齐核实完成，记录对齐策略（编号 or 顺序）与例外清单。
- **Completion Evidence**:
  - TR-0.1 → [mp_split_audit.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/mp_split_audit.txt)：逐子问域分类（启发式，保守）。**关键结论：27 道中 0 道的全部子问都可由现有求解器解出**——每题至少含 1 个需纯计算(P1-3)/图像(P2-3)/开放解释(无求解器)的子问。抽样核实 pdf-156/461/484/532 真实题面确认启发式无误（如 pdf-461 子问(1)(2)方程可解，但(3)(4)需流程/图像推理）。→ **AC-4(≥2) 在 P1-2 单独范围内不可达**，需用户决策（见 CQ-2）。
  - TR-0.2 → [mp_false_capture.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/mp_false_capture.txt)：广谱检测器（marker≥2 / kw≥2 / blanks≥2）误捕获 **57 道当前已解对题**（含 P1-1 增益 pdf-044 等），若激活分发会造成 LOST 回归。**设计修正（CQ-2）**：分发仅在「整题求解返回 null」时作为 fallback 激活，天然保证 629 道已解对题零回归。
  - TR-0.3：仅 6/27 满足 subs==answer_kw==marker_max；多数题 answer 为含 `(1)...(2)...` 的单字符串（kw=1），子问/答案对齐需按编号解析而非单纯顺序；例外清单见 mp_split_audit.txt。
  - 基线复核：重跑 `base_now` 与 `base_p0` 权威 diff → GAINED=0 / LOST=0 / harm=0，基线 757 题 / 629 正确 / precision=1 稳定可复现。（注：eval 实际加载 757 = 103 内置基准用例 + pdf 545 + tier2 109；T0 审计脚本只读了 pdf+tier2=654，内置 103 均为非多部分基准题，不影响 T0 结论。）

---

## T1: 多部分检测 + 通用子问拆分器

- **Status**: `completed`
- **Priority**: high
- **Depends On**: T0
- **Description**:
  - 抽取/复用 [MultiSubCorrectnessSolver](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/MultiSubCorrectnessSolver.cs) 的 `SubMarker` 拆分口径（`[（(]\s*(\d+)\s*[）)]`，编号后紧跟非空白非右括号不匹配，避免误切 `(2024·北京卷)` / 化学式括号），泛化为**通用子问拆分器**（不再门控 `LooksLikeJudgmentQuestion`）。
  - 新增**多部分检测器**（opt-in）：题干含 ≥2 个 `(N)` 子问编号，或 `SplitPdfAnswerKeywords` 拆分后 ≥2 个答案关键词，或 ≥2 个答题空 → 判定为多部分。检测为非时**不激活分发**，走原链路（NFR-6 零回归）。
  - 拆分结果为有序子问片段列表，每个片段携带序号与文本；末尾无编号子问按序补号（沿用 `SplitSubQuestions` 的补号逻辑）。
  - 单元测试覆盖：27 道 K.1 题的拆分数与 `(N)` 最大编号一致；不误切出处分句；非多部分题检测器返回 false。
- **Acceptance Criteria Addressed**: AC-1
- **Test Requirements**:
  - `rule` TR-1.1: 27/27 K.1 题被识别为多部分；子问数与 `(N)` 编号一致 ≥25/27，边界情况登记留痕（AC-1）。
  - `rule` TR-1.2: 无误切：`(2024·北京卷)`、`[Cu(NH3)2]`、`(FeSO4·7H2O)` 等不产生假子问（单元测试断言）。
  - `rule` TR-1.3: opt-in 零误捕获：T0 审计中已解对的非多部分题，检测器返回 false（TR-0.2 清单为空时本规则 trivially 通过）。
  - `rule` TR-1.4: 拆分器对非多部分题返回空列表，不改变原链路行为。

---

## T2: 逐子问路由与求解（复用现有求解器，不新建）

- **Status**: `completed`
- **Priority**: high
- **Depends On**: T1
- **Description**:
  - 对拆分后的每个子问片段，独立调用 `ClassifyTaskDeterministic` 得到 taskType，并复用**与单题 `CallEngineAsync` 相同的求解器瀑布**得到该子问的确定结论（`EngineResponse.Ok=true` 且有 `FinalResult`）或 `null`。
  - **不新建任何化学求解器**；子问求解完全复用现有能力（equation_writer / choice 事实链 / judgment / balance / reaction_check 等）。
  - 子问求解保留可追溯信息（序号 + taskType + 结论 / null），供 T3 聚合与诊断。
  - 纯计算 / 图像 / 开放解释子问现有求解器返回 `null` 属预期，T3 会因此整题返回 `null`（不蒙数）。
- **Acceptance Criteria Addressed**: AC-1, AC-4
- **Test Requirements**:
  - `rule` TR-2.1: 每个子问独立路由与求解，产出 `(序号, taskType, 结论|null)` 序列；不新建求解器（grep 确认无新增 `*Solver` 化学逻辑类）。
  - `rule` TR-2.2: 子问求解失败（`null`）不抛异常、不拖垮网关链路（保守回退）。

---

## T3: 全对聚合 + 采纳门控分支

- **Status**: `completed`
- **Priority**: high
- **Depends On**: T2
- **Description**:
  - 实现「全对聚合」铁律（复用 `MultiSubCorrectnessSolver` 聚合范式）：仅当**全部子问**都得出非 `null` 结论时，聚合成整题 `FinalResult`（形如 `(1)… (2)… (3)…`）；任一子问为 `null` → 整题返回 `null`（不输出部分答案，不蒙数）。
  - 在 [BuildGatewayAnswerAsync](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L631) 增设多部分采纳分支：仅当聚合结论通过 T4 的 AND 校验时采纳；否则 `null`。凡采纳必判正（precision=1 构造保证）。
  - 未采纳/不确定的子问结论不进入返回给学生的话术（不泄露）。
- **Acceptance Criteria Addressed**: AC-2
- **Test Requirements**:
  - `rule` TR-3.1: 任一子问 `null` → 整题 `FinalResult` 为 `null` 且不被采纳（单元测试：构造 2/3 子问可解、1 子问 null → 整题 null）。
  - `rule` TR-3.2: 全部子问可解 → 聚合结论含全部子问序号与结论。
  - `rule` TR-3.3: 采纳分支仅放行通过 AND 校验的聚合结论（与 T4 联测）。

---

## T4: 多部分 AND 判分（仅作用于多部分题）

- **Status**: `completed`
- **Priority**: high
- **Depends On**: T3
- **Description**:
  - 在 `IsAnswerCorrect` / 判分链路中，为**检测为多部分的题**采用 AND 语义：`gatewayCorrect=true` 当且仅当**全部**子问答案关键词均被聚合结论命中（按子问对齐）。
  - **严格作用域**：仅多部分题走 AND；非多部分题保持现有 OR 语义字节级不变（NFR-3 / AC-3 零回归）。通过「题是否被检测器判定为多部分」来选择判分分支，避免全局改动。
  - 单元测试：多部分题仅命中部分关键词 → false；全部命中 → true；非多部分题 OR 行为不变（用现有 `IsAnswerCorrect` 测试集回归）。
- **Acceptance Criteria Addressed**: AC-2, AC-3
- **Test Requirements**:
  - `rule` TR-4.1: 多部分题 AND 判分：部分命中 → false，全部命中 → true（单元测试覆盖 pdf-024 类 `["A","0.2","19"]`）。
  - `rule` TR-4.2: 非多部分题判分字节级不变：现有 `IsAnswerCorrect` 相关单元测试全部通过（0 回归）。
  - `rule` TR-4.3: 逐子问校验脚本 `mp_and_audit.py`：对所有 `gatewayCorrect=true` 的多部分题，证明全部子问关键词命中（AC-2 证据）。

---

## T5: 全量回归验证

- **Status**: `completed`
- **Priority**: high
- **Depends On**: T3, T4
- **Description**:
  - `dotnet build -c Release` → `dotnet test CogniThink.Chem.sln -c Release --no-restore` → 0 failed。
  - 跑前 `Get-FileHash` 确认 bin 下 `pdf_questions.json` / 事实库与源文件 hash 一致（防 stale-DLL）。
  - `$env:ASPNETCORE_ENVIRONMENT='Production'; dotnet run --project src/CogniThink.TutorBrain.EvalRunner -c Release --no-build -- --tag <mp_dispatch>`（757 题，LLM off）。
  - 权威 diff：`python _evalscratch/verify/diff_ckpt.py base_p0 mp_dispatch` → 确认 LOST=0、precision=1、harm=0；新增 GAINED 仅限多部分域且满足 AND 全对。
  - 若 LOST≠0 或出现假阳性 → 回退对应改动，任务退回 `in_progress`。
- **Acceptance Criteria Addressed**: AC-3, AC-4, AC-6
- **Test Requirements**:
  - `rule` TR-5.1: `dotnet test` 0 failed（≥2150 passed）。
  - `rule` TR-5.2: 全库 eval vs `base_p0`：LOST=0、precision=1、harm=0（`diff_ckpt.py` 权威输出，不信 summary）。
  - `rule` TR-5.3: 非多部分题逐案不变（diff 中非多部分子集 LOST=0 / GAINED=0）。
  - `rule` TR-5.4: 新增多部分 GAINED ≥ AC-4 目标（T0 校准后的值），且每题通过 TR-4.3 的 AND 审计。

---

## T6: 台账闭环

- **Status**: `completed`
- **Priority**: medium
- **Depends On**: T5
- **Description**:
  - 更新 [data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md)：27 道多部分大题逐题登记处置结论——解出（列出落库/关键改动）/ 因子问缺计算器→P1-3 / 因子问缺图像→P2-3 / 因子问为开放解释→保持 null。
  - 写比对脚本：K.1 的 27 个 id 集合 = 解出 ∪ 留痕各簇，差集为空（0 静默遗漏）。
- **Acceptance Criteria Addressed**: AC-5
- **Test Requirements**:
  - `rubric` TR-6.1: 27 道处置可追溯性；scale 1-5；anchors 1/3/5 同 AC-5；threshold ≥4；evidence = 台账更新 + 集合比对脚本输出（差集为空）。
  - `rule` TR-6.2: 集合比对脚本输出差集为空（27 = 解出 ∪ 各留痕簇，无重叠无遗漏）。

---

## T7: 红线审查

- **Status**: `completed`
- **Priority**: medium
- **Depends On**: T5
- **Description**:
  - 密钥扫描：本 spec 改动文件 `git grep` 千问 API key 片段 → 0 命中（仓库预存 `ocr_test.py` 旧密钥属 CQ-1 历史债务，非本 spec 引入，不阻塞）。
  - `.cs` 审查：新增/修改代码仅含拆分/路由/聚合/判分逻辑，无化学方程/真值硬编码（事实只存 JSON）。
  - WRONG 正则未改；守恒近似未接入 choice。
  - opt-in 确认：非多部分题不激活分发（代码路径审查 + TR-5.3 实证）。
- **Acceptance Criteria Addressed**: AC-6
- **Test Requirements**:
  - `rule` TR-7.1: 本 spec 改动文件密钥 grep 0 命中。
  - `rule` TR-7.2: 新增 `.cs` 无化学事实硬编码（grep 审查 + 人工核对）。
  - `rule` TR-7.3: WRONG 正则未改、守恒近似未接入 choice（git diff 审查）。

---

## T8: 独立 Review 门禁

- **Status**: `completed`
- **Priority**: high
- **Depends On**: T5, T6, T7
- **Description**:
  - 委托 **fresh-context、只读** 的子代理审查本 spec 全部改动（拆分/路由/聚合/判分 + 台账），产出 `review.md`，裁决是否 PASS。
  - 审查维度（每项 PASS/FAIL + 证据）：① precision=1（无假阳性，AND 全对）；② harm=0；③ 无回归（LOST=0，非多部分字节级不变）；④ 不蒙数（部分子问可解不输出部分答案）；⑤ 事实只存 JSON（无 `.cs` 化学硬编码）；⑥ 台账自洽（27 id 无遗漏）；⑦ 红线（密钥 / WRONG 正则 / opt-in）。
  - 约束：子代理只读，不得改任何文件；审查者不实现、实现者不终审。
- **Acceptance Criteria Addressed**: 全部
- **Test Requirements**:
  - `rubric` TR-8.1: 独立 Review 裁决；scale 1-5；anchors 1=多维度 FAIL 有精度/伤害风险；3=部分存疑需修复；5=全部 PASS 证据可追溯；threshold ≥4（无 precision/harm FAIL）；evidence = `review.md`。
  - `rule` TR-8.2: `review.md` 存在且含 7 维度逐项裁决 + 证据；任一维度 FAIL → 对应任务退回修复，Review 重新触发。

---

## Change Queue（评审问题）

> 实现过程中发现的任何偏差、风险或新增需求，登记到下表并回链到受影响任务与 AC；不得静默扩大范围或降低验收标准。

| ID | 任务 | 问题描述 | 影响的 AC | 状态 | 决议 |
|----|------|----------|-----------|------|------|
| CQ-1（继承） | 红线 | 仓库预存 [ocr_test.py](file:///d:/github/CogniThink-Chem/ocr_test.py) 密钥泄漏（旧提交引入，非本 spec 改动），已在 engine-capability-gap-fix T9 登记；本 spec 不引入新密钥 | AC-6 | 已登记（不阻塞） | 沿用前序 CQ-1 处置：用户侧轮换 key + 决定是否改写 git 历史 |
| CQ-2 | T0/AC-4 | 前置审计发现：① 27 道多部分大题中 **0 道**可由现有求解器全覆盖（每题含 ≥1 需纯计算(P1-3)/图像(P2-3)/开放解释的子问），故 **AC-4(≥2 增益) 在 P1-2 范围内不可达**；② 广谱多部分检测器误捕获 **57 道已解对题**（含 P1-1 增益 pdf-044），若激活分发会 LOST 回归 → 设计修正为「分发仅在整题求解返回 null 时 fallback 激活」（天然零回归） | AC-4, AC-3 | 已决策 | 选 (A)+(C)：先做 P1-3 拿单题即时增益（已完成，trap-limit-2 GAIN +1，P1-3 独立 review PASS）；P1-3 就位后重审仍 0/27 可全解（证据 [p12_reaudit.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p12_reaudit.txt) / [p12_check_states.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p12_check_states.py)）→ AC-4 校准为 ≥0。P1-2 交付分发骨架 + AND 判分 + 台账，零即时增益、零回归，为 P1-4/P2-3 铺地基（用户已确认）。 |

## Review History

| 轮次 | 日期 | 审查者 | 结论 | 关键问题与修复 |
|------|------|--------|------|----------------|
| 1 | 2026-11-22 | fresh-context 只读子代理（Agent 8e805aa1） | **PASS**（七维度全通过） | 无 precision/harm/回归/可执行缺陷；3 项非阻塞观察（混合树夹带 P1-3 既有改动 / pdf_questions.json 范围外图像 spec 改动 / AND 分支生产路径未触发由单测+接线审查保证），详见 [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/multi-part-subquestion-dispatch/review.md) §2 §3 |

## Spec Compliance
- **所有 AC 已映射到任务**：AC-1→T0/T1/T2；AC-2→T3/T4；AC-3→T4/T5；AC-4→T0/T2/T5；AC-5→T6；AC-6→T5/T7；全部→T8。
- **Test Requirements 类型仅 `rule` / `rubric`**：✅ 已遵守。
- **任务按 调研→拆分→求解→聚合→判分→回归→台账→Review 排序**，依赖清晰，无循环。
- **红线贯穿每个任务的 Test Requirements**：precision=1 / harm=0 / LOST=0 / 不蒙数 / 事实只存 JSON / 密钥不入库 / opt-in 零回归。
