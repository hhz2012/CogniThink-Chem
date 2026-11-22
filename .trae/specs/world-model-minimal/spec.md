# P2-2 世界模型与机理层最小可用版（趋势级解释链）

> **状态**：**已批准**（2026-11-22，用户确认 CQ-1=A / CQ-2=a / CQ-3 限定 3 链定性 / CQ-4 零数据改动 / OQ-5 加不计分诊断字段；CQ-5 不动采纳门定死）。批准后开始实现。
> **基线**：`after_p21`（757 题，635 正确，precision=1，harm=0，LOST=0，WORSEN=0）。红线锚点 `base_p0`（629 正确）。
> **路线图定义**：[project-structure-analysis-and-roadmap.md](file:///d:/github/CogniThink-Chem/.trae/documents/project-structure-analysis-and-roadmap.md) L345-349 — 在 Core 落地电子层/电离能趋势、离子水合比较、键角/极性等「趋势级」模型（不给绝对值，标 evidence_level），支撑 3 条示范解释链（金属还原性递变、AgCl 溶于氨水、Na₂CO₃ 显碱性）；网关在原子结构/VSEPR/平衡求解器中复用，输出微观解释（标注「帮助理解，不作判定依据」）；验收 = 3 条解释链可复现且标注层级、不与教材主干矛盾。依赖 P2-1。
> **红线（不可破）**：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 事实只存 JSON。破线即回退。
> **诚实口径（先声明）**：本 spec 预期 eval 正确性增益 = **0**（与 P2-1 同为结构/解释卫生）。解释节点标 `EvidenceLevel=Trend`、「帮助理解，不作判定依据」，**永不进入采纳门**，不改任何题的 verdict。价值在可解释性 + 为 P2-4 前端分层讲解提供数据；不强凑解对题数。

## 1. 背景与问题

路线图目标是让求解从「事实硬匹配」升级为「可追溯推理」，P2-1 已落地统一推理 DAG 契约与 `EvidenceLevel`（含 `Trend` = 「不给绝对值，帮助理解，不作判定依据」）。但 **M3 世界模型基本未实现**：Core 只有 `PeriodicTable`（给绝对值：电子排布/价层/周期族/未成对电子），没有趋势级/机理级模型；网关输出无微观解释，学生看不到「为什么」。

本 spec 在 Core 落地**趋势级/机理级最小模型**（定性、不给绝对值），网关复用并以 `ExplanationNodes`（EvidenceLevel=Trend）输出微观解释，支撑 3 条示范链。**严格不改变采纳门与正确性行为**（红线）。

## 2. 现状审计（只读证据，精确到文件/行号）

### 2.1 Core：只有绝对值模型，无趋势/机理模型；数据加载器就绪

- [PeriodicTable.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Structure/PeriodicTable.cs)（L3-8 声明「纯事实/算法，结果由原子序数唯一确定」）：提供基态/离子/价层电子排布、未成对电子数、周期/族（L105-148、L225-258）—— **全部是可计算绝对值，不是趋势比较**。
- [ChemDataLoader.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Data/ChemDataLoader.cs)：`LoadRules` 读 `rules/rules.json`（L51-52）、`LoadKnowledge`（L45）；`ReactionLibrary.Load`（[Explainer.cs L94](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Explanation/Explainer.cs#L94)）读 `data/knowledge/reactions/*`；`RuleStore` 读 ions/electrolytes/solubility。→ **加载器已就绪**，缺的是消费这些数据的「趋势/机理解释」模型。

### 2.2 数据层：3 条示范链的事实已存在 JSON（无需新增）

| 示范链 | 已有 JSON 事实（文件 + 条目） |
|--------|-------------------------------|
| 金属还原性/金属性递变 | [activity-series.json](file:///d:/github/CogniThink-Chem/data/highschool/activity-series.json)（K Ca Na Mg Al … Au，人教版必修一）；[rules.json](file:///d:/github/CogniThink-Chem/data/rules/rules.json) L33「金属活动性顺序置换」、L426「同周期左→右原子半径减小、非金属性增强」、L439「同主族上→下金属性增强」；[knowledge.json](file:///d:/github/CogniThink-Chem/data/knowledge/knowledge.json) KH-ST-02 元素周期律（rules STR-001/STR-002） |
| AgCl 溶于氨水 | [rules.json](file:///d:/github/CogniThink-Chem/data/rules/rules.json) L241「AgCl 在过量氨水中配位溶解」（predicate `reaction_direction` / `dissolves_by_complexation`）+ L224「难溶盐与氨水不反应（通则）」（例外覆盖）；[reactions-principles.json](file:///d:/github/CogniThink-Chem/data/knowledge/reactions/reactions-principles.json) RX-PL-PPT-001 AgCl ⇌ Ag⁺ + Cl⁻ |
| Na₂CO₃ 显碱性 | [reactions-principles.json](file:///d:/github/CogniThink-Chem/data/knowledge/reactions/reactions-principles.json) RX-PL-HYD-003「碳酸根水解」CO₃²⁻ + H₂O ⇌ HCO₃⁻ + OH⁻（强碱弱酸盐，人教版选必1）；[rules.json](file:///d:/github/CogniThink-Chem/data/rules/rules.json) L195「水解常数与酸性强弱，越弱越水解」 |

→ **事实只存 JSON（硬约束）已满足**：3 条链事实齐全，P2-2 不新增 JSON（除非实现时发现某链缺事实，届时以 JSON 增补、绝不硬编码进 .cs）。

### 2.3 网关：无 Trend 级解释输出；解释字段加性且 gate-safe

- `EngineResponse`（[GatewayContracts.cs L105-160](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/GatewayContracts.cs#L105-L160)）已有 P2-1 加性字段 `reasoning_nodes` / `conclusion_state`；尚无解释字段。
- grep 全 src：`ReasoningNodes` 仅在 **4 个示范 solver 写** + `GatewayContracts` 定义，**生产侧无任何读取**（采纳门 `BuildGatewayAnswerAsync` / 判分均不读）。→ 新增解释字段为加性、wire 兼容、且**不可能泄漏到判分**（只要新字段同样不被采纳门读取，见 AC-8）。

### 2.4 候选题实证：3 条链均有真实题，加解释不改 verdict

探针 [probe_p22_cases.py](file:///d:/github/CogniThink-Chem/_evalscratch/probe_p22_cases.py) 读 `pdf_questions.json` 并查 `checkpoint_after_p21.jsonl`：
- A 金属还原性/金属性：15 候选，**全部 correct=True / Confirmed**（如 pdf-014/081/122…）。
- B AgCl/氨水：10 候选，8 个 correct=True（pdf-040/044/048…），2 个 Insufficient（pdf-165/355，原未解）。
- C Na₂CO₃/水解碱性：63 候选，绝大多数 correct=True（pdf-013/015/017…），少数 Insufficient（pdf-025）。

→ 加解释后：已判正题必须保持 correct（不 LOST）；原 Insufficient 题必须保持 Insufficient、不被误采纳（不 harm）。即 eval 正确数 635 不变（gain=0）。

### 2.5 红线不受影响的依据

只要不改动 `FinalResult` / `Ok` / `TaskType` / `AssetKind` / `NumericValue*` 的赋值与 `BuildGatewayAnswerAsync` 逻辑，**新增 `ExplanationNodes` 对 eval 正确性结果零影响**（checkpoint 不读解释字段，见 P2-1 spec §2.4）。LOST/WORSEN/harm 可证保持 0。

## 3. 目标契约定义

### 3.1 Core 世界模型（趋势级/机理级，不给绝对值）

新增 Core 类型（落点见 CQ-1），**纯函数**：输入（题干文本 / 已解结论 / 涉及物种）→ 可选 `List<ReasoningNode>`（`EvidenceLevel=Trend`）。三个最小模型：

| 模型 | 输入触发 | 输出（定性，无绝对值） | 事实源（已有 JSON） |
|------|----------|------------------------|---------------------|
| M1 金属性/还原性递变比较 | 题干涉及两种元素的金属性/还原性/活动性比较 | 递变方向（同周期/同主族或活动性序）+ 原因（核电荷/电子层数/失电子能力），不给电离能数值 | activity-series.json + rules.json 趋势规则 + KH-ST-02 |
| M2 沉淀-配位溶解机理 | 题干涉及 AgCl（或类似难溶盐）+ 氨水/配位溶解 | 机理：沉淀溶解平衡 + 配位耦合 → 溶解，不给 Ksp 数值 | rules.json L241 + RX-PL-PPT-001 |
| M3 盐类水解酸碱性 | 题干涉及 Na₂CO₃（或类似强碱弱酸盐）溶液酸碱性 | 哪类离子水解、产生 OH⁻/H⁺ → 酸碱性，不给 Kh/pH 数值 | RX-PL-HYD-003 + rules.json L195 |

铁律：**所有模型只给定性方向/机理，不给任何数值（电离能/Ksp/Kh/pH/浓度）**；命中事实必须带 `RuleId`/`KnowledgeId` 指向已有 JSON 条目；无命中不产出（留空，不蒙数）。

### 3.2 `ExplanationNodes`（加性解释字段，与判定链分离）

`EngineResponse` **新增** `ExplanationNodes: List<ReasoningNode>`（JSON 键 `explanation_nodes`，默认空），全部节点 `EvidenceLevel=Trend`。与 `ReasoningNodes`（判定链，EngineDerived/KnowledgeFact，末节点 Conclusion==FinalResult）**分离**：
- `ReasoningNodes` = 判定结论的可溯源链（P2-1，影响"结论是否成立"的展示，不影响判分）。
- `ExplanationNodes` = 帮助理解的微观机理/趋势（不声称 Conclusion==FinalResult，标「帮助理解，不作判定依据」）。

### 3.3 网关富化点（一处，只读）

在 `ProcessStudentMessageAsync` 内、`ConclusionStateDeriver` 之后，调用 Core 世界模型对 `EngineResponse` 富化 `ExplanationNodes`。**只读**：不写回 `Ok`/`FinalResult`/`TaskType`，不调用采纳门，不改任何判定字段。

## 4. 设计决策（CQ — 需用户在批准时确认）

### CQ-1：世界模型落点【关键】

| 选项 | 做法 | 优点 | 缺点 |
|------|------|------|------|
| **A（推荐）Core 落地** | 新建 `src/CogniThink.Chem.Core/WorldModel/`，纯函数读取已有 JSON（复用 ChemDataLoader/ReactionLibrary），网关调用 | 符合路线图 M3「在 Core 落地」；Core 为真值源可复用；与 PeriodicTable 同层 | 网关需跨栈调用（Api 已 ProjectReference Core，无障碍） |
| B 网关侧落地 | 模型写在 Api/Gateway | 改动就近 | 违反路线图 M3；能力散落在网关（与 M4「缺统一机理层」问题同构）；不可被 Core/前端复用 |

**推荐 A**。

### CQ-2：解释字段暴露方式【关键】

| 选项 | 做法 | 优点 | 缺点 |
|------|------|------|------|
| **a（推荐）新 ExplanationNodes 列表** | 与 ReasoningNodes 分离的加性列表，全 EvidenceLevel=Trend | 判定链与解释链语义清晰分离；末节点 Conclusion==FinalResult 不变量不受解释节点干扰；P2-4 可分层渲染 | 多一个字段 |
| b 单 MicroExplanation 字符串 | 一个 `string?` 字段 | 最简 | 丢失 rule/knowledge 溯源与 evidence_level 结构；与 P2-1 契约不对齐 |
| c 混入 ReasoningNodes | Trend 节点追加到现有列表 | 少一个字段 | 混淆判定链与解释链；破坏「末节点 Conclusion==FinalResult」不变量；前端难分层 |

**推荐 a**。

### CQ-3：3 条链的范围与边界【待确认】

严格限定：① 只做金属性递变 / AgCl-氨水配位溶解 / 碳酸根水解酸碱性 三条；② 全部定性、不给绝对值；③ 解释内容必须与 JSON 事实（人教版溯源）一致，不与教材主干矛盾。**不扩展**到电离能数值计算、Ksp 计算、pH 计算、其它离子水解（留后续）。请确认此边界。

### CQ-4：数据：复用现有 JSON，不新增【待确认】

审计证实 3 条链事实已在 JSON（§2.2）。**本 spec 不新增 JSON 文件、不修改现有 JSON**（除非实现时发现某链缺事实，届时以 JSON 增补并经 review，绝不硬编码进 .cs）。请确认接受「零数据改动」。

### CQ-5：不改判分（定死，非真选项）

`ExplanationNodes` 永不被 `BuildGatewayAnswerAsync` / `IsAnswerCorrect` / `GatewayCorrect` 赋值读取。富化点只读、不改判定字段。任何改动都有 harm 风险，违反红线。

## 5. 范围（In Scope）

1. Core 新建世界模型（3 个趋势/机理最小模型，纯函数，定性无绝对值，读已有 JSON）。
2. `EngineResponse` 加性新增 `ExplanationNodes` 字段（wire 兼容）。
3. 网关一处富化点（`ProcessStudentMessageAsync`，ConclusionStateDeriver 之后），只读，不改判定字段。
4. 3 条示范链各 ≥1 真实题 stem 验证：`ExplanationNodes` 非空、内容对应、带 RuleId/KnowledgeId、EvidenceLevel=Trend、且 FinalResult/Ok 与富化前一致。
5. 单测：Core 模型输出正确性 + 富化不改变判定字段 + 采纳门不读解释字段（回归守卫）。
6. 全量 eval 红线核验（diff 相对 after_p21：GAINED/LOST/WORSEN/harm 全 0）。
7. 台账登记 + 自检 + 独立 review（沿用 P2-1 T5/T6/T7 模式）。

## 6. 非目标（Out of Scope，明确不做）

- 不给绝对值：不做电离能/Ksp/Kh/pH/浓度数值计算（趋势级，规划 D10）。
- 不扩展到第 4 条及以上解释链。
- 不改 `BuildGatewayAnswerAsync` 采纳逻辑（CQ-5）。
- 不新增/修改 JSON 数据（CQ-4，审计已证实事实齐全）。
- 不改评估代码判分口径（只允许加性读取新字段做诊断，不影响 GatewayCorrect）。
- 不做前端展示（P2-4）。
- 不接 Core ProblemSolver 到网关 Live 路径（两套栈收敛方向未拍板）。

## 7. 验收标准（AC）

| ID | 验收 | 证据 |
|----|------|------|
| AC-1 | Core 世界模型 3 模型编译通过；给定输入返回正确 Trend 节点（带非空 RuleId/KnowledgeId、EvidenceLevel=Trend、定性无绝对值）；无命中返回空。 | 单测 |
| AC-2 | `EngineResponse` 新增 `ExplanationNodes` 为加性（既有字段/JSON 键不变）；旧序列化兼容。 | 对比 JSON 既有键不变 + build 0 error |
| AC-3 | 网关富化点只读：不改 `Ok`/`FinalResult`/`TaskType`；单测断言富化前后这些字段逐 case 不变。 | 单测 |
| AC-4 | 3 条示范链各 ≥1 真实题 stem：`ExplanationNodes` 非空、内容对应、EvidenceLevel=Trend、且 `FinalResult`/`Ok` 与富化前一致（不翻转 verdict）。 | 单测 + 抽样 |
| AC-5 | **红线**：`diff_ckpt after_p21 → after_p22`：GAINED=0 / LOST=0 / WORSEN=0 / harm=0；correct=635 不变。 | diff 脚本 RED-LINE PASS |
| AC-6 | 不蒙数：`ExplanationNodes` 非空的 case 中凡 `adopted=True` 必 `correct=True`（global adopted∧¬correct=0）；原 Insufficient case 不因加解释被采纳。 | eval 后诊断脚本 |
| AC-7 | 不与教材主干矛盾：3 链解释与 JSON 事实（人教版溯源）逐字一致、定性无数值。 | 单测断言 + review |
| AC-8 | 解释不泄漏到判分：`BuildGatewayAnswerAsync` 源码 grep `ExplanationNodes` = 0 命中；采纳门输入输出（gatewayAnswer/gatewayOk/gatewayConclusion）逐 case 与 after_p21 相同（diff=0）。 | 自检脚本 |

## 8. 风险与回退

- **R1 过度工程（路线图 §7 明确警告）**：若把趋势模型做成数值引擎 → 范围爆炸、与「不给绝对值」冲突。缓解：严格限定 3 链 + 定性 + 无数值；AC-7 卡内容。
- **R2 解释泄漏到判分 → harm**：若采纳门误读 ExplanationNodes → 假采纳。缓解：CQ-5 定死不读；AC-8 grep + 逐 case diff 硬把关；单测守卫。
- **R3 解释与教材矛盾 → 误导**：缓解：解释内容来自已有人教版 JSON 事实（带 RuleId/KnowledgeId 溯源），不硬编码；review 逐条核对；标「帮助理解，不作判定依据」降低误用。
- **R4 富化点意外改判定字段 → LOST**：缓解：富化只读、写新字段；AC-3 单测断言 Ok/FinalResult/TaskType 富化前后逐 case 不变；eval diff 硬把关。
- **回退**：任一红线破 → 回退本 spec 触及文件到 after_p21 状态；新增字段为加性，回退不影响基线。

## 9. 诚实口径

- **P2-2 预期 eval 正确性增益 = 0**。理由：解释节点永不进入采纳门、不改判定字段；checkpoint 不读解释。价值在：① 微观可解释性（学生看到「为什么」）；② 为 P2-4 前端分层讲解提供结构化数据；③ 验证 P2-1 的 `EvidenceLevel.Trend` 契约被真实消费。**不强凑解对题数。**
- 「网关在原子结构/VSEPR/平衡求解器中复用」在本 spec 结束时**仅通过统一富化点对 3 条链的真实题生效**；不追求全题库覆盖（避免虚报）。

## 10. Open Questions（批准时一并确认）

- OQ-1：CQ-1 落点选 A（Core）/ B（网关）？（推荐 A）
- OQ-2：CQ-2 解释字段选 a（ExplanationNodes 列表）/ b / c？（推荐 a）
- OQ-3：CQ-3 三条链 + 定性无绝对值的边界是否认可？
- OQ-4：CQ-4 「零数据改动，复用现有 JSON」是否认可？
- OQ-5：是否把 `ExplanationNodes` 数量/存在性写入 eval checkpoint 仅作诊断（不计分），便于观测覆盖率？（推荐：加，只读诊断，不影响判分。）
