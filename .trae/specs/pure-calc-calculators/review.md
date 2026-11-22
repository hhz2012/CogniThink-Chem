# 独立 Review 门禁报告 — P1-3 纯计算域计算器

> **审查者**：fresh-context 只读子代理
> **审查范围**：严格限定 P1-3——新增 [HydrolysisPhCalculator.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/HydrolysisPhCalculator.cs)、[RedoxRatioCalculator.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/RedoxRatioCalculator.cs)，修改 [LlmGatewayService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs)（CallCalculation 末尾兜底）、[GatewayVsLlmEvaluationService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs)（calculation 采纳门）、[data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md)（K.2 台账）。基线 `base_p0` → 终态 `after_p13`。
> **审查纪律**：只读（仅新建本文件，验证用临时脚本已全部删除）；独立复跑验证命令与 git diff，**不信实现者自述**，每条结论附可复现证据（实际命令 + 实际输出）。
> **总体裁决**：**PASS**（AC-1 ~ AC-6 全部通过，precision=1 / harm=0 / LOST=0 / WORSEN=0；附 1 项非阻塞 spec 文案观察，见维度 6 与上下文说明）。

---

## 0. 独立复跑概览（不信自述）

下表为本次 Review 实际执行的验证命令与结果（均为子代理亲自运行，非引用实现者输出文本）：

| 验证项 | 命令 / 方式 | 独立结果 |
|--------|-------------|----------|
| 权威 diff（AC-1/2/3 回归） | `& "C:\Users\Administrator\AppData\Local\Programs\Python\Python312\python.exe" "_evalscratch\verify\diff_ckpt.py" base_p0 after_p13` | `old=757 new=757`；**GAINED (1): ['trap-limit-2']**；**LOST (0): []**；WORSEN=[]；`harm_old=0 → harm_new=0`；**RED-LINE PASS** |
| checkpoint 独立解析（AC-1） | 内联只读 Python 解析 `checkpoint_after_p13.jsonl`（取 `case.id`） | trap-limit-2：`gatewayCorrect=True` / `gatewayTruthAdopted=True`，conclusion 含「…物质的量比为 1:2」；pdf-517：`gatewayCorrect=False` / `gatewayTruthAdopted=False`，conclusion 含「pH ≈ 9.76」，`gatewayAnswer` 为空（未采纳）；全库 `gatewayCorrect=630` / `adopted=630` / `adopted&&!correct=0` |
| summary 核对（AC-1） | Read `checkpoint_after_p13_summary.txt` | `total=757 / gatewayCorrect=630 / gatewayTruthCorrect=630 / precision=1 / harmCount=0` |
| base_p0 基线独立核算 | 内联 Python 解析 `checkpoint_base_p0.jsonl` | `gatewayCorrect=629` / harm=0；trap-limit-2 与 pdf-517 基线均为 `correct=False & adopted=False` → **gain = +1（629→630）**，与 diff GAINED 逐字吻合 |
| 全库翻转独立交叉验证（AC-2） | 内联 Python 逐题对比 base_p0 ↔ after_p13 的 `gatewayCorrect` 与 `gatewayConclusion` | 全库仅 **1 个翻转**：trap-limit-2 `False→True`；**0 个 `True→False`**；calc 域仅 2 道结论变化（trap-limit-2 解对 / pdf-517 结论新增但未采纳、仍错）；`base_correct=True → after 非 True` = **NONE** |
| 探针 stray（AC-2） | Read `_evalscratch/verify/p13_probe_report.txt` | **生产门控视图（taskType==calculation）TOTAL PROBLEMS = 0**；`regression-risk(already-correct calc)=0`；hydrolysis gated=1（pdf-517）、redox gated=1（trap-limit-2） |
| 采纳门控（AC-6） | Read `GatewayVsLlmEvaluationService.cs` L641-650 + `git diff` | calculation 分支（L646-650）采纳前必经 `IsAnswerCorrect`：`return IsAnswerCorrect(conclusion, c) ? conclusion : null;`（L649），不再盲目 `return conclusion` |
| 事实只存 JSON（AC-4） | Read 两个新 `.cs` 全文 + `git diff --name-only -- data/highschool/` + Read `acid-constants.json` | 常数全部来自 `RuleStore`/`acid-constants.json`（HClO Ka=3.0×10⁻⁸、Kw、acidLadders）；`.cs` 无硬编码化学真值/答案；**`data/highschool/` 0 改动**；`RedoxRatioCalculator` 的 1:2 由解析配平方程计量数动态算出，非硬编码 |
| 台账闭环（AC-5） | 内联只读 Python 校验 `data-defect-register.md` + `git show HEAD:data-defect-register.md` 计数 | 14 排除 + 1 解出 = **15 全部在 register**；K.2 附表 14/14；**pdf-172 在 HEAD 出现 0 次 → 确为本次新增登记** |
| 构建（AC-3） | `dotnet build CogniThink.Chem.sln -c Release -v minimal` | **0 警告 / 0 错误**（已用时间 00:00:03） |
| 测试（AC-3） | `dotnet test CogniThink.Chem.sln -c Release --no-build --nologo -v minimal` | **2150 passed / 0 failed / 0 skipped**（TutorBrain 1237 + Chem 913），exit code 0，与基线 2150 一致 |

