# 多分子路由聚合 - Implementation Plan

## Task 1: 改造 TryRouteMolecule 为全资产分析 + 特征聚合
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None
- **Completion Evidence**:
  - `LlmGatewayService.Molecule.cs` 编译通过（`dotnet build` 0 错误）。`TryRouteMolecule` 现遍历全部 `type=="molecule"` 资产，新增 `AnalyzeMoleculeAsset` / `BuildMoleculeResponse` / `BuildAggregatedMoleculeResponse` 三个私有方法；单成功资产走 `BuildMoleculeResponse`（与旧行为逐字段一致），多成功资产走聚合（FG 并集去重保序、名称 `；` 拼接、结论 `(n)` 分块）；全部失败返回 null。
  - 判分代码未改（`MatchMolecule` OR 不变），`AssetKind` 保持 `"molecule"`。
- **Description**:
  - 修改 [LlmGatewayService.Molecule.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.Molecule.cs) 的 `TryRouteMolecule`：通过 choice 守卫后，遍历**全部** `type=="molecule"` 的 `imageAssets`（替换当前 `FirstOrDefault`），逐资产解析 payload（smiles/molblock）并调用 `MoleculeAnalyzer.Analyze`。
  - 聚合成功解析的资产结果到单个 `EngineResponse`：
    - `MoleculeFunctionalGroups` = 各资产 `FunctionalGroups` 的并集（去重、保留首次出现顺序）。
    - `MoleculeName` = 各资产非空 `MoleculeName` 以 `；` 拼接。
    - `FinalResult` = 各资产 `FinalResult` 按资产顺序分块合并（每块前加 `(n)` 序号 + 分子式前缀，便于阅读；不影响判分）。
    - `MoleculeFormula` / `MoleculeUnsaturation` / `MoleculeIsPolar` / `MoleculeCentralHybridization`：单资产时取该资产值；多资产时取首个成功资产值（判分 OR 已由并集 FG/Name 覆盖，数值字段保持保守不为多分子杜撰合并值）。
  - 全部资产解析失败 → 返回 `null`（如实回退，不臆造）；单个资产时行为与旧代码逐字段一致。
  - 不改判分代码（`MatchMolecule` / `IsStructuredCorrect`），`AssetKind` 保持 `"molecule"`。
- **Acceptance Criteria Addressed**: AC-1, AC-2, AC-5, AC-7
- **Test Requirements**:
  - `rule` TR-1.1: 构造 2 个不同分子资产（如 曲美托嗪 + 2,4,6-三溴苯酚）调用路由，断言响应 `MoleculeFunctionalGroups` 同时含两分子各自 FG（并集）。证据：`dotnet test` 中新增测试通过。
  - `rule` TR-1.2: 构造两分子 FG 重叠（如都含"苯环"），断言并集去重且保序。证据：新增测试通过。
  - `rule` TR-1.3: 构造 1 个坏 SMILES 资产 + 1 个好资产，断言响应仅含好资产特征；构造 2 个坏资产，断言返回 `null`。证据：新增测试通过。
- **Notes**: 可提取私有方法 `AnalyzeOneAsset` / `AggregateMoleculeResults` 保持可读性；`MoleculeAnalyzer.Analyze` 无状态，逐次调用安全。

## Task 2: 补充/维护单元测试，确保单资产行为不退化
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1
- **Completion Evidence**:
  - `MoleculeGatewayTests.cs` 新增 4 个测试：`多分子资产_聚合官能团并集去重保序`（TR-1.1/TR-1.2：并集/去重/保序/名称拼接/结论分块）、`多分子资产_部分失败_仅成功资产贡献`（TR-1.3）、`多分子资产_全部失败_回退null`（TR-1.3）、`单分子资产_不进入聚合分支`（TR-2.2/AC-3）。
  - 全量测试通过：`dotnet test CogniThink.Chem.sln` → 1699 + 957 = 2656 通过，0 失败（基线 2652 + 新增 4）。现有 choice 守卫、单分子特征、结构化判分测试均保持通过（AC-3/AC-4/AC-5）。
- **Description**:
  - 在 [MoleculeGatewayTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/MoleculeGatewayTests.cs) 新增多资产聚合测试（见 TR-1.1/1.2/1.3）。
  - 新增"单资产 == 旧行为"对比测试：对同一单资产请求，断言改动后响应关键字段（`AssetKind`/`MoleculeFunctionalGroups`/`MoleculeName`/`FinalResult` 含关键特征）与预期一致，不退化。
  - 确认现有 `MoleculeGatewayTests` / `StructuredScoringTests` 全部保持通过（choice 守卫、单分子特征、结构化判分 OR 语义不变）。
