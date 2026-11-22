# 独立 Review 门禁报告 — P1-2 多部分大题子问分发

> **审查者**：fresh-context 只读子代理（Agent ID 8e805aa1）
> **审查范围**：严格限定 P1-2——新增 [MultiPartUtility.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/MultiPartUtility.cs)、[LlmGatewayService.MultiPartDispatch.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.MultiPartDispatch.cs)、[MultiPartUtilityTests.cs](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/MultiPartUtilityTests.cs)、[MultiPartAndScoringTests.cs](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/MultiPartAndScoringTests.cs)，修改 [LlmGatewayService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs)（`ProcessStudentMessageAsync` L108-119 注入点）、[GatewayVsLlmEvaluationService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs)（`AllKeywordsHit` L372-401 + `multi_part` 采纳分支 L675-680）、[data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md)（K.1 L341-377 台账 P1-2 说明）。基线 `base_p0` → 对照 `after_p13`（P1-3 终态）→ 终态 `after_p12`（P1-2 终态）。
> **审查纪律**：只读（全程仅 Read/Grep/Glob/Shell + 内联 python，不落盘任何文件，不写本报告）；独立复跑全部验证命令与 git diff，**不信实现者自述**，每条结论附可复现证据（实际命令 + 实际输出）。
> **总体裁决**：**PASS**（七维度全部通过，precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 事实只存 JSON；附 3 项非阻塞观察，见 §2 与 §3 末）。

---

## 0. 独立复跑概览（不信自述）

下表为本次 Review 实际执行的验证命令与结果（均为子代理亲自运行，非引用实现者输出文本）：

| # | 验证项 | 命令 / 方式 | 独立结果 |
|---|--------|-------------|----------|
| 1 | 权威 diff（回归红线） | `& "C:\Users\Administrator\AppData\Local\Programs\Python\Python312\python.exe" "_evalscratch\verify\diff_ckpt.py" base_p0 after_p12` | `old=757 new=757`；**GAINED (1): ['trap-limit-2']**；**LOST (0): []**；WORSEN=[]；`harm_old=0 → harm_new=0`；**RED-LINE PASS** |
| 1b | P1-2 隔离 diff | `... diff_ckpt.py after_p13 after_p12` | **GAINED (0): []；LOST (0): []；WORSEN (0): []；harm=0；RED-LINE PASS**（P1-2 净影响 = 0） |
| 2 | checkpoint 独立核算 + 精度扫描 | 内联只读 python 解析 `base_p0` 与 `after_p12` 两 jsonl | base：total 757 / correct 629 / adopted 629；after_p12：total 757 / correct 630 / adopted 630；**`adopted∧¬correct = 0`**（after_p12 与 base 均为 0） |
| 3 | 27 K.1 逐题状态 | 同上内联脚本（取 `case.id`） | 27/27 全部在库（missing=[]），逐题均为 `(correct=False, adopted=False)`；`correct=True OR adopted=True` 计数 = **0**（0 错采纳） |
| 4 | LOST / GAINED | 同上（base→after 逐案对比） | **LOST=0 []**；**GAINED=1 ['trap-limit-2']**（`taskType=calculation`，非 multi_part，为 P1-3 携带） |
| 4b | 记录级字节一致性（强于聚合 diff） | 内联 python 逐案比对 after_p13 ↔ after_p12 的 `gatewayCorrect / gatewayTruthAdopted / gatewayAnswer / gatewayConclusion` | **differing records = 0 []** → P1-2 对 eval 零净影响，非多部分题字节级不变 |
| 5 | summary 核对 | Read `_evalscratch/checkpoint_after_p12_summary.txt` | `total=757 / gatewayCorrect=630 / gatewayTruthCorrect=630 / precision=1 / harmCount=0` |
| 6 | 构建 | `dotnet build CogniThink.Chem.sln -c Release -nologo -v minimal` | **已成功生成。0 警告 / 0 错误**（exit 0） |
| 7 | 测试 | `dotnet test CogniThink.Chem.sln -c Release --no-build --nologo` | TutorBrain `失败:0 通过:1257`；Chem `失败:0 通过:913`；合计 **2170 passed / 0 failed**（≥ 基线 2150） |
| 8 | 改动范围 git diff | `git --no-pager diff -- <两修改 .cs>` | P1-2 专属 hunk 干净：仅 `AllKeywordsHit`、`multi_part` 采纳分支、`ProcessStudentMessageAsync` 2b 注入块；无 WRONG 正则 / 无守恒近似 / 无夹带 |
| 9 | 密钥扫描 | `Select-String` 7 个 P1-2 文件，pattern `sk-[A-Za-z0-9]\|api[_-]?key\|Bearer\|secret` | **0 命中**（NO_HITS） |
| 9b | 台账 27-id 集合一致性 | 内联 python 抽取 `data-defect-register.md` K.1 表格 id 与规范 27 集合比对 | `count=27`；`in table not in known: []`；`in known not in table: []`；**identical sets: True**（差集为空，0 静默遗漏） |
| 9c | 事实库改动 | `git --no-pager diff --name-only -- data/highschool/` | 空输出 → **0 改动** |
| 9d | multi_part 接线 + 生产触发统计 | 内联 python 统计 `taskType` 分布 + Read 源码 | `records with taskType=multi_part: 0`；接线链路：`engineRequest.TaskType=multi_part`(L117) → `GatewayResult.TaskType`(L126) → `result.TaskType` → AND 分支(L675) |

