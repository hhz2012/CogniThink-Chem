# 独立审查报告 — remaining-148-reliability-engine

> 审查方式：fresh-context、只读（未修改任何代码/数据，仅产出本文件）。
> 审查依据：`.trae/specs/remaining-148-reliability-engine/{spec.md,tasks.md}`、`_evalscratch/rel_final_report.md`、`data-defect-register.md`、各 eval diff / checkpoint 证据、源码与测试。

## 结论：**PASS**

未发现任一红线被突破（LOST=0 全程成立、precision=1、harmCount=0、无逻辑硬编码化学事实、本轮无无溯源改键、半反应零误触发）。终态 `ie_crystal(612) → rel_half(617)` 净 GAIN +5 / LOST 0，全为正确解。见下表逐项核对与末尾观察项（均非阻断）。

---

## 逐项核对表

| # | 核对项 | 结论 | 证据 |
|---|--------|------|------|
| 1 | 零回归：每批 eval diff 均 LOST=0（全程 ie_crystal→rel_half 见 diff_final.txt） | ✅ 通过 | `_evalscratch/diff_final.txt`：`GAINED (5): pdf-178, pdf-203, pdf-204, pdf-304, pdf-313` / `LOST (0)`；`diff_half.txt`（rel_crystal→rel_half）GAINED 4 / LOST 0；`diff_crystal.txt`（rel_kb→rel_crystal）GAINED 0 / LOST 0，（ie_final→rel_crystal）GAINED 4 / LOST 0；`diff_rel_norm_to_rel_kb.txt` GAINED 0 / LOST 0；`diff_norm_isolated.txt`（rel_preT2→rel_norm）GAINED 0 / LOST 0；`diff_norm.txt` LOST 0。**所有 diff 的 LOST 均为 0**。注：各 diff 头部 `OLD=756 NEW=756 totalIds=756` 为历次 eval 一致存在的 1-id 预存差异（757 总数 − 756 唯一 id），非本轮引入。 |
| 2 | precision=1、harmCount=0：baseline(ie_crystal) 与终态(rel_half) summary | ✅ 通过 | `_evalscratch/checkpoint_ie_crystal_summary.txt`：gatewayCorrect=612，`precision=1`，`harmCount=0`，bothWrong=145；`_evalscratch/checkpoint_rel_half_summary.txt`：gatewayCorrect=617，`precision=1`，`harmCount=0`，bothWrong=140。中间批次 rel_preT2 / rel_norm / rel_kb / rel_crystal 全部 precision=1 / harmCount=0（各 summary 逐字节一致为 613）。 |
| 3 | 无逻辑硬编码化学事实：半反应答案应只在 electrode-half-reactions.json；晶胞事实应在资产 | ✅ 通过 | 半反应：`src/.../Gateway/HalfReactionBalancer.cs` 全文审查——KB 路径从 `data/knowledge/electrode-half-reactions.json` 反序列化取 `entry.HalfReaction`（L31-61）；萃取路径 `BalanceHalfReaction`（L324-396）为确定性配平算法（原子→O 用 H₂O→H 用 H⁺/OH⁻→电荷用 e⁻），由输入化学式计算系数，无具体答案常量。grep 全仓：答案串 `CO-2e^-` / `[B(OH)4]^-` 在 `src/*.cs` 中仅出现于 L210 **注释示例**，无逻辑常量；真实答案源为 `electrode-half-reactions.json`（pdf-304）与算法计算（pdf-313）。晶胞：`CrystalCellValidator.cs` 所有真值（basis 位点/计数/类型、摩尔质量、配位数、formulaUnits、晶格常数 a/c）均从 `type=crystal` 资产 payload 解析（ParseCells/ParseCell L79-126）；`Weight`（L131-139）仅映射通用晶相学均摊规则（corner=1/8 等），无逐题答案硬编码（pdf-170 的 Ni²⁺=4 由资产 Ni 角×8+面×6 计算得出）。pdf_questions.json 含 7 处 `"type":"crystal"` 资产。T2 `NormalizeConnectorsAndLatex`（GatewayVsLlmEvaluationService.cs L600）仅做记号语义变换（\rightleftharpoons→= 等），不含化学真值。 |
| 4 | 新增引擎 precision 护栏：两个 FullBankGuard 断言「每条非 null 输出必 IsAnswerCorrect」；网关 crystal/half 块标 concept_fact 由采纳门兜底 | ✅ 通过 | `tests/.../HalfReactionBalancerFullBankGuardTests.cs`：`FullBankGuard_HalfReaction_EveryNonNullOutput_IsAnswerCorrect`（L16-44）遍历全题库，断言每条非 null 半反应输出 `IsAnswerCorrect=true`，并断言 pdf-304/313 必命中；含 4 负例（choice / 无电极反应 / 酸碱歧义 / 无对无介质无 KB → null）。`tests/.../CrystalCellFullBankGuardTests.cs`：`FullBankGuard_Crystal_EveryNonNullOutput_IsAnswerCorrect`（L15-47）遍历全题库断言每条非 null 晶胞输出 `IsAnswerCorrect=true`，并断言 pdf-170/178/203/204/206/222 必命中。网关接线：`LlmGatewayService.cs` crystal 块（L397-402）与 half 块（L408-413）命中后均设 `req.TaskType = "concept_fact"`。采纳门：`GatewayVsLlmEvaluationService.cs` L672-676 `if (gw.TaskType == "concept_fact") return IsAnswerCorrect(conclusion, c) ? conclusion : null;`——凡采纳必判正，precision 恒为 1、harm 恒为 0。 |
| 5 | T8 半反应无误触发：half_fire_scan.tsv 仅 pdf-304/313 命中且 isCorrect=True；eval 全库含 e^- 的 concept_fact 结论恰好 2 条且都判正 | ✅ 通过 | `_evalscratch/half_fire_scan.tsv`：grep `\tTrue\tTrue\t` 仅命中 pdf-304（L408）与 pdf-313（L417）两行，两者 fired=True 且 isCorrect=True；无 fired=True 且 isCorrect=False 的行（`\tTrue\tFalse\t` 匹配到的是 isCorrect→wasCorrect 列，即两题在 rel_crystal 本为 False，属预期 GAIN）。eval 全库审计：解析 `_evalscratch/checkpoint_rel_half.jsonl`（757 行），taskType=concept_fact 且输出含 `e^-` 的结论恰好 2 条（pdf-304、pdf-313），均 gatewayCorrect=True、gatewayTruthAdopted=True、gatewayUsedEngine=True；全库所有 taskType 中含 e^- 的输出也仅这 2 条。0 误触发。 |
| 6 | 答案键改动有官方源溯源：A.1 每条键订正有 answerbook OCR/原页溯源；本轮(T8)未改 answer 键 | ✅ 通过（附观察） | 本轮未改键：`git diff` 工作树 vs HEAD，`src/.../data/pdf_questions.json` 的 `answer` 字段改动仅 4 处——pdf-170（B→4）、pdf-178（C→复合）、pdf-203（N>H>B>Fe→4）、pdf-485（1:1→2:1），**全部登记在 A.1 且附官方 `page_059`/`page_060`/`page_064` 溯源**；pdf-304/313（T8 目标）未出现任何 answer 改动（关键词在 BuildTestCases 加载期已自动拆分，无需改键）。A.1 溯源核对：pdf-485/170/178/203 附 `pdfs/_extract/answer/page_064/059/060/059.json`；pdf-185/213/405/406（5b）附答案册 OCR `page_064/076-077/137`；pdf-237 附官方答案卷（2024 重庆卷 12.A）。**观察（非阻断，见 Findings）**：A.1 中 5a 批次 6 条（pdf-101/148/269/279/280/399）溯源为「note 化学推导」而非答案册 OCR——但这些是**前序批次（Task 5a）已提交**的历史改动（不在本 spec 工作树 diff 中），登记册原则 #2 明确将其界定为「存储键为明显垃圾值 A,B,C,D 且 note 结论明确」的已验证流程，且经 eval 验证零回归（引擎返回 null、GAIN=0），非本轮引入、非无溯源改键。 |
| 7 | 未纳入题已登记且理由诚实：G 类覆盖留 null 题；无新增排除题 | ✅ 通过 | `data-defect-register.md` G 类：原 27 道留 null，T8 解锁 pdf-304/313（G-⑨ 行已划线标注「T8 已解出」），余 25 道维持诚实 null，每条附具体排除理由（①碎片缺上下文 / ②KB 缺口+路由+多重子问 / ③缺反应物锚定 / ④非写方程问法 / ⑤图像依赖 / ⑥有机推断 / ⑦垃圾键 / ⑧非真方程误标）。实证核对：解析 rel_half.jsonl，G 类登记的 25 个 id **全部确实 gatewayCorrect=False**（0 违规——登记与实际一致、诚实）；pdf-304/313 确实判正。无新增排除题：全链路 diff LOST=0，无任何已解题翻转为 null。 |
| 8 | tests/ 下无 _Tmp*.cs | ✅ 通过 | `Get-ChildItem -Path tests -Recurse -Filter "_Tmp*.cs"` → count = 0。 |
| 9 | 内部一致性：终态报告数字（612→617，GAINED 5：pdf-178/203/204/304/313）与 checkpoint summary、diff_final.txt 三方一致 | ✅ 通过 | `rel_final_report.md` §2/§5：基线 ie_crystal=612，终态 rel_half=617，净 GAIN +5（pdf-178/203/204/304/313）；`checkpoint_ie_crystal_summary.txt`=612、`checkpoint_rel_half_summary.txt`=617；`diff_final.txt` GAINED (5) 恰为 pdf-178/203/204/304/313、LOST (0)。gatewayEngineUsed 619→624（+5）、bothWrong 145→140（−5）亦三方自洽。 |

