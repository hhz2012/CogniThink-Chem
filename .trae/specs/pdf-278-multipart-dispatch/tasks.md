# pdf-278 多部分大题分发与判正 - Implementation Plan

> 派生自 `spec.md`。每个任务映射到 AC；TR 仅 `rule` / `rubric`。依赖序：T1 → {T3, T4} → T5 → T6。T3 与 T4 互不依赖、不写同一文件，可并发。

## Task 1: 多部分占位静默 — 打通分发接管链路
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 在 `TryJudgeIonizationShift`（[LlmGatewayService.cs:2263](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L2263)）入口加 `MultiPartUtility.IsMultiPart(raw)` 守卫：多部分题干返回 null，使整题 `Ok!=true` → 分发器接管。
  - 定位 curve 读数占位（pdf-272 的 provenance=curve_reading）的产出函数，对多部分题干同样静默。
  - 非多部分题行为完全不变。
  - 打通后验证：pdf-278 整题 null → `TryDispatchMultiPartAsync` 激活 → 拆出 5 子问 → 因子问(5)未解整体返回 null（不蒙数、不吐占位）。
- **Acceptance Criteria Addressed**: AC-4
- **Test Requirements**:
  - `rule` TR-1.1: 单元测试断言 pdf-278 整题经改动后 `CallEngineAsync` 主结论 provenance 不再是 equilibrium 占位（`gatewayResultProvenance` 非 equilibrium 单部分占位，或整题 `Ok!=true`）；同时断言非多部分单题"醋酸加水稀释，电离程度如何变化"仍返回原占位结论（零回归）。证据：`tests/.../MultiPartPlaceholderGuardTests.cs` 通过。
  - `rule` TR-1.2: 合成多部分 curve 题（含 ≥2 个子问标记 + 曲线读数触发文本）断言 curve 占位静默；非多部分 curve 题不变。证据：同测试文件通过。
  - `rule` TR-1.3: 探针断言 pdf-278 整题改动后分发器被激活、5 子问被拆分（`MultiPartUtility.SplitSubQuestions` 返回 5 段）且子问(5)未解时整体 null。证据：`_evalscratch/probe_pdf278_after_t1.txt`。
- **Notes**: 探针脚本放 `_evalscratch/`，临时 `.cs` 探针跑完删除；Release 构建/测试（避免 Debug DLL 被运行中 API 锁定）。
- **Completion Evidence**:
  - 代码：[LlmGatewayService.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) 三处 `IsMultiPart` 守卫——`TryJudgeIonizationShift`（电离移动占位，L2267）、`TryOrderAcidStrength`（酸性排序占位，L2133）、`TryRouteCurve`（曲线读数占位，L459）。均为多部分整题返回 null / 不采纳，非多部分行为不变。
  - 零回归扫描：`_evalscratch/verify_acidity_mp.py` 确认全库 58 道多部分题中，27 道已解对题无任何一题靠酸性排序/电离移动占位判正（静默安全）。
  - 单元测试：[MultiPartPlaceholderGuardTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/MultiPartPlaceholderGuardTests.cs) 5 例全过（TR-1.1 pdf-278 占位静默 + 非多部分保留；TR-1.2 多部分 curve 静默 + 非多部分保留）；`CurveGatewayTests` 5 例全过（curve 零回归）。
  - 探针：`_evalscratch/probe_t1.txt`（pdf-278 整题 `Ok=null` → 分发器激活条件满足）、`_evalscratch/probe_pdf278_after_t1.txt`（5 子问正确拆分，子问(1)-(4) 已解对，子问(5) 当前被 `TryJudgeIonizationShift` 抢占返回占位 → 聚合后 AND 不命中 → 诚实未解、不蒙数）。临时探针 `.cs` 已删除。

