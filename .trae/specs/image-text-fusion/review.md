# P2-3 图像与文本求解融合 — 独立只读 Review

> **审查模式**：严格只读（未改/创建/删除任何仓库源码或数据文件；未提交 git；临时日志已回收，工作区净变化 0）。
> **最终交付 checkpoint**：`after_p23`。
> **审查者**：fresh-context 只读子代理（general_purpose_task，仅返回报告文本，由主代理落盘）。
> **审查日期**：2026-11-22。
> **总体裁决**：**PASS**。7 项检查全部通过，10 个裁决维度全部满足，无 FAIL、无 Issue，无修复项。
> **P2-3 性质**：零行为变更卫生项 —— 不改采纳门判决，仅加 `EngineResponse.ResultProvenance` 诊断字段 + 评估侧 `gatewayResultProvenance`（不计分）+ 回归测试。预期 eval 正确性增益 = 0（已实证）。

---

## 总体结论

**PASS** —— 全部 7 项检查通过，无 FAIL、无 Issue。10 个裁决维度全部满足：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 未改 data 与采纳门判决 / 解释不泄漏判分 / 契约加性 / 纯加性不改判决字段 / curve_reading 永不被采纳。

---

## 一、Build（检查项 #1）

- 命令：`dotnet build CogniThink.Chem.sln -c Release --nologo` → 0 错误，退出码 0。全量 7 个项目构建成功。
- P2-3 触及文件（GatewayContracts.cs / LlmGatewayService.cs / GatewayVsLlmEvaluationService.cs / EvalRunner Program.cs / ImageTextFusionRegressionTests.cs）0 警告。
- 裁决：**PASS**。

## 二、Test（检查项 #2）

- 命令：`dotnet test CogniThink.Chem.sln -c Release --nologo --no-build` → 合计 **2216 passed / 0 failed / 0 skipped**，与期望 2216 = 2212 + 4 新 完全吻合。
- 新增 4 测试显式过滤：`已通过! - 失败: 0，通过: 4`：
  1. `EngineResponse_ResultProvenance_DefaultsNull_DoesNotAffectOtherFields`（契约加性）
  2. `Pdf259_Equilibrium_TaggedProvenanceEquilibrium_AndConclusionUnchanged`（provenance 标记 + 结论不变）
  3. `Pdf272_CurveReading_TaggedProvenance_AndNeverAdopted`（curve_reading 不采纳红线守卫）
  4. `Synthetic_CurveIntermediateReading_NotAdoptedAsFinalAnswer`（合成中间量读数不采纳守卫）
- 裁决：**PASS**。

## 三、红线 diff 两次 + checkpoint 摘要（检查项 #3）

### 3a. after_p22 → after_p23（零行为变更）
`diff_ckpt after_p22 after_p23`：GAINED (0): [] / LOST (0): [] / WORSEN (0): [] / harm_old=0 harm_new=0 → `RED-LINE: PASS`。
裁决：**PASS**（零行为变更，eval 正确性增益=0 实证）。

### 3b. base_p0 → after_p23
`diff_ckpt base_p0 after_p23`：GAINED (6): [pdf-415, pdf-530, pdf-535, pdf-542, pdf-543, trap-limit-2]（P1-4 携带，非 P2-3 新增）/ LOST=0 / WORSEN=0 / harm=0 → `RED-LINE: PASS`。

### 3c. checkpoint 摘要
`checkpoint_after_p23_summary.txt`：gatewayCorrect=635 / precision=1 / harmCount=0 ✓。
裁决：**PASS**。

## 四、T6 自检复跑 + Provenance 位置 Grep（检查项 #4）

### 4a. T6 自检
`p23_self_check.py` → 23 项全 PASS（密钥扫描 0 命中；未改 data；Provenance 在 Evaluation 服务 2 次 lines=[89,209] 且均在 BuildGatewayAnswerAsync 定义 L673 之前；checkpoint 逐 case gatewayAnswer/gatewayOk/gatewayConclusion 与 after_p22 diff=0；不蒙数；curve_reading 采纳=0；correct=635；契约加性 5 字段；7 个 provenance 标签常量均在 LlmGatewayService 出现）。

### 4b. 独立 Grep 复核 Provenance 位置
`Provenance` in `GatewayVsLlmEvaluationService.cs` 仅 2 行：L89 字段声明 + L209 赋值；`BuildGatewayAnswerAsync` 定义在 L673 ⇒ 采纳门（L673–L803）内 0 引用 provenance → 不读 provenance、不影响判分。
裁决：**PASS**。

## 五、核心红线：curve_reading 永不被采纳（检查项 #5）

