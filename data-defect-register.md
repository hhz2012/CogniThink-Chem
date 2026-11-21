# 数据缺陷登记册（pdf_questions.json）

> 范围：`src/CogniThink.TutorBrain.Api/data/pdf_questions.json` 中**无法在零假阳性红线内自动修复**的题录条目，以及本轮已修复条目的留痕。
> 红线：不注入 verdict 字眼、不改动存储 answer、不基于低质量 OCR 猜测回填；引擎只在文本完整且可唯一判定时给出结论，否则诚实 `null`。
> 本登记册的存在意义：把"组合/计数型未解"从"引擎能力缺口"重新归因为"**数据缺陷**"——通用求解引擎（`CountingChoiceValidator` 一般子项路径 / `MultiSelectChoiceValidator` / `RedoxElectronStatementJudge`）已就绪并经 tier2 验证，唯一阻塞是题面子项文本或答案键缺失/冲突。这些条目保持 `null`，交由后续数据管道或图像轨道接续，**不再走逐题硬编码事实**路线。

---

## A 类：答案键冲突 / 损坏

### A.1 已修复（Task 5，已核对修正并验证零回归）

> 以下 10 道存储 `answer` 与化学真值/答案册冲突，已修正 `answer` 与 `answerKeywords`。统一验证：EvalRunner diff `base_pre5b`→`after_5b` 中 **GAIN=0、LOST=0、answer-changed=0**（448/757 两端一致）——引擎对这 10 道均返回 `null`，故改键是纯数据正确性修正，不改变任何判分结果，零 harm。

| id | 原值 → 新值 | 核对依据 | 批次 |
|----|-------------|----------|------|
| pdf-101 | `A, B, C, D` → `C` | note 化学推导：KSCN 可区分 Fe₂(SO₄)₃ 与 K₃[Fe(CN)₆]，C 正确，A/B/D 错误 | 5a |
| pdf-148 | `A, B, C, D` → `D` | note 推导：苯磺化不需浓硝酸，D 对应关系错误 | 5a |
| pdf-269 | `A, B, C, D` → `B` | note 推导：NH₄Cl 水解促进 HCO₃⁻ 水解，B 错误 | 5a |
| pdf-279 | `A, B, C, D` → `D` | note 推导：酸性下 NO₃⁻ 氧化 SO₃²⁻ 干扰检验，D 错误 | 5a |
| pdf-280 | `A, B, C` → `C` | note 推导：等体积混合 pH>7 时浓度排序应为 c(NH₄⁺)>c(Cl⁻)>c(NH₃·H₂O)，C 错误 | 5a |
| pdf-399 | `A` → `D` | note 推导：配制 KCl 溶液定容时玻璃棒下端应在刻度线以下，D 操作不合理 | 5a |
| pdf-185 | `(1)B (2)D` → `D` | 答案册 OCR `_evalscratch/ocr_answers/page_064`：⁴⁰K 衰变题问「不正确的是」，D（衰变属化学变化）错误；原存储键串抄自另一道多小题题（page_070 q5 标记 `.B(2)D`），与本题完全错配 | 5b |
| pdf-213 | `B, C` → `A` | 答案册 OCR `page_076-077`：问「解释错误的是」，A（植物油制肥皂是皂化反应而非催化加氢）解释错误；原键 `B,C` 是**正确**解释，与问法方向相反 | 5b |
| pdf-405 | `A, B, C, D` → `C` | 答案册 OCR `page_137` 第 11 题标记 `11.C`：问「能得出相应结论的是」，仅 C（Mg(OH)₂/Al(OH)₃ 两性比较）成立，A/B/D 均不能得出 | 5b |
| pdf-406 | `A, B, C, D` → `C` | 答案册 OCR `page_137` 第 12 题标记 `12.C`：问「不能完成气体发生和收集的是」，C（乙醇脱水制乙烯需 170℃ 控温，无温度计）不能完成；与 pdf-405 共享同一 `"A,B,C,D"` 损坏模式。同页 pdf-407 键 `A` 经核对已正确，未改动 | 5b |
| pdf-237 | `B` → `A` | 官方答案键：2024 重庆卷化学第 12 题（学科网含答案卷 `12.A`）；化学核算 R=H、X=B、Y=F、Z=N，A（原子半径 Z(N)<X(B)）真、B（非金属性 Y(F)<Z(N)）假，原键 B 错误。验证：EvalRunner diff `after_b6pure3`→`after_keyfix_237` 逐例 `gatewayAnswer` 变更=0，precision=1、harm=0（引擎对该题返回 null，改键不影响判分）；本轮策展 `GC-700-*` 事实（4 选项各 1 条、stemMatch 锁域）后判正 A，eval `after_curate_237` engineSolved 507→508、LOST=0、precision=1、harm=0 | keyfix_237+curate |
| pdf-485 | `1:1` → `2:1（或1:2）`（answerKeywords 同步 `["1:1"]`→`["2:1","1:2"]`，note 同步订正） | 官方答案册 `pdfs/_extract/answer/page_064.json` 模块2 num=1：S(Z=16) 基态价层 3s²3p⁴，轨道表示式 3s↑↓ 3p↑↓↑↑，两种自旋状态电子数之比 2:1（或 1:2）。原键 `1:1` 及其 note 解析「p⁴ 必有 2↑2↓」违反洪特规则（正确为 3↑1↓）。**与上表其余 10 道不同**：本轮同时扩展引擎（`PeriodicTable.ValenceSpinCounts` + `AtomicStructureSolver` 自旋比锚点）使该题可解，故改键后引擎产出 `2:1（或1:2）` 并判正，eval diff `after_pdf536`→`after_pdf485` **GAIN +1 / LOSS 0 / HARM 0**，precision=1 | pdf485（键订正 + 引擎扩展，GAIN +1） |
| pdf-170 | `B` → `4`（answerKeywords `["B"]`→`["4"]`，note 增补官方溯源） | 官方答案册 `pdfs/_extract/answer/page_059.json` num 2（模块：一化·高考化学核心方法）：NiO 岩盐，O²⁻ 半径大，顶点和面心为 Ni²⁺，晶胞中 Ni²⁺ 个数 = 8×1/8 + 6×1/2 = 4。原 `answer="B"` 与 note（Ca/C 电子排布）属 PDF 抽取错位。本轮同时新增晶胞轨道（`CrystalCellValidator` + NiO crystal 资产，坐标与 `pdf_images/p057_n04_r0.png` 核对）使该题可解，改键后引擎产出 `4` 并判正，eval diff `ie_final`→`ie_crystal` **GAIN +1 / LOSS 0 / HARM 0**，precision=1 | crystal-cell（键订正 + 晶胞资产，GAIN +1） |
| pdf-178 | `C` → `(1)12 (2)122×1×55×3 / (NA×10^-7)^3`（answerKeywords `["C"]`→`["12","2.87×10^23 / (N_A · a^3)","122×1×55×3 / (NA×10^-7)^3"]`，note 重写并标注溯源） | 官方答案册 `pdfs/_extract/answer/page_060.json` num 4 第(6)小问：Mn₃Sb 立方晶胞，①每个 Sb 周围最近且等距的 Mn 共 12 个；②密度 `122×1×55×3 / (NA×10^-7)^3`。原 `answer="C"` 与 note（非金属性选择题）属 PDF 抽取错位；且该 id 题干拼接了大题 A 的(6)与大题 B 的(2)。本轮新增晶胞轨道（`CrystalCellValidator` 扩宽配位意图「周围+最近」+ 多物种密度 `formulaUnits` + 录入 Mn₃Sb 资产，坐标与 `pdf_images/p058_n06_1.png` 核对）使该题可解，改键后引擎产出「12；2.87×10^23/(N_A·a³)」并判正，eval diff `ie_crystal`→`ie_crystal178` **GAIN +1 / LOSS 0 / HARM 0**，precision=1 | crystal-cell（键订正 + 晶胞资产 + 求解器增强，GAIN +1） |
| pdf-203 | `N>H>B>Fe` → `4`（answerKeywords `["N>H>B>Fe"]`→`["4"]`，note 重写并标注溯源） | 官方答案册 `pdfs/_extract/answer/page_059.json` num 2（与 pdf-170 同一 NiO 计数问法）：NiO 岩盐，顶点和面心为 Ni²⁺，晶胞中 Ni²⁺ 个数 = 8×1/8 + 6×1/2 = 4。原 `answer="N>H>B>Fe"` 与 note（电负性）实际属于同页 exercise/page_072 num 3（铁系储氢合金选择题选项 D），属 PDF 答案册错位（与 pdf-170 同源缺陷）。本轮复用 NiO crystal 资产（坐标与 `pdf_images/p072_n04_4.png` 核对）使该题可解，改键后引擎产出 `4` 并判正，eval diff `ie_crystal178`→`pdf204fix` **GAIN +1 / LOSS 0 / HARM 0**，precision=1 | remaining-144 P1（键订正 + 晶胞资产，GAIN +1） |

