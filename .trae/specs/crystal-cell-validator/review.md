# Crystal Cell Validator — 独立审查报告（fresh-context / 只读）

> 审查员角色：fresh-context 独立审查员，只读。除本文件外未修改任何源码 / 数据 / 测试文件。
> 审查范围：`CrystalCellValidator` Spec 立项（spec.md / tasks.md）的 AC-1..AC-9 与红线。
> 审查方法：不采信实施者自证，独立抽样复核关键数字与代码事实（build / test 实跑、jsonl 正则 diff、源码通读、资产 payload 与渲染图目检、官方溯源文件抽样）。

## 结论：PASS

无未关闭的阻断问题；所有 AC 与红线均独立复核通过。详见逐项复核表。

---

## 逐项复核表

| 项 | 类型 | Verdict | 证据（独立复核） |
|----|------|---------|------------------|
| AC-1 | rule | ✅ PASS | 实跑 `dotnet build CogniThink.Chem.sln -c Release --nologo -v q` → 0 警告 / 0 错误 / exit 0。实跑 `dotnet test CogniThink.Chem.sln -c Release --no-build --nologo` → **2109 通过 / 0 失败**（TutorBrain 1198 + Chem 911），exit 0。`--filter FullyQualifiedName~CrystalCell` → 15 通过 / 0 失败（含 FullBankGuard 遍历 757）。 |
| AC-2 | rule | ✅ PASS | `CrystalCellFullBankGuardTests` 在 15 条 CrystalCell 过滤测试中通过：`FullBankGuard_Crystal_EveryNonNullOutput_IsAnswerCorrect` 经 `BuildTestCases()` 遍历全库，断言 `cases.Count > 700`（JSON 加载守卫）、每条非 null 输出 `IsAnswerCorrect=true`、且 `fired` 含 pdf-170/206/222；`Pdf178_Blocked_NoOfficialAnswerTrace_ReturnsNull` 通过；`FirmTargets_OutputContains_ExpectedKeyword` Theory 4/4（170→"4"、206→"4"/"V2/4V1"、222→密度式）。 |
| AC-3 | rule | ✅ PASS | 按任务要求**用正则而非 `ConvertFrom-Json`**（choice 行含重复 A/a 键会抛错）逐行提取：`-match '"id":"([^"]+)"'` 与 `-match '"gatewayCorrect":(true|false)'`。`ie_final` 解析 757 条、`ie_crystal` 解析 757 条，按 id 对齐 diff：**LOSS(true→false)=0**、**GAIN(false→true)=3 = {pdf-170, pdf-206, pdf-222}**、**unchanged=754**。pdf-178 不变（false→false）。 |
| AC-4 | rubric | ✅ PASS | GAIN=3 ≥ 3，且恰好为确定候选 pdf-170/206/222。pdf-178 接受 0 GAIN：理由充分且留痕——`data-defect-register.md` C 类 L72 记录「仓库内无官方溯源（已检索 `pdfs/_extract/answer/`、`solved_patch.json`、`vl_cache3/`）→ 不得改键 → 不录入资产 → 返回 null → 0 GAIN/0 harm」，与 spec §6 决策一致。 |
| AC-5 | rule | ✅ PASS | `pdf_questions.json` pdf-170（L5030-5055）：`answer="4"`（L5037）、`answerKeywords=["4"]`（L5032-5034）、`note`（L5035）引用 `pdfs/_extract/answer/page_059.json num 2`。**抽样复核溯源文件**：`page_059.json` num 2 `answer` 字段明确含「晶胞中Ni²⁺个数为4」，`explanation` 写明「O²⁻半径大，顶点和面心为Ni²⁺，晶胞中Ni²⁺个数=8×1/8+6×1/2=4」——与 payload（Ni corner×8 + face×6）及 note 完全吻合。 |
| AC-6 | rule | ✅ PASS | 通读 `CrystalCellValidator.cs`（312 行）：grep `pdf-|170|206|222|178|NiO|4\.96` → **0 命中**。所有化学事实（位点/计数 site+count、molarMass、晶格常数 a、coordinationNumber、atomsPerFormulaUnit）均从 `pdf_questions.json` 的 `imageAssets[].payload` 解析（`ParseCell` L80-113）。代码中仅含通用结晶学常量（权重 1/8、1/4、1/2、1；nm→cm 换算 1e21；浮点容差 1e-6），无任何逐题答案分支或硬编码。 |
| AC-7 | rule | ✅ PASS | ① register 与 payload 交叉核对一致：`data-defect-register.md` C 类 L71-74 对 pdf-170/206/222 的坐标描述（Ni 顶点×8+面心×6；白锡 Sn 顶点×8+体内×1 Z=2；灰锡 Sn 顶点×8+面心×6+体内×4 Z=8 CN=4；P₄ 顶点×8+面心×6 Z=4 分子 atomsPerFU=4 a=1.0）与 `pdf_questions.json` L5048-5051 / L5927-5944 / L6288-6291 payload **逐字段一致**。② 渲染图目检（Read 打开 PNG）：`p057_n04_r0.png`=FCC（角+面心，NiO 岩盐 ✓）、`p073_n07_2.png`=体心四方（角+体心，白锡 Z=2 ✓）、`p073_n07_3.png`=金刚石立方（角+面心+4 个内部，灰锡 Z=8/CN=4 ✓）、`p078_n07_4.png`=P₄ 分子 FCC（角+面心，边长 a nm，16 P ✓）。 |
| AC-8 | rule | ✅ PASS | null 守卫用例在 `CrystalCellValidatorTests` 全部通过：非晶胞资产（`NonCrystal_Asset_ReturnsNull`）、无晶胞意图（`NoCrystalIntent_ReturnsNull`）、choice 题型（`ChoiceTaskType_ReturnsNull`）、问配位数但缺 coordNumber 事实（`AsksCN_ButNoCoordNumberFact_ReturnsNull_NoFabrication`）、问密度但缺 molarMass（`AsksDensity_ButMissingMolarMass_ReturnsNull`）、异质量比保留符号不臆造数值（`SymbolicRatio_DifferentMass_KeepsMassSymbols`）；`ParseCell` 载荷异常 catch→null（L109-112）；`TrySolve` 域外/空文本/无意图均返回 null（L28-51）。pdf-178 因无 crystal 资产（无 `imageAssets`）→ `ParseCells` 空 → null。FullBankGuard 757 遍历进一步证无臆造。 |
| AC-9 | rule | ✅ PASS | `checkpoint_ie_crystal_summary.txt`：harmCount=0、precision=1。正则 diff：仅 3 个 GAIN（170/206/222）、其余 754 行 `gatewayCorrect` 不变、0 行 true→false → 无 bothWrong→correct 之外的副作用回归。`ie_crystal` 字段直证：pdf-170/206/222 均 `GE=true / TA=true / GC=true`，gatewayAnswer 分别为 "4" / "V2/4V1" / "16；4.96×10^23 / (N_A · a^3)；…"；pdf-178 `GE=false / TA=false / GC=false`（未采纳）。 |

