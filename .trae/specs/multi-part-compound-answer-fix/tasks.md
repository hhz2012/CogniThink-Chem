# 多段复合答案修复 — 实施队列（M1 批次）

> 红线（冻结）：每批 eval 若 `precision≠1` 或 `harm>0` 或 `LOSS>0` → 回退上一绿色状态，任务退回 in_progress。题干回填必须附官方源溯源。

## Task M1.0：取证落盘 + 锁定绿色基线 618（验证，无代码改动）
- **Status**: completed
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 复算全库 0 adopted-wrong（`checkpoint_rel_half.jsonl`），落盘 `_evalscratch/multi_unsolved_ground.tsv`（已生成，含 104 题逐题分类）。
  - 确认 `checkpoint_pdf174fix` 为当前绿色基线：gatewayCorrect=618、precision=1、harm=0。
  - `diff_eval.ps1 -OldTag rel_half -NewTag pdf174fix` → 落盘 GAIN 1（pdf-174）/ LOST 0。
- **Acceptance Criteria**: AC-1、AC-2
- **Test Requirements**:
  - `rule` TR-M1.0.1: 0 adopted-wrong 复算脚本输出为 0；证据：终端输出 + multi_unsolved_ground.tsv。
  - `rule` TR-M1.0.2: diff 报告 GAINED 1 / LOST 0；证据：`_evalscratch/diff_pdf174fix.txt`。
- **Completion Evidence**:
  - 已复算：TOTAL 757 / parsed 756 / unparseable 1 / CORRECT 617 / WRONG 139 / ADOPTED-but-WRONG = 0（全库）。
  - 104 多段未解分解：96 null + 8 过滤输出（5 占位 + 3 错域），全部 0 harm。
  - pdf174fix summary：618 / precision=1 / harm=0；pdf-174 产出「离子方程式：MnO2 + SO3^2- + 2H+ = Mn2+ + SO4^2- + H2O」判正。

## Task M1.1：剩余 103 多段未解分类审计
- **Status**: completed
- **Priority**: high
- **Depends On**: M1.0
- **Description**:
  - 以 `checkpoint_pdf174fix.jsonl`（618 基线）为基准，取其中 gatewayCorrect=false 且答案为多段复合（含 (1)(2) 或多关键词）的题 = 103（104 − pdf-174）。
  - 逐题读源数据 `pdf_questions.json` 题干，分类：
    - **(a) 碎片可回填解锁**：题干是大题子问碎片、大题上下文可经官方源恢复、且恢复后**现有求解器**（ReactionKbWriterSolver / CrystalCellValidator / HalfReactionBalancer / choice/concept 现有匹配器等）会命中。
    - **(b) 需新增求解器能力**：题干完整或可恢复，但无现有求解器覆盖（如多方程安全连接、可行性判决、催化剂识别、弱酸 Ka、有机推理）。
    - **(c) 真·不完整/数据缺陷**：题干或答案本身残缺（如 pdf-461「题目不完整」、pdf-522 缺结构图），无官方源可补。
  - 落盘 `_evalscratch/multi_remaining_audit.tsv`：id / taskType / 类别(a/b/c) / 理由 / 若(a)则现有求解器与命中锚点。
- **Acceptance Criteria**: AC-3
- **Test Requirements**:
  - `rule` TR-M1.1.1: 审计 TSV 存在且 103 题逐题有类别 + 理由；证据：TSV 文件。
  - `rule` TR-M1.1.2: (a) 类每题标注「现有求解器 + 命中锚点」，可被探针复现；证据：探针输出（EQ_PROBE 门控，必要时）。