独立解析 `checkpoint_after_p23.jsonl`：
- **curve_reading ∧ adopted = 0** ✓（核心红线）。
- **pdf-272**：`gatewayResultProvenance=curve_reading`，`gatewayConclusion='x=2.5 时，曲线「H₂C₂O₄」的 y = 0.8'`（中间量读数），`gatewayTruthAdopted=False`、`gatewayCorrect=False` → 正确拦截，不蒙数。
- 全库 curve_reading 仅 1 条（pdf-272），未被采纳。
- 各 provenance 标签 n/correct/adopted 分布：

  | provenance | n | correct | adopted |
  |---|---|---|---|
  | concept_choice | 250 | 250 | 250 |
  | equilibrium | 13 | 13 | 13 |
  | crystal | 6 | 6 | 6 |
  | curve_choice | 6 | 6 | 6 |
  | graph | 1 | 1 | 1 |
  | molecule | 4 | 2 | 2 |
  | curve_reading | 1 | 0 | 0 |
  | null（text_fact） | 476 | 357 | 357 |
  | **合计** | **757** | **635** | **635** |

  与台账 §P.2 表逐行一致 ✓。
裁决：**PASS**。

## 六、契约加性 + 纯加性（不改判决字段）（检查项 #6）

### 6a. GatewayContracts.cs 契约加性
`EngineResponse` 同时具备：旧字段（Ok/FinalResult/DerivationSteps/ConstraintNote/Confidence/AssetKind/NumericValue* 等，未删改）+ P2-1（ConclusionState/ReasoningNodes）+ P2-2（ExplanationNodes）+ **P2-3 新增 ResultProvenance: string?**（默认 null，JSON key `result_provenance`）。

### 6b. LlmGatewayService.cs 7 个融合分支纯加性
7 个分支**仅新增** `ResultProvenance = "..."` 赋值，未改 FinalResult/Ok/TaskType/AssetKind/Confidence 等判决字段：
equilibrium（L392）/ curve_choice（L413）/ concept_choice（L437）/ graph（L448）/ curve_reading（L455）/ molecule（L462）/ crystal（L475）。

### 6c. 全仓 Grep：ResultProvenance 无判分/采纳消费
`ResultProvenance` 全仓 18 行 / 5 文件，分布：LlmGatewayService.cs（7，路由赋值）/ GatewayContracts.cs（1，契约定义）/ GatewayVsLlmEvaluationService.cs（2，字段+诊断复制）/ EvalRunner Program.cs（1，快照复制）/ ImageTextFusionRegressionTests.cs（7，测试断言）。**无任何判分或采纳逻辑（BuildGatewayAnswerAsync / IsStructuredCorrect / IsAnswerCorrect）消费**。
裁决：**PASS**。

## 七、台账自洽（检查项 #7）

`data-defect-register.md` §P：P.1 触及文件清单（5 个 .cs，无 data/**，与 `data_touched=[]` 佐证一致）；P.2 融合策略表与 jsonl 实测分布逐行一致；P.3 verdict-unchanged/不蒙数/curve_reading 采纳=0 与 diff 全 0、curve_reading∧adopted=0 完全吻合。
裁决：**PASS**。

---

## 裁决维度汇总（全 PASS）

| 维度 | 结果 | 证据 |
|---|---|---|
| precision=1 | ✓ | summary `precision=1` |
| harm=0 | ✓ | summary `harmCount=0`；diff `harm_new=0`；self_check `bad=[]` |
| LOST=0 | ✓ | 两次 diff 均 `LOST (0): []` |
| WORSEN=0 | ✓ | 两次 diff 均 `WORSEN (0): []` |
| 不蒙数 | ✓ | self_check PASS；jsonl curve_reading∧adopted=0 |
| 未改 data 与采纳门判决 | ✓ | `data_touched=[]`；diff gatewayOk/Conclusion/Answer diff=0 |
| 解释不泄漏判分 | ✓ | ResultProvenance 全仓无判分消费；Provenance 仅 2 处且在采纳门定义前 |
| 契约加性 | ✓ | 旧字段全保留 + 新增 ResultProvenance 默认 null |
| 纯加性不改判决字段 | ✓ | 7 分支仅追加 provenance 赋值 |
| curve_reading 永不被采纳 | ✓ | jsonl curve_reading∧adopted=0；pdf-272 adopted=False/correct=False |

## 未验证项 / 已知限制（不改变 PASS）

1. **git diff 纯度隔离限制**：工作区自 P0 起未提交，无法用 `git diff` 单独隔离 P2-3。已用三重证据替代并交叉验证：(a) `p23_self_check.py` content attestation + 逐 case 采纳门输入输出 diff=0；(b) `diff_ckpt after_p22→after_p23` 行为级全 0；(c) 直接 Read 7 分支源码确认仅追加 provenance 行。
2. 基线 2212 计数依赖既有基线；2216 = 1303 + 913 为本次实测，4 个新增测试已确认存在并通过，增量自洽。
3. 未运行 LLM-on 模式 eval（checkpoint `llmAvailable=False`，纯引擎真值口径），与 P2-3 零行为变更卫生项的验证目标一致。

## 最终结论

**PASS**。P2-3 符合「零行为变更卫生项」预期：eval 正确性增益=0 被实证（after_p22→after_p23 diff 全 0），provenance 诊断字段纯加性、不进采纳门、不影响判分，curve_reading 中间量读数永不被采纳，precision=1 / harm=0 红线保持，回归测试 4/4 通过，台账自洽。可结案。
