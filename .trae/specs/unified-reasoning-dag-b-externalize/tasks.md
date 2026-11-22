# B-外置批 实现任务队列

> 父规格：[spec.md](./spec.md)。基线 checkpoint：`after_t2_ph`。
> 状态字段仅取：pending / in_progress / blocked / completed / cancelled。
> 每个任务完成时回填「Completion Evidence」。

## 锁定的回归期望值（AC5，从 after_t2_ph 一手捕获，迁移后必须字节级相等）

| 求解器 | 案例 | 期望 FinalResult |
|---|---|---|
| KnownEquation | pdf-539 | `离子方程式：GaN + OH⁻ + 3H₂O → [Ga(OH)₄]⁻ + NH₃↑` |
| KnownEquation | pdf-456 | `化学方程式：Na2SO3 + H2SO4 = Na2SO4 + SO2↑ + H2O` |
| ProductWriter | d1-1 / pdf-044 | `离子方程式：2Fe2+ + Cl2 → 2Fe3+ + 2Cl-` |
| ProductWriter | d1-2 | `离子方程式：2Fe2+ + 4Br- + 3Cl2 → 2Fe3+ + 2Br2 + 6Cl-` |
| ProductWriter | d1-3 | `离子方程式：2AlO2- + CO2 + 3H2O → 2Al(OH)3 + CO32-` |
| ProductWriter | d1-4 | `离子方程式：AlO2- + CO2 + 2H2O → Al(OH)3 + HCO3-` |
| ProductWriter | d1-5 | `离子方程式：Ca2+ + HCO3- + OH- → CaCO3 + H2O` |
| ProductWriter | pdf-515 | `离子方程式：CN- + CO2 + H2O → HCN + HCO3-` |
| Redox R2 | d2-4 | `还原产物是 NO` |
| Redox P3 | trap-limit-1 | `能。Al 是两性金属，与强碱溶液反应生成 H2。` |
| Redox P3 | d8-5 | `不能。酸性电解质溶液中，电极反应式不出现 OH-。` |
| ConceptVerdict | pdf-467 | `×` |

R1 必须保持节点空：`redox-1/2/3` 期望 FinalResult 分别为 `还原剂是 Cu` / `氧化剂是 FeCl3` / `还原剂是 KI`，且 `ReasoningNodes` 空。

---

## Task 1: KnownEquationWriterSolver 外置 + 可溯源

- **Status**: completed
- **Depends On**: —
- **Priority**: high
- **覆盖 AC**: AC1, AC5（pdf-539/456）, AC7, AC8
- **Changes**:
  1. 新建 `data/knowledge/known-equations.json`（`version`/`source` 元数据 + `entries[]`，每条含 `id`/`equation`/`resultPrefix`/`derivationSteps[]`/`constraintNote`）。逐字拷入现有 2 条事实文本。
  2. `KnownEquationWriterSolver.cs`：加 `Lazy<>` 直读 JSON（仿 HalfReactionBalancer）；两个触发分支保持原条件不变，把内联 `const string`/字面量替换为按 key 取 entry；构造 `EngineResponse` 时 `FinalResult = entry.ResultPrefix + entry.Equation`、`DerivationSteps = entry.DerivationSteps`、`ConstraintNote = entry.ConstraintNote`，并加 `ReasoningNodes` 单节点（`KnowledgeId = entry.Id`、`Conclusion == FinalResult`、`EvidenceLevel.KnowledgeFact`）。
  3. 测试：在 `ReasoningSolverTraceabilityTests.cs` 加 3 个——pdf-539 命中（精确 FinalResult + 节点 id 非空）、pdf-456 命中（精确 FinalResult + 节点）、域外输入返回 null 且无节点（R4）。
- **Test Requirements**:
  - (rule) `Assert.Equal("离子方程式：GaN + OH⁻ + 3H₂O → [Ga(OH)₄]⁻ + NH₃↑", resp.FinalResult)` 且 `resp.ReasoningNodes[0].KnowledgeId` 非空。
  - (rule) `Assert.Equal("化学方程式：Na2SO3 + H2SO4 = Na2SO4 + SO2↑ + H2O", resp.FinalResult)` 且节点非空。
  - (rule) `"写出 Fe 与 HCl 反应的化学方程式。"` → `resp` 为 null。
  - (rule) grep `KnownEquationWriterSolver.cs` 不含事实特征子串 `GaN + OH`、`Na2SO3 + H2SO4 =`、`四羟基合镓(III)酸根`、`强酸制弱酸`、`固液不加热制`（触发 token 允许保留）。
  - (rule) `dotnet build` 0 error、无新增 warning；本任务相关既有测试通过。