> Python 解释器：`C:\Users\Administrator\AppData\Local\Programs\Python\Python312\python.exe`（PATH 中的 `python` 是 Windows Store 桩，不可用）。

---

## 1. 维度逐项裁决

### 维度 ① precision=1（无假阳性）— **PASS**
证据（独立复算，不信 summary）：
- 全库 `gatewayTruthAdopted==True && gatewayCorrect!=True` 数量 = **0**（内联 python 实测，after_p12 与 base 均 0）。
- AND 判分**仅作用 multi_part**：[GatewayVsLlmEvaluationService.cs L675](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs) `if (gw.TaskType == MultiPartUtility.MultiPartTaskType)` 分支位于 calculation/choice 等分支之前，且仅当 `AllKeywordsHit` 全命中才 `return conclusion`，否则 `return null`；非多部分题走原 OR 路径字节级不变（after_p13==after_p12 记录级 0 差异实证）。
- 核心精度守卫单测存在且通过：`OneMissing_ReturnsFalse`（缺子问 (3)→不采纳）、`AndSemantics_DiffersFrom_OrSemantics`（同一份部分命中结论 OR 判正 / AND 判负，证明不退化成 OR 假阳性）——见 [MultiPartAndScoringTests.cs L26-45](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/MultiPartAndScoringTests.cs)。
- 27 道 K.1 全部 `(False, False)`，0 错采纳。

### 维度 ② harm=0（不删/盖正确数据，不采纳错误结论）— **PASS**
证据：
- summary `harmCount=0`；diff `WORSEN null->new-harm (0):[]`、`IMPROVE harm->cleared (0):[]`、`harm_new=0`。
- `data/highschool/*.json` 0 改动（`git diff --name-only` 空）；新增 .cs 无化学真值硬编码（见维度 ⑤）。
- 采纳构造安全：凡采纳必经 AND 全对校验（`AllKeywordsHit` 返回 true 才 `return conclusion`，否则 `null`），不存在采纳错误结论的路径。