## Task 3: 新增子问(5)求解能力（浓度表示式 + 电离程度多选）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None（与 T4 并行；最终集成在 T5）
- **Description**:
  - 新建求解器（建议 `WeakAcidIonizationSolver` 或同类命名，置于 `Application/Gateway/`），泛化处理：
    - ① 一元弱酸（或弱碱）稀溶液中，由电荷守恒 `c(H⁺) = c(A⁻) + c(OH⁻)` 得 `c(A⁻) = c(H⁺) - c(OH⁻)`；从子问文本提取 pH → 输出 `10^-pH - 10^(pH-14)` 形式的精确表示式（对 pdf-278 → `10^-5 - 10^-9`）。
    - ② 使弱电解质电离程度（电离度 α）增大的操作多选：升温（电离吸热）、加水稀释 → 选；加含共同离子的强电解质（同离子效应）、增大弱电解质自身浓度 → 不选。对 pdf-278 → BD。
  - 缺 pH / 缺酸根身份 / 非弱电解质 / 选项信息不足 → 返回 null（不蒙数）。
  - 接入 `CallEngineAsync` 路由（分发器逐子问调用时可达；taskType 由 `ClassifyTaskDeterministic` 或子问文本特征决定，需确保子问(5)能命中该求解器）。
- **Acceptance Criteria Addressed**: AC-6, AC-7
- **Test Requirements**:
  - `rule` TR-3.1: 浓度正例 — pdf-278 子问(5)浓度部分结论命中 `10^-5 - 10^-9`；变体 pH=4 稀 HF → `10^-4 - 10^-10` 正确。证据：单元测试。
  - `rule` TR-3.2: 浓度反例 — 无 pH、无酸根身份、非弱电解质输入 → 返回 null。证据：单元测试。
  - `rule` TR-3.3: 选择正例 — pdf-278 子问(5)选择部分产出 BD（或结论含独立 token B、D）；对"加 HCl 抑制/加冰醋酸 α 减小"的不选逻辑正确。证据：单元测试。
  - `rule` TR-3.4: 不蒙数 — 选择信息不足（如无选项）时该部分返回 null 而非乱猜字母。证据：单元测试。
  - `rubric` TR-3.5: 泛化性（AC-7）；scale 1-5；anchors 1=硬编码单题，3=仅 pdf-278 正确但变体偶错，5=规则清晰、变体全对且反例全 null；threshold >= 4；evidence：≥2 同类变体（pH=4 稀 HF、pH=6 稀 NH4⁺ 水解求 c(NH3·H2O) 或对应）正确 + ≥2 反例 null。
- **Notes**: 实现方式（独立规则 vs 扩展现有 concept-facts/choice 域）在实现阶段选定，以"泛化 + 不蒙数 + 不硬编码单题"为准。不得在 pdf-278 上特判。
- **Completion Evidence**:
  - 代码：新建 [LlmGatewayService.WeakAcidIonization.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.WeakAcidIonization.cs)，`TrySolveWeakAcidIonizationExpression` 由电荷守恒产出 `c(A⁻)=10^-pH - 10^(pH-14)` 并判定使电离度增大的选项（升温/稀释选，加共同离子强电解质/增大自身浓度不选）；含多部分整题守卫（只在拆出的单子问上生效，避免整题被单条子问答案抢占阻塞分发）。接入 `CallEquilibrium` 且先于 `TryJudgeIonizationShift`，域外一律 null（不蒙数、不输出部分答案）。
  - 单元测试：[WeakAcidIonizationSolverTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/WeakAcidIonizationSolverTests.cs) 7 例全过（TR-3.1 pdf-278 子问5 + pH=4 HF + pH=3 HCN 两变体浓度与 BD 均正确；TR-3.2 无 pH / 无 c(离子) / 非弱电解质三反例不产出浓度表达式；TR-3.4 无选项不蒙数）。
  - 端到端：pdf-278 聚合结论子问(5) = `c(CH3COO⁻) = 10^-5 - 10^-9 BD`（见 T5 聚焦 eval checkpoint）。