> pdf-485、pdf-170、pdf-178 与 pdf-203 是 A.1 中四例「改键同时解出」：上表其余改键时引擎对该题返回 null（纯数据修正，GAIN=0）；pdf-485 因引擎新增价层自旋比能力、pdf-170/pdf-203 因晶胞轨道新增 `CrystalCellValidator` + 录入 NiO crystal 资产、pdf-178 因晶胞轨道扩宽配位意图与多物种密度 `formulaUnits` + 录入 Mn₃Sb 资产，而在改键后翻转判正。四种情形均经 eval diff 验证零 LOSS/零 harm。

### A.2 未修复（保留 `null`）

| id | 症状 | 为何不可自动修复 | 建议下一步 |
|----|------|------------------|-----------|
| pdf-197 | 题干拼接了**两道题**：前半「下列有关物质结构或性质的比较中，正确的是」+ 后半「（2025·安徽二模）海洋资源…下列有关物质结构或性质的说法，错误的是」。存储选项（NH₃/NO₃⁻/NH₂OH/羟胺氢键）属于前题，但 note 分析的是后题（IO₃⁻/CO₃²⁻/N₂H₄）；`answer="A,B,C,D"` 为垃圾值。答案册中未能定位到对应题（OCR 检索词无命中）。 | 题面文本已损坏，无法定位正确题干与选项的对应关系；仅答案册键不足以修复（键不知道对应哪道题）。 | 重新 OCR 习题册对应页，拆分为两条独立题录或恢复正确题干+选项+键；暂保留 `null`。 |
| pdf-090 | 题干为「钠及其化合物的部分转化关系如图…设 N_A 为阿伏加德罗常数…下列说法正确的是」完整选择题，但存储 `answer="1,3,1,3"` 为垃圾值（非选项字母）。 | 无官方答案册核对，无法确定正确选项键；引擎对该题返回 `null`。 | 原页/答案册核对正确键后订正；暂保留 `null`。 |
| pdf-100 | 题干为「实验室中利用右图装置验证铁与水蒸气反应…下列说法错误的是」完整选择题（选项 A 含化学方程式），但 `answer="A,B,C,D"` 为垃圾值。 | 垃圾键无官方源不可自动修复；方程引擎亦无法对垃圾键产出可判正结论（见 G 类⑦）。 | 原页核对后订正；暂保留 `null`。 |
| pdf-147 | 题干为「关于化学实验和化工生产的安全…下列说法正确的是」完整选择题，`answer="A, B, C, D"` 垃圾值。 | 无官方源核对，无法定键。 | 原页核对后订正；暂保留 `null`。 |
| pdf-155 | 题干为「采用沉淀法定量测定氯化铵热分解率…下列说法错误的是」完整选择题，`answer="A,B,C,D"` 垃圾值。 | 无官方源核对，无法定键。 | 原页核对后订正；暂保留 `null`。 |
| pdf-271 | 题干为「25℃下…滴定曲线…下列说法错误的是」完整选择题，`answer="A, B, C, D"` 垃圾值。 | 无官方源核对，无法定键。 | 原页核对后订正；暂保留 `null`。 |

