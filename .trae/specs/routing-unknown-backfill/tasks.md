# Tasks — P1-5 路由补齐（taskType=unknown 收敛）

> 关联 spec：[spec.md](file:///d:/github/CogniThink-Chem/.trae/specs/routing-unknown-backfill/spec.md)
> 基线：`after_p14`（635 正确 / 122 未解，`taskType=unknown`=32，precision=1，harm=0）；红线锚点 `base_p0`（629 正确）。
> 红线：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 事实只存 JSON。破线即回退。
> 诚实口径：纯路由新增解对数预期 = 0（见 spec §2.1）；本 spec 目标 = 路由卫生（unknown↓ + 零有害漂移）。

## Task Breakdown

| ID | 任务 | 状态 | 产出 |
|----|------|------|------|
| T0 | 只读审计：32 unknown 逐项分类 + cue A 0 漂移预校验 | completed（spec 阶段已实证） | `p15_scope_audit.py` / `p15_answers_dump.py` / `p15_drift_precheck.py` + 结果入 spec §2 |
| T1 | `ClassifyTaskDeterministic` 插入 cue A（方程书写问法补齐 → equation_writer） | completed | `LlmGatewayService.cs` +1 cue 块；0 漂移预校验重跑 PASS（affected=16） |
| T2 | 全库 0 漂移 / 重分类预校验重跑（落码后），确认 16 affected + 5 已判正 | completed | `p15_drift_precheck.py` 重跑 PASS；`p15_drift_precheck.txt` 更新 |
| T3 | 单测：cue A 命中 + 5 已判正题改判 + 不窃取 guard + 回归守卫；全量 `dotnet test` 0 failed | completed | `ClassificationDeterministicTests` +8 测试；全量 2191 passed / 0 failed |
| T4 | eval + `diff_ckpt` 红线核验：unknown↓、LOST=0、WORSEN=0、harm=0；5 已判正保持判正 | completed（含回归修复） | `after_p15` 首版 **LOST=1（pdf-515）** → 根因定位 → 补 `ProductWriterSolver` 进 equation_writer 分支（用户确认方案 a）→ `after_p15b` 两次 diff RED-LINE PASS；`p15_verify_after.py after_p15b` ALL PASS |
| T5 | 台账闭环：新增 §M（P1-5 路由补齐），16 道重分类去向 + 27 道范围外逐项处置，差集为空 | completed | `data-defect-register.md` §M（M.1 16 去向 / M.2 27 范围外 / M.3 闭包 32=5∪27）；`p15_closure_check.py` PASS |
| T6 | 红线自检：密钥扫描 / 未改 JSON 数据 / 未改评估代码 / 不蒙数 | completed | 密钥扫描 0 命中；P1-5 表面积仅 `LlmGatewayService.cs` + 测试 + 台账 + scratch；`data/highschool/*.json` 与评估代码未因 P1-5 改动；adopted∧¬correct=0 |
| T7 | fresh-context 只读子代理独立 review → review.md，裁决 PASS/FAIL | completed | [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/routing-unknown-backfill/review.md) — 总体 PASS，红线全过 |

---

## T0 — 只读审计（32 unknown 分类 + cue A 0 漂移预校验）
**Status**: completed — 审计在 spec 审批前完成，结论入 spec §2。
**Outputs**:
- [p15_scope_audit.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p15_scope_audit.txt) / [p15_scope_audit.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p15_scope_audit.py)：32 道 unknown 分类（mp 3 / img 3 / both 6 / no-cue 20）。
- [p15_answers_dump.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p15_answers_dump.txt) / [p15_answers_dump.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p15_answers_dump.py)：逐题 stem/answer/answerKeywords。
- [p15_drift_precheck.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p15_drift_precheck.txt) / [p15_drift_precheck.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p15_drift_precheck.py)：cue A 全库 757 影响 = 16 affected（11 unknown→ew，5 已判正 touched，5 其它 incorrect）。
**Notes**: 关键结论入 spec §2.1（纯路由增益=0）与 §2.2（32 逐项处置闭包）与 §2.3（cue A + 5 已判正推理）。

## T1 — 插入 cue A（方程书写问法补齐）
**Status**: completed
**Outputs**: [LlmGatewayService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) `ClassifyTaskDeterministic`：在既有 `equation_writer` 块（L319-322）**之后**、P1-4 concept cues（L326-331）**之前**插入：
```csharp
// P1-5：方程书写问法补齐——题干以「…方程式为 / 用…方程式表示」要求书写但未出现「书写/写出」触发词。
// 全库 757 预校验（_evalscratch/verify/p15_drift_precheck.py）：5 道已判正（pdf-174/417/456/515/539）
// 改判后仍由 ReactionKbWriterSolver 产出同一结论（eval 实证保持判正，见 AC-3）；不窃取 choice/judgment/equilibrium。
if (lower.Contains("化学方程式为") || lower.Contains("离子方程式为") ||
    lower.Contains("方程式为") || lower.Contains("用离子方程式表示") ||
    lower.Contains("用化学方程式表示"))
    return "equation_writer";
```
**Verification**:
- `dotnet build CogniThink.Chem.sln -c Release` → 0 错误。
- 重跑 T2 预校验：affected=16，5 已判正列表不变。
**Notes**: 放置顺序必须在 `equation_writer` 之后、`calculation` 之前（不抢占既有高优先级；P1-4 concept cues 保持原序）。不修改任何求解器守卫。

## T2 — 0 漂移 / 重分类预校验重跑（落码后）
**Status**: completed
**Outputs**: 重跑 [p15_drift_precheck.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p15_drift_precheck.py) → 更新 [p15_drift_precheck.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p15_drift_precheck.txt)。
**Verification**:
- affected = 16（与 spec §2.3 一致）；5 道 `gatewayCorrect=True` 为 pdf-174/417/456/515/539（预期，eval 把关保持判正）。
- 端口 sanity：不可变类型 mismatch 为信息性（concept/equation_writer 被求解器运行时改写）。
**Notes**: 若 affected 列表变化 → 停下核对（说明源题面或基线有变动），不得带差异落 T4。

## T3 — 单测
**Status**: completed
**Outputs**: 扩展 [ClassificationDeterministicTests.cs](file:///d:/github/CogniThink-TutorBrain.Tests)：
- (a) cue A 正例 ≥3：pdf-164 / pdf-179 / pdf-462（或等价 stem）→ `ClassifyTaskDeterministic` 返回 `"equation_writer"`。
- (b) 5 道已判正改判断言：pdf-174/417/456/515/539 → 改判后为 `"equation_writer"`（记录「eval 实证保持判正」，单测只断言分类结果）。
- (c) 不窃取 guard ≥3：含「方程式」但属 choice / judgment / equilibrium 的代表性题 → 维持原 taskType（不被 cue A 截走）。
**Verification**: `dotnet build` 0 错误；全量 `dotnet test CogniThink.Chem.sln -c Release` → 0 failed，passed = 2191（含 P1-5 的 6 条分类测试 + 2 条 equation_writer→ProductWriterSolver 回归守卫测试）。

## T4 — eval + 红线 diff
**Status**: completed（含一次回归修复，轨迹完整留痕）
**Outputs**:
- 首版 `run_eval_resume.ps1 -Tag after_p15 -Env Production` → 757/757 failed=0，但 `diff after_p14→after_p15` **LOST=1（pdf-515）** → 破红线，FAIL。
- 根因定位（只读，代码指纹唯一）：pdf-515 在 after_p14 经 LLM 兜底 `reaction_check` → `CallCompile` → `ProductWriterSolver` 解出；cue A 重分类到 `equation_writer` 后该分支缺 `ProductWriterSolver` → LOST。详见 spec §7.1。
- 修复（用户确认方案 a）：`equation_writer` 分支按 `reaction_check` 同序补 `ProductWriterSolver`（命中置 `reaction_check` 走同一采纳门）。build 0 error。
- 复跑 `run_eval_resume.ps1 -Tag after_p15b -Env Production` → 757/757 failed=0。
- `diff_ckpt after_p14 after_p15b`：GAINED=0 / **LOST=0** / WORSEN=0 / harm=0 → RED-LINE PASS。
- `diff_ckpt base_p0 after_p15b`：GAINED=6（P1-4 携带）/ LOST=0 / WORSEN=0 / harm=0 → RED-LINE PASS。
- `p15_verify_after.py after_p15b`：① unknown 32→27 严格下降；② pdf-174/417/456/515/539 全部 correct=True；③ 全局 adopted∧¬correct=0；④ LOST=0。**ALL PASS**。
**Verification**: 上述全过；correct=635（增益 0，诚实），unknown 下降，零回归。
**Notes**: `after_p15`（失败 checkpoint）保留作为回归证据，不删除；最终交付 checkpoint = `after_p15b`。

## T5 — 台账闭环
**Status**: completed
**Outputs**: [data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md) 新增 **§M P1-5 路由补齐闭环**：
- M.1 cue A 重分类 16 道去向表（unknown→ew 11 / concept→ew 2 / calculation→ew 3；含 5 已判正保持判正的实证结论）。
- M.2 范围外 22 道逐项处置（与 spec §2.2 表一致：mp/img/缺陷/有机/KB缺口/重复/装置/分析/碎片），标注归属 spec 或「保持 null + 原因」。
- M.3 集合闭包核对：32 = 重分类触及(16 中属 unknown 的子集) ∪ 范围外(22)… 实际以 checkpoint runtime unknown 下降数 + 未动 unknown 数 = 32 核对，差集为空。
- 新增 `p15_closure_check.py`：断言 P1-5 触及的 unknown 集合 ∪ 保持 unknown 集合 = after_p14 的 32 unknown 全集，无交集无遗漏。
**Verification**: `p15_closure_check.py` PASS。

## T6 — 红线自检
**Status**: completed
**Verification**（脚本化，结果入 review 证据）：
- 密钥扫描：P1-5 改动文件 grep `sk-|api_key|Bearer|secret` → 0 命中。
- 未改数据：`git diff --name-only` 不含 `data/highschool/*.json`（P1-5 不动事实库）。
- 未改评估：`git diff -- .../Evaluation` 为空（WRONG 正则/守恒近似未动）。
- 不蒙数：cue A 命中的题中，凡 `adopted=True` 必 `correct=True`（eval 实证）；无部分命中被采纳。
- 表面积：仅 `LlmGatewayService.cs`（+1 cue 块）+ 测试文件 + 台账 + scratch 验证脚本，无其它源码改动。

## T7 — 独立 review
**Status**: completed
**Process**: 委托 fresh-context **只读** 子代理（不得改任何文件），独立复跑：build / test / eval diff（两次）/ `p15_verify_after.py` / `p15_closure_check.py` / 密钥扫描 / 未知计数核对 / 台账集合比对。裁决维度：precision=1 / harm=0 / 无回归(LOST=WORSEN=0) / 不蒙数 / 未改数据与评估 / 台账自洽 / unknown↓ 实证。任一 FAIL → Issue → 修复 → 复审。子代理仅返回报告文本，由主代理落盘 [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/routing-unknown-backfill/review.md)。

---

## 决策日志（CQ）

| ID | 问题 | 选项 | 决策 | 理由 |
|----|------|------|------|------|
| CQ-1 | P1-5 是否追求「解出几道」？ | A 纯路由（增益 0）/ B 顺带加 KB/事实凑增益 | **A** | unknown 瀑布已跑事实库+方程式 KB，纯路由不解锁新求解器（spec §2.1）；加 KB/事实属范围外独立 spec，强行并入会扩大表面积与精度风险。诚实呈现增益 0。 |
| CQ-2 | cue A 触及 5 道已判正，是否纳入？ | A 纳入（eval 把关）/ B 收窄到不触及已判正 / C 放弃 | **A**（eval 实证：首版回归 1 道，已修复） | 无法用子串区分已判正与目标（同用「…方程式为」）。首版 eval 证伪原推理：pdf-515 实由 `ProductWriterSolver`（reaction_check 链）解出，cue A 改判 equation_writer 后该分支缺此求解器 → LOST=1。用户确认方案 a：equation_writer 分支补 `ProductWriterSolver`（与 reaction_check 同序）→ after_p15b 全绿。其余 4 道（174/417=ReactionKb，456/539=Known）本就在 equation_writer 分支覆盖内，保持判正。AC-3 eval diff 硬把关通过。教训：预校验不能替代 eval，每道 correct-touch 须实证。 |
| CQ-3 | cue A 放置位置？ | A equation_writer 块之后 / B calculation 之后 | **A** | 与方程书写同组；先于 calculation（避免含「体积/mol」等的方程书写题被 calculation 截走）；不抢占 choice/judgment/equilibrium 等高优先级。 |
| CQ-4 | 多部分/图像题被 cue A 顺带改判是否处理？ | A 处理（加 guard 排除）/ B 不处理（行为不变无伤害） | **B** | 这些题 `ReactionKbWriterSolver` 因锚点不全/多义返回 null → 仍未解、无伤害（spec OQ-3）；加 guard 反而扩大表面积。留待 P1-2/P2-3。 |

## Review History

| 轮次 | 日期 | 审查者 | 结论 | 关键问题与修复 |
|------|------|--------|------|----------------|
| 1 | 2026-11-22 | 独立只读子代理（fresh-context，不改文件） | PASS | 红线全过（precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 未改数据与评估 / 台账自洽 / unknown↓）；非阻塞：cue A 注释陈旧（已由主代理修正，正确归属 5 道求解器） |
