# Rule / Fact Ownership Matrix & KP Cross-Reference (auto-generated)

> Generated: `2026-11-22T07:51:07.238365+00:00`  
> Generator: `tools/data-assets/rule_fact_ownership.py` (P3-2)  
> Decision: §6#1 网关为主 / Core 基准回归（已确认，2026-11-22）；P3-2 = 明确分工 + 交叉引用  
> Rule/fact assets: **29 rows** = 26 primary (rules.json + rule-tests.json + 24 highschool) + 3 cross-root collision companions  
> Knowledge points cross-referenced: **50** (Main: 44)  

## ⚠️ Honesty disclaimer

- **rules 侧**：rules.json → KP 由**精确 `knowledgeId` 外键**链接（实测 0 dangling），是确定性的。
- **facts 侧**：highschool 文件**无 knowledge_id 外键**（仅 phenomena.json 带），覆盖由 `name`/`textbookRef` **子串匹配**推断，
  每条事实链接均标注 `match=candidate`，**需人工复核**，可能是措辞差异导致的假阴性（同 P3-1 gap_report 双口径）。
- 本文件是**维护型诊断视图**，不进入 eval 管线、不改引擎行为 → **zero eval impact**。
- 所有权元数据为 P3-2 T0 审计固化的工程元数据（file:line 证据），非化学真值。

## 所有权矩阵（owner_stack / role / 消费者证据 / 域）

