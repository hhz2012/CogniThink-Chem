# B-LlmMain 批独立复核报告（Task 9）

- **批次**：B-LlmMain — `LlmGatewayService` 主体迁移（unified-reasoning-dag spec 最后一批）
- **复核方式**：fresh-context 只读子代理独立重跑全部闸门（不修改 / 不创建 / 不删除任何文件，不提交）；主代理落盘本文件
- **复核基准**：基线 tag `after_b_mpdispatch`；红线锚点 `base_p0`；新 tag `after_b_llmmain`
- **总体裁决**：**PASS（整批通过，无阻塞问题）**

## 闸门结果表

| Gate | 命令 / 检查 | 实际结果 | 红线检查 | 通过 |
|---|---|---|---|---|
| 1 BUILD (Release) | `dotnet build CogniThink.Chem.sln -c Release`（增量 + 强制 `--no-incremental` 重建以暴露警告） | 增量 0 err/0 warn；强制重建 **0 err**，14 条 `warning CS` **全部落在允许的预存集合**（AdaptiveTeachingOrchestrator CS8602×2、CrystalCellValidator CS8603、4 条测试文件 CS1998×3/CS8604×1） | 本批文件（FeasibilityRule/Activity/Precipitate ReasoningNodes、LlmGatewayService*.cs）**0 条新警告** | ✅ |
| 2 TEST 全量 | `dotnet test CogniThink.Chem.sln -c Debug` | exit 0；TutorBrain **1450** 通 / Chem **913** 通 / **0 failed** | 0 failed total | ✅ |
| 2 TEST 过滤 | `~ActivityReasoningNodesTests` / `~EquilibriumSolverTests` / `~StoichiometryStateTrapNodesTests`（`--no-build`） | 16 通 / 14 通 / 5 通，各 0 failed | 0 failed | ✅ |
| 3 EVAL DIFF 1 | `diff_ckpt.py after_b_mpdispatch after_b_llmmain` | old 757 / new 757；GAINED 0 / LOST 0 / WORSEN 0 / IMPROVE 0；harm 0→0；`RED-LINE … PASS` | 全零（GAINED/LOST/WORSEN/harm 均 0） | ✅ |
| 3 EVAL DIFF 2 | `diff_ckpt.py base_p0 after_b_llmmain` | old 757 / new 757；**GAINED 6**（pdf-415/530/535/542/543, trap-limit-2）；LOST 0 / WORSEN 0；harm 0→0；`RED-LINE … PASS` | harm=0 且 LOST=0 且 WORSEN=0（GAINED>0 为相对 base_p0 的历史累计增益，已由 Diff1 证实本批贡献 0） | ✅ |
| 4 COVERAGE | `b_llmmain_coverage.py` | 打印 **`ALL B-LLMMAIN COVERAGE ASSERTIONS PASSED`**；节点计数 grew=**411** / decreased=**0** / unchanged=**346**；`gatewayConclusion` drift=**0**；非法 grew=**0**（grew-but-correct=False=0、grew-but-count≠0=0、grew-not-in-solved-but-nodeless=0）；correct/adopted/harm 不变（635/635/0）；grew 按 taskType：choice 394 / equilibrium 6 / reaction_check 9 / concept_fact 2 | 断言通过；drift=0；非法 grew=0 | ✅ |
| 5a 文件存在 | 3 个 Gateway 节点 .cs + precipitate-types.json + standard-state-phases.json | 均存在 | 全部存在 | ✅ |
| 5b 锚点真实性 | knowledge.json / redox-rules.json / activity-series.json | `KH-IN-06` 存在；reductionProducts 含 `redox-no3-dilute` 与 `redox-h2so4-concentrated`；activity-series 15 元素、**H 在 index 9** | 三项锚点均真实（R4 不臆造） | ✅ |
| 5c R4 不臆造 | grep `ReasoningNodes = null` + 三 builder 空输入行为 | src 全量 0 匹配；PrecipitateBuild 命中全空/blank-id → 空列表（不发汇总）；FeasibilityRule/Activity 调用均在真实命中分支内 | 无 `=null`；空输入→空列表；无真实命中不发节点 | ✅ |
| 5d 纯加性 / 判决不动 | `git diff` LlmGatewayService.cs（+ 运行时佐证） | 30 处新增 `ReasoningNodes =` 赋值；0 处删除/改动 `Ok/Confidence/DerivationSteps/ConstraintNote/TaskType/IsStudentAnswerCorrect/ErrorTags`；`FinalResult` 仅机械提取为同文字面量局部变量供 builder 使用，判决值字节不变 | 仅加 ReasoningNodes，不改判决字段（Diff1 全零 + coverage drift=0 逐 case 实证字节一致） | ✅ |
| 5e 无临时文件 / 无密钥 | glob `**/_Tmp*.cs` + grep 密钥模式 | 0 个 `_Tmp*.cs`；src 无 `sk-…`/api_key/password/secret 硬编码 | 均无 | ✅ |
| 6 LEDGER | data-defect-register.md §N.2 + 父 tasks.md | §N.2 LlmGatewayService.cs 行 =「✅ 已迁移 (B-LlmMain 批)」/ **A·done**；统计「已迁移 A=**12** … B=0；**混合 0**；C=1」；父 tasks.md「混合=0：LlmGatewayService 已在 B-LlmMain 批完成迁移」「6. ✅ 已完成：B-LlmMain 批」 | LlmGatewayService 行 A·done；A·done=12；混合=0；B-LlmMain 标 done | ✅ |

