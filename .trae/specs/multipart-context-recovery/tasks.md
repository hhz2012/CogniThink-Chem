# 多子问母题上下文恢复（轨道 1）- 实施计划

> 基线：`checkpoint_b6baseline`（gwCorr=669/757，precision=1，harm=0，LOST=0，mp27=27/27）。
> 红线：precision=1 / harm=0 / LOST=0 / mp27 不退化。所有任务默认不提交，等用户明确要求。

## Task 1: 实现可开关引语回填 + 全库 ROI 探针
- **Status**: `completed`
- **Completion Evidence**:
  - 实现（探针临时代码，Task 2 已回滚）：`MultiPartUtility.GetLeadingIntro` + `MultiPartDispatch` 环境变量开关 `COGNITHINK_REFILL_SUBQUESTION_CONTEXT`。
  - TR-1.1（关闭态零回归）：env 未设时 `hasIntro=false` → `subReq` 与基线逐字一致（构造性）；回滚后 `b7_final` vs `b6baseline` diff `GAINED(0) / LOST(0)` 实证。
  - TR-1.2：两轮探针四值齐全 — b7_on: GAIN=0 / LOST={pdf-278} / harm=0 / newlyFiredWrong=0；b7_gated: 同。证据 `_evalscratch/probe_context_refill.txt`。
  - TR-1.3：两轮 `verify_mp27 b6baseline b7_on|b7_gated` 均 `PASS (27/27)`。
  - TR-1.4（rubric）：5/5 — 四值齐全，pdf-278 scope-drift 明细可追溯（`_evalscratch/analyze_b7.py`、`dump_one.py`）。
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 在 `MultiPartUtility` 增加获取大题引语的能力（第一个 `(N)` 标记之前的文本），或在 dispatch 内提取。
  - 在 `LlmGatewayService.MultiPartDispatch.TryDispatchMultiPartAsync` 中，子问请求 `RawText` 由「仅子问碎片」改为「引语 + 子问文本」，并置于**默认关闭**的开关（环境变量或静态布尔）后——关闭时与基线逐字一致（构造性零回归）。
  - 开关开启态跑全库（757）Production eval，打快照；与 `b6baseline` diff，产出 `GAIN / LOST / harm / newlyFiredWrong` 探针报告。
  - `newlyFiredWrong` 判定：基线未解（gatewayCorrect=False）+ 开关开启后产出结论且被采纳（gatewayCorrect 变 True）但实际答案键未命中（化学错误）= harm 风险，必须为 0。
- **Acceptance Criteria Addressed**: AC-1, AC-2, AC-3, AC-4, AC-5, AC-6
- **Test Requirements**:
  - `rule` TR-1.1: 开关关闭时，`diff_eval b6baseline <off-snapshot>` 输出 `GAINED (0) / LOST (0)`，证明默认态零回归。证据：eval diff 输出。
  - `rule` TR-1.2: 开关开启态全库探针报告含 `GAIN / LOST / harm / newlyFiredWrong` 四个数值，且 `newlyFiredWrong=0`。证据：`_evalscratch/probe_context_refill.txt`。
  - `rule` TR-1.3: 开启态 `verify_mp27` 输出 `PASS (27/27)`。证据：命令输出。
  - `rubric` TR-1.4: 探针数据完整性；scale 1-5；anchors 1=缺关键数值, 3=有数值但未解释, 5=四值齐全且每题翻正/误判明细可追溯；threshold >= 4；evidence 探针报告。
- **Notes**: 本任务产出的开关 + 回填代码是 Task 2「固化或回滚」的对象。探针用 Production（LLM-off）保证确定性与速度。

