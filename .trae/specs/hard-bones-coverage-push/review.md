# 硬骨头覆盖推进 · 独立审查（Review）

> 审查对象：工作区全部未提交变更（`git diff` / 未跟踪文件），对应 spec `hard-bones-coverage-push`。
> 审查方式：**只读审计**——不修改源码/数据/测试；可运行 `dotnet build` / `dotnet test` / EvalRunner 独立复核（产物落入 bin / `_evalscratch`，不污染源码）；将结论写入本文件。
> 基线 checkpoint：`_evalscratch/checkpoint_baseline_head.jsonl`（560/757）。
> 终态 checkpoint：`_evalscratch/checkpoint_after_pdf485.jsonl`。
> 结项报告：`.trae/specs/hard-bones-coverage-push/final-report.md`。

## 审查员须知

- 所有结论必须有**可复现证据**（命令输出、文件路径、行号、checkpoint id 列表），不得仅凭申请人陈述。
- 重点核对四条铁律：**precision=1、harmCount=0、LOSS=0、红线未越界**。
- 发现任何 FAIL，给出最小复现与证据；不得留模糊判断。
- 本次审查所有 checkpoint 计数均由审查员独立编写的 PowerShell 脚本（`JavaScriptSerializer`，`$ser.MaxJsonLength=[int]::MaxValue`，取 `$o['case']['id']` / `$o['gatewayCorrect']` / `$o['gatewayAnswer']`）重新解析得出，未采信申请人陈述数字。

---

## AC-1 [rule] 零回归

> 每个合入批次 `LOSS=0`、`harmCount=0`、`enginePrecision=1.0`。

- [x] 构建：`dotnet build CogniThink.Chem.sln -c Release` 0 错误。证据：命令输出 `已成功生成。0 个警告 0 个错误`（CogniThink.TutorBrain.PdfOcrRepair / Chem.Core / Chem.Cli / Chem.Tests / TutorBrain.Api / EvalRunner / TutorBrain.Tests 全部生成成功）。
- [x] 全量测试：`dotnet test CogniThink.Chem.sln -c Release --no-build --nologo` 0 失败。证据（通过/失败数）：`CogniThink.TutorBrain.Tests 失败 0 / 通过 1154`、`CogniThink.Chem.Tests 失败 0 / 通过 910`，合计 **2064 通过 / 0 失败 / 0 跳过**（与 final-report 声称的 1154+910=2064 完全吻合）。
- [x] id 级 diff `after_pdf536` → `after_pdf485`：GAIN={pdf-485}、LOSS=∅。证据：审查脚本独立解析两份 jsonl，遍历 757 个 id，输出 `GAIN count=1: pdf-485`、`LOSS count=0:`（空）。
- [x] id 级 diff `baseline_head` → `after_pdf485`：无任何 id 从 True 翻为 False（LOSS=0）。证据：脚本输出 `LOSS(baseline->485) count=0:`（空），`GAIN(baseline->485) count=46`，并确认 `baseline_head gatewayCorrect=True=560 / total=757`。
- 结论：PASS —— 构建/测试全绿，相对前驱与相对基线两条 diff 链路 LOSS 均为 0。

## AC-2 [rule] 净增益

> 最终 `gatewayCorrect` > 560；每批 GAIN ≥ 0。

- [x] `checkpoint_after_pdf485.jsonl` 判正计数 = 606（>560）。证据：审查脚本独立解析输出 `after_pdf485: total=757, gatewayCorrect=True=606, False=151`；summary 文件 `checkpoint_after_pdf485_summary.txt` 同样记录 `gatewayCorrect=606 ... coverage=0.8005 precision=1 harmCount=0`，两路独立来源一致。
- [x] 各批次 GAIN ≥ 0（tasks.md 记录与 checkpoint diff 一致）。证据：关键相邻批次 diff 经脚本复核 —— `after_pdf536(605)→after_pdf485(606)` GAIN +1；`baseline(560)→after_pdf485(606)` 单调净增 +46，无任何回退批次；tasks.md 各批记录的累计 +41/+43/+44/+45/+46 与各 checkpoint summary 判正数（601/603/604/605/606）吻合。
- 结论：PASS —— 终态 606 > 560，全程单调不减。

