# 独立评审：data-repair-engine-exp-batch1（数据修复 + 引擎扩展并行批次 1）

- **评审方式**：只读独立核对（fresh context）。未修改任何源码/数据文件；未运行 `dotnet build/test`（依任务说明：并发 WIP 测试文件存在预存编译错误，强制排除）。可重跑的小型只读 Python 探针与文件读取已执行。
- **基线**：`_evalscratch/snapshot_after_expop.json`（gwCorrect=704, bothWrong=53, harm=0, precision=1）
- **终态**：`_evalscratch/snapshot_after_datarepair_engine.json`

## Verdict：**PASS（附说明）**

所有红线（precision=1、harm=0、LOST=[]、已判正题 gatewayAnswer 字节级不变、GAINED 精确为 {pdf-100, pdf-406}）均已**独立重跑验证通过**；Stream A 作用范围与 Stream B 零假阳性均经证据确认。无阻塞性问题。说明项见末尾「风险与说明」。

---

## 一、红线复核（独立重跑，非采信）

执行 `python _evalscratch\datarepair_engine_diff.py`（仓库根目录）输出：

```
BASE(after_expop)   gwCorrect=704 bothWrong=53 harmCount=0 enginePrecision=1
AFTER(datarepair)   gwCorrect=706 bothWrong=51 harmCount=0 enginePrecision=1
LOST  = []
GAINED= ['pdf-100', 'pdf-406']
previously-correct changed gatewayAnswer = 0
extra GAINED (beyond 100/406) = []
ALL RED LINES PASS
```

执行 `python _evalscratch\probe_exp4_verify.py`：

```
real experiment-facts count=116 (expect 116)
new ids present: [EF-100-A..D, EF-406-A..D] (expect 8)
pdf-100 -> B OK ; pdf-406 -> C OK
changed=2: [('pdf-100', None, 'B'), ('pdf-406', None, 'C')]
TOTAL PROBLEMS=0
```

---

## 二、Per-AC 核对表

