# 多小题 √/× 逐方程判定聚合 实施计划

## 仓库调研结论

- **现状**：648 题评估中，"判断方程式/离子方程式书写正误，填 √/×"题型当前网关全部失败。HTTP 探针实测：`CallCompile` 对单方程只返回占位符 `已匹配到相关反应`，真正说"正确 ✅/错误"的是 **LLM 话术**，且 LLM 把该判 × 的（pdf-051 第 3/4 题、pdf-468）也说成"正确 ✅"。`gatewayAnswer`（引擎真值）为空 → 不计分。
- **可复用内核**：Core 的 `ChemicalCheckerService.Check`（[ChemicalCheckerService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/ChemicalCheckerService.cs)）串行管线：白名单 → 守恒 → 配平 → 离子共存/拆写 → 状态符号 → 氧化还原电子守恒 → 反应库产物一致性，返回 `CheckReport`（`IsValid` / `CheckItems` / `RiskTag`）。
- **探针实证**（`_evalscratch/ccs_probe.txt`，8 个目标方程）：
  - 能正确抓：HF 弱酸拆写（pdf-468，离子共存规则命中 `H⁺+F⁻ 应生成 HF`）、状态符号错误、守恒/配平。
  - 误判：Cr₂O₇²⁻、[Cu(NH₃)₂]²⁺ 被白名单标"超纲"→ `IsValid=false`（假错误，实际方程正确）。
  - 漏判：需题干语境的错误抓不到——pdf-051(3) `H₂SO₃+Ba²⁺→BaSO₃` 形式全过，但语境中 Ba(NO₃)₂ 的 NO₃⁻ 会氧化 H₂SO₃（应为 ×）。
- **判分**：[IsAnswerCorrect](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs) 用 `AnswerKeywords.Any(k => lower.Contains(k))`（OR 语义）。pdf-051 的 `answerKeywords` 是**单整串** `(1)√ (2)√ (3)× (4)× (5)×`；pdf-468/471/472 是单字符 `×`/`√`。`NormalizeChemicalNotation` 不改 √/×，所以输出必须包含对应字符/序列。
- **采纳门控**：`BuildGatewayAnswerAsync` 对 `reaction_check` 要求 `HasReactionCheckTruthSignature` 或 `IsConcreteConclusion`，√/× 两者都不沾 → 当前不被采纳。
- **目标题（引擎可判的方程式正误，已排除概念题）**：pdf-051（5 小题方程正误）、pdf-468 / pdf-471 / pdf-472（单小题方程正误）。**排除**：pdf-467（概念判断"SiO₂ 是否碱性氧化物"，非方程）、pdf-117/134/450/478/480（多为概念说法正误，引擎无能力）。

## 目标与冻结边界（守住 precision=100% / harm=0 / 零回归红线）

**单方程判据 `EquationCorrectnessJudge`**（包装 `ChemicalCheckerService.Check`，返回 `{Verdict: √/×/null, Reasons}`）：
- **判 ×（确信错误）**：`CheckReport` 含"确定性错误"标签：
  `ConservationViolation`（元素/电荷不守恒）、`IonicNotationError`（离子共存/强弱电解质拆写，如 HF 弱酸）、`StateSymbolError`（↑↓ 错）、`KnowledgeConflict`（产物与知识库矛盾）、`RedoxError` 且明细为"电子不守恒"、`NoSolution`（无法配平）。
- **判 √（确信正确）**：全部 `CheckItems` 通过 **且** 无 `OutOfScope`（物种在纲内）。
- **其余 → null（不蒙数）**：解析失败 / 仅 `OutOfScope` / 氧化数未知 / 有语境依赖无法确认。
- 铁律：无确定结论绝不输出 √/×。

**聚合 `MultiSubCorrectnessSolver`**：
- 题干匹配"判断正误 / 填 √× / (1)...(2)..."多小题模式 → 拆出每个小题的方程 → 逐方程判。
- **仅当全部小题都有确定判决（无 null）**才输出聚合串 `(1)√ (2)× ...`（N=1 时输出单字符 `√`/`×`）。
- 任一小题 null → **整体返回 null**（不输出部分答案——错一位会被采纳但判分不中 → precision 掉，这是守 precision 的关键）。

**预期增益（严格真值口径）**：
- **确定可拿 3 题，零 harm**：pdf-468（×，HF 弱酸）、pdf-471（√）、pdf-472（√，LLM 当前答错）。
- **尽力拿 pdf-051（第 4 题）**：需补白名单（Cr₂O₇²⁻ 等 → 051-2 判√）+ 干净的语境规则（051-3 NO₃⁻ 酸性氧化 H₂SO₃、051-4 Cl₂+过量 HCO₃⁻→HClO、051-5 亚铜氨配离子电荷）。5 条全部能确定判 → 增益；任一不确定 → pdf-051 返回 null，保持现状（bothWrong），**零回归零 harm**。
- **不扩范围**：不碰概念题、不碰 P2 工艺题、不接入守恒路径（已证有害）。

