# 匹配侧术语 alias（官能团口径别名）实施计划

## 1. 背景与研究结论

### 现状
- 判分匹配在 [GatewayVsLlmEvaluationService.MatchMolecule](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L532)：把 `MoleculeAnalyzer` 输出的每个官能团名与 `c.Answer` 做规范化**子串包含**匹配，任一命中即采纳。
- 引擎只发**规范名**（如「酮羰基」「卤原子」），但高考答案键常写**同义别名**：
  - 「羰基」← 酮羰基（如 pdf-322：「酮羰基(或羰基)」）
  - 「碳溴键 / 溴原子」「碳氯键 / 氯原子」… ← 卤原子（pdf-322：「碳溴键(或溴原子)」「碳氯键(或氯原子)」）
- 结果：单物质题若答案只写别名（不含规范名），引擎不命中 → miss。

### 全库 alias 需求面扫描（只读，见 [scan_alias_surface.py](computer://d:/github/CogniThink-Chem/_evalscratch/scan_alias_surface.py)）
- **A) 有 molecule 资产且答案含 alias 术语的题：0 个** → 当前分子路由不触发 alias 需求。
- **B) 全库答案含 alias 术语的仅 pdf-322**，且它 **无 molecule 资产**（路由根本不触发）。
- **C) 负向语境风险（「不含/非/无 + alias 术语」）：0**。
- **D) 对 44 个资产题做 alias 翻转仿真：0 翻转** → alias 改动对当前银行 **LOST=0、FP=0**（已实证）。

### 机制有效性受控演示（见 [demo_alias_mechanism.py](computer://d:/github/CogniThink-Chem/_evalscratch/demo_alias_mechanism.py)）
| 分子 | 答案 | 当前（仅规范名） | 加 alias 后 |
|---|---|---|---|
| 溴乙烷 `CCBr` | 碳溴键 | miss（引擎出卤原子） | **hit**（出 碳溴键/溴原子） |
| 丙酮 `CC(=O)C` | 羰基 | miss（引擎出酮羰基） | **hit**（出 羰基/酮羰基） |
| 溴乙烷 | 卤原子 | hit | hit（不回归） |
| 丙酮 | 酮羰基 | hit | hit（不回归） |

## 2. 设计决策（关键，审批请确认）
### 2.1 落点：**分析器输出侧追加 alias 名，不改 `MatchMolecule` 度量**
- 理由（对齐匹配漂移经验 [100023167]）：子串包含度量保持稳定、可解释；alias 做**插入式增量**，不替换度量、不调阈值，避免误匹配通道迁移。
- alias 名都是**化学真值**（分子确有 C=O / 确有 C–Br），不是臆造。
- 单调性：只新增可匹配名 → 已解题旧命中项仍在 → **LOST 构造性为 0**（仿真已实证）。

### 2.2 实现形式：把 alias 作为独立条目加入共享词表 `FunctionalGroupVocabulary.InScope`
- 与既有「酰胺基 / 肽键」共享同一 SMARTS 的先例完全一致（一条 SMARTS 可对应多个显示名）。
- `MoleculeAnalyzer` 与 `MoleculeValidator` 共用，漂移守护测试自动覆盖，口径继续统一。
- `MatchMolecule` 不动。

### 2.3 拟加入的 alias 条目（每条均带独立 SMARTS）
| 新增显示名 | SMARTS | 覆盖的答案口径 | 依据 |
|---|---|---|---|
| 羰基 | `[CX3](=O)([#6])[#6]`（与酮羰基同） | 答案写「羰基」而未写「酮羰基」 | pdf-322「酮羰基(或羰基)」 |
| 溴原子 / 碳溴键 | `[Br][#6]` | 「溴原子」「碳溴键」 | pdf-322「碳溴键(或溴原子)」 |
| 氯原子 / 碳氯键 | `[Cl][#6]` | 「氯原子」「碳氯键」 | pdf-322「碳氯键(或氯原子)」 |
| 氟原子 / 碳氟键 | `[F][#6]` | 「氟原子」「碳氟键」 | 高中常见，对称完备 |
| 碘原子 / 碳碘键 | `[I][#6]` | 「碘原子」「碳碘键」 | 对称完备 |
- 保留原「卤原子」条目（旧口径答案仍可用）。
- **「羰基」仅由酮羰基触发**，不扩到醛基/羧基/酯基——保守、有证据，避免过度泛化；未来确有需求再扩。
- **「卤素」泛称暂不加**（无证据），留待后续。