## Task 4: multi_part AND 判分支持方程两侧物质顺序无关
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None（与 T3 并行）
- **Description**:
  - 在 `GatewayVsLlmEvaluationService` 的 multi_part `AllKeywordsHit` 路径（[line ~402](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L402)）扩展：当关键词经归一化后呈现"化学方程式"形态（含 `=`，两侧为 `+` 连接的物质），则对结论做集合等价比较：两侧物质多重集（含系数）分别相等即命中，与物质书写顺序无关。
  - 仅作用于 multi_part AND 路径；**不**改 `IsAnswerCorrect` 的 OR 路径与非 multi_part 题。
  - 系数必须一致（`2H2+O2=2H2O` 不匹配 `H2+O2=H2O`）；缺物质不命中。
- **Acceptance Criteria Addressed**: AC-5
- **Test Requirements**:
  - `rule` TR-4.1: 正例 — 聚合结论 `CN- + CO2 + H2O → HCN + HCO3-` 对关键词 `CN- + CO2 + H2O = HCO3- + HCN` 命中（产物顺序互换）；反应物顺序互换也命中。证据：单元测试。
  - `rule` TR-4.2: 反例（precision 守卫）— 缺物质（`CN- + CO2 = HCO3- + HCN` 缺 H2O）、系数错（`2CN- + ... = ...`）→ 不命中。证据：单元测试。
  - `rule` TR-4.3: 非 multi_part 的 `IsAnswerCorrect` OR 路径行为不变 — 现有方程判分相关测试全绿，无新增命中也无丢失。证据：现有测试套件绿。
- **Notes**: 实现为"现有归一化子串匹配失败后，额外尝试方程集合等价"，确保对已匹配的题不产生任何改变（LOSS=0 的同态保证），只新增本应等价却漏判的命中。
- **Completion Evidence**:
  - 代码：[GatewayVsLlmEvaluationService.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs) `AllKeywordsHit` 在归一化子串匹配失败后，对方程形态关键词（含 `=`）额外尝试 `EquationSetEquivalent`（两侧物质多重集含系数分别相等，侧内顺序无关、侧间不可互换）；匹配前剥离结论中的纯数字子问编号 `\(\d+\)` 防止 `(3)` 污染物种解析。仅 multi_part AND 路径，现有匹配不变（LOSS=0 同态，precision 由系数/物质必须完全一致兜底）。
  - 单元测试：[MultiPartAndScoringTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/MultiPartAndScoringTests.cs) 追加 7 例全过（TR-4.1 产物/反应物顺序互换命中；TR-4.2 缺物质 / 系数错 / 方向反不命中；TR-4.3 非方程关键词不变 + OR 路径不受影响）。
  - 探针：`_evalscratch/probe_judge.txt` 确认 pdf-278 聚合结论 5 关键词全部命中（含方程子问2），`ALL AllKeywordsHit=True`。

## Task 5: 集成聚合 + 聚焦 eval 验证 pdf-278 判正
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1, Task 3, Task 4
- **Description**:
  - 集成 T1/T3/T4：确认 pdf-278 整题 → 分发 → 5 子问全解 → 聚合 → AND 判分全命中 → 采纳。
  - 聚焦 eval：复制 `checkpoint_after_vsepr509.jsonl` → `checkpoint_pdf278_mp.jsonl`，仅剥离 pdf-278 行，`--tag pdf278_mp` 重跑，`cmp after_vsepr509 pdf278_mp`。
- **Acceptance Criteria Addressed**: AC-1
- **Test Requirements**:
  - `rule` TR-5.1: 聚焦 eval 结果：pdf-278 `gatewayCorrect=True`、`gatewayUsedEngine=True`、`gatewayTruthAdopted=True`、`provenance=multi_part`；cmp 显示 pdf-278 `GAINED`、`LOST=[]`、`harm=0`、`prec=1`。证据：`_evalscratch/cmp` 输出 + checkpoint 记录。