- **Completion Evidence**: ✅ 已完成。新建 `data/knowledge/known-equations.json`（2 条目 `known-eq-gan-strong-base` / `known-eq-na2so3-so2`，事实文本逐字拷贝）；`KnownEquationWriterSolver.cs` 改为 `Lazy<>` 直读 JSON、两个触发分支保持原条件、命中后加 `ReasoningNodes` 单节点（`KnowledgeId=entry.Id`、`EvidenceLevel.KnowledgeFact`、`Conclusion==FinalResult`）。新增 3 单测（GaN/SO2 精确 FinalResult+节点、域外 null）通过。AC1 grep：`.cs` 不含事实特征子串（`GaN + OH`/`Na2SO3 + H2SO4 =`/`四羟基合镓(III)酸根`/`强酸制弱酸`/`固液不加热制`）= 0 命中；触发 token 允许保留。build 0 error 无新增 warning。

---

## Task 2: ProductWriterSolver 外置 + 可溯源

- **Status**: completed
- **Depends On**: —
- **Priority**: high
- **覆盖 AC**: AC2, AC5（d1-1..5 / pdf-044 / pdf-515）, AC7, AC8
- **Changes**:
  1. 新建 `data/knowledge/product-rules.json`（元数据 + `entries[]`，每条含 `id`/`left[]`/`right[]`/`expandFeBr2`）。逐字拷入 6 条骨架（FeBr2 少量/过量、NaAlO2 少量/过量、Ca(HCO3)2 少量、NaCN 少量）。
  2. `ProductWriterSolver.cs`：`Lazy<>` 直读；`MatchProductRule` 的触发判断分支保持原样，把 `new Skeleton(...)` 字面量替换为按 key 取 entry 的 `Left/Right/ExpandFeBr2`；记录命中 entry 的 `id`，在 `TrySolve` 构造响应时加 `ReasoningNodes`（`KnowledgeId = id`、`Conclusion == FinalResult`、`EvidenceLevel.KnowledgeFact`）。配平逻辑不动。
  3. 测试：加 8 个——d1-1..d1-5、pdf-515 精确 FinalResult + 节点非空；pdf-044（BuildTestCases 真实题干）精确 FinalResult + 节点；域外（FeCl3+NaOH）返回 null 无节点。
- **Test Requirements**:
  - (rule) 上表 7 个 ProductWriter 案例逐项 `Assert.Equal(expected, resp.FinalResult)` 且节点非空。
  - (rule) `"FeCl3 溶液中加入少量 NaOH，反应的离子方程式是什么？"` → null。
  - (rule) grep `ProductWriterSolver.cs` 不含骨架物种字面量（`Fe^2+`、`Al(OH)3`、`CaCO3`、`HCN`、`Br2` 等出现在匹配分支之外）。
  - (rule) build 0 error 无新增 warning；既有 `RedoxGatewaySolverTests` P1 全过。
- **Completion Evidence**: ✅ 已完成。新建 `data/knowledge/product-rules.json`（6 条目，含 `expandPlaceholder`/`expandInto` 配置）；`ProductWriterSolver.cs` 删除 `Skeleton` 记录、改为 `Lazy<>` 直读、`MatchProductRule` 触发逻辑原样保留、命中后加 `ReasoningNodes` 单节点；FeBr2 过量占位展开改为读 JSON 配置（`.cs` 无 `Fe^2+`/`Br^-` 产物字面量）。新增 8 单测（d1-1..5 + pdf-044 + pdf-515 精确 FinalResult+节点；域外 null）通过。AC2 grep：`.cs` 不含 `Fe^2+`/`Al(OH)3`/`CaCO3`/`HCN`/`Br2` 等骨架字面量（仅触发 regex 中的 `FeBr2` 允许）。build 0 error 无新增 warning。

---

## Task 3: RedoxGatewaySolver 外置 + 可溯源（R1 保持空）