---

## Actionable Findings

**无必须修复项（无红线突破）。** 以下为非阻断观察，供后续维护参考，不影响本轮结项：

1. **（观察，非阻断）A.1 中 5a 批次 6 条键订正依赖 note 化学推导而非答案册 OCR。** 涉及 pdf-101 / pdf-148 / pdf-269 / pdf-279 / pdf-280 / pdf-399。这些是前序 Task 5a 已提交的历史改动（不在本 spec 工作树 diff 内），登记册原则 #2 已明确界定为「明显垃圾键 A,B,C,D + note 结论明确」的已验证流程，并经 eval 验证零回归（GAIN=0、LOSS=0）。不属于本轮（remaining-148-reliability-engine）引入，也不构成「无溯源改键」。若后续要进一步收紧数据修复规范，可考虑为这 6 条补做答案册原页核对留痕，但不阻塞本轮 PASS。

2. **（观察，非阻断）checkpoint jsonl 有 1 行因 options 含重复键 "A"/"a" 无法被 ConvertFrom-Json 解析。** 表现为 rel_half.jsonl 解析得 756 行（757−1），bothWrong 解析计数为 139（summary 报 140）。属预存数据现象（某题选项同时存在大小写键），与本 spec 无关，不影响判分 summary 与 diff 结论（diff 走 id 级正则、不依赖该行 JSON 解析）。后续数据管道可清理该重复键。