## Task 2: 依据探针数据固化或回滚机制
- **Status**: `completed`
- **Completion Evidence**:
  - 决策（FR-4 回滚分支）：探针 GAIN=0 且引入 pdf-278 回归（门控亦无法避免 scope drift）→ 机制无 ROI，移除特性代码，恢复基线。
  - TR-2.1 / TR-2.2：回滚后 `b7_final` vs `b6baseline` diff `LOST(0)`，summary `harm=0 / precision=1`。
  - TR-2.3：`dotnet test -c Release` 2623 通过 / 0 失败 / 0 跳过（含全部既有护栏测试）。
  - TR-2.4（rubric，AC-6）：5/5 — 去留决策与探针数据严格一致（GAIN=0+回归→回滚），记录于登记册 Z 类 + spec Open Questions。
  - `git diff` 两改动文件内容为空（仅行尾提示）→ 生产代码零净变更。
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 读取 Task 1 探针报告：
    - 若 `GAIN >= 1` 且 `LOST=0 / harm=0 / newlyFiredWrong=0` → **固化**：补充单元测试（引语正确拼入子问、开关关闭=基线、AND 门行为不变），运行现有护栏测试（ConceptFact 等）确认无回归，将开关默认态按决策设定。
    - 若 `GAIN = 0` 或存在 `newlyFiredWrong > 0` 无法在不放宽守卫下消除 → **回滚收口**（FR-4）：移除回填代码或保持开关永久关闭，使最终态与基线一致；在登记册与 spec 记录「轨道 1 场景 A 无收益/有 harm 风险」结论。
  - 回滚收口需向用户报告探针数据并获确认（spec FR-4 已预授权，但最终态需用户知情）。
- **Acceptance Criteria Addressed**: AC-1, AC-2, AC-3, AC-4, AC-5, AC-6
- **Test Requirements**:
  - `rule` TR-2.1: 最终态（固化后默认开启 或 回滚后关闭）`diff_eval b6baseline <final>` 的 `LOST=0`。证据：diff 输出。
  - `rule` TR-2.2: 最终态 `harm=0 / precision=1`。证据：summary。
  - `rule` TR-2.3: `dotnet test -c Release` 全绿（含现有护栏测试 + 本任务新增测试），0 failed。证据：测试输出。
  - `rubric` TR-2.4: 决策与数据一致性（AC-6）；scale 1-5；anchors 1=决策与数据矛盾, 3=决策合理但记录不全, 5=去留决策与探针数据严格一致且有记录；threshold >= 4；evidence 任务 Completion Evidence。
- **Notes**: 若走回滚分支，本任务完成后队列可能直接进入评审（无后续实现），此时 AC-7（场景 B 评估）仍由 Task 3 覆盖。

## Task 3: 场景 B（跨 case 碎片重组）可行性评估
- **Status**: `completed`
- **Completion Evidence**:
  - TR-3.1：量化 — 未解碎片 12 题，相邻可恢复引言 11/12，文本可能足够（不缺图）9 题，缺图 2 题，无邻居 1 题。证据 `_evalscratch/scenario_b_feasibility.txt`。
  - TR-3.2（rubric，AC-7）：4/5 — 明确 NO-GO，理由含：场景 A 零增益证据、无 parentId 匹配歧义（pdf-162 命中 3 个大题）、数据质量差、scope drift 风险放大。
  - 纯只读，未改任何文件（报告写 `_evalscratch/`）。
- **Priority**: medium
- **Depends On**: None（可与 Task 1 并行）
- **Description**:
  - 只读分析题录碎片结构：量化「未解 + 碎片 case（question 以 (N) 开头且 N>1，或引语为空）」的题数；检查其兄弟子问/引言是否在相邻 id 的 case 中可恢复；评估重组启发式（id 邻近 + 引言文本匹配 + 子问编号连续）的假阳性风险。
  - 识别「重组后文本信息足够（反应物/条件在文本里，不依赖缺失图像）」的潜在可解题，与「重组也救不了（缺图/题干缺反应物/答案错位）」的题。
  - 产出明确 go/no-go 建议：若启动场景 B，需独立 spec（涉及题录关联建模 + 跨 case 文本拼接 + 假阳性守卫），不在本 spec 实现。
