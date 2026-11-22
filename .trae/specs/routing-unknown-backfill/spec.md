# P1-5 路由补齐（taskType=unknown 收敛）

> **状态**：已批准并实现完成（含 T4 回归修复；实证更正与已批准偏差见 §7 补遗）。
> **基线**：`after_p14`（757 题，635 正确 / 122 未解，`taskType=unknown` = 32，precision=1，harm=0）。红线锚点 `base_p0`（629 正确）。
> **路线图定义**：`.trae/documents/project-structure-analysis-and-roadmap.md` L330-333 — 扩展 `ClassifyTaskDeterministic`，先全库扫描确认「补规则不会让已解对题 taskType 漂移」，再增量补；验收 = unknown 题数下降 + 已解对题零有害漂移；依赖 P1-1（同文件区域，串行）。

## 1. 背景与问题

`after_p14` 全库 757 题中 `taskType=unknown` 共 **32 道**（路线图原估 28，P1-4 路由改动后分布变化为 32），全部 `gatewayCorrect=False`。这些题未被确定性分类器识别，落入 `unknown` 兜底。

路线图 P1-5 要求收敛这批 `unknown`：补分类规则，使语义明确的题进入正确 taskType 链路，同时保证已解对题零有害漂移。

## 2. 关键审计发现（诚实口径，不信自述）

证据（均只读，未改任何 repo 文件）：
- 范围审计 [p15_scope_audit.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p15_scope_audit.txt) / [p15_scope_audit.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p15_scope_audit.py)：32 道 unknown 分类。
- 答案转储 [p15_answers_dump.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p15_answers_dump.txt)：逐题 stem/answer/answerKeywords。
- 0 漂移预校验 [p15_drift_precheck.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p15_drift_precheck.txt) / [p15_drift_precheck.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p15_drift_precheck.py)。

### 2.1 unknown 瀑布已覆盖廉价求解器 → 纯路由「新增解对数」= 0

`CallEngineAsync` 对 `taskType ∈ {concept, unknown}` 的瀑布（[LlmGatewayService.cs L450-531](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs)）已依次运行：
`AtomicStructureSolver` → `VseprSolver`(键角原因) → **`ConceptFactSolver`**（L476）→ `StoichiometryStateTrapSolver` → **`ReactionKbWriterSolver`**（L512）→ 早返回 `Ok=null`。

即：**任何可被概念事实或方程式知识库解出的 unknown 题，在 `after_p14` 已被解出**。32 道 unknown 全部未解，说明它们均不命中现有事实库 / 方程式 KB。

纯路由改动（把 unknown 改判为 concept/equation_writer/calculation/…）**不会解锁新求解器**：
- `concept` 与 `unknown` 跑同一组求解器（无差异）；
- `equation_writer` 仅比 unknown 多跑 `KnownEquationWriterSolver`（仅硬编码 2 个方程：GaN+强碱、Na2SO3+H2SO4→SO2，见 [KnownEquationWriterSolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/KnownEquationWriterSolver.cs)），32 道均不命中；
- `calculation` 跑 `CallCalculation`（11 个窄域计算器，见 [LlmGatewayService.cs L1126-1196](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs)），32 道无一是其覆盖域。

**结论：P1-5 纯路由的「新增解对数」= 0（诚实）。** 本 spec 的目标不是解新题，而是**路由卫生**（unknown↓ + 零有害漂移），与路线图验收口径一致。真实瓶颈（数据缺陷 / 缺求解器 / 多部分 / 图像）在第 4 节明确划出范围外。

### 2.2 32 道 unknown 的逐项处置