## 3. 诚实预期：当前银行 eval 得分 +0
- alias 需求面（pdf-322）当前无 molecule 资产 → 路由不触发 → 本改动单独不产生 eval 收益。
- 价值：① 补全口径完备性（单物质题答案只写别名时能命中）；② 为后续 curate pdf-322 等真题资产铺路；③ 与词表扩展共同构成「分子资产 curate 即能命中」的完整前提。
- 红线：precision=1、harm=0、LOST=0、mp27 不退化（仿真已实证，实施后全库 eval 复核）。

## 4. 涉及文件
- `src/CogniThink.Chem.Core/Molecules/FunctionalGroupVocabulary.cs`：在 `InScope` 表追加 9 条 alias（羰基×1 + 卤素原子/键×8）。
- 不新建文件、不改 `MatchMolecule`、不改 `data/`、不碰 `images/`。
- 测试：
  - `tests/CogniThink.Chem.Tests/MoleculeAnalyzerTests.cs`：新增 alias Theory（命中正例 + 不回归负例）。
  - 现有 `MoleculeTests.Analyzer与Validator_官能团集合一致` 漂移守护自动覆盖（两表仍同源）。

## 5. 实施步骤
1. 改 `FunctionalGroupVocabulary.InScope`：追加 9 条 alias（SMARTS 如上）。
2. 编译 `dotnet build` 0 错误。
3. 加单元测试：
   - 溴乙烷 `CCBr` → 含 卤原子/溴原子/碳溴键；不含 氯原子。
   - 丙酮 `CC(=O)C` → 含 酮羰基/羰基；不含 醛基。
   - 溴苯 `Brc1ccccc1` → 含 卤原子/溴原子/碳溴键/苯环。
   - 不回归：乙酸乙酯（酯基，不含 羰基/酮羰基——注意：羰基 alias 仅酮羰基触发，酯不触发，故乙酸乙酯不含「羰基」）；乙醛（醛基，不含「羰基」——确认保守边界）。
4. `dotnet test` 全绿。
5. 全库零假阳性复核：复用 `_evalscratch/scan_alias_surface.py`（指向银行）确认 0 翻转；并跑真实全库 eval tag `t2alias`，diff vs `t2vocab` → 预期 **GAIN=0、LOST=0、harm=0、precision=1**。
6. mp27 复核 `verify_mp27.py t2vocab t2alias` → 27/27。
7. 汇报。

## 6. 验证清单
- [ ] 9 条 alias 落入共享词表，两分析器同源（漂移守护绿）。
- [ ] 新增单元测试全绿（命中 + 不回归 + 保守边界：酯/醛不触发「羰基」）。
- [ ] 全库仿真翻转 = 0；eval diff GAIN=0 / LOST=0 / harm=0。
- [ ] mp27 27/27；`dotnet test` 全绿。

## 7. 风险与应对
- **风险**：新增「羰基」「碳溴键」等名导致某题误命中。**应对**：全库 44 资产题仿真 0 翻转；负向语境全库 0；实施后 eval diff 暴露任何新增 True 必逐一人工核对；别名均为化学真值，非凭空生成。
- **风险**：「羰基」由酮羰基触发是否会与答案中「不含羰基」冲突。**应对**：当前银行无此负向语境（已扫），但将该扫描永久保留为护栏；保守地只让酮羰基触发，不泛化到醛/羧/酯。
- **风险**：用户期望 alias 立即涨分。**应对**：第 3 节已诚实声明 +0，审批即确认知悉；涨分需搭配后续 pdf-322 资产 curate。