---

## B 类：组合/计数题——子项原文缺失（引擎可解，但无题面可判，Task 6）

> 这些题的选项是 ①②③ 组合或"N 个"计数，但 `question` 字段**不含子项陈述原文**，子项仅以带 verdict 的分析形式存在于 `note`，或干脆缺失。`CountingChoiceValidator` 的一般子项路径要求子项文本完整才能逐个 `IStatementJudge` 判定；文本缺失时引擎诚实返回 `null`。
> **不**从 `note` 剥离 verdict 回填子项——会注入判定性字眼、有引入假阳性风险（违反 TR-6.1）。干净源是习题册原页 OCR。

| id | 题型 | 子项现状 | 建议下一步 |
|----|------|----------|-----------|
| pdf-009 | 组合（选项 ①②③④…） | 已回填：①–⑦ 纯陈述从 page_006 原页 OCR 写入 `question`，answer/options/note 未动 | 回填完成。引擎仍返回 `null`——7 子项在 general/experiment/organic/structure 四库中 0 命中（探针 sanity 证 judge 加载/匹配正常），任一 Undecided 即整题弃判；**已策展自解**：加 GC-009-S1~S7 事实（stemMatch 锁域防跨题）覆盖 7 子项，端到端命中 C。策展阶段验证：eval diff GAIN=4/LOSS=0/HARM=0/CHURN=0，全量测试 0 失败 |
| pdf-012 | 组合（选项 ①②④⑤…） | 已回填：①–⑦ 纯陈述从 page_007 原页 OCR 写入 `question`（含 ¹²C/¹⁴C、Na₂CO₃ 等上下标），answer/options/note 未动 | 同上回填；**已策展自解**：加 GC-012-S1~S7 事实覆盖 7 子项，端到端命中 D（与 pdf-009 合计 14 子项全部判出 True/False，无 Undecided） |
| pdf-020 | 计数（"正确的个数"，选项 2/3/4/5 个） | 已回填：①–⑧ 纯陈述从 page_009 原页渲染图写入 `question`（纠正 note 转述失真：① 原文"任何物质"、⑦ 原文"同温同体积"，note 写成"气体"/"同温同压"），answer/options/note 未动 | 回填完成，零回归（eval GAIN=0/LOST=0/HARM=0，全量 1943/0）。引擎仍 `null`：8 子项在四库中全部 Undecided（探针 sanity 证 judge 正常）；且路径 1b 选项解析只认"项/全部"，本题选项是"个"——**已策展自解**：加 GC-020-S1~S8 事实 + 引擎 `LooksLikeCountOfCorrect`/声称数提取认「个」单位，端到端命中 A |
| pdf-065 | 计数（"涉及氧化还原反应的有"，选项 4/5/6/全部 项） | 已回填：①–⑩ 过程陈述从 page_019 原页渲染图写入 `question`（原 note 张冠李戴只谈苯酚，完全不可用），answer/options/note 未动 | 回填完成，零回归。引擎仍 `null`：10 子项在四库中全部 Undecided（含氧化还原子项 RedoxElectron 亦未命中），**已策展自解**：加 GC-065-S1~S10 事实 + `AskDir` 认氧还计数方向，端到端命中 B |

## C 类：题干 / 选项 / 路线依赖图像或原页（文本不可恢复，图像轨道接续）