## AC-3 [rule] 红线合规

> 无 verdict 字眼注入题干/选项/解析；存储 answer 变更附官方源证据；无低质 OCR 猜测回填。

- [x] `git diff` 中 `pdf_questions.json` 仅改动 pdf-485 的 `answer`/`answerKeywords`/`note`，**未改动任何题目的 `question`/`options`**。证据：`git --no-pager diff -- src/.../data/pdf_questions.json` 唯一 hunk 位于 `@@ -17472,11 +17472,12 @@` 的 pdf-485 块，变更行仅为 `answerKeywords`（`1:1` → `2:1`/`1:2`）、`note`、`answer`（`1:1` → `2:1（或1:2）`）；`question` 行与 `options` 字段均为未改的上下文行。另用 `git diff | Select-String '^\+.*"(question|options)"'` 输出为空，确认无任何新增/修改的 question|options 字段行。
- [x] 全工作区 diff 未向 `question`/`options` 字段注入「正确/错误/√/×/是/否」等判题结论。证据：题库类文件仅 `pdf_questions.json` 被改且未触 question/options（见上）；其余改动文件为事实库（`concept-facts.json`/`choice-curated-facts.json`/新增 `judgment-statement-facts.json`）与引擎源码/测试，均非题干/选项载体。pdf-485 新 `note` 含「违反洪特规则」，但该字段是答案键订正的**官方源留痕元数据**（红线 #2 要求「留下来源证据」），不参与路由与判分，且不含「正确/错误/√/×」判题标记 —— 合规。
- [x] pdf-485 键变更（1:1 → 2:1（或1:2））有官方源证据：`pdfs/_extract/answer/page_064.json`（模块2 num=1）。证据：读取 `page_064.json`，其 `answers` 数组第二个分块（填空答案块）中 `module="模块2 原子核外电子排布原理"`、`num=1`、`answer="2:1（或1:2）"`，`explanation` 明确：「S 的原子序数为 16…价层电子排布式为 3s2 3p4。根据洪特规则…轨道表示式为 3s↑↓ 3p↑↓ ↑ ↑。因此两种自旋状态的电子数之比为 2:1 或 1:2。」与订正后的存储键完全一致。
- [x] 未向 `general-choice-facts` 注入单题策展事实（`choice-curated-facts.json` / `judgment-statement-facts.json` 是 spec 已批准的独立例外文件，不算污染；已确认其事实有 stemMatch 锁域）。证据：`git --no-pager diff --name-only` 与 `git status` 改动列表中**不含** `data/highschool/general-choice-facts.json`（红线 #6 守住）；`choice-curated-facts.json` 头部 `"source"` 声明「单题策展专用事实库（红线 #6 隔离）…每题 stemMatch 全库唯一」，抽查条目 GC-742-A/B/C 均带 `stemMatch`（如 `["硫单质可以与NaOH","2Na2S"]`）锁域，不跨题误命中。
- [x] 无低质 OCR 猜测回填（所有文本改动来自引擎算法或官方源）。证据：本会话 `pdf_questions.json` 零 question/options 文本改动（无回填动作）；唯一数据订正 pdf-485 有官方答案册 `page_064.json` 支撑。
- 结论：PASS —— 六条红线全部守住。

## AC-4 [rule] 逐批证据

> 每批有 checkpoint jsonl + summary，并在 tasks.md 记录 GAIN/LOSS/HARM。

- [x] `tasks.md` 为本会话每批（concept batch2 / pdf-119 / pdf-536 / pdf-485）记录了 GAIN/LOSS/HARM 与 checkpoint 名。证据：`tasks.md` 第 122-170 行分别记录 `after_concept_batch2`(603,+2)、`after_pdf119`(604,+1)、`after_pdf536`(605,+1)、`after_pdf485`(606,+1)，每批标注 `LOSS 0 / HARM 0 / precision=1.0`。
- [x] 对应 `checkpoint_{tag}.jsonl` 文件存在。证据：`_evalscratch/` 下存在 `checkpoint_after_concept_batch2.jsonl`、`checkpoint_after_pdf119.jsonl`、`checkpoint_after_pdf536.jsonl`、`checkpoint_after_pdf485.jsonl`，且对应 `*_summary.txt` 四份齐全（Glob 命中 4 个 .txt）。
- 结论：PASS —— 逐批证据链完整。