## 结论

- **Verdict: PASS** —— B-LlmMain 批全部红线达标，无阻塞问题。
- 判决中性（纯加性）由运行时逐 case 实证：`after_b_mpdispatch`→`after_b_llmmain` checkpoint diff 全零（GAINED/LOST/WORSEN/harm=0），757 例 `gatewayConclusion` 字节级全同；同时 411 例已解出但原无节点的案例（solved-but-nodeless）获得 `ReasoningNodes` 0→>0，节点只增不减、增长子集合法（无臆造、无重复计数）。
- 全量测试无回归（2363 passed / 0 failed）。

## 信息性备注（非缺陷）

1. Release `-v minimal` 增量构建因「项目均最新」不重发警告；为严格核实「本批文件 0 新警告」，额外用 `--no-incremental` 强制重建，确认 14 条警告全部为允许的预存集合、本批文件 0 警告。
2. `git diff` 是相对 HEAD（`8ba1dd0`，含 B-mpdispatch + B-llmmain 两批未提交改动）的累计视图，因此 LlmGatewayService.cs 的 diff 中还包含 B-mpdispatch 的外置化重构；B-LlmMain 批次本身的判决中性由 checkpoint diff（`after_b_mpdispatch`→`after_b_llmmain` 全零）+ coverage（`gatewayConclusion` drift=0）在运行时逐 case 实证，为权威判据。
3. 复核生成的验证日志位于 `_evalscratch/`：`build_release_rebuild.log`、`test_full.log`、`t_Activity.log`、`t_Equil.log`、`t_Stoich.log`、`coverage_b_llmmain.log`、`diff_llmgw.txt`。

## 审查对象

- [LlmGatewayService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs)
- [FeasibilityRuleReasoningNodes.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/FeasibilityRuleReasoningNodes.cs)
- [ActivityReasoningNodes.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/ActivityReasoningNodes.cs)
- [PrecipitateReasoningNodes.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/PrecipitateReasoningNodes.cs)
- [data-defect-register.md §N.2](file:///d:/github/CogniThink-Chem/data-defect-register.md#L610-L635)
- [父 tasks.md](file:///d:/github/CogniThink-Chem/.trae/specs/unified-reasoning-dag/tasks.md#L108-L125)