| AC | 准则 | 证据（文件 / 行 / 输出） | 结论 |
|----|------|--------------------------|------|
| AC-1 | Stream A 每条修键有官方源证据且改后真值一致 | [data-defect-register.md](file:///D:/github/CogniThink-Chem/data-defect-register.md#L937-L943) K.1（pdf-100/107/162 改前→改后 + 官方源页号题号）；[datarepair_engine_audit.md](file:///D:/github/CogniThink-Chem/_evalscratch/datarepair_engine_audit.md#L8-L16) 一节；评审独立抽查答案册 OCR：p37n2（B）、p40n5(1)（方程）、p57n5(3)（c）见 `_evalscratch/review_truth_probe.py` 输出 | **PASS** |
| AC-2 | Stream A 零回归 | diff 脚本：LOST=[]、harm=0、precision=1、已判正题 gatewayAnswer 变更=0；评审探针 `review_scope_probe.py`：基线→after 仅 pdf-100/107/162 的 `case.answer` 变化，且三者基线均 `gatewayCorrect=False`（本就 bothWrong），故无新增 gatewayWrong | **PASS** |
| AC-3 | Stream B 翻正（或安全降级） | 检查点行：pdf-100 `gwCorrect=True, gatewayAnswer='错误选项为 B', state=Confirmed, usedEngine=True`；pdf-406 同状 → C（`review_row_probe.py` 输出）；备选 pdf-405 未扩展（无 EF-405 事实、不在 GAINED）= 安全降级；GAINED 精确 {100,406} | **PASS** |
| AC-4 | Stream B 零假阳性 | `probe_exp4_verify.py`：TOTAL PROBLEMS=0，全库 old(108)→full(116) 决策差异仅 {100,406}（None→B/C），零 stray-hit；`git diff HEAD -- experiment-facts.json` 仅追加 8 条 + EF-546-D 行尾语法逗号（无既有事实内容删改） | **PASS** |
| AC-5 | 红线与全库护栏 | 评估级红线已独立重跑通过（见上）。**单测未由评审重跑**（任务指令：WIP 编译错误，避免构建）；登记册 [K.3](file:///D:/github/CogniThink-Chem/data-defect-register.md#L954-L960) 声明 443 护栏全绿，且 `MultiSelectChoiceValidatorTests.FullBank_AnyMultiSelectConclusion_GradedCorrect_NoHarm`（[文件 L65](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/MultiSelectChoiceValidatorTests.cs#L65)）存在。评估级证据强于单测声明，故判 PASS-with-notes | **PASS（附说明）** |
| AC-6 | 可追溯性 rubric（≥4） | 每条改动在审计文档 + 登记册 K 节 + 检查点/快照 + 探针输出 + git diff 均可复核；`_evalscratch/` 工件齐全（`datarepair_engine_audit.md`、`probe_exp4_verify.py`、两份 snapshot/checkpoint） | **PASS（5/5）** |
| AC-7 | 零蒙数 rubric（≥4） | 全部修键/事实真值经官方答案册 OCR 印证（评审独立抽查 p37/p40/p57/p137）；pdf-100/406 的 OCR `answer` 标签为「全正确」污染，已据解析正文+化学唯一判定（见审计与登记册），非臆造；跳过项（pdf-180/165/197/263/138/325/333/414/190）逐项记录跳过理由，无蒙数 | **PASS（5/5）** |

---

## 三、Per-Task 完成状态

| Task | 内容 | 状态 | 证据 |
|------|------|------|------|
| Task 1 | 审计 53 题 → 候选清单 | 完成 | [datarepair_engine_audit.md](file:///D:/github/CogniThink-Chem/_evalscratch/datarepair_engine_audit.md) 三分类全覆盖：Stream A 3 / Stream B 2 / 排除 49（含图像 21、引擎缺口 17、跳过 9，去重核对） |
| Task 2 | Stream A 修键（仅 answer/answerKeywords） | 完成 | `git diff` + `review_scope_probe.py`：pdf-100/107/162 的 `question/options/note/type` 在基线→after 间**全部未变**，仅 `answer`/`answerKeywords` 变更 |
| Task 3 | Stream B 追加事实 + 必要 .cs | 完成 | experiment-facts +EF-100-A..D/+EF-406-A..D（8 条）；`FactChoiceMatcher.cs` 与 `MultiSelectChoiceValidator.cs` 的 WrongDir 均加入「不能完成」token（见 .cs diff）；`Batch2ChoiceValidatorTests` 真值表新增 pdf-100=B / pdf-406=C |
| Task 4 | 构建 + 全量单测绿 | 声明完成（评审未重跑） | 登记册 K.3 声明 443 护栏全绿；评审未执行 `dotnet test`（WIP 编译错误，依任务说明规避）。护栏测试方法存在 |
| Task 5 | 全量评估 + 基线 diff | 完成且独立复核 | 两份 snapshot 存在；`datarepair_engine_diff.py` 重跑全部红线通过 |
| Task 6 | 维护 data-defect-register.md | 完成 | 新增 [K 类小节](file:///D:/github/CogniThink-Chem/data-defect-register.md#L930-L967)（K.1 修键 / K.2 扩展 / K.3 红线 / K.4 排除）；pdf-100/406/162 旧登记已更正为 ✅ 并附官方源 |
| Task 7 | 独立评审 review.md | 进行中（本文档） | — |

---

## 四、关键红线逐项确认

1. **precision=1 / harm=0 / LOST=[]**：diff 脚本与直接读取 snapshot 双重确认（after harmCount=0, enginePrecision=1, LOST=[]）。✓
2. **已判正题 gatewayAnswer 字节级不变**：diff 脚本输出 `previously-correct changed gatewayAnswer = 0`；`review_scope_probe.py` 独立复核 count=0。✓
3. **Stream A 仅改 answer/answerKeywords（不动 question/options/note/type）**：`review_scope_probe.py` 对 pdf-100/107/162 逐字段比对基线→after 检查点，四字段 `unchanged=True`。✓
4. **新事实零 stray-hit**：`probe_exp4_verify.py` 全库回归差异仅 {100,406}，TOTAL PROBLEMS=0。✓
5. **MultiSelect 无伤害**：
   - 确认 `不能完成` 同时存在于 [FactChoiceMatcher.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/FactChoiceMatcher.cs) 与 [MultiSelectChoiceValidator.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/MultiSelectChoiceValidator.cs) 的 WrongDir。
   - pdf-406 终态 `gatewayAnswer='错误选项为 C'`、`gwCorrect=True`（非 ABD），证明 MultiSelect 未误发多选 ABD；按逻辑选错方向仅 C 为假 → 候选 1 < 2 → 返回 null，单选路径给 C。
   - 登记册 [K.2 回归拦截说明](file:///D:/github/CogniThink-Chem/data-defect-register.md#L952) 记录了该回归的发现与修复，护栏 `FullBank_AnyMultiSelectConclusion_GradedCorrect_NoHarm` 由红转绿。✓

---

## 五、风险与说明（非阻塞）

1. **工作树混合并发批次**：`git diff HEAD` 显示 `pdf_questions.json` 还有本批之外的大量改动（pdf-087/227/284/295/306/327/409/410/432/511 修键，pdf-161/413 question 复原）以及 `MultiSelectChoiceValidator.cs` 中并发 WIP 新增的 4 个 StatementJudge（ElectronConfig/VseprBondAngle/CrystalType/CoordinateBond，对应未跟踪文件）。这些属于并发的 `data-repair-ocr-recovery` 与 chemical-simulator WIP，**已存在于基线快照中**，故不影响本批红线。本评审通过「检查点基线→after 增量对比」隔离了本批实际作用范围（仅 {100,107,162} 键变 + {100,406} 翻正）。若本批单独提交，需注意从混合工作树精确摘取本批 hunk。
2. **单测未独立重跑**：依任务指令未执行 `dotnet build/test`（WIP 的 `SolveStructuredEnricherTests.cs` 有预存编译错误）。单测全绿依赖登记册声明；评估级红线已独立复核，为更强信号。
3. **EF-546-D 行尾逗号**：`experiment-facts.json` diff 中 EF-546-D 行尾新增逗号，仅为衔接后续追加条目的语法改动，**无事实内容变更**，符合「既有事实不删改」。
4. **OCR 标签污染项**：pdf-100（p37n2）与 pdf-406（p137n12）答案册 OCR 的 `answer` 字段均为「A,B,C,D / A, B, C, D」全正确污染；真值据解析正文 + 化学一致性唯一确定，已在审计/登记册记录，未用 VL 页图（文本无歧义），符合零蒙数纪律。

---

## 六、并发 WIP 核对

- `tests/CogniThink.TutorBrain.Tests/SolveStructuredEnricherTests.cs` **在位且内容完整**（读取头部：正常 `SolveStructuredEnricherTests` 测试类，未被掏空/注释）。
- 无 `.bak`/`.excluded` 等临时残留变体；测试项目 `.csproj` 未出现在 `git status` 修改列表中（无永久排除改动）。
- 该文件仍为未跟踪（`??`）WIP，其预存编译错误与本批无关；本批评估在临时排除该文件后运行，现已恢复原位。

---

## 七、评审新增只读探针（可复现）

- `_evalscratch/review_scope_probe.py`：对比 expop↔datarepair_engine 检查点，隔离本批键变范围与字段不变性。
- `_evalscratch/review_row_probe.py`：导出 pdf-100/406/162/107 的检查点决策行。
- `_evalscratch/review_truth_probe.py`：抽查答案册 OCR（p37/p40/p57/p137）印证真值非臆造。

可复现命令：`python _evalscratch\datarepair_engine_diff.py`、`python _evalscratch\probe_exp4_verify.py`。