- **Acceptance Criteria Addressed**: AC-7
- **Test Requirements**:
  - `rule` TR-3.1: 评估报告量化列出：碎片未解总数、其中相邻 case 可恢复引言的题数、其中文本信息足够（不缺图）的潜在可解题数。证据：`_evalscratch/scenario_b_feasibility.txt`。
  - `rubric` TR-3.2: go/no-go 明确性（AC-7）；scale 1-5；anchors 1=无结论, 3=有结论但理由弱, 5=结论明确且有量化风险/收益支撑；threshold >= 3；evidence 评估报告。
- **Notes**: 纯只读，不改任何文件（报告写 `_evalscratch/`）。

## Task 4: 全量回归验证（最终态）
- **Status**: `completed`
- **Completion Evidence**:
  - TR-4.1：`diff_eval b6baseline b7_final` → `GAINED (0) / LOST (0)`；summary `harmCount=0 / precision=1`。
  - TR-4.2：`verify_mp27 b6baseline b7_final` → `RESULT: PASS (27/27)`，无 True→False 回归。
  - TR-4.3：`tests/` 下无本批引入的 `_Probe*` 残留（仅 `EquationEngineProbeTests.cs` 为前批预存，非本批）。
  - `dotnet build -c Release` 0 错误；`dotnet test -c Release` 全绿。
- **Priority**: high
- **Depends On**: Task 2
- **Description**:
  - 对 Task 2 确定的最终态（开关默认态）跑全量 Production eval，与 `b6baseline` diff：确认 `LOST=0`、`harm=0`、`precision=1`。
  - `verify_mp27 b6baseline <final>`：27/27。
  - `dotnet build -c Release` 0 错误 0 警告；`dotnet test -c Release` 全绿。
  - 确认无临时探针 .cs 残留（若 Task 1 创建了探针测试文件，已删除或转为常驻测试）。
- **Acceptance Criteria Addressed**: AC-1, AC-2, AC-3
- **Test Requirements**:
  - `rule` TR-4.1: `diff_eval b6baseline <final>` 输出 `LOST (0)` 且 `harm=0`。证据：diff + summary。
  - `rule` TR-4.2: `verify_mp27` `PASS (27/27)`。证据：命令输出。
  - `rule` TR-4.3: `tests/` 下无 `_Probe*` 临时探针残留（本 spec 引入的临时探针已清理）。证据：glob 结果为空。
- **Notes**: 最终态若为回滚（与基线一致），则 GAIN 也=0，diff 应为全 0——此为有效验证，证明回滚干净。

## Task 5: 登记册回填 + spec 状态收尾
- **Status**: `completed`
- **Completion Evidence**:
  - TR-5.1：`data-defect-register.md` 追加 **Z 类** 区段（Z.1–Z.7），记录探针数据、回滚决策、场景 B NO-GO、红线验证、证据指针。
  - TR-5.2（rubric）：5/5 — 结论+数据+证据+下一步建议齐全（登记册 Z.6 + spec Open Questions 全部勾选解决）。
  - spec.md Open Questions 3 项全部标记 [x] 并附数据结论。
- **Priority**: low
- **Depends On**: Task 4
- **Description**:
  - 在 `data-defect-register.md` 追加（或更新）轨道 1 区段：记录探针数据、机制去留决策、场景 B go/no-go 结论、证据指针。
  - 回填本 spec `spec.md` 的 Open Questions 状态（已解决/仍开放）。
  - 汇总最终结果向用户报告：gwCorr 变化、红线验证、下一步建议（场景 B 是否启动 / 是否转向轨道 2）。
- **Acceptance Criteria Addressed**: AC-6, AC-7（文档化收尾）
- **Test Requirements**:
  - `rule` TR-5.1: 登记册含轨道 1 区段，记录探针 GAIN/LOST/harm 与决策。证据：`data-defect-register.md` 对应区段。
  - `rubric` TR-5.2: 收尾文档完整性；scale 1-5；anchors 1=缺关键结论, 3=结论有但证据指针不全, 5=结论+数据+证据+下一步建议齐全；threshold >= 4；evidence 登记册区段 + 最终报告。