- **Status**: completed
- **Depends On**: —
- **Priority**: high
- **覆盖 AC**: AC3, AC5（d2-4 / trap-limit-1 / d8-5）, AC6, AC7, AC8
- **Changes**:
  1. 新建 `data/knowledge/redox-rules.json`（元数据 + 分区 `reductionProducts[]` / `electrolysisAnode[]` / `feasibilityRules[]`，每条含 `id` + 产物/判决文本 + 推导）。逐字拷入：MnO4 酸/中/碱、ClO、Cr2O7 酸、NO3 稀/浓、浓H2SO4；电解阳极 Cl→Cl2、含氧酸根→O2；P3 酸性无OH-、Al/Zn 两性强碱→H2。
  2. `RedoxGatewaySolver.cs`：`Lazy<>` 直读；R2 `TryReductionProduct` / `TryElectrolysisProduct` 的 switch 字面量替换为查 JSON 表（介质/浓度判断逻辑不动），命中后把 entry id 经返回值带出到 `TrySolve`；P3 两条规则的 FinalResult/推导取自 JSON。R2/P3 命中路径加 `ReasoningNodes`（`KnowledgeId = id`、`Conclusion == FinalResult`、`EvidenceLevel.KnowledgeFact`）。**R1 `TryRoleIdentification` 不加节点、不加 JSON 表**（Core 计算，无锚点）。
  3. 测试：加 6 个——d2-4 / trap-limit-1 / d8-5 精确 FinalResult + 节点非空；redox-1/2/3（R1）精确 FinalResult + **`ReasoningNodes` 空**；R2 无介质/表外/NO3 无浓稀 → null（不发节点）。
- **Test Requirements**:
  - (rule) d2-4 / trap-limit-1 / d8-5 精确 `Assert.Equal` + 节点非空且 `KnowledgeId` 非空。
  - (rule) redox-1/2/3 精确 `Assert.Equal` 且 `resp.ReasoningNodes` 为 null 或 Count==0。
  - (rule) `"KMnO4 在熔融条件下的还原产物是什么？"` → null；`"硝酸的还原产物是什么？"` → null。
  - (rule) grep `RedoxGatewaySolver.cs`：还原产物/电极产物/P3 判决字面量已移除（`Mn2+`/`MnO2`/`Cr3+`/`NO`/`NO2`/`SO2`/`Cl2`/`O2`/`不出现 OH`/`两性金属` 不再以 return 字面量出现）。
  - (rule) build 0 error 无新增 warning；既有 `RedoxGatewaySolverTests` 全过。
- **Completion Evidence**: ✅ 已完成。新建 `data/knowledge/redox-rules.json`（`reductionProducts` 8 / `electrolysisAnode` 2 / `feasibilityRules` 2）；`RedoxGatewaySolver.cs` 改为 `Lazy<>` 直读，R2/P3 的 switch 字面量替换为 JSON 查表（介质/浓度判断逻辑不动），命中后加 `ReasoningNodes`（`KnowledgeId=entry.Id`）；**R1 `TryRoleIdentification` 完全不动**——无 JSON 表、无节点（R4，Core 计算无锚点）。新增 8 单测（d2-4/trap-limit-1/d8-5 精确+节点；redox-1/2/3 精确且 `ReasoningNodes` 空；KMnO4熔融/无浓稀硝酸 → null）通过。AC3 grep：`.cs` 不含 `Mn2+`/`MnO2`/`Cr3+`/`NO`/`NO2`/`SO2`/`Cl2`/`O2`/`不出现 OH`/`两性金属` return 字面量。build 0 error 无新增 warning；既有 `RedoxGatewaySolverTests` 全过。

---

## Task 4: ConceptVerdictSolver 外置 + 可溯源

- **Status**: completed
- **Depends On**: —
- **Priority**: high
- **覆盖 AC**: AC4, AC5（pdf-467）, AC7, AC8
- **Changes**:
  1. 新建 `data/highschool/concept-verdicts.json`（元数据 + `entries[]`，每条含 `id`/`pattern`（C# 正则字符串，含 `(?<![非不])` 回顾后发）/`verdict`/`derivationSteps[]`/`constraintNote`）。逐字拷入 SiO2 条目。
  2. `ConceptVerdictSolver.cs`：`Lazy<>` 直读（或 RuleStore 读 highschool）；从 JSON 取 `pattern` 编译 `Regex`、取 `verdict`/`derivation`/`constraint`；命中加 `ReasoningNodes`（`KnowledgeId = entry.Id`、`Conclusion == FinalResult`、`EvidenceLevel.KnowledgeFact`）。判断空/否定式逻辑不动。
  3. 测试：加 3 个——pdf-467 精确 `FinalResult == "×"` + 节点非空；否定式 `"(9)SiO2不是碱性氧化物（ ）"` → null；无判断空 `"SiO2为碱性氧化物"` → null。
- **Test Requirements**:
  - (rule) pdf-467 `Assert.Equal("×", resp.FinalResult)` 且节点非空、`KnowledgeId` 非空。
  - (rule) 否定式 / 无判断空 → null，无节点。
  - (rule) grep `ConceptVerdictSolver.cs` 不含 `SiO2`、`碱性氧化物`、`×` 化学真值字面量。
  - (rule) build 0 error 无新增 warning；既有 `Stage3ESolversTests` Concept 段全过。
