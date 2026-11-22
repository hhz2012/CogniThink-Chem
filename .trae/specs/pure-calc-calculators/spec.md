# P1-3 纯计算域计算器（确定性单步数值计算）- 产品需求文档

## Overview
- **Summary**：在绿色基线 `base_p0`（757 题 eval，LLM 关闭）下，全库仍有 **128 道未解**；其中 **27 道为 K.1 多部分大题**（已在 `multi-part-subquestion-dispatch` spec 立项，实施 PARKED，待本 spec 计算器就位后回归），剔除后剩 **101 道非多部分未解**。经只读审计（[p13_scope_report.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p13_scope_report.txt) + [p13_inspect_ids.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p13_inspect_ids.txt)），这 101 道中真正属于「单部分、无图、自由文本无关、题干含确定数值、答案键为数值且与题干对齐、可由给定数据唯一确定计算」的纯数值计算题，经逐题核对有 2 道：`trap-limit-2`（歧化反应氧化剂/还原剂质量比）与 `pdf-517`（NaClO 水解 pH）。**但落地前真值核对（[p13_truth_check.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p13_truth_check.py)）发现 pdf-517 答案键冲突**：权威常数 `Ka(HClO)=3.0×10⁻⁸`（`acid-constants.json`）算出 pH≈9.76，答案键却为 10.3（需 Ka≈2.5×10⁻⁹ 才得 10.3，与权威值矛盾；其 note 亦自承「pH=9.76≈10.3」为强行近似）。按红线「化学推理与答案键冲突→跳过、不蒙数」，pdf-517 **跳过并登记台账**。故本 spec **可兑现 gain = 1 道（trap-limit-2）**；同时仍建立水解 pH 计算器作为正确基础设施（对 pdf-517 诚实输出 9.76，不匹配冲突键→不产生 gain 也不产生 harm），供 P1-2 多部分子问分发复用。
- **Purpose**：在不破 `precision=1 / harm=0 / LOST=0 / 不蒙数` 红线的前提下，用「JSON 事实（常数）+ 薄求解器（解析+算术）」解出原本诚实答 null 的 2 道纯数值题；对无法零风险解出的题（含未定值变量、答案键错位、缺上下文子问、图像/选择/方程域）诚实留痕、登记台账，不强行蒙数。
- **Target Users**：化学题库与求解引擎维护者；本 spec 是 `engine-capability-gap-fix` 的后继（其 Non-Goal 中「纯计算域计算器 ~5 道」的落地），并为 `multi-part-subquestion-dispatch` 提供可复用的子问求解能力。

## Background & Context

