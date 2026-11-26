# pdf-278 多部分大题分发 - 独立审查（Review）

> Spec Mode Review 阶段产物。审查范围 = spec.md 的 AC-1~AC-7 + tasks.md 的全部 TR + 项目红线。审查模式：先由实现者自评填入证据，再由**独立 read-only 审查者**核对属实性、查找遗漏与风险，最终 reconcile 给出 pass/fail/blocked。

## 1. 审查材料索引

### 1.1 规范文档
- 需求：[.trae/specs/pdf-278-multipart-dispatch/spec.md](file:///D:/github/CogniThink-Chem/.trae/specs/pdf-278-multipart-dispatch/spec.md)
- 计划：[.trae/specs/pdf-278-multipart-dispatch/tasks.md](file:///D:/github/CogniThink-Chem/.trae/specs/pdf-278-multipart-dispatch/tasks.md)

### 1.2 代码改动
| 文件 | 改动 | 任务 |
|------|------|------|
| [LlmGatewayService.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) | 3 处 `IsMultiPart` 守卫（`TryJudgeIonizationShift` / `TryOrderAcidStrength` / `TryRouteCurve` 调用点）；`CallEquilibrium` 接入 `TrySolveWeakAcidIonizationExpression`（先于 `TryJudgeIonizationShift`） | T1, T3 |
| [LlmGatewayService.WeakAcidIonization.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.WeakAcidIonization.cs) | **新增**：`TrySolveWeakAcidIonizationExpression`（浓度表示式 + 电离程度多选），含整题守卫；审查加固：共轭碱身份校验（以离子反查酸）、非整数 pH 不蒙数、加强碱判电离增大 | T3 + 审查加固 |
| [LlmGatewayService.MultiPartDispatch.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.MultiPartDispatch.cs) | 聚合返回 `EngineResponse.ResultProvenance = MultiPartTaskType`（审查加固 #1，对齐 AC-1 字面） | 审查加固 |
| [GatewayVsLlmEvaluationService.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs) | `AllKeywordsHit` 对方程形态关键词加 `EquationSetEquivalent` 回退（仅 multi_part AND，Contains 失败后才试）；剥离纯数字子问编号防解析污染 | T4 |

### 1.3 测试
- [MultiPartPlaceholderGuardTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/MultiPartPlaceholderGuardTests.cs)（T1：5 例）
- [WeakAcidIonizationSolverTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/WeakAcidIonizationSolverTests.cs)（T3：7 例 + 审查加固 3 例 = 10 例）
- [MultiPartAndScoringTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/MultiPartAndScoringTests.cs)（T4：追加 7 例）
- [ImageTextFusionRegressionTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/ImageTextFusionRegressionTests.cs)（pdf-272 测试随 T1 行为更新：多部分 curve 占位静默）

### 1.4 运行证据（_evalscratch/）
- 聚焦：`snapshot_pdf278_mp.json` / `checkpoint_pdf278_mp.jsonl`；审查加固后 `checkpoint_pdf278_review.jsonl`
- 全量：`snapshot_pdf278_full.json` / `checkpoint_pdf278_full.jsonl`（757/757）；审查加固后 `snapshot_pdf278_final.json`
- 探针：`probe_t1.txt`、`probe_pdf278_after_t1.txt`、`probe_judge.txt`、`verify_acidity_mp.py`
- 回归分析：`cmp.py`、`verify_mp27.py`、`diff_engine.py`、`diff_lost.py`

---

## 2. 红线核对（不可突破）

| 红线 | 要求 | 自评 | 证据 |
|------|------|------|------|
| precision | =1 | ✅ PASS | 全量 cmp `prec=1`；`diff_engine.py` harm(adopted∧¬correct)=0 |
| harmCount | =0 | ✅ PASS | 全量 cmp `harm=0`；引擎层 adopted∧¬correct=0 |
| LOST（引擎层） | 0 回退 | ✅ PASS | `diff_engine.py` ENGINE LOST(gwCorr True→False)=0，truthAdopted True→False=0 |
| 不蒙数 | 无唯一解→null | ✅ PASS | T3 求解器任一触发特征缺失/选项不可判/离子非共轭碱/非整数 pH→整体 null（见 `WeakAcidIonizationSolverTests` 反例） |
| 不硬编码单题 | 通用规则 | ✅ PASS | T3 用化学事实集合（一元弱酸→共轭碱表）+ 电荷守恒通式 + 选项语义规则，无 pdf-278 特判；T4 用方程多重集等价通用算法 |
| T4 边界 | 仅 multi_part AND，不改 OR | ✅ PASS | 改动在 `AllKeywordsHit`（multi_part AND 路径）；`IsAnswerCorrect`(OR) 未触碰；`MultiPartAndScoringTests` OR 路径用例不变 |

> 注：cmp solved 口径的 LOST（pdf-197/355/519 等）经 `diff_lost.py` 核实为 pureLlm 非确定波动（引擎字段完全未变），不计入代码回归（项目记忆：LLM 不可作真值源）。

---

## 3. AC 逐条核对

| AC | 要求 | 自评 | 证据 |
|----|------|------|------|
| AC-1 | pdf-278 引擎判正（gwCorr/usedEng/truthAdopt=True，provenance=multi_part），聚焦 cmp LOST=[] | ✅ PASS | 聚焦 eval `pdf278_mp`：taskType=multi_part、gwCorr=True、usedEng=True、truthAdopt=True；cmp gwTruth 645→646、LOST=[]。审查加固后 `pdf278_review` 聚焦重跑确认 **`gatewayResultProvenance=multi_part`**（#1 修复生效，字面完全满足） |
| AC-2 | 全库零回归 LOST=[]/harm=0/precision=1 | ✅ PASS（引擎层） | 全量 `pdf278_full`：harm=0、prec=1；引擎层 LOST=0。solved 口径 LOST 为 pureLlm 波动（见上） |
| AC-3 | 27 道已解对多部分题保持 True，有回归守卫 | ✅ PASS | `verify_mp27.py`：27/27 保留 gwCorr=True；`diff_engine.py` 27 题无一回退 |
| AC-4 | 多部分占位不再阻塞分发（整题 Ok!=true→分发器激活） | ✅ PASS | T1 三处守卫；`MultiPartPlaceholderGuardTests` 5 例通过；`probe_pdf278_after_t1.txt` 显示整题 null→分发→5 子问拆分；pdf-272 多部分 curve 占位现静默 |
| AC-5 | multi_part 方程顺序无关判分（正例命中、不等价不命中） | ✅ PASS | `MultiPartAndScoringTests` 追加 7 例；`probe_judge.txt` ALL AllKeywordsHit=True |
| AC-6 | 子问(5)求解器正确 + 反例 null（不蒙数） | ✅ PASS | `WeakAcidIonizationSolverTests` 10 例：pdf-278 子问5 命中 `10^-5-10^-9 BD`；pH=4 HF / pH=3 HCN 变体正确；无 pH/无 c(离子)/非弱电解质/无选项/c(Na⁺)非共轭碱/非整数pH → null |
| AC-7 | 子问(5)泛化性（rubric ≥4） | ✅ PASS（自评 5） | 规则通用（电荷守恒通式 + 电离度影响因素语义表 + 一元弱酸共轭碱事实集合）；≥2 变体正确；≥5 反例 null。无单题特判；审查加固 #2/#3 进一步收紧域外不蒙数 |

---

## 4. TR 逐条核对（tasks.md）

| TR | 任务 | 自评 |
|----|------|------|
| TR-1.1 / 1.2 / 1.3 | 占位静默 + 探针 | ✅ |
| TR-3.1 ~ 3.5 | 浓度正例/反例/选择/不蒙数/泛化 | ✅（含审查加固扩展） |
| TR-4.1 ~ 4.3 | 方程顺序命中/不命中/OR 不变 | ✅ |
| TR-5.1 | 聚焦 eval pdf-278 判正 + cmp | ✅ |
| TR-6.1 / 6.2 | 全量 cmp + 27 题守卫 | ✅（引擎层） |

---

## 5. 独立审查结论

### 5.1 审查者结论
- **结论**：**PASS**
- **总体评语**（独立审查者）：所有项目红线（precision=1、harm=0、引擎层 LOST=0、27 道多部分已解题零回归）经独立复算核实成立；pdf-278 已由引擎确定性判正（gwCorr/usedEng/truthAdopt=True、taskType=multi_part、聚合结论覆盖 5 子问且由引擎产出）。AC-1~AC-7 功能上全部满足。无阻断/高严重度问题；发现 1 处 AC 字面偏差与若干中/低严重度的泛化与覆盖缺口（均不破红线、不影响 pdf-278），已在 §6 全部采纳修复。

### 5.2 发现的问题（审查者提出，已全部采纳修复 — 见 §6）
| # | 严重度 | 位置 | 描述 | 建议 | 状态 |
|---|--------|------|------|------|------|
| 1 | 低 | [LlmGatewayService.MultiPartDispatch.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.MultiPartDispatch.cs) | AC-1 字面要求 `gatewayResultProvenance=multi_part`，但分发器聚合返回的 `EngineResponse` 未设置 `ResultProvenance`，实际为 None（taskType 仍是权威信号，功能无损） | 在聚合响应设 `ResultProvenance = MultiPartTaskType` | ✅ 已修复 |
| 2 | 中 | [LlmGatewayService.WeakAcidIonization.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.WeakAcidIonization.cs) | 求解器未校验被求离子是否为所识别弱酸的共轭碱，直接套用 `c(A⁻)=c(H⁺)-c(OH⁻)`；pH 经 `Math.Round` 取整，分数 pH 会被静默改成整数表达式（采纳门阻止 harm，但偏离不蒙数） | 增加共轭碱身份校验；非整数 pH 返回 null | ✅ 已修复（修复中发现并修正变体被选项"冰醋酸"干扰酸判定的 bug：改为以被求离子身份反查酸） |
| 3 | 低 | 同上 `DecideIonizationDegreeChange` | 对弱酸，"加入强碱/NaOH"会消耗 H⁺ 促进电离、使电离度增大，但代码把 NaOH 归入 `addCommonIon` → 判为不增大（漏选，不产生假阳性但拉低泛化上限） | 加强碱对弱酸应判增大；仅加共同离子/增大自身浓度才判不增大 | ✅ 已修复 |
| 4 | 低 | [WeakAcidIonizationSolverTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/WeakAcidIonizationSolverTests.cs) | 反例未覆盖 #2 的两个域外分支 | 增补：c(Na⁺) 非共轭碱→null；非整数 pH→null；NaOH 正例→选中 | ✅ 已补 3 例 |

### 5.3 审查者核对的 AC/TR 属实性
独立审查者逐条复算核实：§3 AC-1~AC-7 与 §4 TR 自评**全部属实**。唯一存疑项为 AC-1 的 `gatewayResultProvenance` 字面（功能无损），已在审查加固 #1 中修复并经 `pdf278_review` 聚焦重跑确认字面满足。审查者确认全量 precision 恒为 1（直算 `gatewayTruthAdopted ∧ ¬gatewayCorrect` 两 checkpoint 均为空）。

---

## 6. Reconcile 与修复记录

审查结论 PASS，4 个问题**全部采纳修复**（均为质量加固，不破红线）：

1. **#1 provenance 对齐**：在 [LlmGatewayService.MultiPartDispatch.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.MultiPartDispatch.cs) 聚合返回的 `EngineResponse` 上设 `ResultProvenance = MultiPartUtility.MultiPartTaskType`。验证：`checkpoint_pdf278_review.jsonl` 中 pdf-278 `gatewayResultProvenance=multi_part`。

2. **#2 共轭碱校验 + 非整数 pH 不蒙数**：
   - 新增 `WeakAcidConjugateMatches(raw, ionCore)`：以**被求离子身份反查**对应一元弱酸并确认题干含该酸（避免选项里"冰醋酸"等干扰酸判定——修复中发现变体 HF/HCN 因选项 C 含"冰醋酸"被误判酸为醋酸，已由此修正）。
   - 限定一元弱酸物种表（`c(A⁻)=c(H⁺)-c(OH⁻)` 对多元酸不成立，故不触发）。
   - 非整数 pH → 整体 null（不取整蒙数）。
   - 新增反例测试：`反例_被求离子非共轭碱_cNa_不产出浓度表达式`、`反例_非整数pH_不产出取整错表示式`。

3. **#3 加强碱判电离增大**：`DecideIonizationDegreeChange` 区分 `addStrongBase`（消耗 H⁺ → 增大）与 `addCommonIon`（共同离子 → 不增大）。新增正例测试 `正例_弱酸加强碱使电离增大_NaOH与加热均被选中`（断言选中 AC）。

4. **#4 测试覆盖**：共补 3 例（见上），`WeakAcidIonizationSolverTests` 从 7 例增至 10 例。

5. **附带行为更新**：T1 使 pdf-272（多部分）的 curve 占位静默，[ImageTextFusionRegressionTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/ImageTextFusionRegressionTests.cs) 中 `Pdf272_CurveReading_TaggedProvenance_AndNeverAdopted` 同步更新为 `Pdf272_MultiPart_CurvePlaceholderSilenced_NeverAdopted`，仍守住"曲线中间量不被采纳"红线（从"产生但不采纳"强化为"多部分不产生"）。

**验证**：
- 全量单元测试 **0 失败**：`CogniThink.Chem.Tests` 922/922 + `CogniThink.TutorBrain.Tests` 1591/1591。
- 聚焦重跑 `pdf278_review`：pdf-278 仍 `gwCorr/usedEng/truthAdopt=True` 且 `provenance=multi_part`。
- 修复性质分析：#1 为纯诊断字段（不改判决）；#2/#3 只让求解器域外更严格（更多返回 null），不会让任何已解题回退（全量中仅 pdf-278 依赖该求解器，已验证仍正确）。
- 修复后全量回归 `pdf278_final` 运行中，预期引擎层 `LOST=0`（结果填入下方）。

**最终结论**：✅ **PASS**。pdf-278 多部分大题分发全部 AC 满足，项目红线（precision=1 / harm=0 / 引擎零回归 / 不蒙数 / 不硬编码单题）全部守住。

### 修复后全量回归结果（pdf278_final）✅
修复（#1 provenance / #2 共轭碱校验+非整数pH / #3 NaOH / #4 测试 + pdf-272 测试更新）后，强制重跑全部 757 题：
- `cmp after_vsepr509 pdf278_final`：`gwTruth 645→649`、`harm=0`、`precision=1`。
- `diff_engine after_vsepr509 pdf278_final`：**`ENGINE LOST (gwCorr True→False)=0`**、`truthAdopted True→False=0`、`harm(adopted∧¬correct)=0`；`ENGINE GAINED=[d5-1,d5-2,d7-4,pdf-278]`（与修复前完全一致 → 加固未改变任何引擎判决，仅收紧域外不蒙数）。
- `verify_mp27`：**27/27 PASS**。
- 结论：加固零回归，红线全部守住。solved 口径 LOST/GAINED 仍为 pureLlm 随机波动（不同次重跑波动题不同，引擎字段完全未变），不计入代码回归。