| # | asset | owner | role | 消费者证据 (file:line) | 化学域 | 备注 |
|---|-------|-------|------|------------------------|--------|------|
| 1 | `data/rules/rules.json` | Core | 声明式仲裁规则（if-then + conflictGroup；61 条，L0–L4） | Core/Data/ChemDataLoader.cs:51-52 (LoadRules)<br>Core/Inference/ExperimentEngine.cs:24 (EXP-*)<br>Core/Inference/ProcessFlowEngine.cs:44<br>Core/Inference/AqueousEquilibriumEngine.cs:1064<br>Core/Inference/ElectrochemEngine.cs:549<br>Core/Inference/EquilibriumEngine.cs:634<br>Core/WorldModel/WorldModel.cs:62<br>Core/Evaluation/RuleRegressionRunner.cs:88 (RuleEngine.Decide)<br>tests/CogniThink.Chem.Tests/KnowledgeAndRuleTests.cs:46,54<br>Cli/Commands/ExportWebCommand.cs:35 | 全局仲裁（氧化还原/平衡/溶解/实验/转化等） | 网关（757 eval 主链路）不消费 |
| 2 | `data/rules/rule-tests.json` | Core | 规则回归用例（9 条） | Core/Data/ChemDataLoader.cs:54-55 (LoadRuleTests)<br>Core/Evaluation/RuleRegressionRunner.cs:69 (Run) | 规则回归 | — |
| 3 | `data/highschool/acid-constants.json` | Both | 酸/水解常数查找表 | Core/Inference/AqueousEquilibriumEngine.cs:99<br>Api/Application/Gateway/HydrolysisPhCalculator.cs:17<br>Api/Application/Gateway/LlmGatewayService.cs:1998 | 水溶液平衡/水解 | — |
| 4 | `data/highschool/choice-curated-facts.json` | Gateway | 单题策展选择事实（红线 #6 隔离） | Api/Application/Gateway/FactChoiceMatcher.cs:96 (CuratedFactKey) | 选择题 | — |
| 5 | `data/highschool/concept-facts.json` | Gateway | 概念陈述事实 | Api/Application/Gateway/ConceptFactSolver.cs:20 | 概念判断 | — |
| 6 | `data/highschool/counting-facts.json` | Gateway | 计数/组合推理事实 | Api/Application/Gateway/CountingChoiceValidator.cs:25 | 计数选择题 | — |
| 7 | `data/highschool/double-decomposition.json` | Core | 复分解反应条件事实 | tests/CogniThink.Chem.Tests/HighSchoolRulesTests.cs:25 | 复分解反应 | 仅测试守护，无生产消费者 |
| 8 | `data/highschool/electrode-reaction-facts.json` | Gateway | 电极反应事实 | Api/Application/Gateway/RedoxChoiceValidator.cs:23 | 电化学/氧化还原 | — |
| 9 | `data/highschool/electrolytes.json` | Core | 电解质分类事实 | Core/Rules/ElectrolyteRules.cs:31 | 电解质 | — |
| 10 | `data/highschool/equilibrium-rules.json` | Gateway | 化学平衡事实 | Api/Application/Gateway/EquilibriumChoiceValidator.cs:100 | 化学平衡 | — |
| 11 | `data/highschool/experiment-facts.json` | Gateway | 实验事实 | Api/Application/Gateway/FactChoiceMatcher.cs:112 (ChoiceFactKeys) | 实验 | — |
| 12 | `data/highschool/general-choice-facts.json` | Gateway | 通用选择事实 | Api/Application/Gateway/FactChoiceMatcher.cs:90,111 (GeneralFactKey) | 选择题 | — |
| 13 | `data/highschool/infeasible-equation-patterns.json` | Gateway | 不可行离子方程式模式 | Api/Application/Gateway/IonicEquationValidator.cs:25 | 离子方程式 | — |
| 14 | `data/highschool/ion-conflicts.json` | Gateway | 离子冲突（共存）事实 | Api/Application/Gateway/IonicEquationValidator.cs:38 | 离子共存 | — |
| 15 | `data/highschool/ion-solution-context.json` | Gateway | 离子溶液环境事实 | Api/Application/Gateway/IonicEquationValidator.cs:39 | 离子共存 | — |
| 16 | `data/highschool/ions.json` | Core | 离子清单（root A，碰撞） | Core/Rules/WhitelistGuard.cs:51,62<br>Core/Rules/IonizationMap.cs:15<br>Core/Rules/ElectrolyteRules.cs:34 | 离子/电离 | 文件名碰撞；根 B 见 Api/data/chemistry/ions.json |
| 17 | `data/highschool/judgment-statement-facts.json` | Gateway | 判断题通用陈述事实 | Api/Application/Gateway/FactChoiceMatcher.cs:104 (JudgmentFactKey) | 判断题 | — |
| 18 | `data/highschool/na-particle-facts.json` | Gateway | 阿伏伽德罗常数微粒事实 | Api/Application/Gateway/NaChoiceValidator.cs:278 | 化学计量 | — |
| 19 | `data/highschool/organic-facts.json` | Gateway | 有机化学事实 | Api/Application/Gateway/FactChoiceMatcher.cs:113 (ChoiceFactKeys) | 有机化学 | — |
| 20 | `data/highschool/phenomena.json` | Core | 实验现象事实 | Core/Inference/ChainAssignmentCore.cs:121<br>Core/Inference/ExperimentEngine.cs:30<br>Core/Inference/IdentificationEngine.cs:31 | 实验现象/鉴别 | 唯一带 knowledgeId 字段的 highschool 文件 |
| 21 | `data/highschool/solubility.json` | Core | 溶解性表（root A，碰撞） | Core/Rules/SolubilityTable.cs:12<br>Core/Rules/WhitelistGuard.cs:64 | 溶解/离子共存 | 文件名碰撞；根 B 见 Api/data/chemistry/solubility.json |
| 22 | `data/highschool/substances.json` | Core | 物质清单（root A，碰撞） | Core/Rules/WhitelistGuard.cs:76 | 物质分类 | 文件名碰撞；根 B 见 Api/data/chemistry/substances.json |
| 23 | `data/highschool/structure-periodic-facts.json` | Gateway | 物质结构/周期律事实 | Api/Application/Gateway/StructurePeriodicValidator.cs:14<br>Api/Application/Gateway/FactChoiceMatcher.cs:114 (ChoiceFactKeys) | 物质结构/周期律 | — |
| 24 | `data/highschool/transformation-rules.json` | Gateway | 物质转化链事实 | Api/Application/Gateway/TransformationChainValidator.cs:410 | 物质转化 | — |
| 25 | `data/highschool/substance-classification.json` | Gateway | 物质分类事实 | Api/Application/Gateway/ClassificationValidator.cs:269<br>Api/Application/Gateway/TransformationChainValidator.cs:424 | 物质分类 | — |
| 26 | `data/highschool/activity-series.json` | Core | 金属活动性顺序表 | Core/Rules/SolubilityTable.cs:35<br>Core/Inference/ElectrochemEngine.cs:71 | 氧化还原/置换 | — |
| 27 | `src/CogniThink.TutorBrain.Api/data/chemistry/ions.json` | Gateway | 离子知识（root B，碰撞） | Api/Application/Chemistry/ChemicalKnowledgeBase.cs:46-70 (startup)<br>Api/Controllers/ChemistryController.cs:76 (ions endpoint) | 离子/Api KB | 碰撞根 B；与根 A data/highschool/ions.json schema/消费者均不同 |
| 28 | `src/CogniThink.TutorBrain.Api/data/chemistry/substances.json` | Gateway | 物质知识（root B，碰撞） | Api/Application/Chemistry/ChemicalKnowledgeBase.cs:46-70 (startup)<br>Api/Controllers/ChemistryController.cs:51 (substances endpoint) | 物质/Api KB | 碰撞根 B；与根 A data/highschool/substances.json schema/消费者均不同 |
| 29 | `src/CogniThink.TutorBrain.Api/data/chemistry/solubility.json` | Gateway | 溶解性知识（root B，碰撞） | Api/Application/Chemistry/ChemicalKnowledgeBase.cs:46-70 (startup) | 溶解/Api KB | 碰撞根 B；与根 A data/highschool/solubility.json schema/消费者均不同 |

