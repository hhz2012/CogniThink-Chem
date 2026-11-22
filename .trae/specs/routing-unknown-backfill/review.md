# P1-5 路由补齐 — 独立只读 Review

> **审查模式**：严格只读（未改任何文件）。
> **最终交付 checkpoint**：`after_p15b`。
> **审查者**：fresh-context 只读子代理（general_purpose_task，仅返回报告文本，由主代理落盘）。
> **总体裁决**：**PASS**。无必须修复的 Issue。

---

## 1. 复跑证据

1. **build**：`dotnet build CogniThink.Chem.sln -c Release --nologo` → 7 projects success，0 errors，exit 0。**PASS**。
2. **test**：`dotnet test CogniThink.Chem.sln -c Release --nologo` → Chem.Tests 913 passed + TutorBrain.Tests 1278 passed = **2191 passed / 0 failed / 0 skipped**，exit 0。**PASS**。
3. **diff `after_p14 → after_p15b`**：GAINED(0)=[]，LOST(0)=[]，WORSEN(0)=[]，harm 0→0，RED-LINE PASS。**PASS**。
4. **diff `base_p0 → after_p15b`**：GAINED(6)=[pdf-415, 530, 535, 542, 543, trap-limit-2]（P1-4 携带），LOST(0)=[]，WORSEN(0)=[]，harm 0→0，RED-LINE PASS。**PASS**。
5. **`p15_verify_after.py after_p15b`**：unknown 32→27 严格下降；5 touched（pdf-174/417/456/515/539）全部 before_correct=True / after_correct=True / taskType=reaction_check；adopted∧¬correct=0；LOST=0；correct 635=635；T4 VERDICT ALL PASS。**PASS**。
6. **`p15_closure_check.py`**：U=32，U2=27，A=5 [pdf-097, 164, 177, 179, 462]，B=27，A∩B=[]，A∪B==U True，|A|+|B|=32，missing=[]；A 全部→equation_writer；5 correct-touched ∉ U 且仍判正；adopted∧¬correct=0；CLOSURE VERDICT PASS。**PASS**。
7. **密钥扫描**（case-insensitive `sk-|api_key|Bearer|secret` on `LlmGatewayService.cs` + `ClassificationDeterministicTests.cs`）→ No matches found（0 命中）。**PASS**。
8. **Bonus `p15_drift_precheck.py`**：affected=16，correct-touched={pdf-174, 417, 456, 515, 539} 恰好；11 unknown→ew + 3 calc→ew + 2 concept→ew，与台账 §M.1 一致；VERDICT PASS。**PASS**。

## 2. 代码审查

- **cue A 插入位置/内容正确**：在既有 `equation_writer` 块之后、P1-4 concept cues 与 calculation 之前，不窃取高优先级分支（choice/judgment/equilibrium 在更前面已截走）。
- **equation_writer 分支顺序**：`ProductWriterSolver → KnownEquationWriterSolver → ReactionKbWriterSolver`，与 `reaction_check` / `CallCompile`（L788/796/806）同序；每个命中均置 `req.TaskType = "reaction_check"` 走同一采纳门。
- **不蒙数**：`ProductWriterSolver` 只读 `req.RawText`，4 条窄骨架规则 + 门控，无命中/配平失败返回 null；系数来自 `EquationBalancer`（无硬编码真值）。
- **误伤风险可控**：由 `IsAnswerCorrect` 采纳门 + eval diff（LOST=0）把关。
- **表面积确认**：P1-5 仅触及 `LlmGatewayService.cs`（2 hunk）+ 测试文件 + 台账 §M + scratch 验证脚本 + spec/tasks md。

## 3. 红线维度表

| 维度 | 结果 |
|------|------|
| precision=1 | PASS |
| harm=0 | PASS |
| LOST=0 | PASS |
| WORSEN=0 | PASS |
| 不蒙数 | PASS |
| 未改数据 / 评估 | PASS |
| 台账自洽 | PASS |
| unknown↓ | PASS |
| 测试覆盖 | PASS |

## 4. 非阻塞项（已由主代理修正）

cue A 注释（L324-325）原先陈旧（声称 5 道全部经 `ReactionKbWriterSolver`），与 spec §7.1 实证发现矛盾。主代理已修正，现正确归属：174/417=ReactionKb，456/539=Known，515=Product。

## 5. 诚实披露

正确性证据为对已持久化 checkpoint（`after_p14` / `after_p15b` / `base_p0`）的读侧校验 + 当前源码的 build/test；未重新触发 `run_eval_resume.ps1` 生成新 checkpoint（与只读委托授权一致）。

---

**总体：PASS。无必须修复的 Issue。**