### 基线证据（已验证，不可动摇）
- 绿色基线：`_evalscratch/checkpoint_base_now.jsonl`（757 题，`ASPNETCORE_ENVIRONMENT=Production`，LLM 关闭）→ 解对 **629** / 未解 **128**；**precision=1 / harmCount=0**。证据：[checkpoint_base_now_summary.txt](file:///d:/github/CogniThink-Chem/_evalscratch/checkpoint_base_now_summary.txt)。
- `base_now ≡ base_p0`：经 `diff_ckpt.py base_p0 base_now` 确认 GAINED=0 / LOST=0 / harm=0，基线稳定可复现。本 spec 的回归对照锚点 = `base_p0`（== `base_now`）。
- 单元测试基线：`dotnet test CogniThink.Chem.sln -c Release` 既往 0 failed（P1-1 落地时 2150 passed / 0 failed）。任何修改不得破此基线。

### 候选逐题 vetting（LOST=0 精神：不静默遗漏、不夸大范围）
审计脚本 [p13_scope.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p13_scope.py) 初筛「calc 信号 ∧ 无图 ∧ 无自由文本 ∧ 非多部分（子问标记<2）」= 12 道；再经 [p13_inspect_ids.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p13_inspect_ids.py) 逐题核对题干↔答案键对齐性与可确定性，结论如下（**修正了前序会话小结中过于乐观的候选清单**）：

| 题号 | taskType | 答案键首项 | 处置 | 理由 |
|------|----------|-----------|------|------|
| **trap-limit-2** | calculation | `1` / `2` / `1:2` | ✅ **纳入** | 题干给出配平歧化方程，问氧化剂与还原剂质量比；可由化学计量数唯一确定（1:2）。合成探针题，用于证明计算器能力。 |
| **pdf-517** | calculation | `10.3` | ⏭️ **跳过（登记冲突）** | 25℃ 0.01 mol/L NaClO 溶液 pH；权威 Ka(HClO)=3.0×10⁻⁸ 算出 pH≈9.76，与答案键 10.3 冲突（达 10.3 需 Ka≈2.5×10⁻⁹）。按红线不蒙数，跳过；计算器诚实输出 9.76 但不匹配键→无 gain 无 harm。登记台账。 |
| pdf-030 | equilibrium | `25.00%` | ❌ 排除 | 题干含未定值变量 `a g` / `b mL` 且明确要求「列出计算式，不用化简」→ 无唯一数值解（不蒙数）；答案键 `25.00%` 与变量式题干不对齐，疑似数据错位。登记台账。 |
| pdf-165 | unknown | `ds 3d10 4s1` | ❌ 排除 | 题干问 `nCO2:nCO`，答案键却是 Cu 电子排布式 → **答案键错位**（数据缺陷）。登记台账，属 P1-2 多部分子问上下文缺失。 |
| pdf-172 | unknown | `AC` | ❌ 排除 | 题干问固定 CO₂ 体积（L），答案键却是选择项 `AC` → **答案键错位**（数据缺陷）。登记台账。 |
| pdf-160 | unknown | `-2` | ❌ 排除 | checkpoint 中题干仅存大题引语（子问被剥离），答案键跨 5 个子问；非单部分、缺上下文。属 P1-2 域，登记台账。 |
| pdf-463 | calculation | `C` / `9` | ❌ 排除 | 「填字母」选择题答案 + 子问缺 Ksp 数据与选项表上下文；属 P1-2 多部分分发/P1-4 域，非纯数值计算。 |
| trap-limit-3 | calculation | `HCO3…` | ❌ 排除 | 与量有关离子方程式，属方程书写域（P1-1 已做 equation_writer 路由），非数值计算。 |
| pdf-415 | calculation | `OH⁻、CO₃²⁻` | ❌ 排除 | 「除去的离子有」自由填空概念题，属 P1-4 自由填空概念求解器域。 |
| pdf-197 / pdf-263 | choice | 单字母 | ❌ 排除 | 多陈述选择题，属选择事实策展域，非纯数值。 |
| pdf-355 / 356 / 526 / 527 | equation_writer | 方程式 | ❌ 排除 | 方程书写题，P1-1 已路由，非本 spec。 |

> 结论：本 spec 可兑现 gain = **1 道**（trap-limit-2）；pdf-517 因答案键冲突跳过。不把做不到 / 键冲突的题硬塞进 AC。

### 可复用基础设施（已调研，事实只存 JSON 已具备）
- **常数事实库已存在**：`data/highschool/acid-constants.json`（含 `HClO` Ka=3.0×10⁻⁸ 及 `HClO/ClO⁻` 物种映射）、`data/highschool/solubility.json`（Ksp）。由 [RuleStore.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Rules/RuleStore.cs) 加载，`AqueousEquilibriumEngine` 已用 `RuleStore.Load().Query<AcidConstantsData>("acid-constants")`。→ pdf-517 **无需新增 JSON 事实**，求解器查库即可。
- **现有计算引擎**：[LlmGatewayService.CallCalculation](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs)（L1104 起）已覆盖 FeBr₂+Cl₂、Fe+稀HNO₃、简单化学计量、质量分数→量浓度、混合物分解、Hess、离子浓度比较、[Batch2NarrowCalcSolver](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/Batch2NarrowCalcSolver.cs)（窄域判断）。**未覆盖**：盐水解 pH、读配平方程式算氧化/还原剂比例。新计算器以**兜底**方式接入（既有全 miss 才触发），零域外假阳性。
- **采纳门控**：`BuildGatewayAnswerAsync` 经 `IsAnswerCorrect`（答案键子串匹配）校验后才采纳；本 spec 要求 calculation 结论凡采纳必过答案键校验，不得盲目采纳 Ok=true（P1-1 已为晶胞域建立 concept_fact 标记规避盲目采纳，本 spec 沿用「采纳必经校验」原则）。
- **探针范式**：[chem-fact-curation](file:///d:/github/CogniThink-Chem/.trae/skills/chem-fact-curation/SKILL.md) 的 `probe_template.py`（C# 对齐 norm + stray-hit 检查 + 决策模拟，TOTAL PROBLEMS=0 才落库）。本 spec 的计算器探针复用其「目标唯一命中 + 无 stray + 决策与答案键一致」精神。
- **eval 与回归工具**：`run_eval_resume.ps1 -Tag <tag> -Env Production`；`_evalscratch/verify/diff_ckpt.py base_p0 <newtag>`（权威 diff：GAINED/LOST/WORSEN/IMPROVE + harm）。

### 冻结红线（从父 spec 继承，不可破）
- `precision=1`：不得把任何原本正确的题改判错；新计算器不得让任何已解对题变成错。
- `harm=0`：不得删除/覆盖正确数据；引擎不得采纳错误结论；不得清空正确 options。
- `LOST=0`：128 道未解中本 spec 触及的候选每题有明确处置结论（解出 / 排除并登记台账），不静默遗漏。
- `不蒙数`：题干无唯一数值解（含未定值变量、缺数据）→ 求解器返回 null；探针未证明计算值与答案键化学一致 → 不落代码。
- 化学事实/常数**只存 JSON**（`data/highschool/*.json`），不硬编码进 `.cs`；求解器代码只做题干解析 + 算术 + 查库，不含化学判定真值。
- 用户未要求不主动 commit。

## Goals
1. **水解 pH 计算器（基础设施，本次无 gain）**：对「盐（弱酸根/弱碱阳离子）+ 明确浓度 + 问 pH」的单部分题，查 `acid-constants.json` 得 Ka/Kb，按 `[OH⁻]=√(Kb·c)`（或对应式）算 pH，输出含数值答案的结论；无常数/无浓度/非目标域 → null。对 pdf-517 诚实输出 pH≈9.76（不匹配冲突键 10.3→不被采纳，无 gain 无 harm）；作为 P1-2 复用基础设施。
2. **氧化还原比例计算器**：对「题干给出配平（或可直接读出计量数）的氧化还原方程式 + 问氧化剂与还原剂的物质的量/质量比」的题，由方程式计量数与化合价变化确定比例，输出含比值的结论；非目标域/读不出计量数 → null。解出 trap-limit-2。
3. **零回归接入**：新计算器仅在 `CallCalculation` 既有求解器全返回 null 时兜底触发，且以窄域题干签名护栏（如 pH+盐+浓度；配平方程+氧化剂/还原剂+质量比）锁定目标，全库 757 题无 stray 误命中。
4. **全量验证**：`dotnet test` 0 failed；eval diff 对 `base_p0` 满足 LOST=0 / precision=1 / harm=0；新增解对 = 1（trap-limit-2，经探针化学核对）；pdf-517 保持未解且无 harm（计算器诚实输出 9.76 未被采纳）。
5. **台账闭环**：排除候选（pdf-030/160/165/172/463 等）逐题登记 `data-defect-register.md`，标注根因（变量无解 / 答案键错位 / 子问上下文缺失）与归属 spec（P1-2 / P1-4 / 数据修复）。

## Non-Goals
- **不做多部分大题子问分发**（27 道 K.1）：属 `multi-part-subquestion-dispatch` spec；本 spec 只提供可被其复用的原子计算器，不拆子问、不聚合判分。
- **不做自由填空概念求解器**（pdf-415/274/535/543 等）：属 P1-4。
- **不做选择题事实策展**：属 chem-fact-curation 既有流程。
- **不重开 LLM**：eval 始终 Production（LLM 关闭），引擎是唯一真值源。
- **不解含未定值变量的题**（如 pdf-030 的 a/b）：无唯一数值解，违反不蒙数；登记台账，不求解。
- **不修复题库数据**（答案键错位 pdf-165/172、子问剥离 pdf-160）：数据修复属独立范畴，本 spec 只登记；强行修数据有 harm 风险。
- **不把计算器扩展到图像/曲线/装置题**：属 P2-3。
- **不改 `IsAnswerCorrect` 的 OR 语义或 `FactChoiceMatcher` 的 WRONG 正则**。

## Functional Requirements
- **FR-1（水解 pH 计算器）**：新增薄求解器（建议 `HydrolysisPhCalculator`，置于 `Application/Gateway/` 或计算引擎同目录），输入题干文本：① 检测「pH」+ 弱酸盐/弱碱盐物种 + 数值浓度（mol/L）；② 由 `RuleStore` 查对应弱酸 Ka（或共轭酸 Ka），算 Kb=Kw/Ka；③ `[OH⁻]=√(Kb·c)`（或 `[H⁺]=√(Ka·c)` 视水解方向），pH=14+lg[OH⁻]（或 −lg[H⁺]），按题干给的 lg 近似提示取位；④ 输出 `FinalResult` 含数值答案（如 `pH = 10.3`）与推导步骤；⑤ 任一步缺数据/查不到常数/非目标域 → 返回 null（不蒙数）。
- **FR-2（氧化还原比例计算器）**：新增薄求解器（建议 `RedoxRatioCalculator`），输入题干文本：① 检测含配平氧化还原方程式 + 「氧化剂」「还原剂」「质量比/物质的量比」；② 从方程式解析计量数与变价元素，确定氧化剂:还原剂的物质的量比（同一物质歧化时按变价原子数分配，如 NO₂ 歧化 1 被还原 : 2 被氧化）；③ 质量比 = 物质的量比 × 摩尔质量比（同一物质则相等）；④ 输出含比值（如 `1:2`）的结论；⑤ 读不出计量数/无变价/非目标域 → null。
- **FR-3（兜底接入与护栏）**：在 `CallCalculation` 末尾（既有求解器全 miss 后）按序调用 FR-1、FR-2；两者均以**窄域题干签名**命中才返回非 null，域外一律 null，确保 629 道已解对题不受影响（零回归）。
- **FR-4（探针门控）**：每个计算器落代码前，用 Python 探针（C# 对齐的归一化 + 目标题计算模拟 + 全库 stray-hit 扫描）验证：目标题计算值与答案键化学一致、全库无其它题被误命中、决策唯一；**TOTAL PROBLEMS=0 才落代码/改 JSON**。
- **FR-5（台账同步）**：排除候选逐题登记 `data-defect-register.md`，含题号、根因、归属 spec。
- **FR-6（证据与可复现）**：留 eval checkpoint（`checkpoint_after_p13.jsonl` + summary）+ `diff_ckpt base_p0 after_p13` 输出；所有数字与审计/探针/台账自洽。

## Non-Functional Requirements
- **NFR-1（precision=1）**：无任何原本正确的题被新计算器改判错。
- **NFR-2（harm=0）**：不删/不改正确数据；引擎不采纳任何错误结论。
- **NFR-3（LOST=0）**：本 spec 触及的候选每题有明确处置结论，不静默遗漏。
- **NFR-4（不蒙数）**：无唯一数值解 → null；探针未过 → 不落代码。
- **NFR-5（事实只存 JSON）**：求解器不硬编码化学常数/真值；常数来自 `acid-constants.json` 等（本 spec 无需新增常数；若未来扩展必须先落 JSON）。
- **NFR-6（零回归）**：非目标题结果与 `base_p0` 逐案一致（diff LOST=0、WORSEN=0）。
- **NFR-7（可复用）**：计算器以独立静态方法/类实现，输入题干字符串输出 `EngineResponse?`，便于 P1-2 子问分发直接调用。

## Constraints
- **Technical**：Windows PowerShell 5（无 `&&`，用 `;`）；dotnet 8；Python 用全路径 `C:\Users\Administrator\AppData\Local\Programs\Python\Python312\python.exe`；eval 读数从 `AppContext.BaseDirectory/data`（bin 副本），跑前 `Get-FileHash` 确认 bin 与源一致防 stale-DLL。
- **Business**：`precision=1 / harm=0 / LOST=0 / 不蒙数` 红线不可破；破则回退对应改动，任务退回 `in_progress`。
- **Dependencies**：`RuleStore` 常数加载链路；`CallCalculation` 路由；`IsAnswerCorrect` 采纳门；eval 管线与 `diff_ckpt.py`；chem-fact-curation 探针范式。
- **Risks**：① calculation 采纳门若盲目接受 Ok=true，错误计算可能造成 harm → AC-2 全库 diff 验证 harm=0 兜底；② `IsAnswerCorrect` 对 trap-limit-2 的答案键 `['1','2','1:2']` 用 OR 子串匹配，输出含「1」或「2」即可能判正 → 探针必须核对计算值化学上确为 1:2，不依赖宽松判分；③ 窄域签名若过宽可能误命中已解对题 → 探针全库 stray 扫描 + 回归 diff 双保险。

## Assumptions
- `acid-constants.json` 中 `HClO`（Ka=3.0×10⁻⁸）与 `ClO⁻` 映射准确，足以算 pdf-517=10.3（探针复核）。
- trap-limit-2 题干方程 `3NO2 + H2O = 2HNO3 + NO` 计量数可被解析，N 歧化比例 1:2 化学正确（探针复核）。
- eval 可在本环境以 Production（LLM 关闭）运行。

## Acceptance Criteria

### AC-1：新增解对 ≥1（trap-limit-2），pdf-517 不强行匹配
- **Type**: `rule`
- **Given**: 绿色基线 `base_p0`（gatewayCorrect=629，128 未解），目标 trap-limit-2（当前 `gatewayCorrect=false`）；pdf-517 因答案键冲突已排除
- **When**: FR-1/FR-2/FR-3 落地，跑 eval 产 `after_p13`，`diff_ckpt.py base_p0 after_p13`
- **Then**: trap-limit-2 由 `correct=false` 翻转为 `correct=true && truthAdopted=true`；pdf-517 保持 `correct=false`（计算器诚实输出 9.76，不匹配冲突键 10.3→不被采纳）；无任何已解对题翻转为错（LOST=0）
- **Pass Condition**: diff GAINED≥1（且包含 trap-limit-2）；pdf-517 不在 GAINED 中；LOST=0；WORSEN=0；summary precision=1 / harm=0
- **Evidence**: [checkpoint_after_p13_summary.txt] + `diff_ckpt base_p0 after_p13` 输出 + trap-limit-2 探针计算核对（1:2）+ pdf-517 真值核对（9.76 vs 键 10.3）记录

### AC-2：零回归与无 stray 误命中
- **Type**: `rule`
- **Given**: 全部计算器改动完成
- **When**: 探针全库 757 题 stray 扫描 + eval 终态 diff
- **Then**: 除目标 2 道外，其余 755 题 `gatewayCorrect` 与 `base_p0` 逐案一致；新计算器未在任何非目标题上产出非 null 结论
- **Pass Condition**: stray-hit 数 = 0；diff LOST=0 / WORSEN=0 / IMPROVE 仅为目标 2 道；harm=0
- **Evidence**: 探针 stray 扫描输出 + diff 明细

### AC-3：单元测试零失败
- **Type**: `rule`
- **Given**: 全部改动完成
- **When**: `dotnet test CogniThink.Chem.sln -c Release`
- **Then**: 0 failed（passed 数不低于基线 2150）
- **Pass Condition**: failed=0
- **Evidence**: 测试汇总输出

### AC-4：事实只存 JSON、求解器无硬编码化学真值
- **Type**: `rule`
- **Given**: 本 spec 全部 `.cs` 与 JSON 改动
- **When**: `git diff` 审查 + grep 新增求解器中的常数字面量
- **Then**: 新增求解器不含硬编码化学常数（Ka/Ksp/摩尔质量等）或化学判定真值；常数来自 `RuleStore`/JSON；本 spec 无需新增 JSON 常数（HClO 已存在）
- **Pass Condition**: 新增 `.cs` 中无化学真值字面量（数值常量仅限 Kw=1e-14、√ 等数学常数，且有注释说明）；无 `data/highschool/*.json` 非必要改动
- **Evidence**: git diff 审查 + grep 输出

### AC-5：排除候选台账闭环
- **Type**: `rubric`
- **Dimension**: 本 spec 触及候选的处置可追溯性与无遗漏
- **Scale**: 1-5
- **Anchors**: 1 = 仅说「解不了」无明细；3 = 有分类但缺逐题登记；5 = 每个排除候选（pdf-030/160/165/172/463 等）逐题在 `data-defect-register.md` 登记，含根因 + 归属 spec，与审计 id 集合一一对应，0 静默遗漏
- **Pass Threshold**: >= 4
- **Evidence**: `data-defect-register.md` 新增行 + 审计 id 集合比对

### AC-6：采纳门控安全
- **Type**: `rule`
- **Given**: 新计算器产出的 Ok=true 结论
- **When**: 检查 `BuildGatewayAnswerAsync` 对 calculation 结论的采纳路径 + 终态 checkpoint 的 `gatewayTruthAdopted`
- **Then**: 凡被采纳的 calculation 结论必经 `IsAnswerCorrect` 答案键校验；trap-limit-2（计算值与答案键化学一致）`truthAdopted=true`，pdf-517 因答案键冲突（真值 9.76 vs 键 10.3）保持 `truthAdopted=false`（safe non-adoption，保护 precision=1），且无错误结论被采纳（harm=0 交叉验证）
- **Pass Condition**: trap-limit-2 `gatewayTruthAdopted=true`；pdf-517 `gatewayTruthAdopted=false` 且不产生 harm；全库 harm=0
- **Evidence**: checkpoint 对应记录 + 代码审查采纳门

## Open Questions
- [ ] **独立收益仅 2 道，是否值得单独立项？** 默认值得：① 这 2 道是基线中诚实答 null 的真实缺口；② 计算器是 P1-2 多部分子问分发的复用基础设施（子问拆分后，多部分大题中的 pH/比例子问可直接调用），收益会在 P1-2 放大；③ 红线优先于解对数——不为凑数而扩大范围。若用户认为 2 道收益不足，可降级为「仅建计算器框架 + 解 2 道」，其余待 P1-2。
- [ ] pdf-030 的答案键 `25.00%` 是否为数据错位（应为含 a/b 的计算式）？本 spec 不修数据，仅登记；需独立数据修复 spec 确认。
- [ ] 是否在本 spec 顺带为未来扩展预留更多常数（如更多弱酸 Ka）？默认不预留（NFR-5：常数先落 JSON 才能用，无求解器消费的常数不加，避免无用膨胀）。