| 类别 | 题数 | 题号 | 处置 |
|------|------|------|------|
| 多部分大题（仅 mp） | 3 | pdf-176, 198, 541 | 范围外 → P1-2 多部分分发 |
| 仅图像（img） | 3 | pdf-159, 160, 205 | 范围外 → P2-3 图像管道 |
| 多部分 + 图像 | 6 | pdf-097, 177, 215, 391, 413, 528 | 范围外 → P1-2 / P2-3 |
| 数据缺陷（答案错位 / 题面碎片 / 裸字母键） | 7 | pdf-164, 165, 172, 179, 180, 333, 377 | 范围外 → 数据管道键修复（§E 部分已登记） |
| 有机官能团 / 命名（缺分子图 + 缺命名求解器） | 8 | pdf-322, 323, 324, 519, 520, 521, 522, 523 | 范围外 → 图像恢复 + 新命名求解器（未来 spec） |
| 方程书写 KB 缺口 | 1 | pdf-462 | 范围外 → 方程式 KB 窄域增补（未来 spec，需探针 0 假命中） |
| 概念事实重叠 / 重复题录 | 1 | pdf-483 | 范围外（P1-4 §L.2 已排除）→ 数据去重 |
| 装置命名（缺图标号） | 1 | pdf-534 | 范围外 → 图像恢复 |
| 自由文本分析（无对应求解器） | 1 | pdf-537 | 范围外 → 概念解释求解器（未来 spec） |
| 题面碎片 | 1 | pdf-161 | 范围外 → 数据管道补全 |

> 闭包核对：3 + 3 + 6 + 7 + 8 + 1 + 1 + 1 + 1 + 1 = **32**，与 `after_p14` unknown 全集一一对应，差集为空，**0 静默遗漏**。

### 2.3 唯一可落地的路由卫生改动：方程书写问法补齐（cue A）

32 道中，语义最明确、可无歧义归入 `equation_writer` 的是「题干明确要求写方程式但未出现『书写/写出』触发词」的题。现有 `equation_writer` 规则（[LlmGatewayService.cs L319-322](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs)）要求 `("书写"∨"写出") ∧ ("离子方程式"∨"化学方程式"∨"方程式"∨"酯化反应")`，遗漏了「…的化学方程式为______」「…的离子方程式为______」「用离子方程式表示…」等常见高考问法。

**候选 cue A**（插在既有 `equation_writer` 块之后、P1-4 concept cues 之前）：
```
lower 含 "化学方程式为" ∨ "离子方程式为" ∨ "方程式为" ∨ "用离子方程式表示" ∨ "用化学方程式表示"
→ return "equation_writer"
```

**0 漂移预校验结果**（[p15_drift_precheck.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p15_drift_precheck.txt)，全库 757）：
- AFFECTED = 16（old ≠ new）。
- 其中 `gatewayCorrect=True` = **5**（pdf-174, 417, 456, 515, 539）—— 这 5 道当前已判正，但确定性分类器把它们判为 `unknown`，实际由 `unknown` 瀑布中的 `ReactionKbWriterSolver` 解出并改标为 `reaction_check`（checkpoint runtime taskType）。
- 其中 `old=unknown` 重分类（卫生收益）= 11。

**5 道已判正为何预计保持判正（推理，待 eval 实证）**：`equation_writer` 分支（[L566-590](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs)）先跑 `KnownEquationWriterSolver`（这 5 道均非其 2 个硬编码方程 → null），再跑 `ReactionKbWriterSolver`（与 unknown 路径同一求解器、同一输入 → 产出同一结论）。因此改判后答案不变 → 保持判正。**但推理不能替代实证**：AC-3 要求 eval diff 证明这 5 道 `correct` 仍为 True（LOST=0 / WORSEN=0）。若 eval 显示任一回归 → 收窄 cue 或回退，绝不 paper over。

## 3. 方案

仅改 `ClassifyTaskDeterministic` 一处（插入 cue A），不改任何求解器、不改 JSON 数据、不改评估代码。最小表面积。

- **插入位置**：既有 `equation_writer` `if` 块（L319-322）之后、P1-4 三条 concept cues（L326-331）之前。
- **不新建求解器、不新增化学事实**（路由规则不含化学真值，符合「事实只存 JSON」红线）。
- **前置门禁**：T2 重跑 0 漂移预校验（确认 5 道已判正的推理成立由 eval 把关）。

## 4. 验收标准（AC）