### 红线复核

| 红线 | Verdict | 证据 |
|------|---------|------|
| precision=1 | ✅ | `ie_crystal` summary precision=1；FullBankGuard 硬保证（凡非 null 必 `IsAnswerCorrect=true`）；`concept_fact` 采纳门仅采纳判正结论（LlmGatewayService L397-402 置 `TaskType="concept_fact"`）。 |
| LOSS=0 | ✅ | 正则 diff 逐 id 对齐：true→false = 0。 |
| harm=0 | ✅ | `ie_final` / `ie_crystal` 两份 summary harmCount 均=0；pdf-178 不录入资产不蒙数 → 0 harm。 |
| 无唯一解返回 null | ✅ | 缺资产 / 缺坐标 / 缺 molarMass / 缺 coordNumber / choice / 无意图 → 全部 null（AC-8）。 |
| 化学事实只在 JSON | ✅ | 逻辑层 0 逐题硬编码（AC-6 grep 0 命中）；坐标/摩尔质量/配位数均在 payload。 |
| answer 改动有官方溯源 | ✅ | pdf-170 有（page_059.json num 2，已抽样复核）；pdf-178 无官方溯源 → 不改键（answer 仍为 "C"，L5242 未动）→ 符合红线。 |

---

## 发现的问题

无阻断问题、无建议项（所有 AC 与红线均通过）。