- 行数统计：Core=9 / Gateway=19 / Both=1（合计 29）

**分工结论**：`data/rules/rules.json` = Core 仲裁/基准回归规则库（网关不消费）；`data/highschool/*` = 网关事实库（Core 复用子集）；两体系同域不同角色、非冗余；三碰撞文件（ions/substances/solubility）跨根 schema+消费者均不同，**不合并**。

## 知识点交叉引用汇总（50 KP × rules 精确 FK × facts 候选匹配）

- rules.json 61 条规则 → **26** 个 distinct `knowledgeId`（均有效，dangling=0）。
- **24** 个 KP 无任何 rules.json 规则（`kps_without_rules`）：
  `KH-EX-06`, `KH-EX-08`, `KH-HT-01`, `KH-IN-01`, `KH-IN-05`, `KH-IN-10`, `KH-IN-11`, `KH-IN-13`, `KH-OR-01`, `KH-OR-02`, `KH-OR-03`, `KH-OR-06`, `KH-OR-08`, `KH-OR-09`, `KH-OR-10`, `KH-RP-04`, `KH-RP-05`, `KH-RP-09`, `KH-RP-10`, `KH-ST-04`, `KH-ST-05`, `KH-ST-06`, `KH-ST-07`, `KH-ST-08`

| coverage_label | KP 数 |
|----------------|-------|
| rules+facts | 7 |
| rules-only | 19 |
| facts-only | 5 |
| neither | 19 |

解读：`rules-only` = 有精确规则链接但无 highschool 事实候选覆盖（facts 侧可能措辞假阴性）；
`facts-only` = 有事实候选但无规则；`neither` = 两者皆无（最强候选内容缺口，需人工复核）。

## 全部 50 KP 交叉引用明细