- **AC-1 unknown 下降**：`after_p15` eval checkpoint 中 `taskType=unknown` 计数较 `after_p14`（32）**严格下降**；确定性分类器端口（`p15_scope_audit.py` 同口径）unknown 计数亦下降。
- **AC-2 precision=1**：全库 `adopted=True ∧ correct=False = 0`；新增路由不改采纳门契约（`equation_writer` 命中仍由 `reaction_check` 采纳门按 `IsAnswerCorrect` 校验）。
- **AC-3 零有害漂移 / 无回归**：`diff_ckpt after_p14 after_p15` 与 `diff_ckpt base_p0 after_p15` 均 **LOST=0 / WORSEN=0 / harm=0**；特别确认 pdf-174/417/456/515/539 仍 `correct=True`。
- **AC-4 诚实增益**：新增解对数 **≥ 0**（纯路由预期 = 0；若 eval 出现 >0 为附带收益，仍须满足 AC-2/AC-3）。**不蒙数**：不得为凑增益而加事实/KB 条目（那是范围外的独立 spec）。
- **AC-5 不蒙数**：`ClassifyTaskDeterministic` 仅加 cue A 一条；不修改 `ConceptFactSolver` / `ReactionKbWriterSolver` / `KnownEquationWriterSolver` 守卫；不硬编码化学真值。
- **AC-6 测试**：新增路由单测覆盖 (a) cue A 命中的代表题 → `equation_writer`；(b) 5 道已判正题 `taskType` 变化但经推理应保持判正（断言改判后分类结果，eval 实证正确性）；(c) 不窃取 guard：含「方程式」但属 choice/judgment/equilibrium 的题不被 cue A 截走（维持既有优先级）。全量 `dotnet test` **0 failed**。
- **AC-7 台账闭环**：在 [data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md) 新增 §M（P1-5 路由补齐闭环）：登记 cue A 重分类的 16 道去向 + 5 道已判正保持判正的实证 + 22 道范围外逐项处置（与第 2.2 节表一致），差集为空。
- **AC-8 自检**：密钥扫描（sk-/api_key/Bearer/secret）0 命中；`concept-facts.json` 未改动（P1-5 不动数据）；未触碰评估代码；不蒙数验证。

## 5. 开放问题

- **OQ-1（诚实呈现，待用户决策）**：纯路由增益 = 0。本 spec 只做路由卫生。若用户希望「顺带解出几道」，需另立 spec 做方程式 KB 窄域增补（如 pdf-462，需探针 0 假命中）或概念事实增补（多数因数据缺陷/重叠不可行）——**不在本 spec 范围**，避免范围蔓延与精度风险。
- **OQ-2（已决策）**：不新建求解器类、不动 JSON 数据、不动评估代码。仅 `ClassifyTaskDeterministic` 一处插入。
- **OQ-3（留观）**：cue A 会把多部分/图像题（pdf-097/177 等）也改判为 `equation_writer`，但这些题 `ReactionKbWriterSolver` 返回 null（锚点不全/多义）→ 仍未解、无伤害（行为不变），待 P1-2/P2-3 解锁。

## 6. 红线（不可妥协，破线即回退）

`precision=1` / `harm=0` / `LOST=0` / `WORSEN=0` / 不蒙数 / 事实只存 JSON。任何一项破线 → 回退 offending 改动（cue A 或其测试），不得 paper over。

## 7. 实施补遗（实证更正 + 已批准偏差，不破红线）

> 本节记录实现阶段的实证发现与经用户确认的范围微调。§1–§6 已批准主体不变；本节为诚实留痕，不改变验收标准（AC-1~AC-8 全部仍以 eval 实证为准）。

### 7.1 §2.3 推理的实证更正

§2.3 原推理：「5 道已判正（pdf-174/417/456/515/539）由 unknown 瀑布中的 `ReactionKbWriterSolver` 解出，改判 equation_writer 后经同一求解器产出不变 → 保持判正」。**该推理被 eval 证伪（部分）**：