- **Completion Evidence**: ✅ 已完成。新建 `data/highschool/concept-verdicts.json`（1 条目 `concept-verdict-sio2-basic-oxide-false`，`pattern` 含 `(?<![非不])` 负向回顾后发，JSON 中反斜杠转义为 `\\n`/`\\s` 以还原 C# 正则）；`ConceptVerdictSolver.cs` 改为 `Lazy<>` 直读、从 JSON 取 `pattern` 编译 `Regex`、取 `verdict`/`derivation`/`constraint`，命中后加 `ReasoningNodes`；判断空/否定式逻辑原样保留。新增 3 单测（pdf-467 `FinalResult=="×"` + 节点；否定式 null；无判断空 null）通过。AC4 grep：`.cs` 不含 `SiO2`/`碱性氧化物`/`×` 字面量。build 0 error 无新增 warning；既有 `Stage3ESolversTests` Concept 段全过。

---

## Task 5: 跨切验证（eval diff + 覆盖率 + 文档）

- **Status**: completed
- **Depends On**: Task 1, Task 2, Task 3, Task 4
- **Priority**: high
- **覆盖 AC**: AC9, AC10, AC11, AC12
- **Changes**:
  1. 全量 `dotnet test`（TutorBrain + Chem）0 失败。
  2. 离线 eval：`run_eval_resume.ps1 -Tag after_b_ext -Env Production`（后台，LlmAvailable=False）。
  3. `diff_ckpt.py after_t2_ph after_b_ext` → 全零 PASS（AC9）。
  4. 写 `_evalscratch/verify/b_ext_coverage.py`：断言获得非空节点的案例集合 == 预期 13 例、无丢失、无预期外新增、R1 3 例保持 0；并断言 13 例 `gatewayConclusion` 字节级不变（AC10）。
  5. 更新 [data-defect-register.md §N.2](../../../data-defect-register.md) 与父 [tasks.md](../unified-reasoning-dag/tasks.md)：4 求解器标已迁移、记录 `after_b_ext` 与 13 例翻转、B 计数核减（AC12）。
  6. 回填本文件各 Task 的 Completion Evidence；产出最终逐项可核验汇报。
- **Test Requirements**:
  - (rule) `dotnet test` TutorBrain + Chem：0 failed。
  - (rule) diff_ckpt 输出含 `GAINED=0 / LOST=0 / WORSEN=0` 与 `RED-LINE ... -> PASS`。
  - (rule) 覆盖率脚本输出 `ALL ASSERTIONS PASSED`，且打印 gained 集合 == 13 例名单。
  - (rubric, AC11) 抽查 4 个 JSON：均含 version/source、id 稳定语义化 → 评分 ≥ 1。
  - (rule) register/tasks diff 显示 4 行标已迁移。
- **Completion Evidence**: ✅ 已完成。全量 `dotnet test`：TutorBrain 1361 + Chem 913 通过，0 failed。离线 eval `after_b_ext`（Production，`LlmAvailable=False`）：757 例 0 failed。`diff_ckpt.py after_t2_ph after_b_ext`：GAINED=0 / LOST=0 / WORSEN=0 / harm 0→0 → `RED-LINE ... -> PASS`。`b_ext_coverage.py`：非空节点 65→78，added 恰 = 13 例（d1-1..5 / d2-4 / d8-5 / pdf-044 / pdf-456 / pdf-467 / pdf-515 / pdf-539 / trap-limit-1）、removed=0、redox-1/2/3 保持 0、13 例 `gatewayConclusion` 字节级不变、correctness 635/635/harm 0 不变 → `ALL B-EXT COVERAGE ASSERTIONS PASSED`。`data-defect-register.md §N.2` 与父 `unified-reasoning-dag/tasks.md` 已标记 4 求解器迁移完成、B 计数 8→4。**未提交**（按约定不 commit）。

---

## 执行顺序与风险控制

1. Task 1→2→3→4 串行（每完成一个 build 一次，尽早捕获编译错误）；互不写同一文件，可独立验证。
2. 每个 Task 的精确 FinalResult 单测是**零漂移第一道闸**（在 eval 前锁定 JSON 字符串保真）。
3. Task 5 的 diff_ckpt 是第二道闸（覆盖未进单测的案例 + 网关传播）；覆盖率脚本是第三道闸（防臆造/防漏发）。
4. 若任一闸失败：回 Task 对应项修复，不进入下一闸；不蒙混通过。