> Python 解释器：`C:\Users\Administrator\AppData\Local\Programs\Python\Python312\python.exe`（PATH 中的 `python` 是 Windows Store 桩，不可用）。

---

## 1. 维度逐项裁决

### 维度 AC-1：新增解对 ≥1（trap-limit-2），pdf-517 不强行匹配 — **PASS**

证据（独立复算，不信 summary）：
- 权威 diff `diff_ckpt.py base_p0 after_p13` 实际输出：`GAINED (1): ['trap-limit-2']`、`LOST (0): []`、`harm_old=0 harm_new=0`、`RED-LINE: ... -> PASS`。
- `checkpoint_after_p13.jsonl` 独立解析：
  - **trap-limit-2**：`gatewayCorrect=True`、`gatewayTruthAdopted=True`、`gatewayOk=True`、`taskType=calculation`，`gatewayConclusion` =「反应中元素 N 发生歧化：1 份被还原（作氧化剂）、2 份被氧化（作还原剂），故氧化剂与还原剂的物质的量比为 **1:2**。」→ 由 false 翻转为 true 且被采纳。
  - **pdf-517**：`gatewayCorrect=False`、`gatewayTruthAdopted=False`、`gatewayOk=True`、`gatewayConclusion` =「ClO^- 水解使溶液呈碱性，pH ≈ **9.76**（HClO 的 Ka=3.0×10^-8，Kb=Kw/Ka，[OH⁻]=√(Kb·c)）。」但 `gatewayAnswer` 为空 → **计算器诚实输出 9.76，因与冲突键 10.3 不匹配未被采纳**，无 gain 无 harm，符合 AC-1「pdf-517 保持 correct=false」。
- 总量自洽：base_p0 独立核算 `gatewayCorrect=629` → after_p13 `630`，净增恰好 1（= trap-limit-2）；`adopted&&!correct=0`。

### 维度 AC-2：零回归与无 stray 误命中 — **PASS**

证据（双保险：探针 + 全库 diff + 独立翻转分析）：
- **探针**：`p13_probe_report.txt` 生产门控视图（仅 `taskType==calculation` 到达 `CallCalculation`）下 **TOTAL PROBLEMS = 0**，`regression-risk(already-correct calc)=0`。探针中 hydrolysis 虽在 8 道 **choice** 题（pdf-267/268/270/281/283/292/427/518）上匹配，但它们 `task=choice`，**不进入** `CallCalculation`（路由证据见下），标注为 `[ALREADY-CORRECT-regression-risk]` 仅为探针全量扫描的提示，生产中零触及。
- **路由护栏**：[LlmGatewayService.cs L570-574](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L570-L574) `CallCalculation` 仅在 `case "calculation":` 内调用；新兜底位于 [LlmGatewayService.cs L1160-1166](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L1160-L1166)（既有求解器全 miss 后才触发）。
- **全库独立翻转分析**：base_p0 ↔ after_p13 逐题对比，correct 状态仅 **1 个翻转（trap-limit-2 False→True）**，**0 个 True→False**；`base_correct=True → after 非 True` = NONE。与 `diff_ckpt` 的 LOST=0 / WORSEN=0 完全互证。

### 维度 AC-3：单元测试零失败 — **PASS**

证据：
- `dotnet build CogniThink.Chem.sln -c Release -v minimal` → **0 警告 / 0 错误**。
- `dotnet test CogniThink.Chem.sln -c Release --no-build --nologo -v minimal` → 实际输出：
  - `已通过! - 失败: 0，通过: 1237，已跳过: 0，总计: 1237 - CogniThink.TutorBrain.Tests.dll`
  - `已通过! - 失败: 0，通过: 913，已跳过: 0，总计: 913 - CogniThink.Chem.Tests.dll`
  - 合计 **2150 passed / 0 failed / 0 skipped**，exit code 0，不低于基线 2150。

### 维度 AC-4：事实只存 JSON、求解器无硬编码化学真值 — **PASS**

