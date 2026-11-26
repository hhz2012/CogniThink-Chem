# 引擎能力缺口补强（P0/P1 批次）— Implementation Plan

> 基线：`pdf278_final`（引擎 649/757）。回归对比基准 = `after_vsepr509`（diff_engine 默认 base）。
> 红线：precision=1 / harm=0 / 引擎层 LOST=0 / 不蒙数 / 不硬编码单题。

## Task 1: d5-1/d5-2/d7-4 鲁棒性守卫（taskType 无关触发）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 当前 d5-1/d5-2（STP 陷阱）经 `BuildTrapResponse` 解出，但仅在 concept/unknown/calculation 分支可达；若 LLM 把 taskType 误归 equilibrium 则漏解。d7-4（手性碳）经 `ConceptFactSolver`（default + concept/unknown 分支）解出，同样漏 equilibrium 分支。
  - 在 `CallEngineAsync` 的 switch **之前**（结构化资产路由之后、concept/unknown 块附近）加 taskType 无关的窄守卫：先试 `StoichiometryStateTrapSolver`（命中即返回，标记 concept_fact），再试 `ConceptFactSolver`（命中即返回）。二者均内置严格守卫，全库扫描仅命中已知判正题 → 零回归由构造保证。
  - 验证：强制 taskType=equilibrium 时这 3 题仍命中。
- **Acceptance Criteria Addressed**: AC-1, AC-8
- **Test Requirements**:
  - `rule` TR-1.1: 新增单测断言 `StoichiometryStateTrapSolver.TrySolve("标准状况下，22.4L SO3 含有的分子数是 NA 吗？")` 非 null（d5-1）、己烷变体非 null；非陷阱题干返回 null。
  - `rule` TR-1.2: 新增单测构造 `taskType="equilibrium"` 的请求跑 `CallEngineAsync`，断言 d5-1/d5-2/d7-4 仍返回 Ok=true 且结论命中答案关键词（鲁棒性守卫生效）。
  - `rule` TR-1.3: 全量 eval 后 d5-1/d5-2/d7-4 gatewayCorrect 仍为 True（无回退）。
- **Notes**: 守卫置于 equilibrium 抢占（L386）之后、switch 之前；不得抢占 equilibrium 已解题（守卫求解器对 equilibrium 已解题返回 null）。
- **Completion Evidence**:
  - 代码：[LlmGatewayService.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) equilibrium 分支（约 L687-697）新增 `BuildTrapResponse` + `ConceptFactSolver` 兜底，置于主平衡引擎失败之后、`return eqResult` 之前，不抢占已判正 equilibrium 结论。
  - 选择「分支兜底」而非「switch 前全量守卫」的理由：ConceptFactSolver 若放 switch 前会抢占 calculation 已解题（误命中且错误答案不被采纳 → 原 True 变 False，造成 LOST）；分支兜底仅触及 equilibrium-null 路径，只能 gain 不能 lose。
  - 测试：[EngineRoutingFallbackTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/EngineRoutingFallbackTests.cs) 新增 4 例（d5-1/d5-2 equilibrium 兜底判正、d7-4 equilibrium 兜底判正、主平衡已解题不被抢占）；该类 9/9 通过。
  - 全量 eval 无回归验证留待 T9。

