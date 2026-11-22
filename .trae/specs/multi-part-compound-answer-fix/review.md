# 独立评审 — 多段复合答案修复（M1 批次）

> 评审者：独立（fresh context，只读）。本批次对代码/数据**零改动**，仅文档落盘，故评审以**现有落盘证据**为准，不重跑 eval/build。
> 评审基准：[spec.md](file:///D:/github/CogniThink-Chem/.trae/specs/multi-part-compound-answer-fix/spec.md)（AC-1~AC-5 + 冻结红线）/ [tasks.md](file:///D:/github/CogniThink-Chem/.trae/specs/multi-part-compound-answer-fix/tasks.md)（完成证据）。

## 评审检查点（逐条 PASS/FAIL + 证据）

### CP-1（AC-1）：取证证明「多段复合答案无判分 bug」
- [x] 全库扫描 adopted-but-wrong = 0（`checkpoint_rel_half.jsonl`：`gatewayAnswer` 非空且 `gatewayCorrect=false` 的行数）。
- [x] spec §2.1 引用的机制存在于源码：`IsAnswerCorrect`（任一词子串命中即判正）与 `SplitPdfAnswerKeywords`（加载期重切 (1)(2)）。

### CP-2（AC-2）：绿色基线 = checkpoint_pdf174fix（618 / precision=1 / harm=0）
- [x] `checkpoint_pdf174fix_summary.txt`：gatewayCorrect=618、precision=1、harmCount=0。
- [x] `diff_pdf174fix.txt`：GAINED 1 (pdf-174) / LOST 0。

### CP-3（AC-3）：剩余 103 题分类审计 TSV 落盘
- [x] `multi_remaining_audit.tsv` 存在且为 103 数据行（104 − pdf-174），逐题有类别(a/b/c) + 理由。
- [x] 计数复核：(a)=0、(b)=95、(c)=8（程序化计数，不靠目测）。

### CP-4（AC-4）：(a) 类回填解锁满足红线
- [x] (a)=0 ⇒ 本批次无任何 `pdf_questions.json` / `.cs` 改动；`git diff --stat` 仅含 `.md` 文件。
- [x] 无本批次引入的回归（零改动 ⇒ 无回归可能）。

### CP-5（AC-5）：data-defect-register.md 同步
- [x] 顶部存在「M1 多段复合答案审计结论」callout（0 判分 bug / pdf-174 已解 / (a)=0 (b)=95 (c)=8）。
- [x] E 类登记了 (c) 类真实 PDF 缺陷：pdf-117 / pdf-162（订正为跨题污染）/ pdf-164 / pdf-165 / pdf-461 / pdf-522。
- [x] 无无官方源的 answer 改动（`git diff` 证零数据改动）。

### CP-6（冻结红线）
- [x] precision=1、harm=0、LOST=0 全部成立（summary + diff）。

### CP-7（报告自洽）
- [x] `_evalscratch/multi_part_final_report.md` 数字与 checkpoint summary / audit TSV 自洽（618 / 96+8 / 0/95/8）。

### CP-8（队列排空）
- [x] tasks.md 中所有任务 ∈ {completed}，无 pending/in_progress/blocked；每任务有 Completion Evidence。

## 评审结果
- 总体结论：**PASS**（CP-1 ~ CP-8 全部通过，无 FAIL / BLOCKED）
- 可执行发现（已修正，非阻塞）：spec.md §2.2 纯 null 分桶算术原为 99（混入 3 个 hasRaw=True 行：pdf-272/equilibrium、pdf-391/unknown、pdf-522/unknown），已订正为 96 口径（choice 42 / unknown 24 / calculation 18 / equilibrium 8 / judgment 2 / concept 1 / atomic_structure 1），与 `multi_unsolved_ground.tsv` 程序化计数一致。
- 评审者：独立只读评审（fresh context，未修改任何文件；基于 `_evalscratch` 落盘证据 + 源码，未重跑 eval/build）
- 时间：2025-11-21

## 评审证据摘录（独立评审者核实）
- CP-1：`checkpoint_rel_half.jsonl` 全库扫描 → ADOPTED_BUT_WRONG=0；源码 `IsAnswerCorrect`/`SplitPdfAnswerKeywords`/采纳门 均与 spec §2.1 引用一致。
- CP-2：`checkpoint_pdf174fix_summary.txt`=618/precision=1/harm=0；`diff_pdf174fix.txt`=GAIN1(pdf-174)/LOST0。
- CP-3：`multi_remaining_audit.tsv` 103 数据行，程序化计数 a=0/b=95/c=8。
- CP-4：`git diff --stat` 仅 `tasks.md` + `data-defect-register.md`（+本 review.md），零 `.cs`/`.json` 改动。
- CP-5：登记册顶部 M1 callout 存在；E 类登记 pdf-117/162(订正)/164/165/461/522；trap-limit-2/3 标注为合成探针非数据。
- CP-6：precision=1 / harm=0 / LOST=0 全部成立。
- CP-7：报告数字与 summary/audit 自洽（618 / 96+8 / 0/95/8；(b) 分桶 41+22+16+9+4+1+1+1=95 ✓）。
- CP-8：tasks.md M1.0~M1.4 全部 completed，每任务有 Completion Evidence。
