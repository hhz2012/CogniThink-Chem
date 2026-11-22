# Tasks — P2-2 世界模型与机理层最小可用版（趋势级解释链）

> 关联 spec：[spec.md](file:///d:/github/CogniThink-Chem/.trae/specs/world-model-minimal/spec.md)
> 基线：`after_p21`（757 题，635 正确，precision=1，harm=0，LOST=0，WORSEN=0）。红线锚点 `base_p0`（629 正确）。
> 红线：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 事实只存 JSON。破线即回退。
> 诚实口径：预期 eval 正确性增益 = 0（解释卫生）；解释标 EvidenceLevel=Trend、永不进采纳门，不改任何 verdict。

## Task Breakdown

| ID | 任务 | 状态 | 产出 |
|----|------|------|------|
| T0 | 只读审计：Core 世界模型现状 / 3 链 JSON 事实齐备性 / 网关解释字段边界 / 候选题 verdict | completed（spec 阶段已实证） | 证据入 spec §2 |
| T1 | Core 世界模型：新建 3 个趋势/机理最小模型（纯函数、定性无绝对值、读已有 JSON、带 RuleId/KnowledgeId）+ 单测 | completed | [WorldModel.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/WorldModel/WorldModel.cs)；Core build 0 error/0 warning |
| T2 | 契约 + 网关富化：`EngineResponse` 加性 `ExplanationNodes` + `ProcessStudentMessageAsync` 一处只读富化点（不改判定字段） | completed | 加性字段 + `WorldModelEnricher` 富化点；build 0 error |
| T3 | 3 条示范链接通 + 验证：各 ≥1 真实题 stem，断言 ExplanationNodes 非空/内容对应/EvidenceLevel=Trend/判定字段不变 | completed | [WorldModelExplanationTests.cs](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/WorldModelExplanationTests.cs) 6/6 PASS；全量 2212 passed/0 failed |
| T4 | 全量 eval + 红线 diff：相对 after_p21 GAINED/LOST/WORSEN/harm 全 0；correct=635；不蒙数；解释不泄漏判分 | completed | `after_p22`（757/757 failed=0）；两次 diff RED-LINE PASS；[p22_diag.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p22_diag.py) + [p22_chain_probe.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p22_chain_probe.py) PASS |
| T5 | 台账闭环：登记 P2-2 触及范围 + 新增 Core 模型清单 + 3 链事实溯源 | completed | [data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md) 新增 §O（O.1/O.2/O.3） |
| T6 | 红线自检：密钥扫描 / 未改 JSON 数据 / 未改采纳门 / 解释不泄漏判分 / 不蒙数 / 表面积 | completed | [p22_self_check.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p22_self_check.py) 11/11 PASS |
| T7 | fresh-context 只读子代理独立 review → review.md，裁决 PASS/FAIL | completed | [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/world-model-minimal/review.md)（PASS，7/7，0 Issue） |

---

## T0 — 只读审计（spec 阶段已完成）
**Status**: completed — 证据入 spec §2（Core 只有 PeriodicTable 绝对值模型、加载器就绪；3 链 JSON 事实齐备；网关无解释输出且新字段 gate-safe；候选题多数已判正、加解释不改 verdict）。

## T1 — Core 世界模型（3 个最小模型）
**Status**: completed — 新建 [WorldModel.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/WorldModel/WorldModel.cs)：`TrendExplanationNode` 类型 + 静态 `WorldModel.Explain(text, finalResult)`，3 链（M1 金属性/还原性递变 · M2 沉淀-配位溶解 · M3 盐类水解酸碱性）纯函数；进程内 `Lazy<Snapshot>` 缓存加载 activity-series / rules / reactions；节点内容取自 `rule.Conclusion.Display` / `reaction.Equation`，无化学硬编码；每节点附「帮助理解，不作判定依据」；无命中返回空（不蒙数）。Core build 0 error/0 warning。
**Outputs**: 新建 `src/CogniThink.Chem.Core/WorldModel/`：
- M1 金属性/还原性递变比较（读 activity-series.json + rules.json 趋势规则 + KH-ST-02）
- M2 沉淀-配位溶解机理（读 rules.json AgCl 配位溶解规则 + RX-PL-PPT-001）
- M3 盐类水解酸碱性（读 RX-PL-HYD-003 + rules.json 水解规则）
- 统一入口 `WorldModel.Explain(questionText, finalResult, species) → List<ReasoningNode>`（无命中返回空）
**Constraints**: 纯函数；定性无绝对值（不输出电离能/Ksp/Kh/pH/浓度数值）；命中节点带非空 RuleId/KnowledgeId；EvidenceLevel=Trend；不硬编码化学事实（只读 JSON）。
**Verification**: build 0 error；单测覆盖 3 模型命中/无命中/定性内容。

## T2 — 契约 + 网关富化点
**Status**: completed — [GatewayContracts.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/GatewayContracts.cs) `EngineResponse` 加性新增 `ExplanationNodes: List<ReasoningNode>`（默认 `new()`，JSON key `explanation_nodes`，置于 `ReasoningNodes` 后，旧字段不动）；[ReasoningContracts.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/ReasoningContracts.cs) 新增 `WorldModelEnricher.Enrich`（映射 Core `TrendExplanationNode` → Api `ReasoningNode`，`EvidenceLevel=Trend`，仅写 `resp.ExplanationNodes`，不改 Ok/FinalResult/TaskType）；[LlmGatewayService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) 在 `ConclusionStateDeriver.Derive` 之后插入调用。全量 build 0 error；7 警告全为既有（P2-2 文件 0 警告）。
**Outputs**:
- [GatewayContracts.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/GatewayContracts.cs) `EngineResponse` **加性**新增 `ExplanationNodes: List<ReasoningNode>`（默认空；JSON 键 `explanation_nodes`）。不删改既有字段。
- [LlmGatewayService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) `ProcessStudentMessageAsync` 内、`ConclusionStateDeriver` 之后，调用 `WorldModel.Explain` 富化 `ExplanationNodes`。**只读**：不写回 Ok/FinalResult/TaskType，不调采纳门。
**Verification**: build 0 error；单测断言富化前后 Ok/FinalResult/TaskType 逐 case 不变；JSON 既有键不变（wire 兼容）。
**Notes**: 严禁修改 `BuildGatewayAnswerAsync`（CQ-5，AC-8）。

## T3 — 3 条示范链接通 + 验证
**Status**: completed — 新建 [WorldModelExplanationTests.cs](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/WorldModelExplanationTests.cs) 6 测试全绿：M1 周期递变（STR-001/002，2 节点）、M1 活动性序（pdf-176 真题干，RED-001）、M2 AgCl/氨水（pdf-213 真题干，SOL-EXC-001）、M3 碳酸根水解（pdf-277 真题干，RX-PL-HYD-003 +「不涉及 pH 数值」）、无命中不蒙数（空）、富化不改 Ok/FinalResult/ReasoningNodes。全量测试 2212 passed / 0 failed / 0 skipped。
**Outputs**: 新建 `WorldModelExplanationTests`：3 条链各取 ≥1 真实题 stem（取自 probe_p22_cases.py 候选，如金属性比较题 / AgCl-氨水瓶 / Na₂CO₃ 碱性题），断言：
- `ExplanationNodes` 非空；节点 EvidenceLevel=Trend；命中节点 RuleId/KnowledgeId 非空。
- 内容与对应 JSON 事实一致（定性、无绝对值）。
- 富化后 `FinalResult`/`Ok`/`TaskType` 与富化前**完全一致**（不翻转 verdict）。
- 无命中题干不产生解释节点（留空，不蒙数）。
**Verification**: 相关单测全绿；build/test 0 error/0 failed。

## T4 — eval + 红线 diff
**Status**: completed — `after_p22`（Production LLM off）757/757 completed、failed=0，correct=635 / precision=1 / harm=0。`diff_ckpt after_p21 after_p22`：GAINED=0 / LOST=0 / WORSEN=0 / harm=0 → RED-LINE PASS（verdict 不变）。`diff_ckpt base_p0 after_p22`：GAINED=6（P1-4 携带）/ LOST=0 / WORSEN=0 / harm=0 → PASS。诊断 [p22_diag.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p22_diag.py)：全局 `adopted∧¬correct=0`；无 Insufficient 被采纳；`explanationNodes>0 ∧ adopted → 全部 correct`；11/757 题命中解释链（Confirmed=8 全 adopted+correct，Insufficient=3 未采纳）。链触发镜像 [p22_chain_probe.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p22_chain_probe.py)：Python 预测节点数与 C# 实际 0 mismatch。
**Outputs**:
- `run_eval_resume.ps1 -Tag after_p22 -Env Production` → 757/757 failed=0。
- `diff_ckpt after_p21 after_p22`：GAINED=0 / LOST=0 / WORSEN=0 / harm=0 → RED-LINE PASS。
- `diff_ckpt base_p0 after_p22`：LOST=0 / WORSEN=0 / harm=0（GAINED 维持 6 携带）。
- 诊断断言：`adopted∧¬correct=0`（AC-6）；`ExplanationNodes` 非空的 case 中 adopted→correct 全部成立；原 Insufficient case 无一被误采纳；correct=635 不变（诚实增益 0）。
**Verification**: 上述全过。若 LOST≠0 或 harm≠0 → 停下根因（必是 T2 富化意外改了判定字段或采纳门误读），修后复跑，不得带差异收口。

## T5 — 台账闭环
**Status**: completed — [data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md) 新增 §O（插入于 §N 与 `## 处理原则` 之间）：O.1 触及文件清单（7 文件，content attestation，无 `data/**`）；O.2 三链事实来源（全部复用既有 JSON，零数据改动）；O.3 11 题命中清单（按链/节点数/conclusionState/adopted/correct）+ verdict-unchanged 实证 + 不蒙数实证。
**Outputs**: [data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md) 新增 §O（P2-2 世界模型）：
- O.1 本 spec 触及文件清单（content attestation）。
- O.2 Core 新增世界模型清单（3 模型 + 入口）+ 各自事实溯源 JSON 条目。
- O.3 3 条示范链与候选题映射 + verdict 不变声明。

## T6 — 红线自检
**Status**: completed — [p22_self_check.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p22_self_check.py) 11/11 PASS：密钥扫描 0 命中；未改 data/ JSON；`ExplanationNodes` 在 Evaluation 服务仅 2 次（字段 L86 + 赋值 L204）且均在 `BuildGatewayAnswerAsync` 定义（L668）之前（采纳门内 0 引用）；checkpoint 逐 case `gatewayAnswer`/`gatewayOk`/`gatewayConclusion` 与 `after_p21` diff=0；`adopted∧¬correct=0`；correct=635；`EngineResponse` 保留 `DerivationSteps`/`ReasoningNodes`/`ConclusionState` + 新增 `ExplanationNodes`（契约加性）。
**Verification**（脚本化，结果入 review 证据）：
- 密钥扫描：P2-2 改动文件 grep `sk-|api_key|Bearer|secret` → 0 命中。
- 未改数据：P2-2 触及文件均不在 `data/` 下（CQ-4 零数据改动）。
- 未改采纳门：`ExplanationNodes` 在 `GatewayVsLlmEvaluationService.cs` 中 0 引用（AC-8）；`BuildGatewayAnswerAsync` 方法体未变。
- 解释不泄漏判分：checkpoint 逐 case `gatewayAnswer`/`gatewayOk`/`gatewayConclusion` 与 after_p21 完全一致（diff=0）。
- 不蒙数：`after_p22` 全局 `adopted∧¬correct=0`。
- 表面积：仅 Core 新模型 + 加性字段 + 富化点 + 测试 + 台账 + scratch，无其它源码改动。
- 契约加性：`EngineResponse` 保留 `DerivationSteps`/`ReasoningNodes` + 新增 `ExplanationNodes`。

## T7 — 独立 review
**Status**: completed — fresh-context 只读子代理独立复跑 build（0 error/0 warning）/ test（2212 passed/0 failed）/ 两次红线 diff（全 PASS）/ p22_self_check（14/14 PASS）/ 采纳门 ExplanationNodes 0 引用核对 / 3 链 JSON 事实逐条核对 / 台账 §O 自洽核对 → 裁决 **PASS（7/7，0 Issue，无修复项）**。报告落盘 [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/world-model-minimal/review.md)。
**Process**: 委托 fresh-context **只读** 子代理，独立复跑：build / test / eval diff（两次）/ 采纳门逐 case 不变断言 / 3 链解释抽样核对 JSON 事实 / 密钥扫描 / 台账核对。裁决维度：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 未改数据与评估判分 / 解释不泄漏判分 / 定性无绝对值不与教材矛盾。任一 FAIL → Issue → 修复 → 复审。子代理仅返回报告文本，由主代理落盘 [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/world-model-minimal/review.md)。

---

## 决策日志（CQ）

| ID | 问题 | 选项 | 决策 | 理由 |
|----|------|------|------|------|
| CQ-1 | 世界模型落点 | A Core（推荐）/ B 网关 | **A（已批准）** | 见 spec §4 CQ-1 |
| CQ-2 | 解释字段暴露方式 | a ExplanationNodes 列表（推荐）/ b 字符串 / c 混入 | **a（已批准）** | 见 spec §4 CQ-2 |
| CQ-3 | 3 链范围与定性边界 | 限定 3 链 + 定性无绝对值 | **已批准** | 见 spec §4 CQ-3 |
| CQ-4 | 数据 | 复用现有 JSON，零数据改动（推荐） | **已批准** | 见 spec §4 CQ-4；审计已证实事实齐备 |
| CQ-5 | 是否改判分/采纳门 | 不动（定死） | **不动** | 改则破红线；解释为观测性元数据 |
| OQ-5 | checkpoint 诊断字段 | 加 ExplanationNodes 计数/存在性（不计分） | **已批准** | 只读诊断，不影响判分；见 spec §10 |

## Review History

| 轮次 | 日期 | 审查者 | 结论 | 关键问题与修复 |
|------|------|--------|------|----------------|
| 1 | 2026-11-22 | fresh-context 只读子代理 | PASS（7/7，0 Issue） | 无修复项；见 [review.md](file:///d:/github/CogniThink-Chem/.trae/specs/world-model-minimal/review.md) |
