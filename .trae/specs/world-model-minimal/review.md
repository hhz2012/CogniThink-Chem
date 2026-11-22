# P2-2 世界模型与机理层最小可用版 — 独立只读 Review

> **审查模式**：严格只读（未改/创建/删除任何文件；构建产物仅写入 gitignore 的 bin/obj）。
> **最终交付 checkpoint**：`after_p22`。
> **审查者**：fresh-context 只读子代理（general_purpose_task，仅返回报告文本，由主代理落盘）。
> **审查日期**：2026-11-22。
> **总体裁决**：**PASS**。7 项检查全部通过，裁决维度无 FAIL、无 Issue，无修复项。

---

## 1. Build — PASS
- 命令：`dotnet build CogniThink.Chem.sln -c Release --nologo`
- 结果：**0 错误，0 警告**（输出"已成功生成。0 个警告 0 个错误"）。`Directory.Build.props` 未设 `TreatWarningsAsErrors`/`NoWarn`，故为真实 0 警告；P2-2 全部文件 0 警告。
- 备注：任务描述预期的既有警告（AdaptiveTeachingOrchestrator x2、CrystalCellValidator x1、测试文件 x4）本次构建未出现——基线比预期更干净，不构成失败。

## 2. Test — PASS
- 命令：`dotnet test CogniThink.Chem.sln -c Release --nologo --no-build`
- 结果：Chem.Tests **913 passed** + TutorBrain.Tests **1299 passed** = **2212 passed，0 failed，0 skipped**，exit 0。与期望 ~2212 完全吻合。

## 3. 红线 diff 两次 + summary — PASS
- `diff_ckpt.py after_p21 after_p22`：old=757 new=757，**GAINED=0 / LOST=0 / WORSEN=0 / harm=0** → `RED-LINE: PASS`。
- `diff_ckpt.py base_p0 after_p22`：old=757 new=757，GAINED=6（pdf-415/530/535/542/543/trap-limit-2，P1-4 携带，允许）/ **LOST=0 / WORSEN=0 / harm=0** → `RED-LINE: PASS`。
- `checkpoint_after_p22_summary.txt`：gatewayCorrect=635 / gatewayTruthCorrect=635 / precision=1 / harmCount=0 ✓。

## 4. T6 自检复跑 + ExplanationNodes 位置 — PASS
- `p22_self_check.py`：**14 项断言全部 PASS，0 failures**，`T6 SELF-CHECK VERDICT: PASS`。
- 独立 Grep：`ExplanationNodes` 在 `GatewayVsLlmEvaluationService.cs` 仅出现 **2 次**——行 86 `GatewayExplanationNodesCount` 字段声明 + 行 204 计数赋值，**均在 `BuildGatewayAnswerAsync` 方法定义行 668 之前** → 采纳门方法体内 0 引用。
- 全仓 Grep `*.cs`：`ExplanationNodes` 仅出现在契约定义 / 富化写入 / 观测计数 / 测试断言，无任何判分或采纳逻辑消费。

## 5. 3 链解释内容与 JSON 事实一致 — PASS
- `WorldModel.cs` 引用 id：M1 = STR-001/STR-002/RED-001 + activity-series；M2 = SOL-EXC-001 + RX-PL-PPT-001；M3 = RX-PL-HYD-003。
- 逐条核对 JSON 真实存在且文本非硬编：
  - STR-001（rules.json L421）display="同周期从左到右原子半径减小、非金属性增强"，KH-ST-02 ✓
  - STR-002（L434）display="同主族从上到下金属性增强、非金属性减弱"，KH-ST-02 ✓
  - RED-001（L32）display="活动性强的金属能把活动性弱的金属从其盐溶液中置换出来"，KH-IN-06 ✓
  - SOL-EXC-001（L240）display="AgCl 与过量氨水生成 [Ag(NH3)2]+ 而溶解（沉淀-配位耦合）"，KH-RP-07 ✓
  - RX-PL-PPT-001（reactions-principles.json L35）equation="AgCl ⇌ Ag^+ + Cl^-"，KH-RP-07 ✓
  - RX-PL-HYD-003（L25）equation="CO3^2- + H2O ⇌ HCO3^- + OH^-"，KH-RP-06 ✓
  - activity-series.json：K…Au ✓
- 节点描述经 `NodeFromRule`/`NodeFromReaction` 构造：事实文本取自 `rule.Conclusion.Display` / `reaction.Equation`（运行时 `TryGetValue`/`TryGet` 拉取，缺失则不产节点 → 不蒙数），**所有节点均追加「（帮助理解，不作判定依据）」**。
- M3 描述含「定性，不涉及 pH 数值」；全链无 pH=/Ksp=/Kh=/浓度数值输出（单测 `AssertTrendNodes` 亦断言无 `pH=\d` / `Ksp=\d`）。与教材不矛盾：所有 JSON 条 source 指向人教版必修/选必标准内容。

## 6. 契约加性 — PASS
- `EngineResponse` 同时含：`DerivationSteps`（旧，L120-121）、`ConclusionState`（P2-1，L154-155）、`ReasoningNodes`（P2-1，L158-159）、`ExplanationNodes`（P2-2 新增，L163-164，默认 `= new()`，JSON key `explanation_nodes`）。旧字段未删改。
- `WorldModelEnricher.Enrich`（ReasoningContracts.cs L115-128）只读：仅读 `resp.FinalResult` 作上下文、写 `resp.ExplanationNodes`，不改 Ok/FinalResult/TaskType；`LlmGatewayService.cs` 行 127 插入在 `ConclusionStateDeriver.Derive`（123）之后、`GenerateReplyAsync`（130）之前。

## 7. 台账自洽 — PASS
- O.1 触及文件清单（7 个 .cs）全部存在且内容与描述吻合：1 新增 Core（WorldModel.cs）+ 4 修改 Api（GatewayContracts.cs / ReasoningContracts.cs / LlmGatewayService.cs / GatewayVsLlmEvaluationService.cs）+ 1 修改 EvalRunner（Program.cs L286 快照 `explanationNodesCount`）+ 1 新增测试（WorldModelExplanationTests.cs 恰 6 个 [Fact]，与 O.1 逐一对应）。触及清单无 data/**。
- O.3 命中清单：独立用 Python 统计 `checkpoint_after_p22.jsonl` 中 `gatewayExplanationNodesCount>0` 的记录，得 **11 题**，与 O.3 表**逐一对上**（id/节点数/conclusionState/adopted/correct 全匹配）：pdf-057/089/096/137/156/176/213/269/277/446/478；其中 8 个 adopted 全部 correct，3 个 Insufficient 未被采纳 → 不蒙数。

---

## 裁决维度汇总（全 PASS）
precision=1 ✓ / harm=0 ✓ / LOST=0 ✓ / WORSEN=0 ✓ / 不蒙数 ✓ / 未改 data 与判分采纳门 ✓ / 解释不泄漏判分 ✓ / 定性无绝对值不与教材矛盾 ✓ / 契约加性 ✓。

## 证明强度说明（不影响 PASS）
- 全树自 P0 起未提交（register 已注明），故无法用 git diff 单独隔离 P2-2 对 `data/**` 的改动；证明依赖"O.1 声明的 7 触及文件全为 .cs（自检 `data_touched=[]`）"+"after_p21→after_p22 采纳门输入输出逐 case 字节一致（gatewayAnswer/gatewayOk/gatewayConclusion diff=0）"，二者与"零数据判分影响"自洽，但非 git 级强证明。
- 预期的既有警告基线本次为 0；0 错误这一硬要求已满足，不构成失败。