### 维度 ③ 无回归（LOST=0；非多部分字节级不变）— **PASS**
证据（双保险：opt-in 构造 + 记录级实证）：
- base 629 correct → after 630，**LOST=0 []**（内联实测）。
- **after_p13 vs after_p12 记录级 0 差异**（`gatewayCorrect / gatewayTruthAdopted / gatewayAnswer / gatewayConclusion` 全部相等）→ P1-2 代码对 eval 零净影响，非多部分题字节级不变。
- opt-in 构造：[LlmGatewayService.cs L111](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) `if (engineResponse.Ok is not true && MultiPartUtility.IsMultiPart(rawText))`——已解对题（Ok=true）绝不触发分发；dispatch 返回 null 时 `if (dispatched is not null)` 不成立，`engineResponse` / `TaskType` 均不变，等价于无分发。

### 维度 ④ 不蒙数（任一子问未解→整体 null，不输出部分答案）— **PASS**
证据：
- [LlmGatewayService.MultiPartDispatch.cs L41-42](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.MultiPartDispatch.cs)：`if (subResp.Ok is not true || string.IsNullOrWhiteSpace(subResp.FinalResult)) return null;`——任一子问未唯一解出即整体 null，不输出部分答案。
- L28：`if (subs.Count < 2) return null;` 与检测器口径一致。
- 图像子问不传 `imageAssets`（L37-38 注释）→ 图像子问 null → 整体 null，诚实留痕 P2-3，不蒙数。
- 实证：after_p12 中 0 条 `multi_part` 记录被产出/采纳（27 K.1 全 null），与 AC-4 校准 ≥0 一致。

### 维度 ⑤ 事实只存 JSON（无 .cs 化学硬编码）— **PASS**
证据（独立 git diff + 全文审读）：
- **无 `data/highschool/*.json` 改动**：`git --no-pager diff --name-only -- data/highschool/` 输出为空。
- 全文 Read 两个新增 .cs：[MultiPartUtility.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/MultiPartUtility.cs) 仅含 `(N)` 编号正则 `[(（]\s*(\d+)\s*[)）]` 与切分逻辑；[LlmGatewayService.MultiPartDispatch.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.MultiPartDispatch.cs) 仅含分发/聚合/兜底逻辑，逐子问复用现有 `CallEngineAsync`，**不新建求解器、无化学方程/真值/答案硬编码**。
- 测试用例使用通用关键词 `A / 0.2 / 19`、`FeCl₃`，无真实题库答案硬编码。

