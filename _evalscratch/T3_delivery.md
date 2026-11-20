# T3 交付报告 · 有机 / 物质结构与元素周期律选择题校验器

> 任务：按 `ClassificationValidator` 模式新增两个 Tier-2 事实驱动选择题校验器（`OrganicChoiceValidator` / `StructurePeriodicValidator`），事实全在 JSON，判定逻辑在 `.cs`，独立测试直接调 `TryDecide`，以 `llmReference` 反推真值校验。红线：无唯一解返回 null、零假阳性、零回归。
>
> 子集：`_evalscratch/task_inputs/T3.json` 共 103 题。

---

## 1. 新建 / 修改文件清单

**新建（本次任务产物）：**

| 文件 | 说明 |
|---|---|
| [data/highschool/organic-facts.json](file:///d:/github/CogniThink-Chem/data/highschool/organic-facts.json) | 有机化学事实库，52 条 statement，覆盖 13 题 × 4 选项。每条含 `match`（选项子串，全部命中）、`stemMatch`（题干子串，可选，防跨题误命中）、`verdict`（选项陈述是否成立）、`reason`。 |
| [data/highschool/structure-periodic-facts.json](file:///d:/github/CogniThink-Chem/data/highschool/structure-periodic-facts.json) | 物质结构 / 周期律 / 化学用语事实库，64 条 statement，覆盖 16 题 × 4 选项。schema 同上。 |
| [src/.../Gateway/FactChoiceMatcher.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/FactChoiceMatcher.cs) | 共享判定内核：`RuleStore.Load().Query<FactData>(factKey)` 加载 JSON → `ChoiceVerifier.ParseOptions` 解析选项 → `WrongDir` 正则判问法方向 → 逐选项匹配事实。红线：每个选项必须恰好命中 1 条事实（0 或 ≥2 → 整题 null），满足方向的候选必须恰好唯一 → 否则 null。复用 `ClassificationValidator.Norm`（internal）。 |
| [src/.../Gateway/OrganicChoiceValidator.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/OrganicChoiceValidator.cs) | 薄包装：`FactChoiceMatcher.TryDecide(req, "organic-facts", ...)`。 |
| [src/.../Gateway/StructurePeriodicValidator.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/StructurePeriodicValidator.cs) | 薄包装：factKey = `"structure-periodic-facts"`。 |
| [tests/.../T3FactValidatorsTests.cs](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/T3FactValidatorsTests.cs) | 独立测试：`OrganicTruth`(13) / `StructureTruth`(16) 真值表；`[Theory]` 逐题断言；4 个护栏 `[Fact]`（两校验器各自零假阳性遍历全部 103 题、两校验器不重叠、覆盖率对齐真值表）。 |

**本次会话修复的 3 个缺陷（在上一版产物基础上）：**

1. `FactChoiceMatcher.cs` 原本引用了未定义的 `NormOpt`（无法编译）。新增私有方法 `NormOpt`：先调 `ClassificationValidator.Norm`（上下标→ASCII、全角→半角、去空白），再把 em dash `—`(U+2014)、en dash `–`(U+2013)、减号 `−`(U+2212) 统一替换为 `-`。原因：题干/选项常以破折号书写化学键（如 `C—H`、`Si—Si`），而事实库 `match` 关键词用 ASCII `-`，`ClassificationValidator.Norm` 不做这层转换，导致本应命中的选项（如 pdf-501-D）漏判。
2. `organic-facts.json` 的 `OF-366-D`：`match` 原为 `["饱和NaCl", ...]`，但选项文本是“饱和**的**NaCl”，合写 token 不命中。改为 `["饱和","NaCl","鸡蛋清","盐析"]`。
3. `structure-periodic-facts.json` 的 `SF-430-A/B/C/D` 原无 `stemMatch`，其中 `SF-430-B`(`SiO2`+`分子晶体`) 会跨题命中 pdf-499-C（“CO2 和 SiO2 晶体分别为分子晶体和共价晶体”），使 pdf-499-C 命中 2 条 → pdf-499 返回 null。给四条 `SF-430-*` 加上唯一题干限定 `"stemMatch": ["化学用语或表述正确"]`（该短语经全库检索确认仅出现在 pdf-430 题干）。

**未触碰的文件（红线遵守）：** `LlmGatewayService.cs`、`tasks.md`、任何含 `BuildTestCases()` 的共享测试文件、T1/T2 的 validator / JSON / 测试。git status 中这些文件的 `M` 标记来自并发任务，非本次编辑。

---

## 2. 覆盖的题（29 道可被唯一求解，真值 = llmReference 反推 + 化学核对）

### OrganicChoiceValidator（13 题，factKey = `organic-facts`）
| id | 答案 | 考点 |
|---|---|---|
| pdf-335 | B | 烯烃加成反应机理 |
| pdf-366 | C | 糖类/核酸/蛋白质/盐析 |
| pdf-369 | A | 酚醛树脂缩聚/盐析/氢化/ATP |
| pdf-373 | B | 高分子材料（聚乙烯/PTFE/尼龙/有机玻璃） |
| pdf-374 | B | 加聚单体判断 |
| pdf-375 | B | 合金/芳纶/同素异形体/高分子 |
| pdf-379 | A | PET/酚醛树脂/有机玻璃 |
| pdf-385 | C | 分子共平面判断 |
| pdf-392 | C | 醛类同分异构体数（C5H11 8 种） |
| pdf-443 | B | 运动用品材料（涤纶/纤维素/木糖醇/氯乙烷） |
| pdf-447 | B | 金属材料判断 |
| pdf-448 | D | 文物材料（青铜/绢/陶瓷/珐琅彩） |
| pdf-449 | D | 文房材料（羊毛/墨/纸/大理石） |

### StructurePeriodicValidator（16 题，factKey = `structure-periodic-facts`）
| id | 答案 | 考点 |
|---|---|---|
| pdf-069 | B | 甲硅烷与高锰酸钾反应 |
| pdf-154 | B | MOFs 固定 N2O4 / 超分子识别 |
| pdf-181 | C | 同位素 ¹⁴N/¹⁵N 中子数 |
| pdf-183 | B | 氚 ³₁H 粒子数 |
| pdf-186 | A | 核素符号书写（铟 Z=49） |
| pdf-192 | B | 极性分子判断 |
| pdf-200 | C | 晶体与非晶体性质 |
| pdf-209 | B | 共价晶体熔点比较 |
| pdf-210 | A | 实例与解释“不符”判断 |
| pdf-320 | B | 同素异形体/同位素/同分异构/同系物 |
| pdf-326 | D | 仪器与谱图（红外光谱） |
| pdf-430 | C | 化学用语（核素/晶体/σ键/空间结构） |
| pdf-499 | B | SiF4 吸收反应（配位键/极性/晶体/VSEPR） |
| pdf-500 | A | VSEPR 模型 / 分子极性 / 键角 |
| pdf-501 | B | 键角/配位/原子光谱/键能（微观解释） |
| pdf-502 | B | 电负性/价电子/原子半径/键角比较 |

> 注：`T3.json` 的 `cluster` 字段只是粗略提示，与内容不完全对齐（如 pdf-375/pdf-385 被标 `structure_periodic`，但内容属有机，由 `OrganicChoiceValidator` 求解；pdf-154/pdf-192/pdf-502 被标 `other`，但内容属结构/周期律，由 `StructurePeriodicValidator` 求解）。校验器按内容判定，不以 cluster 字段为准。

---

## 3. 诚实 null 的题（74 道，故意返回 null —— 正确行为）

103 − 29 = 74 道，两校验器均返回 null。按 `taskType` / 原因分类：

- **计算题（6）**：`pdf-098, pdf-207, pdf-328, pdf-354, pdf-355, pdf-356` —— 数值计算不在事实匹配范围内。
- **非选择题型（2）**：`d7-4`(structure_verify)、`pdf-507`(vsepr) —— 非单字母选项事实匹配任务。
- **选择题但无法干净唯一判定（66）**：`pdf-104, pdf-127, pdf-132, pdf-182, pdf-184, pdf-191, pdf-201, pdf-214, pdf-219, pdf-220, pdf-223, pdf-225, pdf-226, pdf-231, pdf-236, pdf-241, pdf-245, pdf-246, pdf-247, pdf-251, pdf-256, pdf-302, pdf-319, pdf-321, pdf-325, pdf-329, pdf-330, pdf-332, pdf-334, pdf-336, pdf-339, pdf-342, pdf-344, pdf-347, pdf-348, pdf-349, pdf-350, pdf-351, pdf-353, pdf-358, pdf-359, pdf-361, pdf-364, pdf-371, pdf-380, pdf-387, pdf-390, pdf-393, pdf-394, pdf-396, pdf-400, pdf-402, pdf-428, pdf-429, pdf-431, pdf-433, pdf-435, pdf-436, pdf-437, pdf-438, pdf-439, pdf-504, pdf-512, pdf-525, pdf-540, pdf-545`
  - 主要子类：① 选项依赖图像（装置图 / 结构图 / 晶胞图 / 谱图 / 示意图 / 轨道表示式 / 球棍模型），纯文本无法逐选项干净判定；② 多选 / 多正确字母无唯一解（如 `pdf-225` C,D、`pdf-329` A,C,E）；③ 考点超出两库覆盖（实验装置与操作、元素化合物流程推断、电化学等），任一选项无法命中 → 整题 null 不蒙数。

> 这些题返回 null 已由 `Organic_ZeroFalsePositive_AcrossAllT3` 与 `Structure_ZeroFalsePositive_AcrossAllT3` 两个护栏遍历全部 103 题验证：任一校验器对非真值表题返回非 null 即判失败，实际 0 失败。

---

## 4. 接线需求（由主控统一接到 `LlmGatewayService.CallChoice`）

两个校验器**尚未接线**（按 brief 由主控处理），接线信息如下：

- `OrganicChoiceValidator.TryDecide(req)` —— 事实库 key：`organic-facts`（`data/highschool/organic-facts.json`）。
- `StructurePeriodicValidator.TryDecide(req)` —— 事实库 key：`structure-periodic-facts`（`data/highschool/structure-periodic-facts.json`）。
- **建议插入位置**：`CallChoice` 现有 Tier-2 确定性链之后（`ClassificationValidator` → `TransformationChainValidator` → `NaChoiceValidator`）、通用 `ChoiceVerifier.TryDecide(req, _chem)` 回退之前，作为两段新的短路分支（Confidence = 1.0）。放在现有链之后可保证已被分类/转化/NA 校验器解出的题行为不变（零回归）；两者互不重叠（见下方测试），顺序可任意。
- `RuleStore` 按 `(factKey, Type)` 缓存并扫描 `data/highschool/` 目录，新 JSON 无需额外注册。
- 已通过 `Validators_DoNotOverlap` 测试：两校验器不会对同一题同时断言，可安全短路。

---

## 5. 测试结果（0 失败证据）

构建（独立输出目录，避免与 T1/T2 争抢 bin）：
```
dotnet build src\CogniThink.TutorBrain.Api\CogniThink.TutorBrain.Api.csproj -o _evalscratch\build_T3 -v q --nologo
→ 0 errors（仅 2 个与本任务无关的既有 CS8602 警告，位于 AdaptiveTeachingOrchestrator.cs）
```

测试：
```
dotnet test tests\CogniThink.TutorBrain.Tests\CogniThink.TutorBrain.Tests.csproj `
  --filter "FullyQualifiedName~T3FactValidatorsTests|FullyQualifiedName~ClassificationValidatorTests" `
  -o _evalscratch\build_T3 --nologo
→ 已通过! 失败: 0，通过: 46，已跳过: 0，总计: 46
```

46 项构成：13 道 `Organic_Solves_KnownQuestion` [Theory] + 16 道 `Structure_Solves_KnownQuestion` [Theory] + 4 个护栏 [Fact]（零假阳性×2 / 不重叠 / 覆盖率对齐）+ 13 项 `ClassificationValidatorTests`（回归）。

另：独立诊断脚本 `_evalscratch/T3_diag.ps1`（归一化逻辑与 C# `NormOpt` 对齐）遍历全部 29 道真值表题逐选项核对，输出 `SUMMARY: 29 questions checked, 0 with non-exact-1 options`，与测试结论互证。

---

## 6. 零回归自查

- 未修改任何既有 validator 或既有 JSON（`substance-classification.json`、`na-particle-facts.json` 等均未动）；仅新建文件，并在新建的 `FactChoiceMatcher.cs` 内新增 `NormOpt`。
- 对共享代码仅**只读复用**：`ClassificationValidator.Norm`（internal）与 `ChoiceVerifier.ParseOptions`，未改其实现。
- `ClassificationValidatorTests` 13 项全绿 → 共享 `Norm`/`ParseOptions` 与既有事实库加载路径零回归。
- 两护栏 `[Fact]` 遍历全部 103 题 → 零假阳性、零越界断言。
- `Coverage_MatchesTruthTables` → 恰好 13（有机）+ 16（结构）被解出，真值表范围内不漏解、不多解。

---

## 已知风险 / 注意事项

1. **`expected` 字段不可靠**：T3.json 的 `expected` 在 4 道题上与正确答案不符 —— `pdf-154`(expected A，正确 B)、`pdf-186`(expected B，正确 A)、`pdf-326`(expected A，正确 D)、`pdf-502`(expected D，正确 B)。本任务真值以 `llmReference` + 化学核对为准，测试断言对齐真值表。若主控/评测用 `expected` 字段断言会误判，需以 `llmReference` 为真值源。
2. **未接线**：两校验器未接入 `CallChoice`，接线时需注意与 T1/T2 新增校验器（Equilibrium / IonicEquation / ConceptFact 等）的短路顺序，避免重复断言；本任务已保证两者互不重叠。
3. **召回率取舍**：74 道诚实 null 中多数为图像/多选/计算/超纲，未来可扩充事实库提升召回，但当前以“零假阳性”为硬门，优先精确率。
4. **并发构建**：`dotnet build` 期间若并发任务修改 `LlmGatewayService.cs` / `IonicEquationValidator.cs` 等共享文件导致编译失败，属并发任务范围；本任务代码不依赖这些文件，重试即可（本次最终构建 0 errors）。