- **Completion Evidence**:
  - `_evalscratch/multi_remaining_audit.tsv`（12929 bytes，mtime 13:37）落盘 103 行（104 多段未解 − pdf-174 已解），逐题含 id / taskType / frag / class(a|b|c) / class_reason。
  - 计数复核：**(a)=0**、**(b)=95**、**(c)=8**（pdf-117 / pdf-162 / pdf-164 / pdf-165 / pdf-461 / pdf-522 / trap-limit-2 / trap-limit-3）。
  - (a)=0 的含义：剩余 103 题中，**没有任何一题**仅靠回填大题上下文就能让**现有**求解器命中——要么是能力缺口（b），要么是题面/答案本身残缺（c）。
  - TR-M1.1.2 因 (a)=0 空集满足：无 (a) 候选需要探针复现。

## Task M1.2：(a) 类题干上下文回填（官方源溯源）+ 解锁
- **Status**: completed
- **Priority**: high
- **Depends On**: M1.1
- **Description**:
  - 对 M1.1 识别的 (a) 类候选，从官方源（习题册/答案册 page+num，参照 pdf-174 note 格式）恢复大题上下文，回填到 `src/CogniThink.TutorBrain.Api/data/pdf_questions.json` 的 `question` 字段，`note` 增补官方溯源。
  - 回填后现有求解器应命中（先探针确认），再落盘。
  - 宁可少填不可错填：任一题干回填后若引发 FullBankGuard 误触发（非 null 但判错），立即回退该题。
- **Acceptance Criteria**: AC-4
- **Test Requirements**:
  - `rule` TR-M1.2.1: 每条回填 `note` 含官方源 page/num 溯源；`git diff` 可核，无臆造内容。
  - `rule` TR-M1.2.2: 回填后该题 `gatewayCorrect=true` 且产出经 `IsAnswerCorrect`；证据：探针/eval。
- **Completion Evidence**:
  - M1.1 审计结论 (a)=0 → 本任务**无任何候选需要回填**，故未修改 `pdf_questions.json` 或任何代码/数据文件。
  - `git status --short` 为空（工作树干净，pdf-174 修复已在提交 c5be220 落地）；所有源文件 mtime ≤ pdf174fix 构建时间（13:15），证明本批次无新增改动。
  - TR-M1.2.1 / TR-M1.2.2 因候选空集满足：无回填 → 无 `note` 溯源需审、无判正需复现；零改动即零回归风险。

## Task M1.3：全红线验证（护栏 + 全量测试 + eval diff）
- **Status**: completed
- **Priority**: high
- **Depends On**: M1.2
- **Description**:
  - FullBankGuard（ReactionKbWriterSolver / CrystalCell / HalfReaction 等现有护栏）全绿：凡非 null 必 IsAnswerCorrect。
  - `dotnet test CogniThink.Chem.sln -c Release --no-restore` → 0 failed。
  - eval 新 tag（如 `rel_multi_m1`，LLM-off / Production），`diff_eval -OldTag pdf174fix -NewTag rel_multi_m1`：断言 LOST=0、precision=1、harm=0；记录 GAIN 题。
  - 若红线破坏 → 回退 M1.2 对应回填，本任务不完成。
- **Acceptance Criteria**: AC-4
- **Test Requirements**:
  - `rule` TR-M1.3.1: 全量测试 0 failed；证据：测试汇总。
  - `rule` TR-M1.3.2: diff LOST=0 / precision=1 / harm=0；证据：`_evalscratch/diff_pdf174fix.txt`（rel_half→pdf174fix 证 LOST=0）+ `checkpoint_pdf174fix_summary.txt`（precision=1 / harm=0）。M1.2 零改动，无需新 eval tag。
- **Completion Evidence**:
  - M1.2 对代码/数据**零改动** → 不存在由本批次引入的回归；绿色基线即 M1.0 锁定的 `checkpoint_pdf174fix`。
  - `checkpoint_pdf174fix_summary.txt`：gatewayCorrect=618、precision=1、harmCount=0、bothWrong=139。
  - `diff_pdf174fix.txt`：OLD=756 NEW=756、**GAINED 1 (pdf-174) / LOST 0** → LOST=0 红线成立。
  - 全量测试 `dotnet test CogniThink.Chem.sln -c Release --no-restore`：2139 passed / 0 failed（TutorBrain 1228 + Chem 911，上轮已验证且本批次无代码改动，状态保持）。
  - FullBankGuard（凡非 null 必 IsAnswerCorrect）由 precision=1 实证满足：全库 adopted-wrong=0。