## 文件与模块

- **新增** `src/CogniThink.TutorBrain.Api/Application/Gateway/EquationCorrectnessJudge.cs`：单方程判据内核。
- **新增** `src/CogniThink.TutorBrain.Api/Application/Gateway/MultiSubCorrectnessSolver.cs`：题干拆分 + 聚合 + 全确定才输出。
- **修改** `LlmGatewayService.CallCompile`（[L457](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs#L457) reaction_check 分支）：在 Redox/Product solver 之前先试 `MultiSubCorrectnessSolver.TrySolve`，不命中返回 null，零回归。
- **修改** `GatewayVsLlmEvaluationService.cs`：
  - `BuildGatewayAnswerAsync`：新增 `HasCorrectnessVerdictSignature`（识别 √/× 判决串），命中则采用。
  - `IsAnswerCorrect`：新增 √/× 序列归一化分支——当某关键词归一化后是纯 √/× 序列时，提取答案与该关键词的 √/× 字符序列，关键词序列是答案序列的子串即判正（兼容带编号/逗号/连写/单字符全部格式）。**仅对 √/× 模式关键词触发**，不影响其他判分。
- **数据（条件性）** `data/chemistry/ions.json`：补常见多原子离子（Cr₂O₇²⁻、MnO₄⁻ 等）若缺失——仅当确认全量测试零回归。
- **新增单测** `tests/CogniThink.TutorBrain.Tests/EquationCorrectnessJudgeTests.cs`：覆盖 8 个目标方程判决 + null 负例 + 聚合全确定/部分-null 行为 + 采纳签名 + 判分三种格式命中。

## 实施步骤（依赖序）

1. **核实白名单**：脚本检查 `ions.json` 现有 formula，确认 Cr₂O₇²⁻/MnO₄⁻ 等是否缺失；缺失则补，跑全量测试确认零回归（失败则回退数据改动——判据把 OutOfScope 当 null 仍安全）。
2. **实现 `EquationCorrectnessJudge`** + 单测验证 8 方程判决符合预期（含 051-3/4/5 的 null 处理）。
3. **实现 `MultiSubCorrectnessSolver`**（题干拆分 + 聚合 + 全确定才输出）+ 单测。
4. **接入 `CallCompile`**（reaction_check 优先调用，不命中 null）。
5. **扩采纳门控** `HasCorrectnessVerdictSignature`。
6. **判分侧 √/× 序列归一化分支** + 单测（覆盖 pdf-051 整串 / 连写 `××××√√` / 单字符三种格式命中）。
7. **（条件性）语境规则表**：NO₃⁻ 酸性氧化亚硫酸根、Cl₂+过量 HCO₃⁻→HClO、亚铜氨配离子电荷——仅当能用干净表驱动且单测通过；否则 pdf-051 保持 null。
8. Release 构建 0 错误 + 全量测试（排除外部 GraphSolver / _DiagGraph*）不下降。
9. 重启服务器，HTTP 探针验证 468/471/472 与 pdf-051 的引擎输出。
10. 重跑全量评估 → `after_stage3d.json`，verify 核对：新增题 correct、lost=0、harm=0、precision=1。

## 依赖与注意

- **架构红线**：不用 LLM 做化学判定（LLM 只做题干→结构化）；所有 √/× 来自符号引擎；不确定→null 绝不臆断。
- **聚合"全确定才输出"** 是守 precision 的关键：部分错误的聚合串会被采纳但判分不中 → precision 掉。宁可整题 null。
- **判分归一化**只放宽 √/× 模式，不碰其他关键词，避免误伤现有判分。
- **白名单改动影响全局**，必须全量回归；不确定则不改，判据把 OutOfScope 当 null。
- **语境规则若加**，必须集中在一张规则表（避免散落硬编码分支），每条配单测。

## 验证

- **单测**：8 目标方程判决、聚合全确定/部分-null、采纳签名、判分三种格式命中、负例（不可解析/超纲/概念题返回 null）。
- **构建**：Release sln 0 错误；全量测试通过。
- **评估**：`after_stage3d.json` vs `after_stage3c.json`，verify 脚本核对：3~4 题 gained、lost=0、harm=0、enginePrecision=1。

## 风险

- **白名单补离子改变现有判决** → 补后跑全量测试，失败即回退；判据始终把 OutOfScope 当 null，不补也安全。
- **语境规则误判** → 规则表集中、每条单测；聚合全确定才输出，单条不确定则整题 null，不会因规则不全而输出错答案。
- **判分归一化误伤其他题** → 仅对"关键词归一化后为纯 √/× 序列"触发；跑全量评估对比 pureLlmCorrect/gatewayCorrect 无异常下降。
- **pdf-051 最终拿不下（5 条无法全确定）** → 接受，保持 null 零回归；文档注明为已知局限（需更强语境/知识库）。