## AC-5 [rule] 诚实 null 报告

> 结项报告列出全部仍未解 id 并标注红线原因类别。

- [x] `final-report.md` 未解计数 = 151 = 757 − 606。证据：审查脚本对 checkpoint 解析得 `False id count=151`；final-report 5.1 分类计数脚本求和 `= 151`（77+19+12+12+10+7+6+3+2+1+1+1），与 757−606=151 一致。
- [x] `final-report.md` 的 5.2 清单覆盖 checkpoint 中**所有** `gatewayCorrect=False` 的 id（无遗漏、无多余）。证据（脚本比对）：从 final-report 5.2 节（`### 5.2` 至 `### 5.3`）正则抽取 id 去重得 151 个；与 checkpoint False 集合（151）做双向差集 —— `遗漏 count=0`、`多余 count=0`、`集合完全相等: True`。两侧集合已导出留档 `%TEMP%\ckpt_false_ids.txt` / `%TEMP%\report_unsolved_ids.txt`。
- [x] 每个未解 id 都有原因类别（缺图/碎片/多段/建模域外/坏键等）。证据：5.2 节含 12 个带计数的类别标题（图像/资产依赖 77、工艺/实验大题碎片 19、计算题 12、多段填空 12、选择题 C/D 类 10、缺有机结构图 7、反应方程书写 6、brokenKey 3、诊断探针 2、题干拼接 1、重复碎片 1、题面碎片 1），上一项已证所有 151 个 False id 均落在这些类别块内。
- 结论：PASS —— 诚实 null 报告与 checkpoint 逐 id 完全对齐。

## AC-6 [rule] 测试通过 + 新增覆盖

> 全绿；新增引擎逻辑有单测覆盖代表题。

- [x] 全量测试通过数与失败数：**2064 通过 / 0 失败**（TutorBrain 1154 + Chem.Core 910），应 0 失败，符合。证据见 AC-1。
- [x] 新增引擎能力有代表题单测：`ValenceSpinCounts_价层两种自旋电子数`、`Pdf485_S_价层两种自旋电子数之比_判正`（及前驱批次的护栏测试）。证据：`tests/CogniThink.Chem.Tests/PeriodicTableTests.cs:92` 存在 `ValenceSpinCounts_价层两种自旋电子数`；`tests/CogniThink.TutorBrain.Tests/AtomicStructureSolverTests.cs:121` 存在 `Pdf485_S_价层两种自旋电子数之比_判正`。过滤运行 `dotnet test --filter "FullyQualifiedName~ValenceSpinCounts_价层两种自旋电子数|FullyQualifiedName~Pdf485_S_价层两种自旋电子数之比_判正"` 输出：Chem.Tests `通过 5 / 失败 0`（S/N/Cl/Ar/Fe 五条 InlineData）、TutorBrain.Tests `通过 1 / 失败 0`，全绿。
- [x] 护栏测试（`ConceptFactSolverTests`/`AtomicStructureSolverTests`/`StatementJudgeTests` 等锁定集）未被破坏。证据：全库类 9 个 `Guardrail_全题库*` 测试均存在并随 2064 全绿套件通过，包括 `ConceptFactSolverTests.Guardrail_全题库触发集锁定且凡触发必判正`（:97）、`AtomicStructureSolverTests.Guardrail_全题库无假阳性`（:168）、`VseprSolverTests.Guardrail_全题库无假阳性`（:87）等。
- 结论：PASS —— 全绿且新增能力有代表题 + 全题库护栏覆盖。

## AC-7 [rubric] 覆盖提升幅度

> 相对基线净增 ≥20（通过阈值），目标 ≥40。

- [x] 净增 = 606 − 560 = 46 ≥ 40。证据（id 差集大小）：审查脚本 `baseline_head → after_pdf485` diff 输出 `GAIN count=46`（LOSS=0），即净新增解出 46 题，超过目标 ≥40。
- 结论：PASS —— 已超额达成目标（+46）。

