# 多分子路由聚合 - Independent Review

- [x] CP-R1: 全部 molecule 资产被分析
  - **Type**: `rule`
  - **Covers**: AC-1, TR-1.1
  - **Evidence**: 独立评审核验代码 [LlmGatewayService.Molecule.cs L26-33](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.Molecule.cs#L26-L33)：`foreach` 遍历全部 `imageAssets`、按 `Type=="molecule"` 过滤并逐个 `AnalyzeMoleculeAsset`，已替换旧 `FirstOrDefault`。`snapshot_t2route.json` 中 pdf-322/199/208 结论分别含 (1)-(6)/(1)-(3)/(1)-(2) 分块，与源资产数 6/3/2 一一对应；基线 `t2curate3` 三题 `blockMarkers=[]`（仅第 1 分子）→ 证明确实消费了全部资产。

- [x] CP-R2: 官能团并集去重保序
  - **Type**: `rule`
  - **Covers**: AC-2, TR-1.2
  - **Evidence**: 代码 [L91-96](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.Molecule.cs#L91-L96) 用 `HashSet(Ordinal)` 去重 + `List` 按迭代顺序追加。测试 `多分子资产_聚合官能团并集去重保序`（[MoleculeGatewayTests.cs L137-164](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/MoleculeGatewayTests.cs#L137-L164)）断言并集/去重/保序/名称拼接/分块；独立重跑 `MoleculeGatewayTests` 过滤集 → 9/9 通过、退出码 0。

- [x] CP-R3: 单分子资产行为逐字段不退化
  - **Type**: `rule`
  - **Covers**: AC-3, TR-2.2
  - **Evidence**: 单成功资产走 [L36-38](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.Molecule.cs#L36-L38) → `BuildMoleculeResponse`（[L69-82](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.Molecule.cs#L69-L82)），字段赋值与旧内联代码逐字段一致（`ConstraintNote` 字面量不变、`FinalResult` 无 `(n)` 前缀）。`(n)` 前缀仅在 `successes.Count>=2` 的聚合分支出现。测试 `单分子资产_不进入聚合分支` 通过。

- [x] CP-R4: 选择题不消费分子资产
  - **Type**: `rule`
  - **Covers**: AC-4, TR-2.3
  - **Evidence**: choice 守卫 [L20-22](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.Molecule.cs#L20-L22) 在 `git diff` 中未改动。独立扫描源 `pdf_questions.json`：≥2 个 molecule 资产共 7 题，其中 pdf-433/434/438/439 为 `taskType=choice` 被守卫跳过，仅 pdf-199/208/322 三个非选择题受影响——与 spec 爆炸半径一致。测试 `选择题_分子分支不短路` 通过。

- [x] CP-R5: 解析失败不臆造（部分失败 / 全部失败）
  - **Type**: `rule`
  - **Covers**: AC-5, TR-1.3
  - **Evidence**: [L31-34](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.Molecule.cs#L31-L34)：`AnalyzeMoleculeAsset` 失败返回 null 不加入 `successes`；`successes.Count==0` → `return null`。测试 `多分子资产_部分失败_仅成功资产贡献` 与 `多分子资产_全部失败_回退null` 通过。

- [x] CP-R6: 全库零回归（LOST=0 / harm=0 / precision=1 / mp27 27/27 / 全量测试绿）
  - **Type**: `rule`
  - **Covers**: AC-6, TR-3.1, TR-3.2, TR-3.3
  - **Evidence**: ① `diff_eval.ps1 -OldTag t2curate3 -NewTag t2route` → GAINED (0)、LOST (0)。② `verify_mp27.py t2curate3 t2route` → PASS (27/27)，REGRESSIONS (True->False): []。③ 两份 snapshot 顶层摘要逐字段全等：均为 totalCases=757, gatewayCorrect=673, harmCount=0, enginePrecision=1, bothWrong=84, molCases=45, molHits=7。④ 全量 `dotnet test CogniThink.Chem.sln` → 2656 通过、0 失败（实现侧已跑；评审独立复跑了 MoleculeGatewayTests 9/9）。红线 precision=1 / harm=0 / LOST=0 / mp27 27/27 全部守住。

- [x] CP-U1: 多分子结论覆盖完整性
  - **Type**: `rubric`
  - **Covers**: AC-7, TR-3.4
  - **Scale**: 1-5
  - **Anchors**: 1 = 只反映第 1 个分子（改动前）；3 = 覆盖部分分子有缺漏；5 = 按资产顺序覆盖全部成功解析的分子，官能团/命名无缺漏
  - **Pass Threshold**: >= 4
  - **Evidence**: 独立评审读取 `snapshot_t2route.json`：pdf-322 结论含 (1)-(6) 六个分块，按资产顺序覆盖全部 6 个分子（曲美托嗪/3-溴-2-环己烯酮/硝基氟氯代苯甲酸酯/硝基氨基氯代二苯甲酮/2,4,6-三溴苯酚/邻苯二酚），spec 预期特征全部出现；pdf-199 覆盖 3 分子、pdf-208 覆盖 2 分子。基线 t2curate3 三题仅含第 1 分子。**评分 5/5**（≥4 通过）。

## Review History

### Review R1
- **Result**: `pass`
- **Evidence**: 独立只读评审（fresh subagent，无本会话记忆）核验全部 7 个检查点，均 pass；无 actionable 缺陷。判分文件 `GatewayVsLlmEvaluationService.cs` 不在本次改动集（`git status` 确认），`MatchMolecule` 保持纯 OR 子串语义，未引入 AND 收紧。diff GAINED=0/LOST=0、mp27 27/27、snapshot 摘要全等、全量测试 2656/0 绿。
- **Findings (advisory only, non-blocking)**:
  1. `_evalscratch/apibuild/data/pdf_questions.json` 为陈旧副本（pdf-322 显示 0 资产），非 eval 实际使用数据；后续核验以 `src/.../data/pdf_questions.json` 或实际构建为准。
  2. `diff_eval.ps1` 输出 `totalIds=756` vs snapshot `totalCases=757`（diff 过滤了 1 条不可比行）；GAINED/LOST 均 0，无正确性影响。
- **Blocked By**: N/A
- **Resume When**: N/A
