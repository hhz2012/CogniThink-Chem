# 独立 Review 门禁报告 — P1-4 自由填空概念求解器

> **审查者**：fresh-context 只读子代理（Agent ID 2db2fd46）
> **审查范围**：严格限定 P1-4——修改 [data/highschool/concept-facts.json](file:///d:/github/CogniThink-Chem/data/highschool/concept-facts.json)（version 1.4→1.5，仅追加 5 条事实）、[LlmGatewayService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs)（`ClassifyTaskDeterministic` 在 equation_writer 与 calculation 之间插入 3 条 concept-fill 路由 cue）、[ClassificationDeterministicTests.cs](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/ClassificationDeterministicTests.cs)（P1-4 路由正例 + 合取守卫）、[ConceptFactSolverTests.cs](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/ConceptFactSolverTests.cs)（guardrail 29→34 条目 + 5 条正例 InlineData）、[data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md)（新增 §L 闭环 + K.2 交叉更新 + K.8 后续注）。基线 `base_p0`（629 正确）→ 对照 `after_p12`（P1-4 基线，630 正确）→ 终态 `after_p14`（635 正确）。
> **审查纪律**：只读（全程仅 Read/Grep/Glob/Shell + 真实 Python 全路径，不落盘任何文件，不写本报告）；独立复跑全部验证命令与 git diff，**不信实现者自述**，每条结论附可复现证据（实际命令 + 实际输出）。
> **总体裁决**：**PASS**（七维度全部通过，precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 事实只存 JSON；无阻塞 Issue）。

---

## 0. 独立复跑概览（不信自述）

下表为本次 Review 实际执行的验证命令与结果（均为子代理亲自运行，非引用实现者输出文本）：

| # | 验证项 | 命令 / 方式 | 独立结果 |
|---|--------|-------------|----------|
| 1 | 权威 diff（回归红线，vs base） | `& "C:\...\Python312\python.exe" "_evalscratch\verify\diff_ckpt.py" base_p0 after_p14` | `old=757 new=757`；**GAINED (6)**: 5 道 P1-4 + `trap-limit-2`（P1-3 携带）；**LOST (0): []**；WORSEN=[]；`harm_old=0 → harm_new=0`；**RED-LINE PASS** |
| 1b | P1-4 隔离 diff（vs 基线） | `... diff_ckpt.py after_p12 after_p14` | **GAINED (5)**: `['pdf-415','pdf-530','pdf-535','pdf-542','pdf-543']`（恰为 spec 目标）；**LOST (0): []**；WORSEN=[]；harm=0；**RED-LINE PASS** |
| 2 | checkpoint 独立核算 + 精度扫描 | 内联只读 python 解析 `after_p14` | total 757 / correct **635** / adopted **635**；**`adopted∧¬correct = 0`**（凡采纳必判正） |
| 3 | 5 目标逐题状态 + 新增采纳集 | `p14_verify_after.py` | 5/5 目标全部 `correct=True & adopted=True`；**新增采纳集（after_p14 \ after_p12）恰为 5**（无多余采纳→不蒙数）；LOST=0 → ALL PASS |
| 4 | 路由 0 漂移预校验 | `p14_routing_drift_precheck.py` | affected=4；**risk(was_correct)=0**；3 路由目标 calculation→concept，2 fact-only 不变 → safe；漂移清单写入 `p14_tasktype_diff.txt` |
| 5 | 事实库唯一匹配 + 0 漂移 + 键命中 | `p14_post_t1_verify.py` | entries=34（29+5）；5 新事实各 matches=1（唯一）；drift onto correct=[]；5/5 keyword_hit=True → ALL PASS |
| 6 | 台账集合闭包 | `p14_closure_check.py` | 候选(8) = 解出(5) ∪ 排除(3)，交集空；3 排除（pdf-483/541/177）保持 `correct=False & adopted=False` → PASS |
| 7 | T0 审计复算 | `p14_t0_audit.py` | 5/8 cleanly solvable；pdf-483（歧义+命中已判正 pdf-477）、pdf-541（歧义）、pdf-177（多部分合并键）→ 与 spec §3 排除项完全一致 |
| 8 | 构建 | `dotnet build CogniThink.Chem.sln -c Release` | **已成功生成，0 错误**（exit 0；3 个预存警告位于未被 P1-4 触及的 `AdaptiveTeachingOrchestrator.cs` / `CrystalCellValidator.cs`，与本 spec 无关） |
| 9 | 测试 | `dotnet test CogniThink.Chem.sln -c Release --no-build --nologo` | Chem `失败:0 通过:913`；TutorBrain `失败:0 通过:1270`；合计 **2183 passed / 0 failed / 0 skipped**（≥ 基线 2170，含新增 13 条 P1-4 测试） |
| 10 | 改动范围 git diff | `git --no-pager diff -- <P1-4 文件>` | `concept-facts.json` 46 insertions / 1 deletion（唯一 deletion = version 1.4→1.5，既有 29 条零修改零删除 → 仅追加）；`LlmGatewayService.cs` P1-4 专属 hunk 仅 3 条 `if (...Contains(...)) return "concept";` 路由 cue，无化学真值；无 WRONG 正则 / 无守恒近似 / 无夹带 |
| 11 | 密钥扫描 | grep 4 个 P1-4 改动文件，pattern `sk-\|api_key\|Bearer\|secret`（不区分大小写） | **0 命中** |
| 12 | 台账自洽 | Read §L + 内联集合比对 | §L.1 解出 5 道逐题含 fact id/answer；§L.2 排除 3 道标注原因；§L.3 集合表 5+3=8；K.2 行已更新 ✅；K.8 注明 53→50；闭包脚本 PASS（差集为空，0 静默遗漏） |

> Python 解释器：`C:\Users\Administrator\AppData\Local\Programs\Python\Python312\python.exe`（PATH 中的 `python` 是 Windows Store 桩，不可用）。

---

## 1. 维度逐项裁决

### 维度 ① precision=1（无假阳性）— **PASS**
证据（独立复算，不信 summary）：
- 全库 `gatewayTruthAdopted==True && gatewayCorrect!=True` 数量 = **0**（`p14_verify_after.py` 实测 `adopted∧¬correct=0`；diff `harm_new=0`）。
- 精度由**构造保证**：`ConceptFactSolver` 仅在**唯一匹配**（count==1）时返回 `Answer`，0 命中或 >1 命中均返回 `null`（[ConceptFactSolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/ConceptFactSolver.cs) 守卫完好，且该文件**未被 P1-4 修改**）；评估侧 `IsAnswerCorrect` 采纳门要求答案命中答案键才判正→**凡采纳必判正**。
- 5 条新事实各在全库 757 题中唯一匹配 1 题（`matches=1`），对已判正题 0 漂移，5/5 答案键命中（`p14_post_t1_verify.py`）。
- 新增采纳集恰为 5 目标，无任何题为部分命中而被采纳（`p14_verify_after.py`）。

### 维度 ② harm=0（不删/盖正确数据，不采纳错误结论）— **PASS**
证据：
- 两次 diff `WORSEN null->new-harm (0):[]`、`IMPROVE harm->cleared (0):[]`、`harm_new=0`。
- `concept-facts.json` **仅追加**：`git diff` 唯一删除行为 `version 1.4→1.5` 元数据，既有 29 条事实零修改、零删除；5 条新 entry 与 spec §3 表格逐字一致。
- 无 `data/highschool/*.json` 其他文件改动；新增/修改 .cs 无化学真值硬编码（见维度 ⑤）。

### 维度 ③ 无回归（LOST=0 且 WORSEN=0）— **PASS**
证据（双保险：预校验构造 + 全库实证）：
- `base_p0 629 → after_p14 635`，**LOST=0 [] / WORSEN=0 []**（diff 实测）；`after_p12 630 → after_p14 635`，**LOST=0 []**。
- 路由 0 漂移**预校验**（`p14_routing_drift_precheck.py`）：忠实移植 `ClassifyTaskDeterministic`，对全库 757 题比对 old vs new 规则，**`risk(was_correct)=0`**——没有任何已判正题被新 cue 重分类。
  - affected=4 中，pdf-484 被「ph∧目的」cue 重分类 calculation→concept，但其 `baseline_correct=False`（属 spec OQ-3 预期：多部分题，无唯一事实匹配 → ConceptFactSolver 返回 null → 行为不变，仍未解，无伤害）；其余 3 道即 P1-4 目标。
- 单元测试 `P14_NoSteal_*` 固定合取守卫（ph 无目的 / 配制浓度缺可能 → 不被 concept 截走）与排除域参考题不被重分类。

### 维度 ④ 不蒙数（5 目标非 null 且均判正，无部分命中采纳）— **PASS**
证据：
- `ConceptFactSolver` 唯一匹配铁律：`count>1→null`、`count!=1→null`（守卫完好，文件未改）→ 歧义题（pdf-483/541）自动返回 null，不会蒙。
- `p14_post_t1_verify.py`：5 条新事实各 `matches=1`，无歧义；`p14_verify_after.py`：5 目标全部 `correct+adopted`，**新增采纳集恰为 5**，无多余采纳、无部分命中。
- 3 排除项（pdf-483 漂移/541 歧义/177 多部分合并键）全部保持 `correct=False & adopted=False`（`p14_closure_check.py`），0 损失。

### 维度 ⑤ 事实只存 JSON（无 .cs 化学硬编码）— **PASS**
证据（独立 git diff + 全文审读）：
- 化学真值**全部**在 `concept-facts.json`（5 条新事实的 `required`/`answer`/`derivation`/`constraint` 均在 JSON）。
- [LlmGatewayService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) P1-4 专属 hunk 仅 3 条路由触发短语（`lower.Contains("除去的离子")` / `ph && 目的` / `配制 && 浓度 && 可能` → `return "concept"`），**无化学方程/真值/答案硬编码**；`ConceptFactSolver.cs` 未被修改。
- 测试用例使用代表性题面，断言分类行为，无真实题库答案硬编码进 .cs。

### 维度 ⑥ 台账自洽（5 解出 + 3 排除，集合差集为空）— **PASS**
证据（独立集合比对）：
- [data-defect-register.md §L](file:///d:/github/CogniThink-Chem/data-defect-register.md) 齐全：L.1 解出 5 道逐题登记（含 fact id 与解出路径）；L.2 排除 3 道标注「保持 null + 原因」（漂移/歧义/多部分合并键）；L.3 集合闭包表 5+3=8。
- K.2 表 3 行（pdf-415/535/543）+ K.2 附 1 行已交叉更新为 ✅ 已解出；K.8 加 P1-4 后续更新注（现行未解 53→50，P1-1 闭包快照仍成立）。
- `p14_closure_check.py`：候选(8) = 解出(5) ∪ 排除(3)，两两无交集、差集为空，**0 静默遗漏**。

### 维度 ⑦ 红线（密钥 / WRONG 正则 / 守恒近似 / 不蒙数）— **PASS**
证据：
- 密钥：4 个 P1-4 改动文件（`concept-facts.json`、`LlmGatewayService.cs`、`ClassificationDeterministicTests.cs`、`ConceptFactSolverTests.cs`）grep `sk-|api_key|Bearer|secret` → **0 命中**。
- WRONG 正则 / 守恒近似：P1-4 **未触及任何 Evaluation 代码**；`LlmGatewayService.cs` P1-4 hunk 仅路由 cue，无 `WRONG` 正则 / 守恒近似变更。
- 不蒙数：见维度 ④，唯一匹配铁律 + 新增采纳集恰为 5。
- 透明说明：工作树 `LlmGatewayService.cs` 的 diff 中**同时含 P1-2 / P1-3 的既有改动**（multi-part 2b 注入块、HydrolysisPh/RedoxRatio 兜底块），属多 spec 并发混合工作树的既有状态（P1-2/P1-3 均已独立 review PASS），**非 P1-4 引入**；P1-4 专属 hunk 本身干净，且全库 diff 实证 LOST=0/WORSEN=0 隔离了在途改动的影响。

---

## 2. 上下文说明（工作树混合状态与 P1-4 范围界定，不影响裁决）

- `git status --short` 显示工作树为多 spec 并发：含 P1-2/P1-3 的未提交 .cs 改动、其他 spec 的 `pdf_questions.json` 内容回填、svg 新增等。
- **`src/.../data/pdf_questions.json` 被修改**（在途内容回填，属其他图像/数据 spec），**不在 P1-4 改动文件清单内**。行为中立：after_p14 全库 LOST=0、`adopted∧¬correct=0`，未引入假阳性或回归。此为范围外上下文，不计入 P1-4 裁决。
- P1-4 严格范围 = `concept-facts.json`（仅追加 5 条）+ `LlmGatewayService.cs`（3 条路由 cue）+ 2 个测试文件 + `data-defect-register.md` §L/K.2/K.8。基线 `base_p0`（629 正确）与 `after_p12`（630 正确）对照均成立。
- AC-4 据实达成：T0 审计预测 5 道可解，eval 实证 **GAINED=5**（超 AC-4 ≥1 要求），precision=1 / harm=0 / LOST=0 全线守住。

---

## 3. 总体结论

| 维度 | 裁决 | 关键独立证据 |
|------|------|--------------|
| ① precision=1（无假阳性） | **PASS** | `adopted∧¬correct=0`；唯一匹配→不蒙数 + IsAnswerCorrect 采纳门；5 新事实各 matches=1、5/5 键命中 |
| ② harm=0 | **PASS** | `harm_new=0`；WORSEN=0；`concept-facts.json` 仅追加（唯一删行=version 1.4→1.5）；无化学真值硬编码 |
| ③ 无回归（LOST=0 且 WORSEN=0） | **PASS** | 两次 diff LOST=0/WORSEN=0；`p14_routing_drift_precheck.py` risk(was_correct)=0；单测合取守卫 |
| ④ 不蒙数 | **PASS** | 唯一匹配铁律（守卫完好）；新增采纳集恰为 5；3 排除项保持 null，0 损失 |
| ⑤ 事实只存 JSON | **PASS** | 化学真值全在 concept-facts.json；.cs 仅 3 条路由触发短语，无化学硬编码；ConceptFactSolver.cs 未改 |
| ⑥ 台账自洽 | **PASS** | §L 解出 5 + 排除 3；`p14_closure_check.py` 候选 8 = 5 ∪ 3，差集空，0 静默遗漏 |
| ⑦ 红线（密钥/WRONG/守恒/不蒙数） | **PASS** | 密钥 0 命中；未改 Evaluation（WRONG 正则/守恒近似未动）；不蒙数 verified |

**最终裁决：PASS。** 未发现任何 precision / harm / 回归风险或可执行代码缺陷。P1-4 改动构造上保证 `precision=1 / harm=0 / LOST=0 / 不蒙数 / 事实只存 JSON`，并经全量 757 题 eval（after_p14 correct=635，GAINED=5）与 2183 测试（0 failed）实证。

**Issue 清单：无。**