## Task M1.4：登记册同步 + 终态报告 + 后续方向
- **Status**: completed
- **Priority**: medium
- **Depends On**: M1.3
- **Description**:
  - 更新 `data-defect-register.md`：M1 新解题从「未解」移除；(b)(c) 类登记真实排除理由（能力缺口 / 数据缺陷），不伪装为「记号问题」。
  - 产出 `_evalscratch/multi_part_final_report.md`：取证结论（无判分 bug）、基线 618 → M1 终态、GAIN/LOSS/HARM、(a)(b)(c) 分布与后续 spec 建议。
- **Acceptance Criteria**: AC-5
- **Test Requirements**:
  - `rule` TR-M1.4.1: 登记册与终态一致；`git diff` 确认无无官方源的 answer 改动；证据：diff。
  - `rule` TR-M1.4.2: 报告数字与 checkpoint summary 自洽；证据：报告文件。
- **Completion Evidence**:
  - `data-defect-register.md` 已同步：顶部新增「M1 多段复合答案审计结论」callout（0 判分 bug / pdf-174 已解 / (a)=0 (b)=95 (c)=8）；E 类新增 pdf-164/165/117/461 四行 + 把 pdf-162 从「缺上下文」订正为「跨题答案污染」（pdf-522 原已在 E 类）；G① 对 pdf-164/165「归 E 类」的悬空引用现已落地。
  - `_evalscratch/multi_part_final_report.md`（13758 bytes）落盘：取证结论、基线 618、GAIN +1/LOST 0/HARM 0、(a)/(b)/(c) 分布与 (b) 分桶、(c) 8 题明细、红线状态、后续方向建议。
  - TR-M1.4.1：`git diff --stat` 仅显示 `tasks.md` + `data-defect-register.md` 两个 md 文件改动，**零** `pdf_questions.json` / `.cs` 改动 → 无任何无官方源的 answer 改动。
  - TR-M1.4.2：报告数字与 checkpoint summary 自洽——gatewayCorrect=618、precision=1、harm=0、bothWrong=139（与 `checkpoint_pdf174fix_summary.txt` 一致）；104 分解 96+8、剩余 103 分类 0/95/8（与 `multi_unsolved_ground.tsv` / `multi_remaining_audit.tsv` 程序化计数一致）。

## 完成证据汇总（实施后逐任务填写）
| 任务 | 状态 | 证据 |
|------|------|------|
| M1.0 取证 + 基线锁定 | ☑ | `checkpoint_pdf174fix_summary.txt`=618/precision=1/harm=0；`diff_pdf174fix.txt`=GAIN1(pdf-174)/LOST0；`multi_unsolved_ground.tsv`=104 题逐题分类，全库 adopted-wrong=0 |
| M1.1 103 题分类审计 | ☑ | `multi_remaining_audit.tsv` 103 行：(a)=0 / (b)=95 / (c)=8，逐题类别+理由；(a)=0 ⇒ 无回填可解锁题 |
| M1.2 (a) 类回填解锁 | ☑ | (a)=0 ⇒ 零代码/数据改动；`git status` 干净（已提交 c5be220）；无回退风险 |
| M1.3 全红线验证 | ☑ | M1.2 零改动 ⇒ 无新回归；基线 precision=1/harm=0/LOST=0（`diff_pdf174fix.txt`）；全量测试 2139 passed/0 failed |
| M1.4 登记 + 报告 | ☑ | `data-defect-register.md` 同步（M1 callout + E 类新增/订正 6 题）；`multi_part_final_report.md`（13758B）落盘；`git diff` 仅 2 个 md、零 answer/code 改动；数字与 checkpoint 自洽 |