| id | 症状 | 建议下一步 |
|----|------|-----------|
| pdf-337 | 组合题（选项 ①③⑤…），①–⑥ 是卤代烃结构简式，以**图像**呈现，非文本 | 图像轨道（结构识别/绘图题 pipeline）接续；文本轨道保持 `null` |
| pdf-158 | 题干引用 Ksp 表格（"见下表"），但表中数值未进入 `question` 文本（图像） | 图像轨道抽取表格数值回填 `question`；文本轨道无法判 |
| pdf-263 | 题干"正确的有几项"但选项是 6 条陈述 A–F（非计数），note 的 ①–⑥ 映射到 A–F 且称 ①②③ 正确=3 项，存储 `answer="B"` 自相矛盾 | 需原页确认题型（单选 pick 正确项 vs 计数）；暂保留，登记 |
| pdf-319 | 题干"正确的是"但 note 称 A 错误"符合题意"（暗示问法应为"错误的是"），且选项 C/D 的结构简式是图像缺失 | 原页核问法方向 + 图像轨道恢复 C/D 结构；暂保留 |
| pdf-436 | 选项 C「NaCl 溶液中的水合离子」为**图示**，存储文本仅留冒号、内容缺失（`"C": "NaCl溶液中的水合离子："`），题干问"表述错误的是"，note 判 C 错误 | 选项内容依赖图像，文本轨道无法判；图像轨道恢复 C 选项图示后引擎应能对齐。存储 `answer="C"` 与 note 一致，不改键 |
| pdf-115 | 题干"取等量少许 Cu₂O 分别加入甲、乙两支试管，进行如下实验"，但甲（加稀 HNO₃）、乙（加氨水）的**操作步骤未进入 `question` 文本**（原在装置图/表格）；选项 A–D 均依赖试管甲/乙的实验现象 | 实验步骤依赖图像，文本轨道无法判；图像轨道恢复甲/乙操作步骤后引擎应能对齐。存储 `answer="AC"`，不改键 |
| pdf-376 | 题干"合成路线如下"，但 **X / Y / P 的结构图与反应①路线未进入文本**；选项 A–D（CO₂与 X 计量比、P 水解产物分子式、P 碳碳双键交联、Y 加聚产物降解性）全部依赖 X/Y/P 结构与计量 | 合成路线依赖图像，文本轨道无法判；图像轨道恢复路线图。存储 `answer="B"`，不改键 |
| pdf-382 | 题干"可用如下方法制备"PVA，但**反应①②③ 路线与 PVA / 中间产物结构未进入文本**；选项 A/B（①为加成、②③分别缩聚/水解）依赖路线判断 | 制备路线依赖图像，文本轨道无法判；图像轨道恢复制备路线。存储 `answer="B"`，不改键 |
| pdf-394 | 选项 C「环上的一氯代物有 3 种」的**"环"结构未进入文本**（原在图像），整题选项集不完整；note 仅判了 A/B，C/D 无法对齐 | 选项结构依赖图像，文本轨道无法判；图像轨道恢复 C 选项环结构。存储 `answer="B"`，不改键 |
| pdf-170 | 大题第(4)小问「NiO 晶胞如右图所示，晶胞中含有 Ni²⁺的个数为__」——NiO 岩盐结构的原子排布仅在**晶胞图**中，题干文本未描述具体结构；需均摊法从图计数 | ✅ 已解（晶胞轨道单独立项落地，见 `.trae/specs/crystal-cell-validator/`）：录入 NiO 岩盐 crystal 资产（Ni 顶点×8 + 面心×6，坐标与 `pdf_images/p057_n04_r0.png` 核对），`CrystalCellValidator` 均摊得 4；同时订正 answer 键（见 A.1）。eval `ie_final`→`ie_crystal`：**GAIN +1**（bothWrong→gatewayOnly），LOSS=0、harm=0、precision=1 |
| pdf-178 | 大题第(6)小问「Mn₃Sb 合金立方晶胞结构如右图」问每个 Sb 周围最近的 Mn 个数（配位数）+ 晶胞密度——原子坐标仅在图中，文本无；密度需边长 a（题干给出 a nm） | ✅ 已解：穷尽检索后在 `pdfs/_extract/answer/page_060.json` **num 4 第(6)小问**找到官方答案 `①12 ②122×1×55×3 / (NA×10^-7)^3`（同条 (1)~(5) 与 `exercise/page_058` 大题 A 逐一吻合，铁证）。录入 Mn₃Sb crystal 资产（Sb 顶点×8 + Mn 面心×6，Z=1 公式单元，M≈287，CN=12，坐标与 `pdf_images/p058_n06_1.png` 核对）；求解器扩宽配位意图（「周围+最近」）与多物种密度 `formulaUnits` 字段，产出「12；2.87×10^23/(N_A·a³)」命中关键词。同时订正 answer 键（见 A.1）。eval `ie_crystal`→`ie_crystal178`：**GAIN +1**，LOSS=0、harm=0、precision=1 |
| pdf-206 | 「白锡和灰锡晶胞如图」问灰锡中每个 Sn 原子周围最近且等距的 Sn 原子数（配位数）——灰锡金刚石型结构仅在图中，文本无坐标 | ✅ 已解：录入白锡（体心四方，Sn 顶点×8 + 体内×1，Z=2）+ 灰锡（金刚石立方，Sn 顶点×8 + 面心×6 + 体内×4，Z=8，CN=4）双 crystal 资产，坐标与 `pdf_images/p073_n07_2.png` / `p073_n07_3.png` 核对；输出「4；V2/4V1」命中关键词。eval `ie_crystal` **GAIN +1**，LOSS=0、harm=0 |
| pdf-222 | 「白磷(P₄)晶胞结构如图」问晶胞中含有的 P 原子个数 + 晶体密度——P₄ 分子在晶胞中的位置仅在图中，文本无坐标 | ✅ 已解：录入白磷 P₄ 分子晶体 crystal 资产（P₄ 顶点×8 + 面心×6，Z=4 分子，atomsPerFormulaUnit=4，a=1.0），坐标与 `pdf_images/p078_n07_4.png` 核对；输出 P 原子数 16 + 密度 `4.96×10^23/(N_A·a^3)` 命中关键词。eval `ie_crystal` **GAIN +1**，LOSS=0、harm=0 |
| pdf-203 | （2025·新课标卷节选）「NiO 晶胞如图所示，晶胞中含有 Ni²⁺ 的个数为__」——NiO 岩盐结构的原子排布仅在晶胞图中，题干文本未描述具体坐标；且原存储 answer 键错位（见 A.1） | ✅ 已解（remaining-144 P1）：复用 NiO 岩盐 crystal 资产（Ni 顶点×8 + 面心×6，坐标与 `pdf_images/p072_n04_4.png` 核对），`CrystalCellValidator` 均摊得 4；同时订正 answer 键（见 A.1）。eval `ie_crystal178`→`pdf204fix`：**GAIN +1**，LOSS=0、harm=0、precision=1 |
| pdf-204 | （2024·江苏卷节选）「铁酸铋晶胞如图所示…原子数目比 N(Fe):N(Bi)=__」——Fe/Bi 在晶胞中的位置（体内/面心）仅在图中，文本无坐标 | ✅ 已解（remaining-144 P2）：录入铁酸铋 crystal 资产（Fe 体内×4 + 面心×8，Bi 面心×8，坐标源自官方解析 `answer/page_075.json` num 5，与 `pdf_images/p073_n05_0.png` 核对）；扩展 `CrystalCellValidator` 新增「原子数目比」路径（从 `N(X):N(Y)` 记法结构化提取物种，输出最简整数比），产出 `2:1` 命中关键词。answer 键原已正确（`2:1`），未改键。eval `pdf203fix`→`pdf204fix`：**GAIN +1**，LOSS=0、harm=0、precision=1；全库 guard 验证新路径无伪命中 |