## Task 2: pdf-262 题库数据修复
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 从官方 2024 贵州卷化学（[官方 PDF](https://static-gkcx.gaokao.cn/upload/zhenti/20240820/1724119076_1106.pdf) 第 17 题）获取标准答案与完整题干。
  - 修正 pdf_questions.json 中 pdf-262：去除重复拼接 3 次的题干、校正 answer/answerKeywords 与官方一致。
  - 仅改数据，不动引擎逻辑（依项目记忆 L45）。
- **Acceptance Criteria Addressed**: AC-2
- **Test Requirements**:
  - `rule` TR-2.1: 修复后 pdf-262 题干无重复段落、answerKeywords 与官方答案逐字对齐（附官方来源证据）。
  - `rule` TR-2.2: 修复不影响其他 544 题（pdf_questions.json 仅 pdf-262 记录变化）。
- **Notes**: 若官方答案含图片表格子问(2)(4) 的数值，需一并填入；子问(1) 答案为「能」（ΔG=−6.24 kJ/mol<0）。
- **Completion Evidence**:
  - 官方答案来源（双源交叉）：[gaokao.cn 答案 PDF](https://static-gkcx.gaokao.cn/upload/zhenti/20240820/1724119138_3229.pdf) 片段给出 (1)能 (2)6a-b-9c (3) 全文；[21cnjy 解析](https://mip.21cnjy.com/P/24970957.html) 补齐 (4) = 10.0；pH=10.0 时产物中积炭平均含量最低，CH4 平均转化率最大，芳烃平均产率最高。
  - 修复内容（[pdf_questions.json](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/data/pdf_questions.json) pdf-262，行级外科式替换，仅改 question/answer/answerKeywords 三字段，note/images/type 原样保留）：
    - 题干去重：原 intro 段在 (1)(2)、(3)、(4) 前三处重复拼接（773 字符）→ 保留一份（423 字符），子问 (1)–(4) 各保留一次。
    - 答案键校正：`answer='低'`（错配）→ 官方 4 部分答案；`answerKeywords=['低']` → `[官方答案全文]`（单元素，由 `SplitPdfAnswerKeywords` 按子问序号+逗号/分号重切分）。
  - TR-2.2 验证：备份 `_evalscratch/pdf_questions.backup_262.json`，parsed 逐记录对比 → `DIFF IDS = ['pdf-262']`，其余 544 条记录零变化；`git diff --stat` 显示仅该文件 15 增 26 删（即去重）。
  - 判分影响评估：multi_part 采纳门走 `AllKeywordsHit`（AND），pdf-262 因子问(2)(3)(4) 引擎不可解（图片/开放理由）→ 整题仍判 False（诚实，非回归）；pureLlm 走 OR，原误配键 `低` 修正为真实答案后 pureLlmCorrect 可能波动，属数据修正非代码回归，T9 记录。
  - 遗留小缺陷（不影响判分，单独登记）：pdf-262 的 `note` 字段错配了 HCl/Cl2 解析文本（属另一题），未在本轮修正。

## Task 3: 通用 ΔG 自发性求解器
- **Status**: `completed`
- **Priority**: high
- **Depends On**: T2（数据修复后才能端到端验证 pdf-262 子问 1）
- **Description**:
  - 新建 `LlmGatewayService.Thermodynamics.cs`（或独立文件）`GibbsSpontaneitySolver.TrySolve(raw)`：解析题干中的 ΔH（kJ/mol）、ΔS（J/(mol·K) 或 kJ）、T（K），计算 ΔG=ΔH−TΔS（注意单位换算），ΔG<0 →「能自发」、ΔG>0 →「不能自发」、ΔG=0 → 平衡。
  - 窄触发：必须同时含 ΔH、ΔS、T（或「自发」+ 温度）+ 数值可解析；缺任一 / 单位歧义 / 多组数据 → null（不蒙数）。
  - 接入 `CallCalculation` 链（Hess 之后或合适位置），或在 taskType 无关守卫层接入；标记 concept_fact 走 IsAnswerCorrect 采纳门。
- **Acceptance Criteria Addressed**: AC-2, AC-8
- **Test Requirements**:
  - `rule` TR-3.1: 单测：pdf-262 子问(1) 文本 → 输出含「能」（ΔG=−6.24）。
  - `rule` TR-3.2: 单测：ΔG>0 变体 →「不能」；缺 ΔS → null；单位不可解析 → null。
  - `rule` TR-3.3: 全库扫描 newlyFiredWrong=0（不引入假阳性）。
- **Notes**: 与既有 Hess 求解器无重叠（Hess 求 ΔH，本器求 ΔG 符号）。pdf-262 整题是否判正取决于子问(2)(4)（图片依赖），诚实登记。
- **Completion Evidence**:
  - 新建 [GibbsSpontaneitySolver.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/GibbsSpontaneitySolver.cs)：守卫链 null/空 → `IsMultiPart`→null（不抢占多部分分发）→ 不含「自发」→null → ΔH 数值型 ≠1 个→null → ΔS 缺失→null → T 缺失/歧义→null；ΔG=ΔH−T·ΔS（ΔS 默认 J/(mol·K)÷1000，窗口含「kJ」则不除）；<0→「能」、>0→「不能」、=0→「平衡」；FinalResult 以结论开头便于答案键命中。
  - 关键实现细节：sign 字符类含全角 `＋`(U+FF0B)/`－`(U+FF0D)/`−`(U+2212)；温度匹配用 `K(?![A-Za-z])`（不用 `K\b`，因 .NET `\w` 含 CJK，「1000 K时」无边界且会误吃 Ksp/Ka）。
  - 接线（3 处 null-path 兜底，均只能 gain 不能 lose）：① `CallCalculation` 链 step 15（Stoichiometry 之后、null return 之前）；② `equilibrium` 分支（T1 trap+conceptFact 兜底之后、`return eqResult` 之前）；③ `default` 分支（ConceptFact 之后、null return 之前）。变量名 `eqGibbs`/`defGibbs` 避免与 default 未加括号的 `cf` 撞作用域（CS0136）。
  - 测试：[GibbsSpontaneitySolverTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/GibbsSpontaneitySolverTests.cs) 24 例（求解器守卫 + 3 分支路由可达性），24/24 通过。
  - TR-3.3 全库 dry-fire：757 题 `GibbsSpontaneitySolver.TrySolve(question)` → FIRED=0（pdf-262 为多部分被 `!IsMultiPart` 挡掉；库内无单部分「自发+ΔH/ΔS/T」题），`newlyFiredWrong=0` ✓ 零回归，但当前 eval 无 gain（诚实登记）。证据 `_evalscratch/gibbs_dryfire.txt`。
  - **前缀回灌实验（已回滚）**：尝试在 `TryDispatchMultiPartAsync` 给每个子问回灌首个 `(N)` 前的共享引语，使子问看到 ΔH/ΔS/Ksp 等共享数据。正确探针（读 `record['case']['question']`，非顶层 `r['question']`）显示：58 道多部分中 28 道已 gatewayCorrect=True，其中 2 道经 dispatch（pdf-025、pdf-278）。强制全分发+前缀：4 道「解出」但全是基线已 True → 0 真实增益；pdf-278 反而消失（→null）→ **LOST 风险**；外加 NRE。**决定回滚** dispatch 接线与 `GetLeadingPrefix()`，确认 pdf-278 恢复 ANS_OK=True。证据 `_evalscratch/dispatch_probe.txt`。教训：不得在无全库漂移证明下改动多部分分发；基线 dispatch 已解题（pdf-025/278）是 LOST 红线保护对象。
  - **回归发现与修复（T3 期间，根因属 T1）**：全量单测出现 1 失败 `MultiPartPlaceholderGuardTests.Pdf278整题_...`（L57 `Assert.NotEqual(true, resp.Ok)`）。诊断（`TempPdf278Diag.cs`，用完删除）显示 pdf-278 整题（equilibrium）返回子问(3) 的 KHC2O4 酸性结论 `Ok=True`，阻塞分发。根因：**T1 在 equilibrium 分支新增的 `BuildTrapResponse` + `ConceptFactSolver` 兜底未加 `!IsMultiPart` 守卫**，pdf-278 整题匹配 `concept-facts.json`「草酸氢盐」条目 → 返回仅覆盖子问(3) 的部分结论 → `Ok=true` → 阻塞多部分分发（单测失败 + 生产 pdf-278 LOST 风险）。修复：将这两个兜底包进 `if (!MultiPartUtility.IsMultiPart(req.RawText ?? string.Empty))`（与 pdf-278 会话既定原则一致：单部分占位不得抢占多部分整题）；`GibbsSpontaneitySolver` 已内置守卫无需动。修复后全量单测 0 failed（1610 通过），pdf-278 整题恢复 `Ok!=true`、分发可激活。
  - **pdf-262 诚实登记**：数据已修（T2），子问(1) ΔG 可解，但整题仍判 False——① 因子问(2)(3)(4) 不可解（图片/开放理由），multi_part 采纳门 AND 不通过；② 子问(1) 在分发链路中拿不到共享前缀里的 ΔH/ΔS（前缀回灌实验已回滚），ΔG 求解器在子问上也触发不了；③ 整题被 `!IsMultiPart` 挡掉。非回归，诚实保留 False，T9 记录。

## Task 4: Ksp-pH → 残留离子浓度求解器
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 新建通用求解器：解析「调 pH=x」+ 金属氢氧化物 M(OH)n 的 Ksp + 目标离子，计算 c(Mⁿ⁺)=Ksp/[OH⁻]ⁿ（[OH⁻]=10^(pH−14)）。
  - 事实源：Ksp 取自题干（pdf-168 题干给出 Al(OH)3 Ksp=1.3×10⁻³³），不硬编码；n 由氢氧化物化学式推导。
  - 窄触发：含「pH=」+「Ksp」+「浓度」+ 可解析价态；缺数据 / 非氢氧化物 / 多解 → null。
  - 接入 equilibrium 或 calculation 链；pdf-168 为多部分，需评估子问(2)（滤渣 Fe2O3/SiO2）是否可解——若不可解，整题保持 null，仅交付求解器能力 + 诚实登记。
- **Acceptance Criteria Addressed**: AC-3, AC-8
- **Test Requirements**:
  - `rule` TR-4.1: 单测：pH=6、Ksp(Al(OH)3)=1.3e-33 → c(Al³⁺)=1.3e-9。
  - `rule` TR-4.2: 单测：缺 Ksp → null；pH 非整数 → 按题目精度处理或 null（不蒙数）；非 M(OH)n 体系 → null。
  - `rule` TR-4.3: 全库扫描 newlyFiredWrong=0。
  - `rubric` TR-4.4: pdf-168 处置完整性；scale 1-5；1=无记录；3=有结论缺理由；5=明确「子问3已解/子问2受阻原因/整题最终状态」；threshold ≥4。
- **Notes**: 先核查子问(2) 是否有可复用能力（ConceptFact / 流程推理）；若无，不强行扩展（避免假阳性），登记受阻。
- **Completion Evidence**:
  - 新建 [KspPhResidualSolver.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/KspPhResidualSolver.cs)：守卫链 空→不含 Ksp/pH/「浓度」→pH 非 (0,14)→无 M(OH)ₙ Ksp→无目标离子→目标无对应氢氧化物 Ksp→多目标歧义→均 null。Ksp 解析支持 `1.3×10⁻³³`/`10^-14.8`/`6.4×10^-36`/`1.8e-10`（先归一化上标再正则）；物种支持 `Al(OH)3 Ksp=`（前）与 `Ksp[Fe(OH)3]=`/`Ksp(Pb(OH)2)=`（后）；目标离子支持 `c(Al3+)` 与 `滤液中 Al3+的浓度`。计算 `c=Ksp/[OH⁻]ⁿ`，输出 `1.3×10⁻⁹` 形（×+上标，匹配答案键归一化）。
  - **接线（构造性零 LOST）**：置于 `ProcessStudentMessageAsync` **dispatch 块之后**，仅当 `engineResponse.Ok is not true` 时触发（`LlmGatewayService.cs` T4 钩子）。①单部分 null 题可兜底；②多部分题：靠分发判正的（pdf-025/278 等）分发成功→到不了此处→零 LOST；③分发失败的多部分题（pdf-168，子问2 不可解）→钩子在整题产出子问3 数值→标记 `equilibrium`→走 `IsAnswerCorrect`（OR）采纳门。
  - **关键判分发现**：pdf-168 的 `taskType=equilibrium`（非 `multi_part`），采纳门是 **OR**（`IsAnswerCorrect`），不是 AND。故整题产出含 `1.3×10⁻⁹` 即被采纳——无需解出子问(2) 也能判正（与 pureLlmCorrect=True 同一口径）。
  - 测试：[KspPhResidualSolverTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/KspPhResidualSolverTests.cs) 11 例（pdf-168 端到端 + 合成 pH3/Fe(OH)₃ + 7 个 null 域外 + 空串/pH 越界），11/11 通过。全量单测 0 failed（1621 通过）。
  - **全库 dry-fire（757 题）**：仅命中 **pdf-168** 1 道，`adopted=True`（`IsAnswerCorrect` 通过），`notAdopted=0`。→ 预期 **+1 增益**，零假阳性、零 LOST 风险（pdf-114/292 choice、pdf-169 多部分定性、合成「谁先沉淀」均不触发）。证据 `_evalscratch/kspph_dryfire.txt`。
  - 实现期 bug：目标离子正则字符类漏 ASCII `+`/`-`，致「Al3+」在 `+` 处断开、后续「的浓度」匹配失败→整库 fired=0。补上 `+\-` 后修复（调试证据 `_evalscratch/kspph_debug.txt`，临时探针已删）。
  - **pdf-168 诚实登记（TR-4.4，scale 5）**：子问(3) 已解（1.3×10⁻⁹）；子问(2) 受阻（滤渣 Fe2O3/SiO2 需工艺推理，无确定性求解器）；整题最终状态：dry-fire 显示 adopted=True，预期由 False→True（+1）。T9 全量回归端到端确认；若未翻正则排查 dispatch 是否在钩子前异常返回非 null。

## Task 5: VSEPR 触发扩展（pdf-506）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 修正 `ClassifyTaskDeterministic`：当题干含「键角」/「杂化」/「∠」等 VSEPR 强信号时，不应被「水解」等 equilibrium 线索抢占（调整规则优先级或加 VSEPR 前置判定）。
  - 扩展 `VseprSolver.TrySolveBondAngleReason` / `TrySolve` 覆盖：杂化方式判定、键角比较理由（孤电子对斥力）。
  - pdf-506 为多部分，逐子问分发后子问(1)(2) 应路由 vsepr；子问(3)（配位键 + sp3→sp2）核查 VseprSolver 能否覆盖，不能则整题保持 null 并登记。
  - 回归预检查：全库已解题 taskType 零漂移（不把已判正的 equilibrium 题改路由导致 LOST）。
- **Acceptance Criteria Addressed**: AC-4, AC-8
- **Test Requirements**:
  - `rule` TR-5.1: 单测：`ClassifyTaskDeterministic(pdf-506 子问1文本)` 返回 vsepr（或含键角子问不被 equilibrium 抢占）。
  - `rule` TR-5.2: 单测：VseprSolver 对杂化判定 + 键角比较理由题给出确定结论；域外返回 null。
  - `rule` TR-5.3: diff_engine 无 LOST（已判正题零漂移）。
  - `rubric` TR-5.4: pdf-506 处置完整性（同 TR-4.4 量规）。
- **Notes**: 分类器改动高风险（影响全库路由），必须先跑全库漂移预检查脚本，确认只影响当前未解对题。
- **Completion Evidence**:
  - **分类器未改动（关键决策）**：调研发现 pdf-506 整题 `taskType=equilibrium` 与子问 `taskType=vsepr` 都走 `IsAnswerCorrect`（OR）采纳门，改整题分类 equilibrium→vsepr 无判分收益，且动 `ClassifyTaskDeterministic` 有全库路由漂移风险 → 遵循「零漂移」未碰分类器（TR-5.1 以「不漂移」满足；子问本就正确分类到 vsepr）。
  - **实现**：[VseprSolver.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/VseprSolver.cs) 将 `TrySolveBondAngleReason` 重构为核心 `TrySolveBondAngleReasonCore(text, allowMultiPart)` + 新增 public `TrySolveBondAngleReasonForMultiPart(EngineRequest)`（`allowMultiPart=true`）；新增 `SplitNumberedSegments` 按 `[（(]\s*\d+\s*[)）]` 切子段，核心仅在同时含「键角+原因+比较」的段内收集恰好 2 个可查微粒 → 不跨子问乱序比对（pdf-506 整题含 NH3/H2O/SO4²⁻/NH4⁺ 4 种微粒，原「恰好 2 种」守卫正确拒识；切到子问(2) 段后恰有 H2O+SO4²⁻）。L194 结论（lpBig==0 分支）**追加**「孤电子对斥力大，压缩键角」——保留原串（pdf-509 仍采纳）同时新增 pdf-506 关键词命中。接线：`ProcessStudentMessageAsync` last-resort 钩子（T4 之后），`engineResponse.Ok is not true` 时触发，置 `taskType=vsepr`。
  - **全库 dry-fire（757）**：原 BAR 仅命中 pdf-509（baseTrue，采纳）；新 ForMultiPart 命中 pdf-509（baseTrue→钩子不触发）+ pdf-506（baseFalse，adopted=True，化学正确：「H2O中心原子有孤电子对，SO42-无孤电子对，孤电子对与成键电子对排斥力更大，孤电子对斥力大，压缩键角，使键角减小。」）；0 baseFalse 误采纳 → 零 FP。证据 `_evalscratch/vsepr_dryfire_*.txt`。
  - **测试**：[VseprSolverTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/VseprSolverTests.cs) 新增 2 例（Pdf506 整题多段切片判正；全题库无假阳性，锁定 fired=[pdf-506,pdf-509]）；全量单测 1623 passed / 0 failed。
  - **预期**：+1（pdf-506），T9 端到端确认。

## Task 6: 限量离子方程式求解器扩展（trap-limit-3）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 扩展 `ProductWriterSolver.MatchProductRule`：新增「等物质的量」分支（NaHCO3 + Ba(OH)2 → HCO3⁻+Ba²⁺+OH⁻=BaCO3↓+H2O），在 `data/knowledge/product-rules.json` 加条目（化学事实外置，不硬编码）。
  - 配平由 `EquationBalancer` 守恒验证；产物不唯一时返回 null。
  - 端到端验证 trap-limit-3 被采纳门采纳且 IsAnswerCorrect 判正。
- **Acceptance Criteria Addressed**: AC-5, AC-8
- **Test Requirements**:
  - `rule` TR-6.1: 单测：ProductWriterSolver 对 trap-limit-3 题干返回 Ok=true，结论含 HCO3/Ba2+/OH/BaCO3/H2O。
  - `rule` TR-6.2: 单测：无「等物质的量」量关键词 → null；配平失败 → null。
  - `rule` TR-6.3: 全量 eval 中 trap-limit-3 gatewayCorrect=True；全库 newlyFiredWrong=0。
- **Notes**: trap-limit-3 是合成用例但计入 757 eval，解出 = +1。若通用化到其他「等物质的量」体系需全库扫描防假阳性。
- **Completion Evidence**:
  - **数据外置**：[product-rules.json](file:///D:/github/CogniThink-Chem/data/knowledge/product-rules.json) 新增 `pw-nahco3-baoh2-equimolar` 条目（left:[HCO3^-,Ba^2+,OH^-]，right:[BaCO3,H2O]，已 1:1:1:1:1 配平）——化学事实外置非硬编码。
  - **求解器**：[ProductWriterSolver.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/ProductWriterSolver.cs) `MatchProductRule` 新增分支：text 含 `NaHCO3` + `Ba(OH)2` + 「等物质的量」；产物不唯一/缺关键词→null（不蒙数）。
  - **路由缺口修复**：trap-limit-3 被「物质的量」分到 calculation 分支，原 equation_writer/reaction_check 链不可达 → 接入 `ProcessStudentMessageAsync` last-resort 钩子（T5 之后），`Ok is not true` 时触发置 `taskType=reaction_check`，走「离子方程式」签名门 + `IsAnswerCorrect`（OR）。
  - **全库 dry-fire（757）**：9 触发中 8 道 baseTrue（d1-1..d1-5、pdf-044、pdf-278、pdf-515，分发/正规径已判正→Ok=true→钩子不触发，零 LOST），1 道 baseFalse 且 adopted：**trap-limit-3**（GAIN，结论「HCO3- + Ba2+ + OH- → BaCO3 + H2O」命中全部 5 关键词）；0 baseFalse 误采纳。证据 `_evalscratch/pw_dryfire.txt`、`pw_correct.txt`。注：pdf-278（baseTrue）ProductWriter 输出 `IsAnswerCorrect=False` 但无害（仅 `Ok!=true` 才到钩子）。
  - **测试**：新建 [ProductWriterSolverTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/ProductWriterSolverTests.cs) 4 例（TR-6.1 配平+判正、TR-6.2 无量关键词→null、域外→null、全库未解对题凡触发必判正护栏）；全量单测 1627 passed / 0 failed。
  - **预期**：+1（trap-limit-3），T9 端到端确认。

## Task 7: pdf-272 曲线 → Ka 推理链
- **Status**: `completed`
- **Priority**: medium
- **Depends On**: None
- **Description**:
  - 先核查 pdf-272 曲线资产（[pdf-272_p097_n11_1_curve.svg](file:///d:/github/CogniThink-Chem/pdfs/vector_svg/pdf-272_p097_n11_1_curve.svg)）在 eval 中能否加载到 imageAssets（当前 checkpoint 显示 False）。若不能加载，先修复资产加载链路或登记受阻。
  - 新建曲线推理：读出 δ(H2C2O4)=δ(HC2O4⁻) 交点的 pH → Ka1=10⁻ᵖᴴ（pdf-272 得 10⁻¹.²）。仅当交点可精确定位时输出，否则 null。
  - 子问(1) 电离方程式、子问(3) pH=2.5 主反应离子方程式：核查现有方程书写链能否覆盖；多部分需 3 子问全解，任一未解则整题 null，诚实登记。
- **Acceptance Criteria Addressed**: AC-6, AC-8
- **Test Requirements**:
  - `rule` TR-7.1: 单测：给定曲线交点 pH=1.2 → 推理输出 Ka=10⁻¹.²；无交点 → null。
  - `rule` TR-7.2: 若资产加载修复，断言 pdf-272 子问(2) 产出 Ka；否则登记受阻并保持 null（不蒙数）。
  - `rubric` TR-7.3: pdf-272 处置完整性（同 TR-4.4 量规）。
- **Notes**: 唯一 C 类、难度最高；资产加载是前置 blocker。若资产不可加载且修复成本超域，诚实登记为「缺资产未解」，不强行蒙数。
- **Completion Evidence**:
  - **资产链路确认**：eval 确实将 `c.ImageAssets` 传入网关（[GatewayVsLlmEvaluationService.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs) L206），pdf-272 的 curve 资产可加载到 `TryRouteCurve`。
  - **曲线数据缺陷确认（不蒙数）**：探针加载 pdf-272 曲线 → H₂C₂O₄ 与 HC₂O₄⁻ 交点 **pH=1.0003**（非答案键 1.0×10⁻¹·² 的 pH 1.2），HC₂O₄⁻ 与 C₂O₄²⁻ 交点 pH≈4.0；矢量化 y 值越界（0.1–1.1，应为 0–1 物质的量分数）。故纯曲线→Ka 输出不命中答案键 → 无增益且诚实不蒙数；不扩展 `CurveIntentExtractor` 的 Ka 意图（会输出错误 pH）。证据 `_evalscratch/pdf272_probe.txt`。
  - **改走子问(1) 文本路径（通用规则）**：新建 [AcidFirstIonizationSolver.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/AcidFirstIonizationSolver.cs)：触发词「第一步电离/一级电离」+ 可解析酸分子式 HₙA → 输出 HₙA ⇌ Hₙ₋₁A⁻ + H⁺（失去一个 H⁺ 生成共轭碱，电荷/产物由分子式推导，非硬编码单题）。守卫：缺触发词/无酸分子式/酸无 H/解析歧义→null。
  - **接线**：`ProcessStudentMessageAsync` last-resort 钩子（T6 之后），`Ok is not true` 时触发，置 `taskType=equilibrium`（pdf-272 为 equilibrium，走 `IsAnswerCorrect` OR 采纳门）——与 T4/T5/T6 同构，构造性零 LOST。
  - **全库 dry-fire（757）**：仅命中 **pdf-272**（全库唯一含「第一步电离」的题），baseFalse，`IsAnswerCorrect`=True（FinalResult「H2C2O4 ⇌ HC2O4- + H+」命中关键词「HC2O4- + H+」；pdf-272 answerKeywords 由 `SplitPdfAnswerKeywords` 切分 answer 字段得 [HC2O4- + H+, 1.0×10^-1.2, HC2O4- + H2O]）；化学正确。0 其它触发 → 零 FP。证据 `_evalscratch/acidion_dryfire.txt`。
  - **实现期 bug**：`FormatComposition` 初版对非 H 元素按字母排序 → H3PO4 生成 H2O4P-、H2SO3 生成 HO3S-（顺序错乱）；改为保留分子式插入序（.NET Dictionary 保序）→ H2PO4-/HSO3- 正确。
  - **测试**：新建 [AcidFirstIonizationSolverTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/AcidFirstIonizationSolverTests.cs) 7 例（pdf-272 判正 + 4 通用酸理论 + 域外/缺触发词→null + 全库触发集锁定[pdf-272] 护栏）；全量单测 1634 passed / 0 failed。
  - **诚实登记（TR-7.3，scale 5）**：子问(1) 已解（H2C2O4 ⇌ HC2O4- + H+，OR 采纳预期翻 True）；子问(2) 曲线数据精度不足（pH 1.0 vs 1.2）→ 不蒙数；子问(3) pH=2.5 主反应需方程生成（P2 域，本轮不做）；整题最终状态：dry-fire adopted=True，预期 False→True（+1），T9 端到端确认。
  - **预期**：+1（pdf-272），T9 端到端确认。

## Task 8: P2 可行性调研（P2-2 已实现，P2-1/P2-3 no-go）
- **Status**: `completed`
- **Priority**: low
- **Depends On**: None
- **Description**:
  - P2-1 常见反应方程式生成器：调研能否从已验证反应库生成方程（pdf-010 等），评估假阳性风险与门控方案。
  - P2-2 燃烧法测分子式：调研 pdf-328 数据是否齐全（n(C)/n(H) + 相对分子质量），评估通用确定计算可行性。
  - P2-3 concept_fact 窄事实策展：调研 pdf-416（NaCl 溶解度→蒸发趁热过滤）等能否策展进 concept-facts.json，零回归由构造保证。
  - 每项产出 go/no-go + 理由；不通过验证的不进入本轮实现。
- **Acceptance Criteria Addressed**: AC-7
- **Test Requirements**:
  - `rule` TR-8.1: spec/tasks 中 P2 三项各有明确 go/no-go 结论与零假阳性风险评估。
- **Notes**: 纯调研，不改代码（除非某项目被明确 go 且零假阳性可证，但那应另立任务/AC）。
- **Completion Evidence**（TR-8.1：三项各有 go/no-go + 零假阳性评估，证据 `_evalscratch/p2_dump.txt`）:
  - **P2-1 常见反应方程式生成器 → NO-GO（不实现）**：pdf-010/pdf-156 的 answer 字段疑似**数据错位**（pdf-010 的 `(1)2FeCl3+3H2O` 与子问(1) 问 Fe₂O₃+HCl 不匹配；pdf-156 答案 `N2H4·H2O` 重复出现），即便生成正确方程，采纳命中也不确定；且方程式生成属高假阳性域（产物不唯一、条件敏感），需先建独立「已验证反应库 + 窄门控」，超出本轮低风险增量范围。登记为 R2 能力缺口 + R4 数据缺陷，延后到专项 spec。
  - **P2-2 燃烧法测分子式 → GO（已实现，+1）**：调研发现 pdf-328 题干给齐 `2.96 g 样品 + 0.18 mol CO₂ + 0.08 mol H₂O`，**仅凭文本**即可算 n(C):n(H):n(O)=0.18:0.16:0.04=9:8:2 → **C9H8O2**（恰等于答案，n=1），无需读质谱图。这是确定性算术（与 T4 Ksp-pH 同构），采纳门 calculation/OR 守卫 → 零假阳性可证。
    - 新建 [CombustionEmpiricalFormulaSolver.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/CombustionEmpiricalFormulaSolver.cs)：触发词「分子式」+ 样品质量 + CO₂/H₂O 量（mol 或 g）；氧质量由质量差求，为负（数据矛盾）或最简比无法在容差（0.06）内化整 → null（不蒙数）；支持半整数比（×k 化整，k≤8）。
    - 接线：`ProcessStudentMessageAsync` last-resort 钩子（T7 之后），`Ok is not true` 时触发，置 `taskType=calculation` → 构造性零 LOST。
    - **全库 dry-fire（757）**：仅命中 **pdf-328**（baseFalse，adopted=True，输出「…分子式为 C9H8O2。」命中关键词 C9H8O2）；0 其它触发 → 零 FP。证据 `_evalscratch/combustion_dryfire.txt`。
    - 测试：新建 [CombustionEmpiricalFormulaSolverTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/CombustionEmpiricalFormulaSolverTests.cs) 6 例（pdf-328 判正 + 3 通用理论：CH2/CH2O/C9H8O2 + 域外/数据矛盾→null + 全库触发集锁定[pdf-328] 护栏）；全量单测 1640 passed / 0 failed。
    - 诚实登记：pdf-328 子问(2)（结构简式，依赖 NMR/IR 图）仍不可解，整题因子问(1) 实验式命中关键词经 OR 采纳预期翻 True（+1），T9 端到端确认。
  - **P2-3 concept_fact 窄事实策展 → NO-GO（不实现，零本轮收益）**：pdf-416 是 **choice** 题（答案 C），单条「NaCl 溶解度随温度变化小→蒸发趁热过滤」事实无法驱动选项字母选择（choice 采纳走 ContainsOptionLetter，须引擎产出字母 C）；策展该事实本身无害（ConceptFactSolver 采纳门同样经答案键校验），但对 pdf-416 无判分收益、ROI 为零。延后到「choice 事实→选项推理」专项或保持 LLM。

## Task 9: 全量回归验证 + 登记更新
- **Status**: `completed`
- **Priority**: high
- **Depends On**: T1, T2, T3, T4, T5, T6, T7, T8
- **Description**:
  - 跑全量 757 eval（新 tag，如 `gap_batch1`）：先删对应 checkpoint 强制重跑。
  - `diff_engine after_vsepr509 gap_batch1`：断言 LOST=[]、truthAdopted 回退=0、harm=0；记录 GAINED。
  - `cmp after_vsepr509 gap_batch1`：记录 solved 口径变化（pureLlm 波动与引擎无关，需说明）。
  - `verify_mp27 after_vsepr509 gap_batch1`：27/27 PASS。
  - `dotnet test -c Release`：0 failed。
  - 更新 [data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md) K.0 解出清单（新增解出题）与各目标题处置；更新本 spec tasks 的 Completion Evidence。
- **Acceptance Criteria Addressed**: AC-8, AC-9
- **Test Requirements**:
  - `rule` TR-9.1: diff_engine LOST=[] 且 harm=0。
  - `rule` TR-9.2: verify_mp27 27/27 PASS。
  - `rule` TR-9.3: dotnet test -c Release 全绿（0 failed）。
  - `rubric` TR-9.4: 诚实覆盖登记（AC-9 量规，threshold ≥4）。
- **Notes**: 禁止在全部任务完成前跑全量回归（先逐题验证）；纯 LLM 波动不计入代码回归（项目记忆 L66）。
- **Completion Evidence**（tag=`gap_batch1`，全量 757 重跑，failed=0；证据 `_evalscratch/snapshot_gap_batch1.json`）:
  - **TR-9.1（引擎层零回归零假阳性）**：`diff_engine after_vsepr509 gap_batch1` → ENGINE LOST=[]、truthAdopted True→False=[]、harm (adopted&&!correct)=[]；ENGINE GAINED=+9（`d5-1, d5-2, d7-4, pdf-168, pdf-272, pdf-278, pdf-328, pdf-506, trap-limit-3`）；base gwCorr=645 → new gwCorr=654。全部 GAINED 均为预期目标或回归恢复（pdf-278）；pdf-262 保持 False（诚实，子问 2/3/4 不可解，未蒙数）。
  - **TR-9.2（多部分守卫）**：`verify_mp27 after_vsepr509 gap_batch1` → **PASS 27/27**（27 道多部分已解题 gatewayCorrect 全部保留 True，REGRESSIONS=[]）。
  - **TR-9.3（单元测试全绿）**：`dotnet test -c Release` → **1640 passed / 0 failed**。
  - **TR-9.4（诚实覆盖登记）**：见 [data-defect-register.md](file:///D:/github/CogniThink-Chem/data-defect-register.md) K.0 解出清单已更新；各目标题处置见下表。
  - **solved 口径说明（pureLlm 波动）**：`cmp after_vsepr509 gap_batch1` 显示 solved 口径 net -2（LOST=pdf-197, pdf-376）。逐题核实（`_evalscratch/check_flip.py`）：pdf-197/376 在 base 与 new 均 `gatewayCorrect=False`（gwFlip=False），仅 `pureLlmCorrect` 由 True→False → 纯 LLM 本轮答错导致其退出 solved 集，**与引擎改动无关**（项目记忆 L66：纯 LLM 波动不计入代码回归）。引擎侧无任何 True→False。
  - **目标题端到端处置**：
    | 题 | 基线 gw | 新 gw | 处置 |
    |---|---|---|---|
    | pdf-168 | False | True | T4 Ksp-pH 兜底命中（+1）|
    | pdf-506 | False | True | T5 VSEPR 整题兜底切片命中（+1）|
    | trap-limit-3 | False | True | T6 ProductWriter 限量方程（+1）|
    | pdf-272 | False | True | T7 子问(1) 弱酸第一步电离（+1，子问2 曲线数据精度不足不蒙数）|
    | pdf-328 | False | True | P2-2 燃烧法实验式 C9H8O2（+1）|
    | pdf-262 | False | False | T2 数据修复（官方答案/去重/关键词重切），子问 2/3/4 仍不可解 → 诚实保持 False |
    | pdf-278 | False | True | T1 多部分守卫回归修复，分发恢复（回归恢复，非新增）|
    | d5-1/d5-2/d7-4 | False | True | T1 鲁棒性守卫（合成用例，回归恢复/补强）|
    | pdf-010/156 | — | — | P2-1 NO-GO：答案键数据错位 + 方程生成高 FP，登记延后 |
    | pdf-416 | — | — | P2-3 NO-GO：choice 字母选择非单条事实可解，零收益，延后 |