---

## 铁律专项核对（precision=1 / harm=0）

- [x] **凡采纳必判正**：在 `checkpoint_after_pdf485.jsonl` 中，对所有 `gatewayAnswer` 非空的 case，`gatewayCorrect` 必须为 True（无一例外）。反例列表（应为空）：**审查脚本遍历 757 条，凡 `gatewayAnswer` 非空且 `gatewayCorrect=False` 的反例数量 = 0**（反例列表为空）。
- [x] **harm=0**：相对 `baseline_head`，无 case 从 `gatewayCorrect=True` 变为 False，也无已判正题的 `gatewayAnswer` 被改成错误值。证据：`baseline→after_pdf485` id 级 diff `LOSS count=0`；summary `harmCount=0`；precision=1（采纳门 `IsAnswerCorrect` 保证凡采纳必与存储键一致）。
- [x] **无新假阳性**：本会话新增触发（pdf-465/389/119/536/485）的 `gatewayAnswer` 经化学核验与官方答案一致。证据：脚本抽查 5 题在 `after_pdf485` 中均 `gatewayCorrect=True` 且 `baseline` 均为 False；其 `gatewayAnswer` 分别为 pdf-465「…分子间能形成氢键…」、pdf-389「C8H8 3…二氯代物共有 3 种」、pdf-119「√」、pdf-536「…防止NaHTe被氧化而失效…」、pdf-485「2:1（或1:2）」——其中 pdf-485 与官方源 `page_064.json` 逐字一致（见 AC-3），其余 4 题因 `gatewayCorrect=True` 已通过采纳门与存储 answer 对齐，且化学结论正确。

## 红线专项核对

- [x] #1 不注入 verdict 字眼（题干/选项）：pdf_questions.json diff 未触 question/options；无判题词注入（见 AC-3）。
- [x] #2 不改动存储 answer 除非官方源证明错误（仅 pdf-485，已留证）：唯一 answer 改动为 pdf-485，附 `page_064.json` 官方证据（见 AC-3）。
- [x] #3 不基于低质 OCR 猜测：本会话零 question/options 回填，唯一数据订正有官方源（见 AC-3）。
- [x] #4 不蒙数（引擎只在可唯一判定时给结论，否则 null）：151 道不可唯一判定题保持 False/null 并分类说明（见 AC-5）；新锚点配严格守卫（价层限定），summary `precision=1` 佐证无蒙数。
- [x] #5 零假阳性/零回归：precision=1（反例 0）、harmCount=0、LOSS=0（见铁律专项）。
- [x] #6 不向通用选择题事实库注入单题策展：`general-choice-facts.json` 未出现在改动列表；策展事实隔离在 `choice-curated-facts.json` 并带 stemMatch 锁域（见 AC-3）。

---

## 发现与问题（Findings）

> 列出任何偏差、风险或存疑项，附证据。无则写「无」。

1. **无阻塞性发现。** 所有验收标准 AC-1~AC-7 与四条铁律（precision=1、harm=0、LOSS=0、红线未越界）均经审查员独立复算通过，数字与申请人陈述一致。
2. **观察项（非缺陷）**：pdf-485 的 `note` 字段订正后含「违反洪特规则」字样。该字段是答案键官方源订正的留痕元数据（红线 #2 要求留下来源证据），不参与题面路由与判分，且不含「正确/错误/√/×」判题标记，故不构成红线 #1 违规；但建议未来订正 note 时可更聚焦于来源引用，减少在解析字段中出现价值判断措辞，以进一步降低观感歧义。
3. **数据点复核**：`page_064.json` 内含两个答案分块（前 7 条为选择 A/B/C/D，后 6 条为填空），与 pdf-485 对应的是第二分块 `module="模块2 原子核外电子排布原理"`、`num=1`、`answer="2:1（或1:2）"`，证据指向准确无误。

## 最终结论

- 整体裁定：**PASS**
- 若 FAIL/BLOCKED，阻塞项与必须修复项：无。
- 审查员签字（agent 标识 + 时间）：独立审查 subagent · 2026-11-21