### 维度 ⑥ 台账自洽（27 id 无遗漏，K.1 有 P1-2 说明）— **PASS**
证据（独立集合比对）：
- [data-defect-register.md K.1 L349-377](file:///d:/github/CogniThink-Chem/data-defect-register.md) 表格逐题登记 27 个 id；内联 python 集合比对：`identical sets: True`，差集为空，0 静默遗漏。
- K.1 节含 P1-2 交付说明（分发骨架 + AND 判分 + AC-4 校准为 ≥0）与实证说明（27 道全 `correct=false / adopted=false`、0 错采纳、diff 证据）。

### 维度 ⑦ 红线（密钥 / WRONG 正则 / 守恒近似 / opt-in）— **PASS**
证据：
- 密钥：7 个 P1-2 改动文件（2 新增 .cs + 2 测试 .cs + 2 修改 .cs + data-defect-register.md）grep 0 命中。
- WRONG 正则 / 守恒近似：`git diff` 两个修改文件的 P1-2 专属 hunk 仅含 `AllKeywordsHit`、`multi_part` 采纳分支、`ProcessStudentMessageAsync` 2b 注入块——**未触及 `MultiSelectChoiceValidator` / `FactChoiceMatcher` 的 WRONG 正则，未把守恒近似接入 choice**。
- opt-in：见维度 ③，分发仅在整题 `Ok!=true && IsMultiPart` 时 fallback 激活。
- 透明说明：两个修改文件的工作树 diff 中**同时含 P1-3 的既有改动**（`LlmGatewayService.cs` 的 HydrolysisPh/RedoxRatio 兜底块；`GatewayVsLlmEvaluationService.cs` 的 calculation 分支门控改动）。这是多 spec 并发混合工作树的既有状态（P1-3 已在 tasks.md CQ-2 独立完成并 review PASS），**非 P1-2 引入**；P1-2 专属 hunk 本身干净，且 `after_p13==after_p12` 记录级 0 差异证明 P1-2 净影响为零。

---

## 2. 上下文说明（工作树混合状态与 P1-2 范围界定，不影响裁决）

- `git status --short` 显示工作树为多 spec 并发：含 P1-3 的 `HydrolysisPhCalculator.cs` / `RedoxRatioCalculator.cs`（untracked）、其他 spec 的 `pdf_questions.json` 修改、svg 新增等。
- **`src/.../data/pdf_questions.json` 被修改**（触及 K.1 id 如 pdf-166/167/168/169/260/261/461），diff 片段显示为 **svg 路径重排**，属其他图像 spec，**不在 P1-2 改动文件清单内**。行为中立：after_p12 中 27 K.1 仍全 `(False, False)`、全库 LOST=0、`adopted∧¬correct=0`，未引入假阳性或回归。此为范围外上下文，不计入 P1-2 裁决。
- P1-2 严格范围 = 2 新增 .cs + 2 测试 .cs + 2 修改 .cs 的 P1-2 专属 hunk + data-defect-register.md K.1 更新。基线 `base_p0`（629 正确）对照成立。
- AC-4 已据实校准为 ≥0（T0 + P1-3 重审确认 0/27 可由现有求解器全覆盖子问），本 spec 交付纯基础设施、零即时增益、零回归——与实测 GAINED=1（仅 trap-limit-2，P1-3 携带，非 multi_part）一致。

---

## 3. 总体结论

| 维度 | 裁决 | 关键独立证据 |
|------|------|--------------|
| ① precision=1（无假阳性） | **PASS** | `adopted∧¬correct=0`；AND 仅作用 multi_part；精度守卫单测通过；27 K.1 全 (False,False) |
| ② harm=0 | **PASS** | `harmCount=0`；WORSEN=0；`data/highschool/` 0 改动；采纳必经 AND 全对 |
| ③ 无回归（LOST=0，字节级不变） | **PASS** | LOST=0；after_p13==after_p12 记录级 0 差异；opt-in 构造 |
| ④ 不蒙数 | **PASS** | 任一子问 null→整体 null；0 multi_part 产出；图像子问留痕 P2-3 |
| ⑤ 事实只存 JSON | **PASS** | 新增 .cs 无化学硬编码；`data/highschool/` 0 改动 |
| ⑥ 台账自洽 | **PASS** | 27-id 集合差集为空；K.1 有 P1-2 交付说明 |
| ⑦ 红线（密钥/WRONG/守恒/opt-in） | **PASS** | 密钥 0 命中；WRONG 正则/守恒近似未改；opt-in 确认 |

**最终裁决：PASS。** 未发现任何 precision / harm / 回归风险或可执行代码缺陷。P1-2 改动构造上保证 `precision=1 / harm=0 / LOST=0 / 不蒙数`，并经全量 757 题 eval 与 2170 测试实证。

**非阻塞观察（不改变 PASS，仅透明披露）**：
1. 工作树两个修改文件夹带 P1-3 既有改动（混合树常态，P1-2 专属 hunk 干净，已由 after_p13→after_p12 记录级 0 差异隔离证明）；
2. `pdf_questions.json` 被其他图像 spec 修改（范围外，行为中立，27 K.1 仍全 null、全库无回归）；
3. `multi_part` AND 分支在本次 eval 生产路径未被触发（0 条 multi_part 记录——因 0/27 子问可全解），其正确性由单测（AC-5 / AC-6 精度守卫）+ 代码接线审查保证，待 P1-4 / P2-3 解锁子问后可由 eval 实证。此为 AC-4 校准 ≥0 的预期结果，非缺陷。