### 过程中确认的非问题（澄清，非缺陷）

- pdf-206 的 `gatewayAnswer` 字段仅记录 "V2/4V1"（评估侧采纳落库片段），不代表 `FinalResult` 缺 "4"；`FirmTargets` 测试断言输出同时含 "4" 与 "V2/4V1"，FullBankGuard 断言 `IsAnswerCorrect` 通过，eval `GC=true`——三证一致，无缺陷。
- `CrystalCellValidator` 对密度比采用 `LastIndexOf` 定序（L245-249）以避免配位子问提前出现颠倒顺序——属实现稳健性处理，经 pdf-206 实测产出 "V2/4V1" 命中关键词，无问题。

---

## 审查覆盖声明

本次为 **fresh-context、只读** 的独立审查：
- 未修改任何源码 / 数据 / 测试 / 配置文件；唯一写入即本 `review.md`。
- 未采信 `tasks.md` 完成证据表的自证表述，所有关键结论均经独立复核：build/test 实跑、jsonl 正则 diff（规避 `ConvertFrom-Json` 在重复键上的抛错）、`CrystalCellValidator.cs` 通读与硬编码 grep、4 个 crystal 资产 payload 与 register 交叉核对、4 张渲染 PNG 目检、`page_059.json` 溯源抽样。
- 覆盖 spec.md 全部 AC-1..AC-9 与 §4 红线。

---

## Review #2 — pdf-178 增量复审（fresh-context / 只读）

> 审查员角色：fresh-context 独立审查员，只读。除本文件外未修改任何源码 / 数据 / 测试 / 规约文件。
> 审查范围：仅核对本次「pdf-178 解除阻塞」增量（tasks.md T10/T11），不重审 Review #1 已覆盖的 pdf-170/206/222 既有结论；不采信实施者自证，独立复核。
> 审查方法：溯源两文件通读交叉、`pdf_questions.json` 条目目检、渲染 PNG 目检、`CrystalCellValidator.cs` 通读、两份 summary 读取、**用正则 `-match`（规避重复键导致 `ConvertFrom-Json` 抛错）逐行比对 `checkpoint_ie_final.jsonl` 与 `checkpoint_ie_crystal178.jsonl` 的 id↔gatewayCorrect**。

### 结论：PASS

pdf-178 由阻塞（0 GAIN / 0 harm）翻转为确定解出，增量 GAIN 3→4；precision=1、harmCount=0、LOSS=0 全部保持；官方溯源真实、求解器零回归、密度数学等价。无未关闭阻断问题。

### 逐项核对

