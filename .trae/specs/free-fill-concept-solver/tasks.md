# Tasks — P1-4 自由填空概念求解器

> 关联 spec：[spec.md](file:///d:/github/CogniThink-Chem/.trae/specs/free-fill-concept-solver/spec.md)
> 基线：`after_p12`（630 正确 / 127 未解，precision=1，harm=0）；红线锚点 `base_p0`（629 正确）。
> 红线：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 事实只存 JSON。破线即回退。

## Task Breakdown

| ID | 任务 | 状态 | 产出 |
|----|------|------|------|
| T0 | 只读审计：锁定 5 道候选 + 触发 + 答案键 + 0 漂移预校验 | completed（spec 阶段已实证） | `p14_t0_audit.py` / `p14_routing_drift.py` / `p14_existing_facts2.py` + 结果入 spec §3 |
| T1 | Part A：向 `concept-facts.json` 追加 5 条事实（唯一/0漂移/键命中已断言） | completed | `concept-facts.json` version 1.4→1.5，+5 条目（仅追加）；`p14_post_t1_verify.py` ALL PASS |
| T2 | Part B：`ClassifyTaskDeterministic` 路由放宽（calculation 之前插入 concept-fill cue），附全库 0 漂移预校验脚本 | completed | `LlmGatewayService.cs` +3 cue；`p14_routing_drift_precheck.py` risk(was_correct)=0 |
| T3 | 单测：事实匹配三态 + 路由 0 漂移；全量 `dotnet test` 0 failed | completed | +13 测试（5 事实正例 + 8 路由），全量 2183 passed / 0 failed |
| T4 | eval `after_p14` + `diff_ckpt`（vs base_p0、vs after_p12）红线核验 | completed | correct=635；diff after_p12→after_p14 GAINED=5/LOST=0/WORSEN=0/harm=0；base_p0 同 |
| T5 | 台账闭环：5 道解出逐题登记 + 3 道排除项标注，集合差集为空 | completed | `data-defect-register.md` 新增 §L；`p14_closure_check.py` PASS（候选 8 = 解出 5 ∪ 排除 3） |
| T6 | 红线自检：密钥扫描 / 事实只存 JSON / WRONG 正则未改 / 不蒙数 | completed | 密钥扫描 0 命中；JSON 仅追加（唯一删除行=version 1.4→1.5）；未改 Evaluation；不蒙数 verified |
| T7 | fresh-context 只读子代理独立 review → review.md，裁决 PASS/FAIL | completed | [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/free-fill-concept-solver/review.md)（七维度全 PASS，无 Issue） |

---

## T0 — 只读审计（锁定候选 + 触发 + 0 漂移）
**Status**: completed
**Outputs**:
- [p14_scope_scan.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p14_scope_scan.txt)：100 道未解非 K.1 枚举。
- [p14_t0_audit.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p14_t0_audit.py)：5 道 cleanly solvable（唯一匹配 + 0 漂移 + 答案键命中）。
- [p14_routing_drift.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p14_routing_drift.py)：路由 cue 对已判正题匹配 = 0。
- [p14_existing_facts2.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p14_existing_facts2.py)：现有事实对 5 目标匹配 = 0（追加后无歧义）。
**Notes**: 审计在 spec 审批前完成，结论入 spec §3。排除项 pdf-483/541/177 已记录原因。

## T1 — Part A：追加 5 条事实到 concept-facts.json
**Status**: completed — version 1.4→1.5，+5 条目（仅追加，既有 29 条未动）。`p14_post_t1_verify.py`：34 条目、5 目标各 1 命中、0 漂移、5/5 答案键命中，ALL PASS。
**Outputs**: `data/highschool/concept-facts.json` 仅追加 5 条 entry（id/kind/required/requiredAny/answer/derivation/constraint），不修改既有 29 条。
**Verification**:
- 重跑 `p14_t0_audit.py`：5 目标追加后总匹配数仍为 1（唯一）。
- `git diff --stat -- data/highschool/concept-facts.json` 仅含新增行，无删除/修改行。
**Notes**: 事实内容以 spec §3 表格为准；`answer` 字段用题库真值（命中答案键）；`constraint` 标注「高中限定 / 流程语境」。

## T2 — Part B：路由放宽（受 0 漂移保护）
**Status**: completed — `LlmGatewayService.cs` 在 equation_writer 与 calculation 之间插入 3 条 cue（「除去的离子」/「ph∧目的」/「配制∧浓度∧可能」→ concept）。`p14_routing_drift_precheck.py`：affected=4，risk(was_correct)=**0**；3 路由目标 calculation→concept，2 fact-only 不变。
**Outputs**:
- [LlmGatewayService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) `ClassifyTaskDeterministic`：在 `calculation` 规则（L323）**之前**插入：
  - `lower.Contains("除去的离子")` → `"concept"`
  - `lower.Contains("ph") && lower.Contains("目的")` → `"concept"`
  - `lower.Contains("配制") && lower.Contains("浓度") && lower.Contains("可能")` → `"concept"`
- 新增预校验脚本 `_evalscratch/verify/p14_routing_drift_precheck.py`：模拟新分类规则对全库 757 题，断言**已判正题的 taskType 0 漂移**（输出漂移清单，空才 PASS）。
**Verification**:
- 运行预校验脚本 → 漂移清单为空。
- `dotnet build` 0 错误；代表性已判正题（如 pdf-477、各 calculation 解对题）taskType 不变（单测断言）。
**Notes**: 放置顺序必须在 `calculation` 之前、在 `choice`/`judgment`/`equation_writer`/`atomic_structure` 之后（不抢占已有高优先级分类）。与 P1-1 equation_writer 插入式 0 漂移预校验同口径。

## T3 — 单测
**Status**: completed — 新增 8 条路由测试（`ClassificationDeterministicTests`：3 目标→concept、2 fact-only→unknown、3 条 0 漂移/合取守卫）+ 5 条 ConceptFact 正例 InlineData；更新 guardrail（29→34 条目、触发集 +5）。`dotnet build` 0 错误；全量 `dotnet test` **2183 passed / 0 failed**（Chem 913 + TutorBrain 1270）。
**Outputs**: 测试文件覆盖：
- (a) `ConceptFactSolver`：唯一命中→返回、>1 命中→null、0 命中→null 三态（可新增测试或扩展既有）。
- (b) 路由 0 漂移：断言 ≥3 个代表性已判正题经新规则后 `taskType` 不变；断言 5 个目标题被分到 `concept`。
**Verification**: `dotnet test CogniThink.Chem.sln -c Release` → 0 failed，passed ≥ 2170。

## T4 — eval + 红线 diff
**Status**: completed — eval `after_p14`（Production LLM off，757/757、failed=0）correct=**635** / adopted=635。`diff_ckpt after_p12 after_p14`：GAINED=**5**（pdf-415/530/535/542/543）/ LOST=0 / WORSEN=0 / harm=0；`diff_ckpt base_p0 after_p14`：GAINED=6（+P1-3 携带 trap-limit-2）/ LOST=0 / WORSEN=0 / harm=0。`p14_verify_after.py`：5 目标 correct+adopted，全局 adopted∧¬correct=0，新增采纳集恰为 5，ALL PASS。
**Outputs**:
- `checkpoint_after_p14.jsonl` / `.done` / `snapshot_after_p14.json` / `_summary.txt`（Production，LLM off，757 题）。
- `diff_ckpt base_p0 after_p14`：GAINED≥1、LOST=0、WORSEN=0、harm=0。
- `diff_ckpt after_p12 after_p14`：隔离 P1-4 净增益（应 ≥1，LOST=0）。
**Verification**: 5 目标题 `correct=True & adopted=True`；全库 `adopted∧¬correct=0`；无正确→错误翻转。
**Notes**: 跑前 `Get-FileHash` 确认 bin 下 `concept-facts.json` 与源文件一致（防 stale-DLL）。

## T5 — 台账闭环
**Status**: completed — `data-defect-register.md` 新增 §L（L.1 解出 5 道 / L.2 排除 3 道 / L.3 集合闭包）；K.2 表 3 行 + K.2 附 1 行交叉更新为 ✅ 已解出；K.8 加 P1-4 后续更新注（现行未解 53→50）。`p14_closure_check.py`：候选(8)=解出(5)∪排除(3)，无交集，3 排除保持 null，PASS。
**Outputs**: [data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md)：
- 5 道解出 id（pdf-415/542/543/530/535）逐题登记处置结论 = 概念事实库解出。
- 排除项 pdf-483/541/177 标注「保持 null + 原因」（漂移/歧义/多部分合并键）。
- 比对脚本：P1-4 候选集合 = 解出 ∪ 排除，差集为空（0 静默遗漏）。

## T6 — 红线自检
**Status**: completed — ① 密钥扫描（`sk-|api_key|Bearer|secret`）P1-4 改动文件 0 命中；② 事实只存 JSON：`concept-facts.json` 仅追加（唯一删除行=version 1.4→1.5，既有 29 条未动），`.cs` 仅加 3 条路由触发短语无化学事实；③ 未改任何 Evaluation 代码（WRONG 正则/守恒近似未动）；④ 不蒙数：5 目标结论非 null 且均判正，新增采纳集恰为 5，无部分命中被采纳。
**Verification**（脚本化，结果入 review 证据）：
- 密钥扫描：本 spec 改动文件 grep `sk-|api_key|Bearer|secret` → 0 命中。
- 事实只存 JSON：`git diff --name-only -- '*.cs'` 中无化学事实硬编码；`concept-facts.json` 仅追加。
- WRONG 正则未改：`git diff -- .../Evaluation` 不含 `WRONG`/守恒近似变更。
- 不蒙数：5 目标题结论非 null 且均判正；无任何题为部分命中而被采纳。

## T7 — 独立 review
**Status**: completed — fresh-context 只读子代理（Agent ID 2db2fd46）独立复跑全部红线（build 0 错误 / test 2183 passed 0 failed / 两次 diff RED-LINE PASS / 各 verify 脚本 PASS / 密钥扫描 0 命中 / 事实唯一匹配 / 台账闭包），七维度全 **PASS**、无 Issue；主代理已落盘 [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/free-fill-concept-solver/review.md)。
**Outputs**: [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/free-fill-concept-solver/review.md)
**Process**: 委托 fresh-context **只读** 子代理（不得改任何文件），独立复跑：build / test / eval diff / 密钥扫描 / 事实唯一匹配断言 / 台账集合比对。七维度裁决（precision=1 / harm=0 / 无回归 / 不蒙数 / 事实只存 JSON / 台账自洽 / 红线）。任一 FAIL → Issue → 修复 → 复审。子代理仅返回报告文本，由主代理落盘 review.md。

---

## 决策日志（CQ）

| ID | 问题 | 选项 | 决策 | 理由 |
|----|------|------|------|------|
| CQ-1 | 是否新建独立自由填空求解器类？ | A 新建 / B 复用 ConceptFactSolver | **B** | 现有求解器已具备「唯一匹配→不蒙数 + IsAnswerCorrect 采纳门」契约，新建属重复造轮子且扩大表面积。 |
| CQ-2 | pdf-483（与已判正 pdf-477 触发重叠）是否纳入？ | A 纳入（接受漂移风险）/ B 排除保持 null | **B** | 漂移会威胁已判正题（LOST 风险），违反红线；暂无更窄唯一触发，保持 null 0 损失。 |
| CQ-3 | pdf-541（与 pdf-414 歧义）是否纳入？ | A 修改求解器支持互斥条件 / B 排除 | **B** | 修改求解器匹配语义属更大改动，超出本 spec 最小表面积；歧义→null 无增益无伤害，保持 null。 |
| CQ-4 | 路由放宽 cue 的放置位置？ | A calculation 之前 / B calculation 之后 | **A** | 目标题当前被 calculation 截走；必须在 calculation 之前拦截才能进入 concept 事实库。已预校验 0 漂移。 |

## Review History

| 轮次 | 日期 | 审查者 | 结论 | 关键问题与修复 |
|------|------|--------|------|----------------|
| 1 | 2026-11-22 | fresh-context 子代理 (2db2fd46) | PASS（七维度全过） | 无 Issue；无修复 |