- **Acceptance Criteria Addressed**: AC-3, AC-4, AC-5
- **Test Requirements**:
  - `rule` TR-2.1: 现有全部 molecule 路由与结构化判分测试通过（`dotnet test` 退出码 0）。证据：测试运行输出。
  - `rule` TR-2.2: 单资产请求的响应特征字段非退化（含分子式/官能团/杂化等关键特征）。证据：新增对比测试通过。
  - `rule` TR-2.3: choice 题（含多分子资产）路由返回 `null`，不消费分子资产。证据：现有/新增 choice 守卫测试通过。
- **Notes**: 测试数据可用 `MakeMoleculeAsset` 内联构造，避免依赖银行具体资产顺序；如需真实资产，给 `LoadMoleculeAsset` 增加按索引加载的重载（不改生产数据）。

## Task 3: 全库零回归验证 + 结论覆盖完整性核验
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1, Task 2
- **Completion Evidence**:
  - `rule` TR-3.1（diff 零回归）：`diff_eval.ps1 -OldTag t2curate3 -NewTag t2route` → `GAINED (0)`、`LOST (0)`，无任何 `baseFalse→True` 或 `baseTrue→False` 翻转。
  - `rule` TR-3.2（mp27 守卫）：`verify_mp27.py t2curate3 t2route` → `RESULT: PASS (27/27 multi-part solved cases retained gatewayCorrect=True)`，`REGRESSIONS (True->False): []`。
  - `rule` TR-3.3（全量测试）：`dotnet test CogniThink.Chem.sln` → 1699 + 957 = **2656 通过，0 失败**。
  - `rubric` TR-3.4（结论覆盖完整性，score 5/5 ≥ 4）：抓取 `snapshot_t2route.json`，pdf-322 `gatewayConclusion` 含 `(1)`–`(6)` 六个分子分块，覆盖醚键/酰胺基、碳碳双键/酮羰基/碳溴键、酯基/硝基/碳卤键、氨基/硝基/酮羰基、2,4,6-三溴苯酚、邻苯二酚；pdf-199 覆盖 3 分子、pdf-208 覆盖 2 分子（改动前均只反映第 1 个）。
  - 汇总指标全等（`cmp_summary.py`）：t2curate3 与 t2route 均为 `total=757 gatewayCorrect=673 harm=0 precision=1 bothWrong=84 molCases=45 molHits=7` → 结论丰富化未触碰任何正确性指标，precision=1 / harm=0 / LOST=0 红线守住。
- **Description**:
  - `dotnet test CogniThink.Chem.sln --nologo -v q` 全绿。
  - 后台跑全库 eval（Production LLM-off）：`powershell -ExecutionPolicy Bypass -File _evalscratch\run_eval_resume.ps1 -Tag t2route -Env Production`，等待 `checkpoint_t2route.done`。
  - diff 基线：`powershell -ExecutionPolicy Bypass -File _evalscratch\diff_eval.ps1 -OldTag t2curate3 -NewTag t2route`，确认 `LOST=0`、`harm=0`、`precision=1`、无意外 `baseFalse→True`（受影响 3 题 pdf-199/208/322 均已 True，预期无翻转）。
  - mp27 守卫：`python _evalscratch\verify_mp27.py t2curate3 t2route`，确认 27/27。
  - 抓取 `snapshot_t2route.json` 中 pdf-322 / pdf-199 / pdf-208 的 `gatewayConclusion`，人工核验结论覆盖各自全部分子资产的官能团/名称（AC-7 rubric，阈值 ≥4）。
- **Acceptance Criteria Addressed**: AC-6, AC-7
- **Test Requirements**:
  - `rule` TR-3.1: `diff_eval.ps1` 输出显示相对 `t2curate3` 的 `LOST=0`、`harm=0`、`precision=1`，且无未核验的新增正确。证据：diff 输出文本。
  - `rule` TR-3.2: `verify_mp27.py t2curate3 t2route` 输出 `27/27`。证据：脚本输出。
  - `rule` TR-3.3: `dotnet test` 全绿（退出码 0）。证据：测试汇总。
  - `rubric` TR-3.4: 维度=多分子结论覆盖完整性；scale 1-5；anchors 1=仅第1分子, 3=部分覆盖, 5=按序全覆盖无缺漏；threshold ≥4；evidence=pdf-322 `gatewayConclusion` 覆盖 6 分子 FG/名称（醚键/酰胺基、碳碳双键/酮羰基、硝基/酯基/碳卤键、氨基/硝基/羰基、2,4,6-三溴苯酚、邻苯二酚）。
- **Notes**: 若 diff 出现任何 `baseFalse→True` 翻转，必须人工核验为合法求解方可接受；任何 `baseTrue→False` 或 harm > 0 即红线破，停止并回退排查。不主动 commit/push，改完留工作区等用户指示。