| # | 核对项 | 结论 | 独立证据 |
|---|--------|------|----------|
| 1 | 官方溯源真实性（page_060 num 4 第(6)） | ✅ 通过 | `pdfs/_extract/answer/page_060.json` L19 `answers[num=4].answer` 明确含 `(6)①12 ②122×1×55×3 / (NA×10^-7)^3`，module=「一化·高考化学核心方法（基础+中档）」。 |
| 2 | 同条 num 4 的 (1)~(5) 与 exercise/page_058 大题 A 逐一吻合 | ✅ 通过 | `pdfs/_extract/exercise/page_058.json` 大题 A：num1→(1) 基态 Cu 价层电子排布 → 官方 (1)`3d10 4s1`；num2→(2) 还原酸浸 MnO2 离子方程 → 官方 (2)`MnO2 + SO3^2- + 2H+ = Mn2+ + SO4^2- + H2O`；num3→(3) 浸金银催化剂 → 官方 (3)`[Cu(NH3)4]2+`；num4→(4) 滤液1循环目的 → 官方 (4)；num5→(5) 氧化性顺序 Fe³⁺/Cu²⁺/MnO2 → 官方 (5)`MnO2 > Fe3+ > Cu2+`；num6→(6) Mn3Sb 晶胞 → 官方 (6)。六小问一一对应，铁证 sub-(6) 即 pdf-178 晶胞问。exercise 中 num5（L42）起为大题 B（黄铜矿流程），印证 pdf-178 题干拼接了 A(6)+B(2)（与 note 一致）。 |
| 3 | 数据改动（pdf_questions.json pdf-178） | ✅ 通过 | `src/CogniThink.TutorBrain.Api/data/pdf_questions.json` L5234-5266：`answer`→`(1)12 (2)122×1×55×3 / (NA×10^-7)^3`（L5244）、`answerKeywords`→`["12","2.87×10^23 / (N_A · a^3)","122×1×55×3 / (NA×10^-7)^3"]`（L5237-5241）、`note` 标注 page_060 num4 第(6)溯源（L5242）；已追加 `type=crystal` 资产（L5248-5264）：Sb corner×8 M122、Mn face×6 M55、`formulaUnits=1`、`molarMass=287`、`a=1.0`、`coordinationNumber=12`。answer 改动与官方溯源逐字一致，红线满足。 |
| 4 | 渲染图与坐标一致 | ✅ 通过 | 目检 `src/.../data/pdf_images/p058_n06_1.png`：图例 ●=Mn、○=Sb；Sb 白球占立方 8 顶点，Mn 黑球占 6 面心，与 payload（Sb corner×8 / Mn face×6）完全一致。 |
| 5 | 求解器 asksCN 扩宽零回归 | ✅ 通过 | `CrystalCellValidator.cs` L39 `asksCN = text.Contains("配位数") \|\| (text.Contains("周围") && text.Contains("最近"))`。关键守卫：L30-31 `ParseCells` 后 `if (cells.Count == 0) return null;` **先于** L39 执行 → 无 crystal 资产的题在计算 asksCN 之前即返回 null，扩宽意图不可能污染非晶胞题；L28 另对 choice 返回 null；L43 `!asksCount && !asksCN && !asksDensity → null`。正则 diff 证 753 行不变、LOSS=0，实证零跨轨道回归。 |
| 6 | 求解器 FormulaUnits 回退零回归 | ✅ 通过 | `Cell` record 新增可选 `FormulaUnits`（L22）；`ParseCell` 读 `formulaUnits`（L94）；`DensityAnswer` L214 `var z = cell.FormulaUnits ?? Z(cell);`。pdf-178 多物种 basis（Sb+Mn）Z(cell)=4 为总原子数，用 `formulaUnits=1` 得正确公式单元 Z；pdf-206（L5939-5968，白锡/灰锡双胞）与 pdf-222（L6299-6314，P4 单胞）payload 均**无 `formulaUnits` 字段** → 回退 `Z(cell)`，与增量前行为一致（pdf-222：Z=4×M124×1e21=4.96e23；pdf-206 走 `SymbolicDensityRatio` 用 Z(cell)，不触 DensityAnswer）。 |
| 7 | 逻辑层无硬编码答案 | ✅ 通过 | 通读 `CrystalCellValidator.cs`（317 行）：grep `pdf-|170|178|206|222|Mn3Sb|287` → 0 命中。仅含通用结晶学常量（权重 1/8·1/4·1/2·1、nm→cm 换算 1e21、浮点容差 1e-6）。所有化学事实（species/site/count/molarMass/a/coordinationNumber/formulaUnits）均从 `imageAssets[].payload` 解析。 |
| 8 | 密度等价性 | ✅ 通过 | 数学推导：Mn3Sb Z=1 公式单元，M=122+3×55=287；a nm→a×10⁻⁷ cm，V=(a×10⁻⁷)³=a³×10⁻²¹ cm³；ρ=Z·M/(N_A·V)=287/(N_A·a³·10⁻²¹)=287×10²¹/(N_A·a³)=**2.87×10²³/(N_A·a³)**。求解器 `coeff=1×287×1e21=2.87e23` → 输出 `2.87×10^23 / (N_A · a^3)`，与官方式 `(122+55×3)/(N_A×(a×10^-7)³)` 数学等价。 |
| 9 | 评估证据 summary | ✅ 通过 | `checkpoint_ie_final_summary.txt`：total=757、gatewayCorrect=609、bothWrong=148、precision=1、harmCount=0。`checkpoint_ie_crystal178_summary.txt`：gatewayCorrect=613、bothWrong=144、precision=1、harmCount=0、coverage=0.8098。609+4=613、148−4=144，自洽。 |
| 10 | 正则逐行 diff（id↔gatewayCorrect） | ✅ 通过 | 自行用 PowerShell `-match '"id":"([^"]+)"'` / `-match '"gatewayCorrect":(true|false)'` 逐行解析（**未用 `ConvertFrom-Json`**，规避重复键抛错）：base 解析 757 / new 解析 757；**GAIN(false→true)=4 = {pdf-170, pdf-178, pdf-206, pdf-222}**；**LOSS(true→false)=0**；unchanged=753；无缺键/多键。 |
| 11 | pdf-178 行直证 | ✅ 通过 | `checkpoint_ie_crystal178.jsonl` pdf-178 行：`gatewayConclusion`=`12；2.87×10^23 / (N_A · a^3)；2.87×10^23/(N_A·a^3)`（含「12」）、`gatewayCorrect=true`、`gatewayTruthAdopted=true`、`gatewayAnswer` 同上。求解器产出命中 answerKeywords，IsAnswerCorrect 通过（FullBankGuard 绿 + eval GC=true 双证）。 |
| 12 | 规约/任务/缺陷登记一致性 | ✅ 通过 | `spec.md` §6「pdf-178 已解除阻塞」决策已落地、AC-4 GAIN≥4、AC-5b 溯源规则满足；`tasks.md` T10/T11 证据已填、T12 标记 🔄 待回填（本次复审完成后应回填）；`data-defect-register.md` L73 pdf-178 已从「⏸ 仍阻塞」改为「✅ 已解」，与 spec §6 / 本次 eval 一致。 |