- **Notes**: 若聚焦 eval 未达 AC-1，回到对应任务（T3/T4）修复，不得蒙混通过。
- **Completion Evidence**:
  - 聚焦 eval：`--tag pdf278_mp` 重跑 pdf-278，[snapshot_pdf278_mp.json](file:///D:/github/CogniThink-Chem/_evalscratch/snapshot_pdf278_mp.json)。结果：`taskType=multi_part`、`gatewayCorrect=True`、`gatewayUsedEngine=True`、`gatewayTruthAdopted=True`；聚合结论 5 子问齐全（子问5 = `c(CH3COO⁻) = 10^-5 - 10^-9 BD`）。`cmp after_vsepr509 pdf278_mp`：`gwTruth 645→646`、`LOST=[]`、`harm=0`、`precision=1`。
  - 注：cmp 的 `GAINED=0` 因 pdf-278 此前 pureLlm 已答对（在 solved 集合内），本次提升是网关引擎从错变对（gwTruth +1），正是 G1「pdf-278 引擎解出」目标。

## Task 6: 全量 757 回归验证 + 27 题守卫
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 5
- **Description**:
  - 新增回归守卫测试：锁定全库 27 道已解对多部分题集合（来自 `scan_mp_fired.py` 输出），断言改动后它们 `gatewayCorrect` 保持 True（用基线 checkpoint 比对）。
  - 全量 eval `--tag pdf278_full`，`cmp after_vsepr509 pdf278_full`。
- **Acceptance Criteria Addressed**: AC-2, AC-3
- **Test Requirements**:
  - `rule` TR-6.1: 全量 cmp：`LOST=[]`、`harm=0`、`precision=1`。证据：cmp 输出。
  - `rule` TR-6.2: 27 道已解对多部分题全保持 True（0 回归），回归守卫测试通过。证据：测试输出 + 比对脚本 `_evalscratch/verify_mp27.py` 输出。
- **Notes**: 全量 eval 仅在 T1-T5 全部完成且聚焦 eval 通过后才跑（遵守"禁止先跑全量回归"）。
- **Completion Evidence**:
  - 全量 eval：`--tag pdf278_full` 强制重跑全部 757 题（空 checkpoint），`completed=757 / failed=0`，[snapshot_pdf278_full.json](file:///D:/github/CogniThink-Chem/_evalscratch/snapshot_pdf278_full.json)。
  - 全量 cmp：`python _evalscratch/cmp.py after_vsepr509 pdf278_full` → `gwTruth 645→649`、`harm=0`、`precision=1`。
  - **引擎零回归（红线核心）**：[_evalscratch/diff_engine.py](file:///D:/github/CogniThink-Chem/_evalscratch/diff_engine.py) 逐题对比两 checkpoint 引擎字段 → `ENGINE LOST (gwCorr True→False) = 0`、`truthAdopted True→False = 0`、`harm (adopted∧¬correct) = 0`。本次代码改动未让任何一道引擎答对题回退。
  - **27 题多部分守卫**：[_evalscratch/verify_mp27.py](file:///D:/github/CogniThink-Chem/_evalscratch/verify_mp27.py) → `RESULT: PASS (27/27 multi-part solved cases retained gatewayCorrect=True)`，0 回归。
  - 真实引擎增益 4：`pdf-278`（本次 T1/T3/T4：占位→多部分分发全解）；`d5-1 / d5-2 / d7-4` 为本次全量重跑中 LLM 分类波动触发**已有**兜底能力（非本次代码改动，净增益、零回归）。
  - solved 口径 `LOST(3)=[pdf-197, pdf-355, pdf-519]` 说明：经 [_evalscratch/diff_lost.py](file:///D:/github/CogniThink-Chem/_evalscratch/diff_lost.py) 逐题核实，该 3 题的引擎字段（`gwCorr/usedEng/truthAdopt/taskType`）在 baseline 与 new **完全一致**（均为 False/False/False），唯一变化是 `pureLlmCorr True→False`，属全量重跑 LLM 非确定性（项目记忆 L66：LLM 存在随机漏检、不可作真值源），**非代码回归**；引擎真值源层面零回退。

---

## 实现顺序与并发
1. T1（占位静默）先落地，打通分发链路。
2. T3（子问5求解器）与 T4（判分扩展）**可并发**（独立文件 / 独立模块），但均有 precision 红线，实现后各自跑单元测试。
3. T5 集成 + 聚焦 eval。
4. T6 全量回归 + 守卫。