3. **（观察，非阻断）T2 归一化当前题库 GAIN=0。** 隔离 diff（rel_preT2→rel_norm）证实 0 增 0 损，是纯可靠性加固；T4 探针已证当前无题纯粹被记号阻挡（求解器不产出）。这是 spec 已诚实记录的结论，非缺陷——为后续 KB/路由打通后消除记号假阴性奠基。无需本轮动作。

---

## 红线复核（冻结项，全部满足）

| 红线 | 状态 | 证据 |
|------|------|------|
| LOST = 0（每批 + 全程） | ✅ | 见核对项 1 |
| precision = 1 | ✅ | 见核对项 2（baseline 与终态及所有中间 batch） |
| harmCount = 0 | ✅ | 见核对项 2 |
| 无逻辑硬编码化学事实 | ✅ | 见核对项 3（事实只在 JSON / crystal 资产） |
| 无无溯源改键 | ✅ | 见核对项 6（本轮工作树 4 处改键均有 page 溯源；T8 未改键） |
| 半反应零误触发 | ✅ | 见核对项 5（全库仅 2 条 e^- 结论，均判正） |
| 不蒙数（不唯一返回 null） | ✅ | HalfReactionBalancer / CrystalCellValidator 域外一律 null（核对项 3/4 负例测试） |