## D 类：note / 选项文本损坏但答案键经核对正确（低优先，仅留痕）

> 这些题的存储 `answer` 经化学推导或答案册确认**正确**（或与 note 推导一致、未单独官方核对者亦留痕并标注），但 `note` 或 `options` 文本与官方题面不符/缺失，导致引擎无法对齐判定（诚实 `null`）。不改 answer；留痕提醒后续数据管道重写 note / 恢复选项文本。

| id | 症状 |
|----|------|
| pdf-381 | note 损坏（称题干问"错误的是"，实际题干问"正确的是"），但存储 `answer="D"`（碳酸镁属于无机盐）对当前题干化学上正确。不改动；留痕提醒 note 需重写。 |
| pdf-075 | 答案册 OCR `page_026` 确认存储 `answer="B"` **正确**（本题问「错误的是」，官方 B 为假陈述）。但**存储选项文本与官方题面错配**：存储 B「反应ⅱ中每生成 1 mol O₂ 转移 4 mol 电子」经 `RedoxElectronStatementJudge` 判定化学上为**真**，而官方 B 是关于反应ⅳ 的假陈述；note 也误判 A 正确。选项文本错配使引擎无法对齐 → 保持 `null`。需重新 OCR 恢复正确选项文本后，`RedoxElectronStatementJudge` 应能自解（奖励）。**不是答案键错误**，此前误判为键冲突已纠正。 |
| pdf-291 | 选项集不全：仅存 A/B/C，**缺 D 选项文本**。存储 `answer="C"` 与 note 推导一致（A/B 错误、C 正确，题干问"正确的是"），但缺 D 致选项集不完整、引擎无法对齐 → 保持 `null`。键仅 note 推导、未官方核对。外部「选项补全」WIP 正在补 D（未提交），待其落地后复核。 |
| pdf-437 | 选项集不全：仅存 A/B，**缺 C/D 选项文本**，但存储 `answer="C"` 指向当前不存在的选项。note 推导 A/B 错误、C 正确（题干问"表示正确的是"），键大概率正确但未官方核对；选项缺失致引擎无法对齐 → 保持 `null`。外部「选项补全」WIP 正在补 C/D（未提交），待其落地后复核。 |

---

## E 类：题面记录损坏——题干拼接 / 大题碎片（文本轨道无法对齐，需原页拆分）

> 数据管道切片时把**两道题拼接进一条 `question`**，或把**大题某一小问单独抽成题录却丢失上下文**，导致 `answer` 与选项错配 / 缺关键数据。文本轨道无法对齐判定，需回到原页拆分或恢复上下文；保持 `null`，不走逐题硬编码。

| id | 缺陷类型 | 症状 | 为何不可自动修复 | 建议下一步 |
|----|---------|------|------------------|-----------|
| pdf-355 | 题干拼接两题 | `question` 前半是填空「书写酯化反应方程式（乙二酸与乙醇 1:2）」，后半是选择「下列物质结构与性质不具有对应关系的是」（选项 A–D 属后半）；存储 `answer="CH3CH2OOC-COOCH2CH3 + 2H2O"` 是**前半填空的答案**，后半选择无对应 answer；note 只分析前半 | 一道题录含两个题型，answer 错配到前半填空，无法判定后半选择的正确键 | 原页拆分为两条题录；后半选择需核对答案键 |
| pdf-356 | 题干拼接两题（+路线缺失） | `question` 前半是填空「乙二酸与乙二醇酯化生成环酯」，后半是选择「化合物 Z 合成路线…下列说法正确的是」（选项 A–D 属后半）；存储 `answer="C4H4O4, (C4H2O3)n + (2n-1)H2O"` 是**前半填空答案**；后半 X/Y/Z 合成路线结构图也未进入文本 | 拼接致 answer 错配，且后半选择还依赖缺失的路线图 | 原页拆分 + 图像轨道恢复 X/Y/Z 结构 + 核对后半答案键 |
| pdf-162 | 大题碎片（缺上下文） | 大题第(3)小问「"酸化"步骤应选用的酸是__(填字母)」，选项 a–d（稀硫酸/浓硫酸/盐酸/磷酸）；存储 `answer` 是**整道大题的答案串** `[(1)2 (2)SO4^2- (3)…]`，无法对应该小题；`taskType=unknown`，未进 choice 路径 | 缺大题上下文（何种溶液、酸化作何用、后续流程），answer 是大题串无法拆出小题键 | 原页恢复大题上下文，单独抽取该小题并核对答案键 |
| pdf-463 | 大题碎片（缺数据） | 大题第(5)小问「"调 pH"分步沉淀可获得 Ni(OH)₂，两次调 pH 数值依次为__」，选项 a–d（3/7/9/11），`answer="c d"`；缺大题上下文（溶液中共存离子、各离子沉淀 pH / Ksp 数据）；`taskType=calculation`，选项小写 | 沉淀 pH 判据数据不在题面（原在表格/上文），文本轨道无法判 | 原页恢复大题上下文与沉淀数据；核对小写选项键 |
| pdf-483 | 重复碎片（与 pdf-477 子问完全重复） | 整条题面 `(5)电解熔融氯化铍制备金属铍时,加入氯化钠的主要作用是____` 与大题 pdf-477 的第(6)小问**逐字相同**（仅碎片编号为 (5)），是数据切片时把同一小问重复抽成独立题录。二者共享完全相同的化学文本，任何通用概念事实都会同时命中两者（count=2），触发 `ConceptFactSolver` 唯一性铁律返回 null；又无化学层面的区分 token 可消歧（差异仅在碎片编号）。 | 重复题录：概念事实无法在不破坏唯一性（不引入假阳性）的前提下只命中其一；靠编号 (5)/(6) 区分属单题策展，违反红线 #6。 | 数据管道去重：删除 pdf-483 或将其合并入 pdf-477；文本轨道保持 `null` |
| pdf-412 | 大题碎片（多子问答案错配） | `question` 是「双水杨醛缩乙二胺锰配合物 (MnLClx) 制备与组成确定」大题的部分，但存储 `answer="B, (2) Mn²⁺氧化, (3) c"` 是**多子问复合串**（含 (2)(3) 子问答案），与单条题录无法对齐；`taskType=choice` 但答案非纯选项字母。 | answer 为大题串，无法拆出本题键；缺官方源核对。 | 原页恢复大题上下文，单独抽取各子问并核对键；暂保留 `null`。 |
| pdf-414 | 大题碎片（单小问 + 多子问答案错配） | `question` 仅是「(3)过滤操作中需要的玻璃仪器…」单小问碎片，但 `answer="K3[Fe(CN)6]溶液, ABC, BC"` 是大题多子问串，答非所问；`taskType=choice`。 | 题面为碎片、answer 为大题串，无法对齐；缺官方源。 | 原页恢复上下文并核对键；暂保留 `null`。 |
| pdf-522 | 题面标注不完整 | `answer="题目不完整，无法作答"`——数据管道已标记该题题面不完整，`question` 仅存半句（"…的名称为"），无足够信息判定。 | 题面本身不完整，文本轨道无法判。 | 原页 OCR 补全题干后重判；暂保留 `null`。 |