- cue A 首版 eval `after_p15` 出现 **LOST=1（pdf-515）**，其余 4 道保持判正。
- 根因（代码指纹唯一，非推理）：pdf-515 在 `after_p14` 的确定性分类返回 `unknown` → 保留 LLM 兜底分类 `reaction_check` → 进入 `CallEngineAsync` 的 `reaction_check` 分支 → `CallCompile` → 其中 `ProductWriterSolver`（NaCN + 少量 CO2 规则）解出，输出 `离子方程式：CN- + CO2 + H2O → HCN + HCO3-`（指纹：`→` 箭头 + 去 `^`，唯一定位 `ProductWriterSolver`，区别于 `ReactionKbWriterSolver` 的 `=` 与保留 `^`）。
- cue A 把 pdf-515 重分类为 `equation_writer` 后，`equation_writer` 分支原本只调 `KnownEquationWriterSolver` + `ReactionKbWriterSolver`，**不含 `ProductWriterSolver`**；而 `ReactionKbWriterSolver` 因题干无 H2O（KB 检索要求所有反应物出现在题干）返回 null → LOST。
- 其余 4 道的真实求解器：pdf-174/417 = `ReactionKbWriterSolver`（模板）；pdf-456 = `KnownEquationWriterSolver`（SO2 规则）；pdf-539 = `KnownEquationWriterSolver`（GaN 规则）。均在 `equation_writer` 分支覆盖范围内 → 保持判正。

**教训**：预校验（分类器 Python 端口）只能确认 taskType 漂移，**不能确认正确性保持**——只有 eval 能。不得仅凭推理事先声称「会保持判正」；每道 correct-touch 都须 eval 实证。

### 7.2 已批准偏差：equation_writer 分支补齐 ProductWriterSolver

按 CQ-2「若 eval 显示任一回归 → 收窄 cue 或回退，绝不 paper over」，T4 暴露回归后给出三选项，**用户确认方案 (a)**：在 `equation_writer` 分支按 `reaction_check` 作家链同序（Product → Known → ReactionKb）插入 `ProductWriterSolver.TrySolve`，命中后置 `req.TaskType = "reaction_check"` 走同一采纳门（凡采纳必经 `IsAnswerCorrect` 校验 → precision=1，不蒙数）。

- 这是对原 spec §3「仅改 `ClassifyTaskDeterministic` 一处，不改任何求解器」的**经批准的范围微调**：不改求解器本身，只在 `equation_writer` 分支的求解器编排中补入已有求解器 `ProductWriterSolver`，使其与 `reaction_check` 路径功能对齐。不新建求解器、不加化学事实、不动 JSON 数据、不动评估代码。
- 风险由全量 eval 护栏验证：`diff after_p14→after_p15b` 与 `diff base_p0→after_p15b` 均 LOST=0/WORSEN=0/harm=0；`ProductWriterSolver` 窄锚点（4 条规则，须特定反应物 + 量关键词）不命中即 null，未引入新假阳性。

### 7.3 最终验收结果（eval `after_p15b`，Production LLM off）

- 757/757 完成，failed=0；correct=635 / adopted=635。
- `diff after_p14 → after_p15b`：GAINED=0 / **LOST=0** / WORSEN=0 / harm=0 → 红线 PASS（pdf-515 恢复判正，零回归）。
- `diff base_p0 → after_p15b`：GAINED=6（P1-4 携带：pdf-415/530/535/542/543 + trap-limit-2）/ LOST=0 / WORSEN=0 / harm=0 → 红线 PASS。
- AC-1：unknown 32 → 27（严格下降）。AC-2：global `adopted∧¬correct=0`。AC-3：pdf-174/417/456/515/539 全部 correct=True。AC-4：新增解对数 = 0（诚实）。AC-8：密钥扫描 0 命中；`data/highschool/*.json` 未改；评估代码未改。
- 证据：`p15_drift_precheck.py`、`p15_verify_after.py after_p15b`（ALL PASS）、`p15_closure_check.py`（PASS）、全量 `dotnet test` 2191 passed / 0 failed。台账闭环见 `data-defect-register.md` §M。
