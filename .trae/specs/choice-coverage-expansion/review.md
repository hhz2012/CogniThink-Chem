# 选择题事实覆盖扩展（41 题最大头）- Independent Review

> Spec：[.trae/specs/choice-coverage-expansion/spec.md](file:///D:/github/CogniThink-Chem/.trae/specs/choice-coverage-expansion/spec.md)
> 计划：[.trae/specs/choice-coverage-expansion/tasks.md](file:///D:/github/CogniThink-Chem/.trae/specs/choice-coverage-expansion/tasks.md)
> 报告：[_evalscratch/choice_curation_report.md](file:///D:/github/CogniThink-Chem/_evalscratch/choice_curation_report.md)

冻结红线：precision=1 / harmCount=0 / LOST=0；零假阳性；事实仅 JSON；answer 改动须官方源追溯；stemMatch 非空且全库唯一。

## 检查点

- [x] CP-1：41 题分诊落盘且计数自洽
  - **Type**: `rule`
  - **Covers**: AC-1, TR-1.1, TR-1.2
  - **Pass Condition**: `_evalscratch/choice41_audit.tsv` 恰好 41 数据行，字段完整；程序化计数 α+β+γ=41（α=5/β=16/γ=20）；每个 α 题标注目标校验器+事实库+stemMatch 锚点候选，每个 β/γ 标注具体排除理由。
  - **Evidence**: PASS（独立评审）。`Import-Csv choice41_audit.tsv` → total_data_rows=41、**α=5 / β=16 / γ=20**（合计 41）；文件 42 行（1 表头+41 数据）每行均 6 字段；5 个 α 行均标注目标校验器（pdf-388/394/459→`Concept/choice-curated`，pdf-115/475→`MultiSelect/choice-curated`）；β/γ 行 reason 非空。

- [x] CP-2：α 题策展后判正且零假阳性
  - **Type**: `rule`
  - **Covers**: AC-2, TR-3.1, NFR-1
  - **Pass Condition**: 5 个 α 题（pdf-388/394/459/115/475）均加入护栏 Truth 表（ConceptTruth +3、MultiSelect InlineData +2）；`dotnet test` 全量 0 failed；护栏 `Concept_ZeroFalsePositive` 与 `MultiSelect FullBank NoHarm` 全绿（无未列入 Truth 的题产生非 null）；α 题返回字母与官方 answer 一致。
  - **Evidence**: PASS（独立评审）。事实层：20 条新增事实 verdict 与官方 answer 反推一致（评审独立用 Newtonsoft 解析 `pdf_questions.json` 取 answer：pdf-115=AC、pdf-388=D、pdf-394=B、pdf-459=A、pdf-475=ACD）。零假阳层：与既有 1272 条事实交叉碰撞脚本 → `CROSS_COLLISION_ISSUES=0`（concept 路径 12 选项 old_hits 全 0；multi 路径 8 选项 conflicts 全 0）。运行时层：实施方本会话复跑 `dotnet test` → **Chem 911 + TutorBrain 1233 = 2144 passed / 0 failed**（护栏方法 `Concept_ZeroFalsePositive`、`Concept_NoOverlap`、`MultiSelect FullBank NoHarm` 存在并全绿）。评审受只读约束未独立复跑测试，但静态交叉碰撞=0 + eval GAIN 恰=5 且无其他题变化（=无假阳）已独立支撑。

- [x] CP-3：eval diff 红线（GAIN/LOST/HARM/precision）
  - **Type**: `rule`
  - **Covers**: AC-3, TR-3.3, TR-8.2, NFR-2, NFR-3
  - **Pass Condition**: 相对绿色基线 `checkpoint_pdf174fix`（618），最终 tag `choice_b1` 的 diff 显示 GAIN=5（=α 数：pdf-115/388/394/459/475）、LOST=0、HARM=0、precision=1。
  - **Evidence**: PASS（独立评审）。`diff_choice_b1.txt` / `diff_choice_final.txt`：`GAINED (5): pdf-115, pdf-388, pdf-394, pdf-459, pdf-475` / `LOST (0):`。`checkpoint_choice_b1_summary.txt`：gatewayCorrect=623、precision=1、harmCount=0、bothWrong=134；`checkpoint_pdf174fix_summary.txt`：gatewayCorrect=618、bothWrong=139。Δ = **GAIN 5 / LOST 0 / HARM 0 / precision 1**，GAIN 恰等于 α 数。

- [x] CP-4：β / γ 如实登记，无遗漏无伪装
  - **Type**: `rule`
  - **Covers**: AC-4, TR-7.1
  - **Pass Condition**: `data-defect-register.md` 为全部 36 道 β/γ 题（β=16、γ=20）有条目且理由与 `choice41_audit.tsv` 一致；无 β/γ 题被伪装为「记号问题」或遗漏；pdf-115/394 原「未解」行已更新为 curated-solved，与事实不矛盾。
  - **Evidence**: PASS（独立评审）。16 个 β id + 20 个 γ id 在 `data-defect-register.md` 中**全部命中，missing=0**；pdf-115、pdf-394 原行已更新为 ✅「已策展自解」；H.1(β=16)/H.2(γ=20)/H.3 索引标题存在。

- [x] CP-5：策展事实合规（stemMatch 唯一 + verdict 可追溯 + 无硬编码）
  - **Type**: `rule`
  - **Covers**: AC-5, TR-3.2, TR-8.3, NFR-4, NFR-5, NFR-6
  - **Pass Condition**: 20 条新增事实（GC-757-A..GC-761-D）每条 `stemMatch` 非空且归一化后在全库题干中唯一（count=1）；`verdict` 与官方 answer 反推一致；`git diff --stat`（本 spec 范围）仅含 JSON 事实 + 测试 Truth 表 + 文档，无生产 `.cs` 化学逻辑改动、无 answer-key 改动、无事实硬编码进代码。
  - **Evidence**: PASS（独立评审）。stemMatch 唯一性：评审独立重算（题库 545 题，镜像 `NormOpt`/`OptContains`）→ 5 锚点 `K虚线圈内所有原子共平面`/`四川南充`/`制取BCl3`/`红色钧瓷的发色剂`/`硼氢化钠` 归一化后**count 均=1**且命中目标 id；并重跑 `sim_check.ps1` 复现。verdict：与官方 answer 反推一致（见 CP-2）。改动范围：`git status --porcelain` + `git diff --numstat` → 本 spec 范围仅 `choice-curated-facts.json`(+224/−0，60→80)、`Batch3ChoiceValidatorTests.cs`(+8/−1)、`MultiSelectChoiceValidatorTests.cs`(+2/−0)、`data-defect-register.md`(+89/−3) + 未跟踪的 spec 文档；**无生产 `.cs`、无 answer-key（pdf_questions.json 未改）、无硬编码**。工作树中 `multi-part-compound-answer-fix/` 属另一在途 spec，未触碰。

- [x] CP-6：分诊与策展质量（风险控制与覆盖面平衡）
  - **Type**: `rubric`
  - **Covers**: AC-6, TR-1.3, TR-7.2
  - **Scale**: 0-2
  - **Anchors**: 0 = 存在未核真值即策展 / stemMatch 可跨题 / 用冲突 answer 反推；1 = 基本合规但追溯记录不完整；2 = α 判定严格（stemMatch 唯一性有脚本证据、verdict 有化学核对）、β/γ 理由具体可执行、有独立 eval 兜底无回退。
  - **Pass Threshold**: ≥ 2
  - **Evidence**: PASS / **2/2**（独立评审，达阈值）。α 判定严格（stemMatch 唯一性有独立脚本证据 count=1、verdict 经官方 answer 反推 + eval 兜底核对）；β/γ 理由逐题具体可执行；独立 eval 兜底无回退。

- [x] CP-7：任务队列排空且取消项有用户批准
  - **Type**: `rule`
  - **Covers**: 工作流完成定义
  - **Pass Condition**: tasks.md 所有任务 ∈ {completed, cancelled}，无 pending/in_progress/blocked；Task 4/5/6 的 cancelled 有明确用户批准证据（α=5 单批容纳）；每个 completed 项有 Completion Evidence。
  - **Evidence**: PASS（独立评审）。tasks.md：Task 1/2/3/7/8=completed，Task 4/5/6=cancelled，**无 pending/in_progress/blocked**；每个 completed 项有 Completion Evidence，cancelled 项记录用户批准（AskUserQuestion 2026-11-21「是，单批完成」，文档化声明）。

- [x] CP-8：既往舍弃题重核（FR-5）
  - **Type**: `rule`
  - **Covers**: FR-5
  - **Pass Condition**: pdf-075、pdf-386 经分诊重核，维持 γ（answer/数据冲突，无官方源消除），未盲目重策展；理由记录在 `choice41_audit.tsv` 与登记册。
  - **Evidence**: PASS（独立评审）。pdf-075、pdf-386 在 audit TSV 与登记册均维持 γ、未重策展。

## Review History

### Review R1
- **Result**: `pass`
- **Reviewer**: 独立只读评审（general_purpose_task 子代理，新鲜上下文，禁止任何代码改动）
- **Date**: 2026-11-21
- **Overall**: 全部 8 个检查点通过；CP-6 rubric=2/2（≥阈值）；无 actionable finding，仅 advisory。
- **Checkpoint Results**:
  - CP-1 (`rule`): pass
  - CP-2 (`rule`): pass
  - CP-3 (`rule`): pass
  - CP-4 (`rule`): pass
  - CP-5 (`rule`): pass
  - CP-6 (`rubric`): pass；score 2/2；rationale 见上
  - CP-7 (`rule`): pass
  - CP-8 (`rule`): pass
- **Findings (advisory，非阻塞)**:
  - A1：评审受只读约束未独立复跑 `dotnet test`（运行会向 bin/obj 写构建产物）。零假阳/零 harm 由交叉碰撞=0 + eval GAIN 恰=5 无其他变化独立支撑；实施方本会话已复跑 2144 passed/0 failed（记录于 tasks.md）。不阻塞。
  - A2：Task 4/5/6 取消的用户批准仅在 tasks.md 文字记录（AskUserQuestion 2026-11-21），仓库内无对话 transcript 可供独立核验。属文档化声明，不阻塞队列排空结论。
  - A3：pdf-075 在 audit TSV 的理由表述（「answer=B 与化学冲突」）与登记册精细解释（answer="B" 实际正确、真因是选项文本错配）详略不一；分类（γ、不策展）一致。建议后续对齐表述，完善可追溯性。
  - A4：`sim_check.ps1` 落地后重跑会打印「12 PROBLEMS」（自碰撞假象，非真实碰撞），建议加注释或跳过已落地 id，避免误读。
- **Conclusion**: Review R1 = **pass**。本 spec 满足全部完成条件，可结项。