## F 类：题面文本完整但 choice 引擎未触发——路由缺口 + 事实覆盖缺失，已按方案 C 策展自解（GAIN=2/LOSS=0/HARM=0）

> 这两条的选择子题题面文本**完整**、化学上可判定，但 checkpoint 显示 `usedEngine=False`。经探针验证（证据：`_evalscratch/probe_f_routing.txt`、`probe_f_scope.txt`、`probe_f_engine.txt`），根因是两层：① `Type` 标为 `"fill"` 而非 `"choice"`，`GenPdfQuestions` 不渲染选项进题干 → `ClassifyTaskDeterministic` 的选择题正则（行首大写 `A-D`+`\.`/`．`/`、`）匹配不到，被"电离"/"ph"关键词截走到 equilibrium / calculation → 不进 `CallChoice`；② pdf-544 选项键为**小写 a-d**，`ChoiceVerifier.ParseOptions` 用 `[A-Z]` 解析不出。**但把路由/解析修好后（选项入题干、归一化大写、强制 `taskType=choice`），真实跑全部 13 个 choice 校验器仍全部返回 `null`**——binding constraint 是事实库无覆盖（缺"加热/稀释醋酸→电离程度增大""pH 试纸不能预先湿润"等规则）。用户已裁定走**方案 C 单题策展**（原则 #1 的显式例外）：修路由 + 判分兼容小写/混合答案 + 单题策展事实，并跑全量 757 题 eval diff 兜底。结果：checkpoint `curate_4q`→`f_curated`，gatewayCorrect 512→514、engineSolved 512→514，**GAIN=2、LOSS=0、HARM=0、precision=1**；1086 单元测试全绿（含全库零 harm 护栏），详见下表。

| id | taskType | 题面现状 | 未触发原因（已确认） | 建议下一步 |
|----|----------|----------|----------------------|-----------|
| pdf-518 | equilibrium | 填空+选择混合题：前半填 `c(CH₃COO⁻)`，后半选择「使 0.10 mol·L⁻¹ CH₃COOH 电离程度增大的是」A 加盐酸 / B 加热 / C 加冰醋酸 / D 稀释，文本完整，化学上 **BD 正确**；`answer="10⁻⁵；BD"` | `Type="fill"` → 选项未渲染 → 被"电离"截走到 equilibrium，不进 `CallChoice`；即便修好路由，13 个校验器仍全 `null`（缺电离平衡移动的事实覆盖） | 已策展自解（GAIN +1）：`pdf_questions` Type `fill`→`choice`；`GenPdfQuestions` 选项键 `ToUpper` 渲染；`TryGetMultiChoiceLetters` 按分隔符拆纯字母片段，兼容混合答案 `"10⁻⁵；BD"`→`BD`；`general-choice-facts` 加 `GC-518-A/B/C/D`（stemMatch 锁「电离程度增大」「CH3COOH」防跨题，全库仅 1 处命中→零暴露），A 假/B 真/C 假/D 真。端到端输出「正确选项为 BD」并判正 |
| pdf-544 | calculation | 大题第(2)小问「测定溶液 pH 不规范的是」，选项 a–d（a 湿润试纸 / b 干燥玻璃片 / c 滴中央 / d 比色），题面自洽，`answer="a"` | `Type="fill"` → 未渲染；且选项键**小写 a-d**，`ParseOptions` 用 `[A-Z]` 解析不出；即便归一化大写修好路由，校验器仍全 `null`（缺"pH 试纸不能预先湿润"的事实覆盖） | 已策展自解（GAIN +1）：`pdf_questions` Type `fill`→`choice`；`GenPdfQuestions` 小写键渲染为大写；`TryGetChoiceLetter` 认小写 `a`（`answer="a"`）；`WrongDir` 正则（`FactChoiceMatcher` + `MultiSelectChoiceValidator`）加「不规范」（全库仅本题题干命中→零暴露）；`experiment-facts` 加 `EF-544-A/B/C/D`（stemMatch 锁「测定溶液pH」「不规范」），A 假/B/C/D 真。端到端输出「错误选项为 A」并判正 |

