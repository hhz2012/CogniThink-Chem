# Spec — P3-2 规则/事实体系归一（明确分工 + 交叉引用）

> 路线图：[project-structure-analysis-and-roadmap.md](file:///d:/github/CogniThink-Chem/.trae/documents/project-structure-analysis-and-roadmap.md) §P3-2 + §6#1。
> 基线：`after_p31`（757 题，635 正确，precision=1，harm=0，LOST=0，WORSEN=0）。红线锚点 `base_p0`（629 正确）。
> 红线：precision=1 / harm=0 / LOST=0 / WORSEN=0 / 不蒙数 / 事实只存 JSON。破线即回退。
> **诚实口径：预期 eval 正确性增益 = 0**。本项为纯文档 + 只读工具 + `ARCHITECTURE.md` 更新，**不改任何运行时数据（data/** JSON）、不改任何 .cs、不进入 eval 管线** → 必为零漂移。目标是消除「两套规则」的认知负担，不是提分解题。

---

## 1. 背景：两套规则/事实体系的真实关系（T0 只读审计，带证据）

### 1.1 体系 A — 旧声明式规则库 `data/rules/rules.json`
- **61 条** if-then 规则，schema：`{id, name, layer, knowledgeId, preconditions, conclusion, gradeScope, evidence, source, conflictGroup, tests}`。
- 分层：Conservation 3 / Structural 38 / Principle 14 / Conditional 3 / Exception 3。
- **61/61 带 `knowledgeId` 外键**（指向 `data/knowledge/knowledge.json` 的 KP id）；仅 7 条带 `tests`；`data/rules/rule-tests.json` 9 条回归用例。
- **消费者：仅 Core 栈**（[ChemDataLoader.LoadRules](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Data/ChemDataLoader.cs#L51-L52)）：
  - 推理引擎按 rule-id 取用：[ExperimentEngine](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Inference/ExperimentEngine.cs#L24)（EXP-*）、[ProcessFlowEngine](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Inference/ProcessFlowEngine.cs#L44)、[AqueousEquilibriumEngine](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Inference/AqueousEquilibriumEngine.cs#L1064)、[ElectrochemEngine](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Inference/ElectrochemEngine.cs#L549)、EquilibriumEngine、[WorldModel](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/WorldModel/WorldModel.cs#L62)。
  - 仲裁：[RuleEngine.Decide](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Rules/RuleEngine.cs) 仅被 [RuleRegressionRunner](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Evaluation/RuleRegressionRunner.cs#L88) + 单元测试 [KnowledgeAndRuleTests](file:///d:/github/CogniThink-Chem/tests/CogniThink.Chem.Tests/KnowledgeAndRuleTests.cs) 调用。
  - 校验：[RuleSetValidator](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Rules/RuleSetValidator.cs)（测试）。
- **网关（757 eval 主链路）从不加载 rules.json，也不调用 RuleEngine.Decide**（全仓 grep `rules.json` / `RuleEngine` / `.Decide(` 在 Api 网关 0 命中）。

### 1.2 体系 B — 网关事实库 `data/highschool/*.json`（24 文件）
- 扁平事实集合（schema 各异：statements / entries / rules / table / series / conditions 等），用 `match`/`stemMatch` 支撑网关选项键匹配。
- **消费者：两栈都用**：
  - 网关 15 处 [RuleStore.Load().Query<T>(key)](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Rules/RuleStore.cs#L32-L68)：[FactChoiceMatcher](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/FactChoiceMatcher.cs#L106-L123)、ChoiceVerifier 及 10+ validator。
  - Core 读取子集：acid-constants / ions / phenomena / solubility / activity-series / electrolytes / substances（SolubilityTable、ElectrochemEngine、WhitelistGuard、IonizationMap、ElectrolyteRules、AqueousEquilibriumEngine、Experiment/ProcessFlow/Identification 引擎）。
- 24 文件**全部至少有一个消费者**（无完全死数据；`double-decomposition` 仅被 [HighSchoolRulesTests](file:///d:/github/CogniThink-Chem/tests/CogniThink.Chem.Tests/HighSchoolRulesTests.cs#L25) 守护）。
- 仅 `phenomena.json` 带 `knowledgeId` 字段；其余 highschool 文件**无 knowledgeId 外键**，交叉引用须靠名称候选匹配（同 P3-1 gap_report 双口径）。

### 1.3 重叠、外键与缺口（实测）
- **外键健康**：rules.json 61 条规则的 26 个 distinct `knowledgeId` **全部有效**（0 dangling FK，与 knowledge.json 50 KP 比对实测）。
- **覆盖缺口**：50 KP 中 **24 个无任何 rules.json 规则**（含 `KH-RP-09 电解池`，即 P3-1 gap_report 标出的 strong candidate：无事实、13 题）。这些域由 highschool 事实覆盖，或仍是内容缺口（属后续策展 spec，不在本项修复）。
- **认知负担根源**：同一化学域（氧化还原/活动性、平衡、溶解、离子/电解质、转化）在两体系用**不同 schema** 编码——rules.json 的 if-then+conflictGroup 服务 Core 仲裁，highschool 的扁平 match 服务网关匹配。两者并非重复冗余，而是**同域不同角色**，但缺少一张「谁负责什么」的统一视图。

### 1.4 三文件名跨根碰撞（P3-1 已标注，本项明确分工）
- `ions.json` / `substances.json` / `solubility.json` 在 `data/highschool/`（根 A，Core RuleStore）与 `src/CogniThink.TutorBrain.Api/data/chemistry/`（根 B，Api ChemicalKnowledgeBase）各有一份，schema 与消费者均不同。本项在 ownership matrix 中逐根标注归属，**不合并**。

---

## 2. 已锁定决策（用户确认，2026-11-22）

| CQ | 问题 | 决策 | 理由 |
|----|------|------|------|
| CQ-1 | §6#1 两套栈收敛方向 | **a 确认推荐方向：网关为主求解入口、Core ProblemSolver 退为基准回归引擎** | 用户确认；与 ARCHITECTURE.md §3.2 推荐一致。本项把该决策从「待确认」升级为「已确认」并落位。 |
| CQ-2 | P3-2 归一策略 | **a 明确分工 + 交叉引用（不改数据/代码，零 eval 漂移）** | 用户确认；审计显示两体系各有活跃消费、无完全死数据，合并 schema 风险高（牵动 Core 98 基准 + 27 真题 + 网关 757 eval），易破红线。 |
| CQ-3 | 运行时数据 | **零 data/** JSON 改动、零 .cs 改动、零 eval 管线改动（默认红线）** | 诚实口径：纯文档 + 只读工具。 |

---

## 3. 范围与产出

### 3.1 所有权矩阵生成器（只读，maintained）
新建 `tools/data-assets/rule_fact_ownership.py` → 生成（tracked）：
- `rule-fact-ownership.md`：人读所有权矩阵。覆盖 **26 个规则/事实资产**（rules.json、rule-tests.json、24 个 highschool 文件），每行：资产 / `owner_stack`（Core / Gateway / Both）/ `role`（仲裁规则 / 事实陈述 / 查找表 / 回归用例）/ 消费者证据（file:line）/ 所属化学域。三碰撞文件逐根分两行标注。
- `rule-fact-crossref.json`：机读交叉引用。对 knowledge.json 全部 50 KP，输出：
  - `rules`：引用该 KP 的 rules.json rule id 列表（**精确 knowledgeId 匹配**，非候选）。
  - `facts`：覆盖该 KP 的 highschool 文件列表（**名称候选匹配**，标注 `match=candidate`，同 P3-1 gap_report 的 STRICT/LENIENT 双口径，LENIENT 仅作上下文）。
  - `coverage_label`：`rules+facts` / `rules-only` / `facts-only` / `neither`。
  - 顶层 `dangling_rule_fks`（实测应为 `[]`）与 `kps_without_rules`（实测 24 个，列 id）。
- **只读**打开 `data/**`（`r`/`rb`）；产物只写 `tools/data-assets/`；不含化学真值（纯读取/外键查表/名称匹配）。
- 与 P3-1 关系：不替代 `gap_report.md`（那是 KP×事实×题的缺口视图）；本项是 KP×规则(rules.json)×事实(highschool) + 所有权视图，互补。

### 3.2 ARCHITECTURE.md 更新（文档，零运行时影响）
- [ARCHITECTURE.md](file:///d:/github/CogniThink-Chem/ARCHITECTURE.md) §3.2：把收敛决策标注从「推荐，待确认」改为「**已确认（2026-11-22，用户确认 §6#1）**」。
- 新增「规则/事实体系分工」小节：声明
  - `data/rules/rules.json` = Core 仲裁/基准回归规则库（if-then + conflictGroup），网关不消费；
  - `data/highschool/*` = 网关事实库（扁平 match），Core 复用子集；
  - 两体系为「同域不同角色」非冗余；新增能力的事实/规则落位遵循 §3.2 收敛纪律；
  - 链接到 `rule-fact-ownership.md` 作为 maintained 所有权视图。
- 三碰撞文件分工在此小节明确（不合并）。

### 3.3 台账闭环
[data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md) 新增 §S（插入于 §R 末尾 `---` 与 `## 处理原则` 之间）：
- S.1 触及文件清单（content attestation：仅 `tools/data-assets/rule_fact_ownership.py` + 生成产物 + `ARCHITECTURE.md` + `_evalscratch/verify/p32_self_check.py` + 台账本身；**无 data/** JSON 改动、无运行时 .cs 改动**）。
- S.2 所有权矩阵产物（26 资产覆盖 + 三碰撞逐根标注）。
- S.3 交叉引用产物（50 KP × 规则精确 FK × 事实候选匹配；0 dangling FK；24 KP 无规则）。
- S.4 ARCHITECTURE.md §6#1 已确认 + 分工小节。
- S.5 eval 零漂移（after_p31→after_p32 diff 全 0）。

### 3.4 全量 eval + 红线 diff
- `run_eval_resume.ps1 -Tag after_p32 -Env Production` → 预期 757/757 failed=0，correct=635（与 after_p31 逐 case 相同，因无运行时改动）。
- `diff_ckpt after_p31 after_p32`：GAINED/LOST/WORSEN/harm 全 0 → RED-LINE PASS。
- `diff_ckpt base_p0 after_p32`：LOST/WORSEN/harm=0（GAINED 维持 6 携带）。

### 3.5 红线自检 + 独立 review
- `_evalscratch/verify/p32_self_check.py`：密钥 0 命中 / 52 JSON sha256 与 after_p31 基线（`p31_data_baseline.json`）全一致 / 无运行时 .cs 在触及清单 / 产物不被运行时 .cs 引用 / 两次 diff RED-LINE PASS / 生成器幂等 / not 蒙数（correct=635, harm=0, total=757）。
- fresh-context 只读子代理独立 review → `review.md`，裁决 PASS/FAIL。

---

## 4. 验收标准（AC）

| ID | 标准 | 证据 |
|----|------|------|
| AC-1 | 所有权矩阵覆盖全部 26 个规则/事实资产，每项含 owner_stack / role / 消费者证据（file:line）；三碰撞文件逐根标注 | `rule-fact-ownership.md` 渲染；行数与资产数核对 |
| AC-2 | 交叉引用覆盖全部 50 KP；rules 侧精确 knowledgeId（0 dangling），facts 侧名称候选并标注候选性质；输出 coverage_label + 24 个无规则 KP 清单 | `rule-fact-crossref.json` 可解析；`kps_without_rules` 长度=24 |
| AC-3 | ARCHITECTURE.md §3.2 标 §6#1 已确认；新增「规则/事实体系分工」小节并链接 ownership 矩阵 | 文档直读 |
| AC-4 | 零运行时改动：52 JSON sha256 与 after_p31 基线全一致；无 .cs 改动；eval after_p31→after_p32 diff 全 0；base_p0→after_p32 RED-LINE PASS | `p32_self_check.py` + diff_ckpt |
| AC-5 | 产物（ownership/crossref 及生成器）不被任何运行时 .cs 引用；生成器幂等；密钥 0 命中 | grep 0 命中；连跑两次 manifest 等价 |
| AC-6 | 台账 §S 自洽（触及清单与实际一致；零数据/代码改动 attestation 与实测吻合） | 台账直读 + 自检 |

任一 AC 不满足 → 停下修复 → 复审。**不满足即不闭合。**

---

## 5. 非目标（明确不做）

- **不合并** rules.json 与 highschool 为单一 schema（高风险，牵动双栈回归与 eval）。
- **不迁移**任何数据文件、不改 rules.json / highschool 内容（包括不修复 24 个「无规则 KP」——那是内容策展，需独立 spec 评估 eval 影响）。
- **不改任何 .cs**：不动 RuleEngine / RuleStore / 网关 validator / Core 引擎。
- **不新增**网关或 Core 的求解行为；eval 正确性预期零变化。
- 不替代 P3-1 的 `gap_report.md`（互补视图）。

---

## 6. 风险与取舍

- **漂移风险 = 极低**：触及面仅文档 + 只读工具。唯一需防的是「声称零改动但实际误改」——由 `p32_self_check.py` 逐文件 sha256 比对 after_p31 基线 + 两次 diff_ckpt 双重铁证兜底。
- **文档措辞风险**：ARCHITECTURE.md 分工描述须与审计证据一致，否则误导。以本 spec §1 审计事实为准，子代理 review 复核。
- **交叉引用 facts 侧为候选匹配**：沿用 P3-1 已验证的 STRICT/LENIENT 双口径并明确标注「候选·需人工复核」，不宣称确定性，避免 蒙数。
