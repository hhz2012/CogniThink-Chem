# 多子问母题上下文恢复（轨道 1）- 独立评审

> 本 spec 的交付是一次**探针驱动的架构方向验证 + 回滚收口**（最终生产代码零净变更）。评审重点：证据完整性、红线守住、决策与数据一致。所有证据在 `_evalscratch/`（gitignore），文档在 `.trae/specs/multipart-context-recovery/` 与 `data-defect-register.md` Z 类。

- [x] CP-R0: 生产代码零净变更（回滚干净）
  - **Type**: `rule`
  - **Covers**: NFR-2, NFR-4, FR-4
  - **Evidence**: `git --no-pager diff -- <两文件>` 空输出（exit 0）；Read 确认 `MultiPartUtility.cs` 仅含 `SubMarker/IsMultiPart/SplitSubQuestions`（无 `GetLeadingIntro`），`LlmGatewayService.MultiPartDispatch.cs` 子问 `RawText = subText`（无引语拼接/开关）；全仓 grep `COGNITHINK_REFILL_SUBQUESTION_CONTEXT|GetLeadingIntro` 仅命中文档，src/配置/测试零匹配。

- [x] CP-R1: 已判正题零回归（LOST=0）
  - **Type**: `rule`
  - **Covers**: AC-1, TR-1.1, TR-2.1, TR-4.1
  - **Evidence**: `diff_eval.ps1 -OldTag b6baseline -NewTag b7_final` → `GAINED (0):` / `LOST (0):`。

- [x] CP-R2: harm=0 且 precision=1
  - **Type**: `rule`
  - **Covers**: AC-2, TR-2.2, TR-4.1
  - **Evidence**: `checkpoint_b7_final_summary.txt` → `harmCount=0`、`precision=1`、`gatewayCorrect=669`（=基线）。

- [x] CP-R3: mp27 守卫 27/27 不退化
  - **Type**: `rule`
  - **Covers**: AC-3, TR-1.3, TR-4.2
  - **Evidence**: `verify_mp27.py b6baseline b7_final` → `RESULT: PASS (27/27)`，`REGRESSIONS (True->False): []`。

- [x] CP-R4: 回填（机制存在时）仅作用于未解多子问题；最终态无特性代码
  - **Type**: `rule`
  - **Covers**: AC-4
  - **Evidence**: 最终态无特性代码（同 CP-R0）；触发边界 [LlmGatewayService.cs#L111](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L111) 仅在 `engineResponse.Ok is not true && IsMultiPart(rawText)` 时分发；两轮探针 757 题唯一变化是 pdf-278，其余 756 题逐字不变。

- [x] CP-R5: 全库探针 newlyFiredWrong=0（无错误采纳）
  - **Type**: `rule`
  - **Covers**: AC-5, TR-1.2
  - **Evidence**: `diff b6baseline vs b7_gated` 与 `vs b7_on` 均 `GAINED (0): / LOST (1): pdf-278`；`dump_one b7_gated pdf-278` → `gatewayCorrect=False`、`truthAdopted=False`（子问(1) scope drift 致不命中答案键→AND 门判负，非错误采纳）；两 summary `harmCount=0`。GAINED 为空 → 无新增采纳 → newlyFiredWrong 结构性为 0。

- [x] CP-U1: 机制去留由探针数据决定（决策有效性）
  - **Type**: `rubric`
  - **Covers**: AC-6, TR-1.4, TR-2.4
  - **Scale**: 1-5
  - **Score**: 5
  - **Anchors**: 1 = 无探针数据靠主观；3 = 有数据但未据此决策；5 = 数据完整且去留决策与数据严格一致
  - **Pass Threshold**: >= 4
  - **Evidence**: 两轮探针四值齐全且独立复核（GAIN=0 / LOST={pdf-278} / harm=0 / newlyFiredWrong=0 / mp27=27/27）；决策与数据严格一致（GAIN=0+回归→按 FR-4 回滚，不硬上场景 B）；记录齐全：登记册 Z.1–Z.7 + spec.md Open Questions 3 项全 [x]。

- [x] CP-U2: 场景 B 可行性评估质量
  - **Type**: `rubric`
  - **Covers**: AC-7, TR-3.1, TR-3.2
  - **Scale**: 1-5
  - **Score**: 5
  - **Anchors**: 1 = 未评估或主观；3 = 列举碎片但无量化；5 = 量化风险/收益并给出明确 go/no-go
  - **Pass Threshold**: >= 3
  - **Evidence**: `scenario_b_feasibility.txt` 量化 12 碎片 / 11 可恢复引言 / 9 文本足够 / 2 缺图 / 1 无邻居；明确 NO-GO，4 项理由均有证据（场景 A 零增益、pdf-162 无 parentId 命中 3 个大题的匹配歧义、碎片数据质量差、scope drift 放大）；给出未来启动需独立 spec 的前置条件。

## Review History

### Review R1
- **Result**: `pass`
- **Evidence**: 独立只读评审（general_purpose 子代理，未修改任何文件）。8/8 检查点全部通过（CP-R0–R5 rule 全 PASS；CP-U1=5/5、CP-U2=5/5，均过阈值）。
  - 复跑验证命令：`diff_eval b6baseline b7_final` → GAINED 0 / LOST 0；`verify_mp27 b6baseline b7_final` → PASS 27/27；`git diff` 两文件空；`diff b6baseline b7_on|b7_gated` → GAINED 0 / LOST pdf-278。
- **Findings**: 无 actionable 发现。3 条 advisory（均不阻塞）：① 已修正 — probe/登记册中 b7_gated 的 summary precision 数值已与 summary 对齐（见下）；② `diff_eval` totalIds=756 vs summary total=757 为脚本按 case.id 去重，两快照 id 集相同，良性；③ review.md 已由本评审回填。
- **Corrections applied**: 修正 `probe_context_refill.txt` 与登记册 Z.2 中 b7_gated 的 precision 数值（summary 实为 1，engineSolved=668；b7_on 为 0.9985，engineSolved=669），并加注说明该比值为 gatewayCorrect/engineSolved、安全精度以 harmCount=0 为准。