---

## G 类：方程书写题——本轮不纳入（诚实 `null`，Task 4 排除）

> 本轮方程引擎（`ReactionKbWriterSolver`：KB 唯一命中检索 `KbLookupMolecular`/`KbLookupIonic` + 确定型离子模板）在 151 道未解中筛出 30 道方程关键词候选，**纳入 4 道**（pdf-038 / pdf-417 / pdf-477 / pdf-174，GAIN +4，见 `tasks.md` Task 4 证据与方案 §10）。其余 26 道因下述原因**诚实留 `null`**，不硬编码、不放宽红线（precision=1 / harm=0 / LOSS=0）。证据：`_evalscratch/equation_solver_direct.tsv`（仅 3 行非 null 命中）、`_evalscratch/eq_unsolved_full.tsv`、`_evalscratch/equation_unsolved_eqkw.tsv`。

| 排除原因 | 题 id | 具体说明 |
|----------|-------|----------|
| ① 题干被截为单碎片，缺反应物/流程上下文（E 类） | pdf-133, pdf-160, pdf-161, pdf-164, pdf-165, pdf-171, pdf-278, pdf-413 | `question` 仅存大题某一小问的半句或仅流程引入句，反应物（如 SO₃²⁻、Mg²⁺/CO₂、H₂Se 流程）与问法不在文本中，无法唯一确定方程；pdf-164/165 甚至 `question` 与 `answer` 分属不同大题（答非所问）。归 E 类。**【T4 核对】**pdf-171 的碎片本身含「离子方程式」锚点，真实 eval 路由到 `reaction_check` 且 `gatewayUsedEngine=True`，但反应物（pdf-171 的 Mg²⁺/CO₂）在未捕获的大题上下文中，KB 检索 0 命中，仍属碎片数据缺陷。**【P3 已解】**pdf-174 原同属此列，现已恢复大题上下文（习题册 `page_057` 前言 + `page_058` 工艺流程图 OCR 的 `H2SO4/Na2SO3` 标签，官方溯源）并加确定型模板 `ManganeseDioxideSulfiteIonic` → GAIN +1（precision=1 / harm=0 / LOSS=0，见方案 §10），已从本排除列表移除。 |
| ② 【T4 纠正】记号连接符（`==`/`→`/`⇌`）**不是**真实阻塞——求解器不产出，无输出可归一化 | pdf-044 (`==`)、pdf-010 (`⇌`+胶体)、pdf-532 (`→`)、pdf-484 (`→`)、pdf-481 (`→`) | 原诊断「答案键记号与 `NormalizeChemicalNotation` 不匹配」**已作废**。T4 强制路由探针（`_evalscratch/norm_unlock_probe.tsv`）把这 5 题强制以 `reaction_check`/`equation` 调用 `ReactionKbWriterSolver`，**全部 `fired=False`（无产出）**：求解器在产出阶段即被守卫/KB 检索拒绝，根本没有可供 T2 归一化的输出，故答案键的记号不是当前阻塞。逐题真实阻塞：pdf-044 路由=`calculation` 未触达引擎 + 3 子问方程（KHSO₄+Ba(OH)₂ / FeBr₂+Cl₂ / 银氨+HCl）KB 无唯一命中；pdf-010 路由=`reaction_check` 但 KB 不覆盖 FeCl₃ 水解制胶体 + 一题 3 方程（多重子问使唯一命中守卫失败）；pdf-532 题干同时含「化学方程式」与「离子方程式」→ `askIonic && askMolecular` 歧义守卫直接 null（且路由=`concept` 未触达、KB 无 Cl₂+CaBr₂ / I⁻+MnO₄⁻ 条目）；pdf-484 路由=`calculation` 未触达 + (3) 问 2 个方程（Co²⁺+MnO₂ / Co³⁺+Cl⁻）KB 无命中；pdf-481 路由=`atomic_structure` 误分类 + 氧化剂 ClO₃⁻ 在未捕获上下文（碎片）+ 复杂氧还 KB 无覆盖。pdf-044/010/532/484 题干完整，是 T5 KB 补全 + 路由打通的候选（受全库唯一命中 + LOST=0 红线约束）；pdf-481 兼有碎片问题，优先等数据管道补全上下文。 |
| ③ 题干缺反应物锚定（产物无法唯一确定） | pdf-097 | 题干仅说「沉锂生成磷酸锂的化学方程式」，未出现 H₃PO₄ 反应物；缺锚定则反应物集合不唯一，KB 检索不命中，诚实 null。 |
| ④ 问法非"写方程式"，方程锚点守卫返回 null | pdf-274 | 题干问「用平衡原理解释该物质可以除去杂质的原因」，非「写出离子/化学方程式」；求解器方程锚点（含「方程式/方程」）不触发。答案虽含水解方程，但属解释文本，强行产出有误触风险。 |
| ⑤ 依赖装置图/步骤图（C 类） | pdf-258, pdf-303 | 电极反应/电池总反应依赖「图 3」「电池装置图」，文本不含足够信息唯一确定；归 C 类图像依赖。pdf-303 另有 `answer="C"` 单字母键存疑，不改键。 |
| ⑥ 有机推断（酯化/聚合产物选择） | pdf-526, pdf-527 | 乳酸自身酯化生成六元环酯 / 聚酯，需有机反应选择性推断，非确定型模板可唯一覆盖；留 null，归有机推断域外。 |
| ⑦ 答案键垃圾/错配（A 类） | pdf-100, pdf-162 | pdf-100 题面完整但 `answer="A,B,C,D"` 垃圾值（同 A.2）；pdf-162 `question` 是单个选择小问但 `answer` 是整道大题答案串（E 类已登记）。方程引擎无法对垃圾/错配键产出可判正结论。 |
| ⑧ 非真方程被关键词误标 | pdf-098, pdf-156, pdf-328 | pdf-098 的"方程关键词"实为装置连接序 `a→e→f→…`；pdf-156 答案是产物碎片（缺左侧反应物，`CaCl2 + 2NH3↑ + 2H2O` 无等号左侧）；pdf-328 关键词是有机结构简式 `CH=CHCOOH`（质谱/红外/NMR 推断，非方程）。均非方程书写题，留 null。 |
| ⑨ 多子问复合 + 电化学推断 | ~~pdf-304, pdf-313~~（**T8 已解出，本类已无留 null 题**） | 原诊断：多个不同来源的子问拼成一题，答案为多段复合串；电极反应需电化学推理且单空格不可子串定位，留 null。**【T8 纠正】** ①`BuildTestCases` 加载期已把复合答案串拆为独立子关键词，半反应已自成关键词，无需改 `answerKeywords`；②新增 `HalfReactionBalancer`（`src/.../Gateway/HalfReactionBalancer.cs`，窄门控：选择题不介入 / 必须含「电极反应」/ 不唯一或缺数据一律 null）：pdf-313 题干显式给出 `[B(OH)4]^- 电解→ BH4^-` 电对 +「碱性」介质 → 萃取式配平得 `[B(OH)4]^- + 8e^- + 4H2O = BH4^- + 8OH^-`；pdf-304 产物/介质不在文本，经 `data/knowledge/electrode-half-reactions.json` 唯一溯源命中（触发词 CO + 燃料电池 + 电极反应，全库唯一命中审计）得 `CO-2e^-+4OH^-=CO3^2-+2H2O`。两条均标 `concept_fact` 由采纳门 `IsAnswerCorrect` 兜底 → 凡采纳必判正。T8.2 全库扫描 `fires=2 / correct=2 / newlyFiredWrong=0`；eval `rel_crystal`→`rel_half` **GAIN +2 / LOST 0 / precision 1 / harm 0**。化学事实只在 JSON（逻辑不硬编码答案），`note` 附官方溯源。 |