### 红线复核（增量）

| 红线 | Verdict | 证据 |
|------|---------|------|
| precision=1 | ✅ | `ie_crystal178` summary precision=1；FullBankGuard 硬保证（凡非 null 必 IsAnswerCorrect=true）；采纳门 concept_fact 仅采纳判正结论。 |
| harmCount=0 | ✅ | 两份 summary harmCount 均=0；pdf-178 由 bothWrong→gatewayOnly，无 bothCorrect 翻转。 |
| LOSS=0 | ✅ | 正则 diff 逐 id 对齐：true→false = 0。 |
| 无唯一解返回 null | ✅ | 扩宽意图仅在有 crystal 资产时可达（L31 先返回）；缺 coordNumber / 缺 molarMass / 缺 a 均 null（既有守卫未动）。 |
| 化学事实只在 JSON | ✅ | 逻辑层 0 逐题硬编码（grep 0 命中）；formulaUnits/coordinationNumber/molarMass 均在 payload。 |
| answer 改动有官方溯源 | ✅ | pdf-178 answer 逐字取自 `page_060.json` num4 第(6)，且经 exercise/page_058 (1)~(5) 交叉铁证属同一大题。 |

### 发现的问题

无 blocker / 无 major。

**过程中确认的非问题（澄清，非缺陷）**：
- `page_060.json` 中官方第(6)密度式被抽取为 `122×1×55×3 / (NA×10^-7)^3`，疑似上游 OCR/抽取 artifacts（两项质量之间应为 `+` 而非 `×`，体积因子中 `a` 似被吞）。但这属于**既有上游抽取数据**，非本增量引入；本增量忠实以此为官方溯源改键（满足红线），同时求解器产出化学上正确且数学等价的 `2.87×10^23 / (N_A · a^3)` 并已列入 answerKeywords，eval 判正通过（GC=true）。不影响正确性。
- `CrystalCellValidator.cs` L38 注释称「上游 HasCrystalAsset 门控」，实际门控为求解器内部 L31 `cells.Count==0 → null`（先于 asksCN 计算）。表述略不精确但功能正确，不构成缺陷。

### 审查覆盖声明

本次为 **fresh-context、只读** 的增量独立复审：
- 未修改任何源码 / 数据 / 测试 / 规约文件；唯一写入即本 `review.md` 追加章节（Review #1 原内容完整保留未删改）。
- 所有关键结论独立复核：溯源两文件通读交叉、pdf-178 条目与渲染图目检、求解器通读 + 硬编码 grep、两份 summary 读取、**正则逐行 diff（规避 ConvertFrom-Json 重复键抛错）**。
- 覆盖增量相关全部红线与 AC-4 / AC-5b / AC-6 / AC-8 / AC-9。
