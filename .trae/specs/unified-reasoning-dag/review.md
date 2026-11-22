# P2-1 统一推理 DAG 与结论三态契约 — 独立只读 Review

> **审查模式**：严格只读（未改/创建/删除任何文件）。
> **最终交付 checkpoint**：`after_p21`。
> **审查者**：fresh-context 只读子代理（general_purpose_task，仅返回报告文本，由主代理落盘）。
> **总体裁决**：**PASS**。9 个裁决维度全部通过，0 Issue，无修复项。

---

## 1. 复跑证据

1. **build**：`dotnet build CogniThink.Chem.sln -c Release --nologo` → exit 0；`已成功生成。0 个警告 / 0 个错误`。**PASS**。
2. **test**：`dotnet test ... --no-build`（全量）→ exit 0；`CogniThink.TutorBrain.Tests 失败:0 通过:1293` + `CogniThink.Chem.Tests 失败:0 通过:913` = **合计 2206 passed / 0 failed**。过滤两个 P2-1 测试类：`失败:0 通过:15`（ReasoningContractDeriverTests 10 + ReasoningSolverTraceabilityTests 5）。**PASS**。
3. **`p21_verify_after.py after_p21`** → `T4 VERDICT: PASS (correct=635==635, adopted∧¬correct=0, derive_mismatch=0)`；Ok 分布 `true=615 false=26 null=116`；conclusionState 分布 `Confirmed=641, CandidateSet=0, Insufficient=116, MISSING=0`。**PASS**。
4. **diff `after_p15b → after_p21`**：`GAINED(0) LOST(0) WORSEN(0) IMPROVE(0) harm_old=0 harm_new=0` → `RED-LINE ... PASS`。**PASS**。
5. **diff `base_p0 → after_p21`**：`GAINED(6): [pdf-415, 530, 535, 542, 543, trap-limit-2] LOST(0) WORSEN(0) harm_new=0` → `RED-LINE ... PASS`（GAINED=6 为 P1-4 携带，非 P2-1 引入）。**PASS**。
6. **`p21_self_check.py`** → 11 项全 `[PASS]`，`T6 SELF-CHECK VERDICT: PASS (0 failures)`。**PASS**。

## 2. 代码审查（独立 Grep/Read 核对）

- **(a) 采纳门未改**：`ConclusionState` 在 `GatewayVsLlmEvaluationService.cs` 仅出现 **2 次**（L83 字段定义、L200 `EvaluateCaseAsync` 赋值）；`BuildGatewayAnswerAsync` 定义在 **L664**，两处均在其之前 → 方法体内无引用。checkpoint 逐 case 比对 `after_p15b` vs `after_p21`：`gatewayAnswer` / `gatewayOk` / `gatewayConclusion` 三者 diff 均 = **0**（采纳门输入输出完全不变）。
- **(b) 示范链可溯源**：4 个示范 solver 命中路径源码抽读均填 `ReasoningNodes`，带非空 `KnowledgeId`/`RuleId`，Step 连续，末节点 `Conclusion==FinalResult`：
  - ConceptFactSolver L51-62（KnowledgeId=match.Id，Conclusion=match.Answer==FinalResult@L49）
  - ReactionKbWriterSolver L107-119（RuleId/KnowledgeId，Conclusion=prefix+equation==FinalResult@L105）
  - AtomicStructureSolver L89-109（Step1/2 连续，KnowledgeId="periodic_table" + RuleId="rule:aufbau_hund_pauli"，末 Conclusion=ans==FinalResult@L83）
  - VseprSolver L207-218 与 L290-301（RuleId / KnowledgeId="vsepr_table"，Conclusion==FinalResult）
  - 无命中路径均 `return null`（R4 守卫，不蒙数）。
- **(c) 未改数据**：台账 N.1 触及清单 = 新增 1 + 加性修改 8 + 测试 2，均为 `.cs`，无 `data/` 条目；self_check `data_touched=[]`。（自 P0 未提交，按约定用 content attestation，非 git diff。）
- **(d) 派生只读**：`ConclusionStateDeriver.Derive`（ReasoningContracts.cs L88-104）只读 `resp.ConclusionState/Ok/FinalResult` 并返回值，**不写回** `Ok/FinalResult/TaskType`；唯一调用点 LlmGatewayService.cs L123 仅赋值 `engineResponse.ConclusionState`。
- **契约加性**：`GatewayContracts.cs` 保留 `DerivationSteps`（旧，L121）+ 新增 `ConclusionState`（默认 Insufficient）/ `ReasoningNodes`（默认空）；`ReasoningContractDeriverTests` JSON 测试断言旧键保留、新键出现 → 不破坏 wire。

## 3. 台账核对

- **N.2 待迁移 13 生产者**：`Grep "DerivationSteps ="` 在 src 命中 17 文件，扣除 4 示范 solver = 13，与清单逐文件吻合（RedoxGateway 5 处、KnownEquation 2 处，其余各 1，数字一致）。
- **N.3 映射表**：Confirmed=641（=615+26）、CandidateSet=0、Insufficient=116、合计 757，与 `p21_verify_after` 输出逐字对应。

## 4. 红线维度表

| # | 维度 | 结果 | 关键证据 |
|---|------|------|----------|
| 1 | precision=1 | PASS | adopted∧¬correct=0；两次 diff harm=0 |
| 2 | harm=0 | PASS | diff after_p15b→after_p21 harm_new=0；diff base_p0→after_p21 harm_new=0 |
| 3 | LOST=0 | PASS | 两次 diff LOST=0 |
| 4 | WORSEN=0 | PASS | 两次 diff WORSEN(null→new-harm)=0 |
| 5 | 不蒙数 | PASS | adopted∧¬correct=0；R4 守卫「无命中返回 null」通过；4 示范 solver 无命中路径均 return null |
| 6 | 未改数据 / 评估判分 | PASS | self_check data_touched=[]；checkpoint 逐 case gatewayAnswer/gatewayOk/gatewayConclusion diff=0；ConclusionState 仅 L83/L200，均在 BuildGatewayAnswerAsync(L664) 之前 |
| 7 | 契约加性不破坏 wire | PASS | EngineResponse 保留 DerivationSteps + 新增 ReasoningNodes/ConclusionState（默认值）；JSON 测试断言旧键保留 |
| 8 | 示范链可溯源 | PASS | 4 示范 solver 命中路径均带非空 KnowledgeId/RuleId、Step 连续、末节点 Conclusion==FinalResult；TraceabilityTests 5/5 通过 |
| 9 | CandidateSet==0 诚实 | PASS | p21_verify CandidateSet=0；台账 N.3=0；当前网关无候选生产路径（契约占位，符合 CQ-4） |

## 5. 非阻塞项

无。无 Issue，无修复项。

## 6. 诚实披露

正确性证据为对已持久化 checkpoint（`after_p15b` / `after_p21` / `base_p0`）的读侧校验 + 当前源码的 build/test；未重新触发 `run_eval_resume.ps1` 生成新 checkpoint（与只读委托授权一致）。checkpoint 文件 `_evalscratch\checkpoint_after_p21.jsonl` 存在且 757 行完整。

---

**总体：PASS。无必须修复的 Issue。**