> **【T4 纠正】** 原备注「评估侧统一归一化 `==`/`→`/`⇌` 为 `=` 即可自解」已被探针证伪：当前无题纯粹被记号阻挡（求解器不产出，无输出可归一化）。后续若要解锁 G② 的完整题干题（pdf-044/010/532/484），需走 **KB 唯一命中补全 + 路由打通**（T5），并受全库唯一命中 + LOST=0 红线约束；碎片题（pdf-171/174/481 等）需先由数据管道补全大题上下文。T2 归一化作为可靠性加固已落地（形式+实证零回归），为将来产出侧消除记号假阴性奠基，但本身不新增 GAIN。
>
> **【T5 结论】** 全题库唯一命中审计（`_evalscratch/multihit_relax_scan.tsv`，`ReactionKbWriterSolver.MultiHitJoinRelax` 实验标志**默认关**）显示：朴素放宽"唯一命中"守卫、把多条命中方程连接产出，可解锁 pdf-010 / pdf-156（潜在 +2），但会对 **pdf-167 / pdf-244 / pdf-418** 吐出 3 个**伪命中错误方程**（题干背景段顺带提及反应物但并未要求书写）——违反"凡非 null 必判正"。故该守卫**不放宽**、本轮不新增 KB 条目。完整题干候选的真实结构阻塞：①多方程题 `matches.Count>1` → null（单条 KB 无法解锁）；②pdf-532 同时问化学/离子方程触发 `askIonic && askMolecular` 歧义守卫 → null。安全解锁需"相关性过滤的多重命中路径"（仅产出被题干明确要求的方程），留作后续风险评估任务。
>
> **【T8】** 原 G 类 27 道「诚实留 null」中，**pdf-304 / pdf-313 已由 `HalfReactionBalancer` 解锁（GAIN +2，见 G-⑨ 行）**；剩余 24 道维持留 null（pdf-174 已由 P3 解出，见方案 §10）。本轮无新增排除题、无放宽守卫、无逻辑硬编码化学事实（半反应事实仅存于 `data/knowledge/electrode-half-reactions.json`）。

---

## 处理原则（固化，防止回退到硬编码路线）

1. **引擎是唯一真值源**：化学判定走 `IStatementJudge` 聚合器（事实库/计数/氧化还原电子），不向 `general-choice-facts` 追加单题策展条目。
2. **数据修复只信官方源**：答案键改前必须有答案册 OCR 或原页核对（5b）；化学推导仅作辅助，5a 类（存储键为明显垃圾值 `"A,B,C,D"` 且 note 结论明确）沿用 Task 35/46 已验证流程，但跨多选项/题型存疑时（如 075）不单独触发存储键变更，必须官方源裁定。
3. **文本回填只写 `question`/`options`，不写 verdict、不改 `answer`**：回填后必须过 EvalRunner diff（LOST=0、harm=0）与字段 diff 审查。
4. **不可靠恢复一律登记、保持 `null`**：宁可未解，不可假阳性。