证据（独立 `git diff` + 全文审读 + grep）：
- **无 `data/highschool/*.json` 改动**：`git --no-pager diff --name-only -- data/highschool/` 输出为空（本 spec 不新增/修改任何常数 JSON）。
- **常数来自 RuleStore/JSON**：[acid-constants.json](file:///d:/github/CogniThink-Chem/data/highschool/acid-constants.json) 含 `HClO` Ka=3.0×10⁻⁸（L11）、`kw`=1×10⁻¹⁴（L4）、`acidLadders.HClO=["HClO","ClO^-"]`（L36）；[HydrolysisPhCalculator.cs L16-17](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/HydrolysisPhCalculator.cs#L16-L17) 经 `RuleStore.Load().Query<AcidConstantsData>("acid-constants")` 加载，Ka 查 `data.Ka`（L37）、Kw 查 `data.Kw`（L41）——**Kw 亦来自 JSON，未硬编码 1e-14**。
- **`.cs` 无化学判定真值/答案硬编码**：
  - [HydrolysisPhCalculator.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/HydrolysisPhCalculator.cs) 数值字面量仅：正则、pH 有效域界（0<pH<14，数学域约束，ConstraintNote 标注「25℃」）、`Math.Sqrt/Log10/Pow`；无任何题目答案或特定 Ka/Ksp/摩尔质量字面量。
  - [RedoxRatioCalculator.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/RedoxRatioCalculator.cs) 的 O=-2 / H=+1 / F=-1 / Ⅰ主族+1 / Ⅱ主族+2（L151-157）是**通用氧化态赋值规则**（解析任意方程所需的算法规则），有注释（L115-116）；trap-limit-2 的「1:2」由 `nReduced`/`nOxidized` 动态求和得出（L60-66），探针亦确认从方程 `3NO2 + H2O = 2HNO3 + NO` 解析得 `ratio=1:2 (el=N)`——**非硬编码 1:2**，是泛化解析器。
- 本 spec 未触碰 `pdf_questions.json`、`Program.cs`、tests（属其他在途 spec，见上下文说明）。

### 维度 AC-5：排除候选台账闭环（rubric ≥4） — **PASS（5/5）**

证据（独立复算，不依赖实现者输出）：
- 期望审计集合 = 14 排除 + 1 解出 = 15：`trap-limit-3, pdf-030, pdf-517, pdf-415, pdf-197, pdf-263, pdf-355, pdf-356, pdf-463, pdf-160, pdf-165, pdf-172, pdf-526, pdf-527`（排除）+ `trap-limit-2`（解出）。
- 内联脚本以独立 token 正则（`(?<![\w-])id(?![\w-])`，避免 pdf-17 误匹配 pdf-172）核对 `data-defect-register.md`：**15/15 全部出现，missing = NONE**。
- `### K.2 附：P1-3 纯计算域审计排除闭环` 小节存在，表内 14 个排除 id 全覆盖（14/14），每题含根因 + 归属 spec/处置 + 登记位置。
- **pdf-172 确为新增登记**：用可靠计数法（正则 `Matches().Count`，规避 PowerShell `$LASTEXITCODE` 对 cmdlet 失效的坑）核对——`git show HEAD:data-defect-register.md` 中 `pdf-172` 出现 **0 次**，工作树版本出现 **2 次**（§E 主表缺陷行 + K.2 附表行），且主表行标注「**P1-3 新登记**」。→ 0 静默遗漏，rubric 锚点 5 满足。

### 维度 AC-6：采纳门控安全 — **PASS（附 1 项非阻塞 spec 文案观察）**

证据：
- [GatewayVsLlmEvaluationService.cs L644-650](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L644-L650) 的 `git diff` 显示 calculation 分支由原「直接 `return conclusion`」改为：
  ```csharp
  if (gw.TaskType == "calculation")
  {
      if (string.IsNullOrWhiteSpace(conclusion)) return null;
      return IsAnswerCorrect(conclusion, c) ? conclusion : null;   // L649
  }
  ```
  → **凡被采纳的 calculation 结论必经 `IsAnswerCorrect` 答案键校验**，不再因 `Ok=true` 盲目采纳。
- 行为交叉验证（checkpoint）：
  - trap-limit-2（计算值 1:2 与键 `['1','2','1:2']` 化学一致）→ `gatewayTruthAdopted=True`、`gatewayCorrect=True`。
  - pdf-517（诚实 9.76 vs 冲突键 10.3）→ `gatewayTruthAdopted=False`、`gatewayAnswer` 为空，`gatewayCorrect=False` → 错误/冲突结论未被采纳。
  - 全库 `adopted && !correct = 0`（harm=0 交叉验证成立）。
- **非阻塞观察（spec 文案不一致，非实现缺陷）**：spec.md AC-6 的 Pass Condition 字面写「目标 2 道 `gatewayTruthAdopted=true`」，但同一 spec 的 Overview / AC-1 / Background 反复明确要求 **pdf-517 因答案键冲突不得采纳**（`truthAdopted=false`）。二者字面矛盾。实现选择了后者（不采纳 pdf-517），这正是保护 `precision=1 / harm=0` 红线的正确行为；若按 AC-6 字面强行要求 pdf-517 `adopted=true`，反而会破红线。建议后续修订 spec.md AC-6 措辞为「凡采纳必经 `IsAnswerCorrect`；trap-limit-2 采纳=true；pdf-517 因键冲突不采纳（safe non-adoption）；全库 harm=0」。此项不阻塞本次门禁（实现行为正确）。

---

## 2. 上下文说明（不影响本 spec 裁决，但须告知）

当前工作树是**多 spec 并发的混合状态**，除 P1-3 外还包含（已用 `git status --short` 独立确认）：
- `multi-part-compound-answer-fix`：`.trae/specs/multi-part-compound-answer-fix/*`、`src/.../data/pdf_questions.json`、`src/CogniThink.TutorBrain.PdfOcrRepair/Program.cs` 有改动，另有多个 `pdf_questions.json.bak_*` 备份文件；
- `choice-coverage-expansion`：`tests/.../*ChoiceValidatorTests.cs`、`MultiSelectChoiceValidatorTests.cs` 等测试真值表改动；
- `verify-50-triage` / `verify-fix-all-suspects` / `multi-part-subquestion-dispatch` 等在途 spec 目录，以及 `README.md`、`docs/*`、`ARCHITECTURE.md` 等文档改动。

本 Review **严格限定 P1-3 范围**：
- 已独立确认 `git diff -- <P1-3 文件>` 中，`LlmGatewayService.cs` 仅含 CallCalculation 末尾 +8 行兜底（单一 hunk，@@ -1157,6 +1157,14 @@）、`GatewayVsLlmEvaluationService.cs` 仅含 calculation 采纳门 +2/-2（单一 hunk），无其他 spec 的夹带改动；
- 已独立确认 base_p0 ↔ after_p13 两 checkpoint 间 757 题仅 1 题 correct 翻转（False→True），无题数据漂移 → P1-3 未篡改任何题数据；
- `dotnet test` 在当前混合工作树上仍 **2150 passed / 0 failed**，说明 P1-3 改动与并发改动无编译/测试冲突。

---

## 3. 总体结论

| 维度 | 裁决 | 关键独立证据 |
|------|------|--------------|
| AC-1 新增解对 ≥1 / pdf-517 不强行匹配 | **PASS** | diff GAINED=['trap-limit-2']；trap-limit-2 correct=True&adopted=True（结论含 1:2）；pdf-517 correct=False&adopted=False（结论含 9.76，未采纳）；629→630 |
| AC-2 零回归 / 无 stray | **PASS** | LOST=[]/WORSEN=[]/harm 0→0；独立翻转分析 0 个 True→False；探针生产门控 TOTAL PROBLEMS=0；CallCalculation 仅 calculation 触发 |
| AC-3 单元测试零失败 | **PASS** | build 0 错；test 2150 passed / 0 failed / 0 skipped |
| AC-4 事实只存 JSON / 无硬编码真值 | **PASS** | `data/highschool/` 0 改动；常数来自 acid-constants.json；`.cs` 无化学真值/答案硬编码；RedoxRatio 泛化解析非硬编码 1:2 |
| AC-5 台账闭环（rubric） | **PASS（5/5）** | 15/15 id 全在 register；K.2 附表 14/14；pdf-172 在 HEAD 0 次 → 新增登记；0 静默遗漏 |
| AC-6 采纳门控安全 | **PASS** | calculation 分支 L649 必经 `IsAnswerCorrect`；trap-limit-2 采纳、pdf-517 不采纳；adopted&&!correct=0（附 spec AC-6 文案非阻塞观察） |

**最终裁决：PASS。** AC-1 ~ AC-6 全部通过，红线 `precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 事实只存 JSON` 全部守住，无实现层 FAIL 或可执行代码缺陷。建议放行 P1-3；仅将「修订 spec.md AC-6 中『目标 2 道 truthAdopted=true』的文案（与 pdf-517 不采纳的设计自洽）」列为紧邻后续文档动作，不阻塞本次门禁。
