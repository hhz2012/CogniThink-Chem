# MoleculeAnalyzer 官能团词表扩展 实施计划

## 1. 背景与研究结论

### 现状
- 判分链路（[GatewayVsLlmEvaluationService.cs:532](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L532) `MatchMolecule`）：`molecule` 路由把 `MoleculeAnalyzer` 识别的官能团名与 `c.Answer` 做规范化子串匹配，**任一命中即采纳**。
- 词表实际有 **两套且已不一致**：
  - [MoleculeAnalyzer.GroupSmarts](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Molecules/MoleculeAnalyzer.cs#L73-L87)（12 类，**eval 实际走的**）——无「酰胺基/肽键」、无「硫醚」；
  - [MoleculeValidator.WhitelistGroups](file:///d:/github/CogniThink.Chem.Core/Molecules/MoleculeValidator.cs#L48-L63)（13 类，含「肽键」但无硫醚，醚键/氨基 SMARTS 同样偏窄）——供 `ChemicalCheckerService` 用，非 eval 路径。
  - `MoleculeAnalyzer` 注释自称「与 MoleculeValidator 白名单一致」，实际不一致 → 存在口径漂移。

### 实测暴露的 4 个盲区（证据 [track2_dryfire.txt](computer://d:/github/CogniThink-Chem/_evalscratch/track2_dryfire.txt)、[track2_dryfire_conclusion.md](computer://d:/github/CogniThink-Chem/_evalscratch/track2_dryfire_conclusion.md)）
| 盲区 | 现状（错误） | 影响 |
|---|---|---|
| 缺「酰胺基」 | 乙酰胺 `CC(N)=O` 的 -NH₂ 被 `[NX3H2][#6]` **误标为「氨基」** | pdf-322(1) 漏；且是**潜在假阳性**（含酰胺的分子若答案含「氨基」会误命中） |
| 缺「硫醚」 | 词表无 R–S–R | pdf-519（脏数据） |
| 「醚键」漏芳基醚 | `[OD2]([CX4])[CX4]` 要求两侧碳均 sp³；苯甲醚 `COc1ccccc1` 不识别 | pdf-322(1)、pdf-351/361 输出缺项 |
| 「氨基」只认 -NH₂ | `[NX3H2]` 漏仲/叔胺；二甲胺 `CNC`、哌嗪 N 不识别 | pdf-322(4) |

### 拟定 SMARTS（已用 Python rdkit 2026.09.2 对 20 个用例只读校验，**0 失败**——见 [validate_smarts_proposal.py](computer://d:/github/CogniThink-Chem/_evalscratch/validate_smarts_proposal.py)）
- 酰胺基 / 肽键（同一 SMARTS，发两个名以覆盖两种答案口径）：`[CX3](=O)[NX3]`
- 硫醚：`[SX2]([#6])[#6]`（二价硫、两邻位均碳 → 自动排除硫醇 `[SX2H]`、二硫 S–S、亚砜/砜）
- 醚键（扩）：`[OD2;!$([OD2][CX3]=O)]([#6])[#6]`（允许 sp²/芳基碳，负向排除酯的烷氧基氧；`[#6]` 不含 H → 自动排除醇/酚/羧基的 O–H）
- 氨基（扩）：`[NX3;!$([NX3][CX3]=O);!$([NX3]~O)][#6]`（覆盖伯/仲/叔胺；负向排除酰胺 N 与硝基 N）

校验覆盖的无假阳性断言：硫醇/二硫 ≠ 硫醚；酯 ≠ 醚；酚/醇 ≠ 醚；硝基 N ≠ 氨基；酰胺 N ≠ 氨基（修掉误标）；现有 pin（甘氨酸氨基、乙醚醚键、乙酸乙酯酯基非醚键、pdf-323）全部不回归。

### 全库零假阳性预检（实施前，只读——见 [prefp_scan_vocab.py](computer://d:/github/CogniThink-Chem/_evalscratch/prefp_scan_vocab.py)）
对银行内 **44 个 molecule 资产题** 用旧/新词表各跑一遍子串匹配：**regress=0、gain=0、FP=0**。3 题（pdf-351/353/361）官能团输出集新增了化学正确的项（醚键/氨基/酰胺基），但匹配状态不变 → 证明本次改动是**输出纠错**，对当前银行不翻已解题、不增误命中。

### 诚实预期：本次改动单独不产生 eval 得分收益
银行里答案含「酰胺基/肽键/硫醚」的题只有 **pdf-322、pdf-519**，但二者**当前都没有 molecule 资产**（分子路由根本不触发）。因此词表扩展是：
1. **纠错**（修掉酰胺 N 误标氨基的潜在假阳性、补全芳基醚/仲胺/硫醚的正确输出）；
2. **铺路**（让未来为 pdf-322 / pdf-519 / 同类真题 curate 分子资产时能命中）；
3. **消除 Analyzer / Validator 口径漂移**。

> 把词表扩展转化为实际 eval +分，需要另行为 pdf-322 等 curate 分子结构资产（且 pdf-322 是 6 子结构复合题，涉及多分子资产/命名，工作量较大）——**不在本计划范围**，待本计划落地后单独立项。

## 2. 范围界定（审批时请确认）
- ✅ **本计划做**：`MoleculeAnalyzer` 词表/SMARTS 扩展 + 与 `MoleculeValidator` 口径统一（抽共享表）+ 单元测试 + 全库零假阳性回归 + 全量 eval/mp27/测试复核。
- ❌ **本计划不做**（留作后续独立计划）：
  - 匹配侧术语 alias（答案写「羰基」但引擎出「酮羰基」、答案写「碳溴键」但引擎出「卤原子」）——改 `MatchMolecule` 逻辑，假阳性风险更高，需单独 FP 扫描；
  - 为 pdf-322 / pdf-519 curate 分子资产；
  - `MoleculeValidator` 的「S 元素标超纲」策略调整（保持现状，仅让其可识别硫醚）。

## 3. 涉及文件与改动
- `src/CogniThink.Chem.Core/Molecules/MoleculeAnalyzer.cs`：把 `GroupSmarts` 改为引用共享词表（见下）；其余特征计算不动。
- `src/CogniThink.Chem.Core/Molecules/MoleculeValidator.cs`：把 `WhitelistGroups` 改为引用同一共享词表；保留 `OutOfScopeGroups` 与元素白名单不动。
- 新增 `src/CogniThink.Chem.Core/Molecules/FunctionalGroupVocabulary.cs`：单一真相源 `public static readonly (string Name, string Smarts)[] InScope = {…}`，含本计划的 15 条（原 12 − 偏窄 2 + 新 3 + 扩 2，去重后 15 条：苯环/双键/三键/羟基/羧基/醛基/酮羰基/酯基/醚键(扩)/氨基(扩)/酰胺基/肽键/硫醚/硝基/卤原子）。
- `tests/CogniThink.Chem.Tests/MoleculeAnalyzerTests.cs`：新增「扩词表」Theory（酰胺/肽键、硫醚、芳基醚、仲/叔胺命中；硫醇/二硫/酯/酚/硝基 N 不误中；酰胺 N 不再误标氨基）。
- `tests/CogniThink.Chem.Tests/MoleculeTests.cs`：新增一条「Analyzer 与 Validator 课内词表完全一致」漂移守护测试（取两表 name 集合断言相等）；现有 `PeptideBond_Recognized` 等保持绿。
- 不碰 `data/`、`src/CogniThink.TutorBrain.Api/` 判分逻辑、`images/` 原始图。

## 4. 实施步骤（依赖序）
1. **新建 `FunctionalGroupVocabulary.cs`**：落共享 `InScope` 表（含拟定 SMARTS）。
2. **改 `MoleculeAnalyzer`**：`GroupSmarts` → 引用 `FunctionalGroupVocabulary.InScope`；编译通过。
3. **改 `MoleculeValidator`**：`WhitelistGroups` → 引用同一表；确认 `PeptideBond_Recognized` 等现有用例语义不变。
4. **加单元测试**（先写断言再跑红→绿，或写完即跑）：覆盖第 3 节所列正/负例；加漂移守护。
5. **本地零假阳性扫描**：复用 `_evalscratch/prefp_scan_vocab.py`（指向银行），确认 regress=0 / FP=0；如出现意外 GROUP-CHANGE 导致匹配翻转，停下分析。
6. **全量测试**：`dotnet test CogniThink.Chem.sln`，预期 2623 全绿（新增用例并入）。
7. **全库 eval**：新 tag（如 `t2vocab`），无探针环境变量；diff vs `t2curate2`，**预期 GAIN=0、LOST=0、harm=0、precision=1**。
8. **mp27 复核**：`verify_mp27.py t2curate2 t2vocab`，预期 27/27。
9. **汇报**：诚实说明得分 0 变动但输出纠错 + 漂移消除 + 铺路完成；列后续可立项项。

## 5. 依赖与注意事项
- **单调性构造**：除「酰胺 N 不再误标氨基」是移除一个误报外，其余均为新增检测 → 对「已解题因命中某官能团而正确」的情形，旧命中项仍在，LOST 构造性为 0（预检已实证 regress=0）。
- **SMARTS 由 RDKit C# SWIG（GraphMolWrap）执行**，与 Python rdkit 同一内核；Python 预检已验证等价，但实施后仍以 C# 端 `hasSubstructMatch` 实测为准（单元测试即此）。
- **不臆造**：所有新增检测都是 RDKit 子结构真值，不产生新数值；`MoleculeName`/极性等其他维度不动。
- **`MoleculeValidator` 影响面**：仅 `ChemicalCheckerService` 使用，不改其超纲判定策略，现有测试全绿即安全。

## 6. 验证清单（完成标准）
- [ ] `FunctionalGroupVocabulary.InScope` 被 Analyzer 与 Validator 共用，name 集合一致（漂移守护测试绿）。
- [ ] 新增单元测试全绿：4 类新增/扩展命中 + 6 类负例（硫醇/二硫/酯/酚/硝基N/酰胺N 不误中）+ 现有 pin 不回归。
- [ ] 全库预检：regress=0、FP=0。
- [ ] `dotnet test`：2623 + N 全绿，0 失败。
- [ ] eval diff vs `t2curate2`：GAIN=0、LOST=0、harm=0、precision=1。
- [ ] mp27：27/27 PASS。

## 7. 风险与应对
- **风险**：扩展「氨基」后，某分子的 N 被新识别为氨基，而答案键恰好含「氨基」但题意并非问该分子氨基 → 假阳性。**应对**：全库预检已扫 44 个资产题 0 FP；实施后 eval diff 会暴露任何新增 True，逐一人工核对；负向语境（「不含…」）全库当前为 0（见 [scan_vocab_gap.py](computer://d:/github/CogniThink-Chem/_evalscratch/scan_vocab_gap.py) §4），但永久保留该扫描为护栏。
- **风险**：共享表重构引入编译/引用错误。**应对**：步骤 2/3 各步后即编译；改动面小（仅字段引用替换）。
- **风险**：用户期望「扩词表 = 涨分」，但实际 +0。**应对**：本计划第 1 节已诚实声明，审批即确认知悉；涨分需搭配后续资产 curate 立项。