| id | name | module | tier | rules (exact FK) | facts (candidate) | label |
|----|------|--------|------|------------------|-------------------|-------|
| `KH-IN-01` | 物质分类与胶体 | Inorganic | Main | — | — | neither |
| `KH-IN-02` | 离子反应与离子方程式 | Inorganic | Main | `CONS-CHARGE-001`, `SOL-001`, `SOL-002` | — | rules-only |
| `KH-IN-03` | 氧化还原基本概念 | Inorganic | Main | `CONS-ELEC-001`, `RED-002`, `RED-003` | — | rules-only |
| `KH-IN-04` | 钠及其化合物 | Inorganic | Main | `EXPT-004` | `choice-curated-facts.json`(4), `phenomena.json`(2) | rules+facts |
| `KH-IN-05` | 镁铝及其化合物 | Inorganic | Main | — | — | neither |
| `KH-IN-06` | 铁铜及其化合物 | Inorganic | Main | `EXPT-003`, `RED-001` | — | rules-only |
| `KH-IN-07` | 氯与卤素 | Inorganic | Main | `ACID-001`, `ACID-EXC-001` | — | rules-only |
| `KH-IN-08` | 硫及其化合物 | Inorganic | Main | `COND-002`, `RED-005` | `phenomena.json`(4) | rules+facts |
| `KH-IN-09` | 氮及其化合物 | Inorganic | Main | `COND-001`, `EXPT-005` | `phenomena.json`(4) | rules+facts |
| `KH-IN-10` | 硅与无机非金属材料 | Inorganic | Extended | — | — | neither |
| `KH-IN-11` | 金属冶炼与金属材料 | Inorganic | Extended | — | — | neither |
| `KH-IN-12` | 化工工艺流程 | Inorganic | Main | `PROC-004` | — | rules-only |
| `KH-IN-13` | 物质的量与化学计量 | Inorganic | Main | — | — | neither |
| `KH-OR-01` | 烃：烷、烯、炔与苯 | Organic | Main | — | — | neither |
| `KH-OR-02` | 卤代烃 | Organic | Main | — | `general-choice-facts.json`(2) | facts-only |
| `KH-OR-03` | 醇与酚 | Organic | Main | — | — | neither |
| `KH-OR-04` | 醛与羧酸 | Organic | Main | `ORG-001` | — | rules-only |
| `KH-OR-05` | 酯与油脂 | Organic | Main | `ORG-002` | — | rules-only |
| `KH-OR-06` | 同分异构体 | Organic | Main | — | `choice-curated-facts.json`(3), `concept-facts.json`(1), `general-choice-facts.json`(4), `organic-facts.json`(2), `structure-periodic-facts.json`(1) | facts-only |
| `KH-OR-07` | 有机反应类型 | Organic | Main | `ORG-003` | — | rules-only |
| `KH-OR-08` | 有机合成路线 | Organic | Main | — | — | neither |
| `KH-OR-09` | 糖类、蛋白质 | Organic | Main | — | `general-choice-facts.json`(1) | facts-only |
| `KH-OR-10` | 高分子与单体判断 | Organic | Main | — | — | neither |
| `KH-RP-01` | 反应热与盖斯定律 | ReactionPrinciples | Main | `THERMO-001` | — | rules-only |
| `KH-RP-02` | 化学反应速率 | ReactionPrinciples | Main | `EQ-003` | `general-choice-facts.json`(2) | rules+facts |
| `KH-RP-03` | 化学平衡 | ReactionPrinciples | Main | `EQ-001`, `EQ-002` | — | rules-only |
| `KH-RP-04` | 弱电解质电离 | ReactionPrinciples | Main | — | `general-choice-facts.json`(1) | facts-only |
| `KH-RP-05` | 水的电离与 pH | ReactionPrinciples | Main | — | — | neither |
| `KH-RP-06` | 盐类水解 | ReactionPrinciples | Main | `SOL-006`, `SOL-007` | — | rules-only |
| `KH-RP-07` | 沉淀溶解平衡 | ReactionPrinciples | Main | `PROC-002`, `PROC-003`, `SOL-003`, `SOL-004`, `SOL-005`, `SOL-008`, `SOL-EXC-001` | `general-choice-facts.json`(6) | rules+facts |
| `KH-RP-08` | 原电池 | ReactionPrinciples | Main | `RED-004` | `concept-facts.json`(1), `experiment-facts.json`(2), `general-choice-facts.json`(8) | rules+facts |
| `KH-RP-09` | 电解池 | ReactionPrinciples | Main | — | — | neither |
| `KH-RP-10` | 金属腐蚀与防护 | ReactionPrinciples | Main | — | — | neither |
| `KH-EX-01` | 基本实验操作 | Experiment | Main | `EXP-009` | — | rules-only |
| `KH-EX-02` | 气体制备与净化 | Experiment | Main | `EXP-016` | — | rules-only |
| `KH-EX-03` | 物质检验与鉴别 | Experiment | Main | `EXP-001`, `EXP-002`, `EXP-003`, `EXP-004`, `EXP-005`, `EXP-006`, `EXP-008`, `EXP-010`, `EXPT-001`, `EXPT-002` | — | rules-only |
| `KH-EX-04` | 分离与提纯 | Experiment | Main | `EXP-011`, `EXP-012`, `EXP-013`, `EXP-014`, `EXP-015`, `PROC-001` | — | rules-only |
| `KH-EX-05` | 定量实验与误差分析 | Experiment | Main | `EXP-017`, `EXP-018`, `EXP-019`, `EXP-020` | — | rules-only |
| `KH-EX-06` | 探究实验与方案评价 | Experiment | Main | — | — | neither |
| `KH-EX-07` | 实验安全与环保 | Experiment | Main | `EXP-007` | — | rules-only |
| `KH-EX-08` | 减压蒸馏与色谱简介 | Experiment | Extended | — | — | neither |
| `KH-ST-01` | 原子结构与核外电子排布 | Structure | Main | `STR-003` | — | rules-only |
| `KH-ST-02` | 元素周期律 | Structure | Main | `STR-001`, `STR-002` | — | rules-only |
| `KH-ST-03` | 化学键 | Structure | Main | `BOND-001`, `BOND-EXC-001` | `choice-curated-facts.json`(1), `general-choice-facts.json`(4), `structure-periodic-facts.json`(1) | rules+facts |
| `KH-ST-04` | 分子空间构型与杂化 | Structure | Main | — | — | neither |
| `KH-ST-05` | 分子间作用力与氢键 | Structure | Main | — | — | neither |
| `KH-ST-06` | 晶体类型与晶胞计算 | Structure | Main | — | — | neither |
| `KH-ST-07` | 配合物 | Structure | Extended | — | `choice-curated-facts.json`(1), `general-choice-facts.json`(2) | facts-only |
| `KH-ST-08` | 波谱简单判读（红外、核磁） | Structure | Extended | — | — | neither |
| `KH-HT-01` | 手性碳与对映异构 | Organic | Extended | — | — | neither |

