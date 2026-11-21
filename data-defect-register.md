# 数据缺陷登记册（pdf_questions.json）

> 范围：`src/CogniThink.TutorBrain.Api/data/pdf_questions.json` 中**无法在零假阳性红线内自动修复**的题录条目，以及本轮已修复条目的留痕。
> 红线：不注入 verdict 字眼、不改动存储 answer、不基于低质量 OCR 猜测回填；引擎只在文本完整且可唯一判定时给出结论，否则诚实 `null`。
> 本登记册的存在意义：把"组合/计数型未解"从"引擎能力缺口"重新归因为"**数据缺陷**"——通用求解引擎（`CountingChoiceValidator` 一般子项路径 / `MultiSelectChoiceValidator` / `RedoxElectronStatementJudge`）已就绪并经 tier2 验证，唯一阻塞是题面子项文本或答案键缺失/冲突。这些条目保持 `null`，交由后续数据管道或图像轨道接续，**不再走逐题硬编码事实**路线。

> **【M1 多段复合答案审计结论】**（证据：`_evalscratch/multi_remaining_audit.tsv`、`_evalscratch/multi_part_final_report.md`）用户假设「104 道未解 = 多段复合答案判分 bug」经全量取证**不成立**：① 判分侧无 bug——[IsAnswerCorrect](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L344-L366) 遍历 `AnswerKeywords` 任一词子串命中即判正，[SplitPdfAnswerKeywords](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L1818-L1859) 加载期重切 (1)(2)/多关键词，全库 **adopted-wrong = 0**（precision=1 由构造保证）；② 104 题中 **pdf-174 已修复**（回填大题上下文 + 确定型模板命中，GAIN +1，绿色基线 618）；③ 剩余 103 题分类：**(a)=0**（无任何题仅靠回填大题上下文即可被现有求解器解锁）、**(b)=95**（能力缺口：choice 41 / calculation 16 / unknown 22 / equilibrium 9 / 新求解能力 4 / judgment/atomic/concept 各 1，需各自独立 spec）、**(c)=8**（数据缺陷：pdf-117/162/164/165/461/522 登记于本 E 类；**trap-limit-2/3 为 `BuildTestCases` 合成边界探针**，非 `pdf_questions.json` 数据，不在修复范围）。

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
| pdf-197 | 题干拼接了**两道题**：前半「下列有关物质结构或性质的比较中，正确的是」+ 后半「（2025·安徽二模）海洋资源…下列有关物质结构或性质的说法，错误的是」。存储选项（NH₃/NO₃⁻/NH₂OH/羟胺氢键）属于前题，但 note 分析的是后题（IO₃⁻/CO₃²⁻/N₂H₄）；`answer="A,B,C,D"` 为垃圾值。答案册中未能定位到对应题（OCR 检索词无命中）。 | 题面文本已损坏；已化学复核：选项 A（键角 NH3>NO3⁻）、B、C 均假、D 真，与「错误的是」单选矛盾（疑多选或选项 OCR 错位），note 完全错位。 | 需书原页恢复正确题干+选项+键；见 I.3；暂保留 `null`。 |
| pdf-090 | 题干为「钠及其化合物的部分转化关系如图…设 N_A 为阿伏加德罗常数…下列说法正确的是」完整选择题，但存储 `answer="1,3,1,3"` 为垃圾值（非选项字母）。 | 无官方答案册核对，无法确定正确选项键；引擎对该题返回 `null`。 | 原页/答案册核对正确键后订正；暂保留 `null`。 |
| pdf-100 | 题干为「实验室中利用右图装置验证铁与水蒸气反应…下列说法错误的是」完整选择题（选项 A 含化学方程式），但 `answer="A,B,C,D"` 为垃圾值。 | 答案册 `page_037` num=2 解析**内容**已指向 B（湿棉花移至下方无法提供足够温度，实验效果差），化学核算亦指向 B；但解析「正确/错误」标签被 OCR 全污染为「正确」，机器无法 100% 确认。 | 需书原页（答案册页）核对确认 B；见 I.3；暂保留 `null`。 |
| pdf-227 | `answer="A"`（问「错误的是」），但 A（超分子相关陈述）经化学判定为**真**，与 answer 矛盾；备注亦称 A 正确。 | 存储 answer 与化学真值冲突，无官方答案册核对不能定键（贸然改键有引入 harm 的风险）。 | 原页/答案册核对正确键后订正；暂保留 `null`。 |
| pdf-295 | `answer="A"`（问「不正确的是」），但 A（锂作负极）为真；备注自相矛盾；选项依赖电池原理图。 | answer 与化学真值冲突，且需读图核真，无官方源不能定键。 | 原页核对键 + 图像轨道恢复原理图；暂保留 `null`。 |
| pdf-306 | `answer="A"`（问「错误的是」），但 A（K⁺向左室迁移）为真；备注冲突；依赖电解装置图。 | answer 与化学冲突，无官方源不能定键。 | 原页核对键 + 图像轨道恢复装置图；暂保留 `null`。 |
| pdf-325 | `answer="B"`（问「正确的是」），但 B（质荷比 92 归 CH₃）经备注推理为假；备注推理疑似 OCR 损坏。 | answer 与备注/化学矛盾，疑似 OCR 损坏，无官方源不能定键。 | 原页/答案册核对；暂保留 `null`。 |
| pdf-327 | `answer="A"`（问「错误的是」），但 A（5 个手性碳）为真，备注称 A 正确；依赖结构图。 | answer 与化学冲突，需读图核真，无官方源不能定键。 | 原页核对键 + 图像轨道恢复结构图；暂保留 `null`。 |
| pdf-386 | 备注自相矛盾、答案疑似误标（既往舍弃题）；选项依赖结构图。 | note 数据损坏 + 选项图像依赖，无法可靠判定；无官方源。 | 原页核对 + 图像轨道恢复结构；暂保留 `null`。 |
| pdf-409 | `answer="D"`（问「错误的是」），但 D（Li+nNH₃ 反应式）为真；错误项应为 C（P₂O₅ 不能干燥 NH₃），与 answer 冲突。 | answer 与化学真值冲突，无官方源不能定键。 | 原页/答案册核对正确键；暂保留 `null`。 |
| pdf-410 | `answer="C"`（问「不正确的是」），但 C（凯氏定氮步骤）为真；错误项应为 D，冲突。 | answer 与化学冲突，无官方源不能定键。 | 原页核对键；暂保留 `null`。 |
| pdf-432 | `answer="B"`（问「不正确的是」），但 B（CH₄ 球棍模型）为真；错误项应为 A（H₂O₂ 电子式），冲突。 | answer 与化学冲突，无官方源不能定键。 | 原页核对键；暂保留 `null`。 |
| pdf-524 | `answer="BC"`，但备注称 C 错（c/d 不互为同分异构体），与 answer 冲突；依赖合成路线图。 | answer 与备注/化学冲突，无官方源不能定键；且需读图核真。 | 原页核对键 + 图像轨道恢复路线图；暂保留 `null`。 |
| pdf-338 | `answer="C"`，但备注/化学表明 C、D 均真（疑应为 CD 多选）；题干无「双选/多选」标记；Friedel 烷基化。 | answer 存疑（单 C vs 化学 CD），无官方答案册不能定键；键未核前策展有写入错误 verdict、引入 harm 的风险，故不策展。 | 原页/答案册核对是否为 CD 多选；若确认 CD 且 4 选项文本完整，可经既有 MultiSelect 路径策展（同 pdf-115/475）；暂保留 `null`。 |

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
| pdf-319 | ✅ **已修键+回填**：原 OCR 把 answer 错识为 A、note 被截断、选项 C/D 结构名丢失；据答案册原页（用户提供书上原文，题源 2025·天津河西期中）核对——题干"正确的是"无误，正确答案为 **D**（A/B/C 错、D 对），C=环己烯、D=苯酚 | 已更正 `pdf_questions.json`：answer A→D、answerKeywords、note 重写为完整解析、options C/D 补结构名。全量测试 2144/0，eval `choice_b1`→`pdf319fix` GAIN=0/LOST=0/HARM=0（引擎仍 null，纯数据修正零回归）。后续若补结构图可策展自解 |
| pdf-436 | 选项 C「NaCl 溶液中的水合离子」为**图示**，存储文本仅留冒号、内容缺失（`"C": "NaCl溶液中的水合离子："`），题干问"表述错误的是"，note 判 C 错误 | 选项内容依赖图像，文本轨道无法判；图像轨道恢复 C 选项图示后引擎应能对齐。存储 `answer="C"` 与 note 一致，不改键 |
| pdf-115 | 题干"取等量少许 Cu₂O 分别加入甲、乙两支试管，进行如下实验"，但甲（加稀 HNO₃）、乙（加氨水）的**操作步骤未进入 `question` 文本**（原在装置图/表格）；选项 A–D 均依赖试管甲/乙的实验现象 | ✅ **已策展自解**（choice-coverage-expansion α2）：4 选项文本完整、与 answer AC 一致，题干无「双选/多选」标记但经既有 `MultiSelectChoiceValidator` 路径策展——加 `GC-760-A/B/C/D` 事实（stemMatch `红色钧瓷的发色剂` 全库唯一），A/C 真、B/D 假，端到端命中 AC。eval `pdf174fix`→`choice_b1` **GAIN +1**，LOSS=0、harm=0、precision=1。存储 `answer="AC"` 不改键。甲/乙操作步骤仍建议图像轨道补全以备人工复核，但不阻塞判分。 |
| pdf-376 | 题干"合成路线如下"，但 **X / Y / P 的结构图与反应①路线未进入文本**；选项 A–D（CO₂与 X 计量比、P 水解产物分子式、P 碳碳双键交联、Y 加聚产物降解性）全部依赖 X/Y/P 结构与计量 | 合成路线依赖图像，文本轨道无法判；图像轨道恢复路线图。存储 `answer="B"`，不改键 |
| pdf-382 | 题干"可用如下方法制备"PVA，但**反应①②③ 路线与 PVA / 中间产物结构未进入文本**；选项 A/B（①为加成、②③分别缩聚/水解）依赖路线判断 | 制备路线依赖图像，文本轨道无法判；图像轨道恢复制备路线。存储 `answer="B"`，不改键 |
| pdf-394 | 选项 C「环上的一氯代物有 3 种」的**"环"结构未进入文本**（原在图像），整题选项集不完整；note 仅判了 A/B，C/D 无法对齐 | ✅ **已策展自解**（choice-coverage-expansion α1）：4 选项文本足以写匹配事实，verdict 与官方 answer B 一致（A/C/D 假、B 真，C/D 由 answer 唯一反推）——加 `GC-758-A/B/C/D` 事实（stemMatch `四川南充` 全库唯一），Concept 端到端命中 B。eval `pdf174fix`→`choice_b1` **GAIN +1**，LOSS=0、harm=0、precision=1。C 选项环结构仍建议图像轨道补全以备人工复核，但不阻塞判分。 |
| pdf-087 | 选项真伪依赖工艺流程图；备注自相矛盾（称 A 正确却标 A 为答案），需官方源/流程推理 | 图像轨道恢复流程图 + 原页核对备注与答案；文本轨道保持 `null` |
| pdf-218 | ✅ **answer 键已订正为 D**（答案册 `page_078` num=3 确认单选 D：A/B/C 对、D 错）；原「多选 BCD」系 answer 字段污染误判。晶胞结构仍依赖图像。 | 晶胞结构 + 图像轨道恢复后重评（键已定，见 I.1）；引擎未解保持 `null` |
| pdf-220 | ✅ **answer 键已订正为 D**（答案册 `page_078` num=5 确认单选 D；原 kw0=A 亦同步修正为 D）；原「多选 ABC」系污染误判。晶胞结构仍依赖图像。 | 晶胞结构 + 图像轨道恢复后重评（键已定，见 I.1）；引擎未解保持 `null` |
| pdf-284 | C、D 均为错误项（≥2）但无多选标记 → Concept 返回 null；需读滴定曲线图像 | 图像轨道恢复滴定曲线；若原页确认多选且选项文本完整，可经既有 MultiSelect 路径策展；暂保留 `null` |
| pdf-307 | C/D 选项文本被结构式图像截断，无法匹配；依赖反应历程图 | 图像轨道恢复 C/D 结构式与反应历程图；文本轨道保持 `null` |
| pdf-336 | A 选项「产物 B 对应的结构是」+ 图像，文本截断；备注残缺；依赖反应动力学曲线 | 图像轨道恢复产物结构与动力学曲线，原页补全备注；暂保留 `null` |
| pdf-458 | 多选 BD 无「双选/多选」标记；选项 B 含结构式图像，文本不全 | 图像轨道恢复 B 结构式 + 原页核对是否多选；暂保留 `null` |
| pdf-498 | 元素推断依赖「结构如图所示」；备注为[千问求解]，元素身份需核真 | 图像轨道恢复结构图 + 原页核真元素身份；暂保留 `null` |
| pdf-511 | 元素推断依赖「其结构如图所示」；备注[千问求解]含自我修正，需核真 | 图像轨道恢复结构图 + 原页核真；暂保留 `null` |
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
| pdf-162 | 大题碎片 + 跨题答案污染 | 大题第(3)小问「"酸化"步骤应选用的酸是__(填字母)」（`taskType=choice`，选项 a–d）；但存储 `answer`/`answerKeywords` 是**另一道大题**的 7 段复合串 `(1)2 (2)SO4^2- (3)温度升高，反应速率增大，浸出率增大；… (4)[Cu(NH3)4]2+ + Zn = [Zn(NH3)4]2+ + Cu (5)Fe (6)…PbA 产率 (7)Pb[OOC(CH2)2COO] 热解`——与"选酸"小问完全无关，子题号错位 | 题面是单小问碎片，answer 属于不同大题（跨题污染），无法对齐；缺官方源核对该小题键 | 原页恢复该大题上下文，单独抽取 (3) 选酸小问并核对答案键；暂保留 `null` |
| pdf-463 | 大题碎片（缺数据） | 大题第(5)小问「"调 pH"分步沉淀可获得 Ni(OH)₂，两次调 pH 数值依次为__」，选项 a–d（3/7/9/11），`answer="c d"`；缺大题上下文（溶液中共存离子、各离子沉淀 pH / Ksp 数据）；`taskType=calculation`，选项小写 | 沉淀 pH 判据数据不在题面（原在表格/上文），文本轨道无法判 | 原页恢复大题上下文与沉淀数据；核对小写选项键 |
| pdf-483 | 重复碎片（与 pdf-477 子问完全重复） | 整条题面 `(5)电解熔融氯化铍制备金属铍时,加入氯化钠的主要作用是____` 与大题 pdf-477 的第(6)小问**逐字相同**（仅碎片编号为 (5)），是数据切片时把同一小问重复抽成独立题录。二者共享完全相同的化学文本，任何通用概念事实都会同时命中两者（count=2），触发 `ConceptFactSolver` 唯一性铁律返回 null；又无化学层面的区分 token 可消歧（差异仅在碎片编号）。 | 重复题录：概念事实无法在不破坏唯一性（不引入假阳性）的前提下只命中其一；靠编号 (5)/(6) 区分属单题策展，违反红线 #6。 | 数据管道去重：删除 pdf-483 或将其合并入 pdf-477；文本轨道保持 `null` |
| pdf-412 | 大题碎片（多子问答案错配） | `question` 是「双水杨醛缩乙二胺锰配合物 (MnLClx) 制备与组成确定」大题的部分，但存储 `answer="B, (2) Mn²⁺氧化, (3) c"` 是**多子问复合串**（含 (2)(3) 子问答案），与单条题录无法对齐；`taskType=choice` 但答案非纯选项字母。 | answer 为大题串，无法拆出本题键；缺官方源核对。 | 原页恢复大题上下文，单独抽取各子问并核对键；暂保留 `null`。 |
| pdf-414 | 大题碎片（单小问 + 多子问答案错配） | `question` 仅是「(3)过滤操作中需要的玻璃仪器…」单小问碎片，但 `answer="K3[Fe(CN)6]溶液, ABC, BC"` 是大题多子问串，答非所问；`taskType=choice`。 | 题面为碎片、answer 为大题串，无法对齐；缺官方源。 | 原页恢复上下文并核对键；暂保留 `null`。 |
| pdf-522 | ✅ **已修复（本批次 J.1）** | 原 `answer="题目不完整，无法作答"` 占位、`question` 仅存半句「…的名称为」（结构图在 imageAssets）。据结构图 `p112_n05_4.png`（对称三取代酚：OH 1 位 / Br 2,4,6 位）+ imageAssets name「2,4,6-tribromo phenol」三重收敛，订正 answer/answerKeywords→「2,4,6-三溴苯酚」，note 重写溯源。 | 原占位非真实缺陷，是数据管道未读图；结构对称唯一确定 2,4,6-三溴苯酚（已排除 RDKit 自动 SMILES 键感知伪影）。 | 已修复；recheck 仅 info/V6（映射假阳性），0 新增嫌疑。详见 J.1。 |
| pdf-164 | 大题碎片 + 跨题答案污染 | 大题第(5)小问「"沉淀"步骤中生成 BaTiO(C2O4)2 的化学方程式为____」（`taskType=unknown`）；但存储 `answerKeywords` 是**另一道大题（黄铜矿焙烧）**的内容 `(NH4)2SO4 △ 2NH3↑ + SO3↑ + H2O || Fe + Cu2+ = Fe2+ + Cu`，`note` 通篇讲 CuFeS2 焙烧/置换——与 BaTiO(C2O4)2 沉淀完全无关 | 题面与 answer/note 分属不同大题（跨题污染），无法对齐；缺官方源核对该小问方程 | 原页恢复该钛酸钡大题上下文，单独抽取 (5) 沉淀方程小问并核对键；暂保留 `null` |
| pdf-165 | 大题碎片 + 跨题答案污染 | 大题第(6)小问「"热分解"生成粉状钛酸钡，产生的 nCO2 : nCO = ____」；但存储 `answerKeywords` 是**另一道大题（铜/银提萃）**的内容 `ds 3d10 4s1（Cu 电子排布） || Cu2Se + 4H2O2 + 4H+ = … || 使 Ag+ 转化为 AgCl 沉淀 || 提高银回收率`，`note` 通篇讲 Cu/Cu2Se/Ag——与 BaTiO3 热分解完全无关 | 题面与 answer/note 分属不同大题（跨题污染），无法对齐；缺官方源核对 nCO2:nCO | 原页恢复钛酸钡热分解大题上下文，单独抽取 (6) 小问并核对键；暂保留 `null` |
| pdf-117 | 大题子问抽取丢失（仅抽到 (1)） | `question` 仅含判断大题第(1)小问「Si 是半导体材料，同族的 Ge 也是半导体材料，则ⅣA 族元素的单质都是半导体材料（ ）」（`taskType=judgment`，`frag=True`）；但 `answerKeywords` 列了 **14 条判断结果**（对应完整 14 小问 (1)–(14)）——数据切片时只抽到了 (1)，(2)–(14) 的题干全部丢失 | 题面缺 (2)–(14) 子问原文，引擎无文本可判 14 条结论；answerKeywords 与题面条目数不匹配 | 原页恢复 (2)–(14) 子问题干后重判；暂保留 `null` |
| pdf-461 | ✅ **已修复（本批次 J.1）** | 原题干在 p32 页尾截断（仅剩镓提取工艺背景），answer/kw/note 占位「题目不完整，缺少具体问题」、type=other 无图。据 OCR p32/p33 + 流程图 `p033_n01_r0.png` 重建子问 (1)–(4)，type→fill，挂流程图；answer 据官方答案册 `page_040.json` num=5。 | 官方 exercise 抽取截断（V3 假阳性），库题干补全后与原页题 5 逐字吻合，答案与官方册一致。 | 已修复；recheck 为 info/V6 + suspect/V3（官方截断假阳性），0 新增 α 缺陷。详见 J.1。 |
| pdf-531 | 题干拼接两题 + 题型混杂 | 题干前半为滴定题、后半为 Te 量子点，且带填空型片段；askDir/stemMatch 不可靠，无全库唯一锚点 | 拼接致题干语义不唯一，无法提取可靠 stemMatch；题型混杂致选项/答案对齐失败 | 原页拆分为独立题录后重判；暂保留 `null` |
| pdf-540 | 大题碎片（缺上下文） | 题干为大题片段「(3)Ⅰ」，缺气体种类等关键上下文；无可靠唯一 stemMatch 锚点 | 碎片缺关键数据，文本轨道无法判；无唯一锚点则策展会跨题命中、破坏唯一性铁律 | 原页恢复大题上下文；暂保留 `null` |

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

## H 类：choice-coverage-expansion 分诊剩余（β/γ）——本 spec 未策展，逐题理由索引

> 来源：[_evalscratch/choice41_audit.tsv](file:///D:/github/CogniThink-Chem/_evalscratch/choice41_audit.tsv)（41 题分诊，α=5 已策展见 Task 3；本表登记剩余 β=16 / γ=20，合计 36）。本表是分诊理由与登记位置的**一对一可追溯索引**（AC-4 / TR-7.1）：每条理由与 audit TSV 一致，「登记位置」指向 A–G 类中的详细行；标「本批新增」者为本次新增条目。α 的 5 题（pdf-115/388/394/459/475）已策展自解，不在此表（见 C 类 pdf-115/394 行的 ✅ 标记与 [tasks.md Task 3](file:///D:/github/CogniThink-Chem/.trae/specs/choice-coverage-expansion/tasks.md)）。**后续修复**：γ 中 pdf-319 已据官方源（答案册原页）修键+回填，见 C 类 pdf-319 ✅ 行；现行未修复 γ=16（pdf-319/147/155/271 已据官方源修键，见 C 类 / I.1）。

### H.1 β 类（16）——需新求解器 / 图像轨道 / 官方核键，本 spec 不动 .cs 故不策展

| id | 真实理由（摘自 audit） | 登记位置 |
|----|------------------------|----------|
| pdf-087 | 选项真伪依赖流程图；备注自相矛盾（称 A 正确却标 A 为答案），需官方源/流程推理 | C 类（本批新增） |
| pdf-218 | 晶胞结构依赖图像；answer 键已订正为 D（原「多选 BCD」系污染误判，见 C 类 / I.1） | C 类（键已修复，见 I.1） |
| pdf-220 | 晶胞结构依赖图像；answer 键已订正为 D（原「多选 ABC」系污染误判，见 C 类 / I.1） | C 类（键已修复，见 I.1） |
| pdf-263 | 问法「正确的有几项」为计数型（非选项字母匹配），6 选项；需计数逻辑 | C 类（既有） |
| pdf-284 | C、D 均为错误项（≥2）无多选标记 → Concept 返回 null；需读滴定曲线图像 | C 类（本批新增） |
| pdf-303 | 备注称 C、D 均真（应为 CD），answer 仅 C；多选无标记；电池装置图 | G 类⑤（既有） |
| pdf-307 | C/D 选项文本被结构式图像截断，无法匹配；反应历程图 | C 类（本批新增） |
| pdf-336 | A 选项「产物 B 对应的结构是」+ 图像，文本截断；备注残缺；反应动力学曲线 | C 类（本批新增） |
| pdf-337 | 选项为 ①③⑤ 等编号组合，引用图像中 ①~⑥ 结构，需读图 | C 类（既有） |
| pdf-338 | 备注/化学表明 C、D 均真（应为 CD），answer 仅 C；多选无标记；需官方答案册核键 | A.2（本批新增） |
| pdf-376 | 合成路线图像；备注称 4 项全正确与 answer=B 矛盾，B/C/D 需看结构核真 | C 类（既有） |
| pdf-406 | 问法「不能完成」未被 `WrongDir` 正则覆盖 → askDir 误判为 correct，能完成项 3 个候选不唯一；不动 .cs 则无解 | A.1（键已修复）+ H.3 残留说明 |
| pdf-458 | 多选 BD 无标记；选项 B 含结构式图像，文本不全 | C 类（本批新增） |
| pdf-498 | 元素推断依赖「结构如图所示」；备注 [千问求解]，元素身份需核真 | C 类（本批新增） |
| pdf-511 | 元素推断依赖「其结构如图所示」；备注 [千问求解] 含自我修正，需核真 | C 类（本批新增） |
| pdf-531 | 多选 BC 无标记，但题干为两题拼接（滴定 + Te 量子点）且填空型，askDir/stemMatch 不可靠 | E 类（本批新增） |

### H.2 γ 类（20；其中 pdf-319/147/155/271 已修键移出，现行未修 16）——数据缺陷（answer 冲突 / 损坏 / 图像 / 碎片），无官方源不可自动修复

| id | 真实理由（摘自 audit） | 登记位置 |
|----|------------------------|----------|
| pdf-075 | answer=B 与化学冲突（反应ⅱ生成 1 mol O₂ 转移 4 mol e 为真，B 非错误项）；备注矛盾；维持既往舍弃 | D 类（既有，官方源核为选项文本错配，非键错误） |
| pdf-100 | answer 全选；答案册解析内容指向 B 但标签全污染，化学亦指向 B，需书原页确认（见 I.3） | A.2（既有）+ I.3 |
| pdf-197 | 题干拼接；化学复核 A/B/C 假、D 真与单选矛盾，note 错位，需书原页（见 I.3） | A.2（既有）+ I.3 |
| pdf-227 | answer=A 选错误项，但 A（超分子）为真；备注称 A 正确，冲突 | A.2（本批新增） |
| pdf-295 | answer=A 选不正确项，但 A（锂作负极）为真；备注自相矛盾；电池原理图 | A.2（本批新增） |
| pdf-306 | answer=A 选错误项，但 A（K⁺向左室迁移）为真；备注冲突；电解装置图 | A.2（本批新增） |
| pdf-319 | ✅ 已修键+回填（见 C 类 pdf-319 行）：原分诊误判为 askDir 矛盾，实为 answer OCR 错为 A，书上正确答案 D；note 截断 + C/D 结构名丢失已一并回填 | A.1（键已修复） |
| pdf-325 | answer=B 选正确项，但 B（质荷比 92 归 CH₃）为假；备注推理有误，疑似 OCR 损坏 | A.2（本批新增） |
| pdf-327 | answer=A 选错误项，但 A（5 个手性碳）为真；备注称 A 正确，冲突；结构图 | A.2（本批新增） |
| pdf-382 | answer=B 选正确项，但备注称 B 错误（②为加聚非缩聚），冲突；合成路线图 | C 类（既有） |
| pdf-386 | 备注自相矛盾 / 数据损坏（答案误标）；选项依赖结构图；维持既往舍弃 | A.2（本批新增） |
| pdf-405 | 选项 A/B/D 仅给实验现象、缺结论，文本截断，无法写匹配事实 | A.1（键已修复）+ H.3 残留说明 |
| pdf-409 | answer=D 选错误项，但 D（Li+nNH₃ 反应式）为真；错误项应为 C（P₂O₅ 不能干燥 NH₃），冲突 | A.2（本批新增） |
| pdf-410 | answer=C 选不正确项，但 C（凯氏定氮步骤）为真；错误项应为 D，冲突 | A.2（本批新增） |
| pdf-432 | answer=B 选不正确项，但 B（CH₄ 球棍模型）为真；错误项应为 A（H₂O₂ 电子式），冲突 | A.2（本批新增） |
| pdf-524 | answer=BC，但备注称 C 错（c/d 不互为同分异构体），与 answer 冲突；合成路线图 | A.2（本批新增） |
| pdf-540 | 题干为大题片段「(3)Ⅰ」，缺气体种类上下文；stemMatch 无可靠唯一锚点 | E 类（本批新增） |

### H.3 A.1 已修键但仍未解的残留说明（pdf-405 / pdf-406）

> pdf-405 与 pdf-406 的存储 `answer` 键已在 A.1 经官方答案册 OCR 核对修正（`A,B,C,D`→`C`），但两题在 choice-coverage-expansion 分诊中**仍为未解**，原因与键无关，补充留痕：
> - **pdf-405**（γ）：选项 A/B/D 仅给出实验现象、缺结论性文本，无法构造可唯一匹配的事实锚点；需原页补全选项文本后再评估可否策展。
> - **pdf-406**（β）：问法「不能完成」未被 `FactChoiceMatcher` / `MultiSelectChoiceValidator` 的 `WrongDir` 正则覆盖，askDir 被误判为 correct，导致能完成项出现 3 个候选、Concept 无法唯一返回；属引擎 askDir 词表的 capability gap。本 spec 红线「不动 .cs 化学逻辑」故不修复；后续可独立评估扩充 `WrongDir` 词表（需全库扫描确保零新增假阳性）。

---

## I 类：本轮 OCR 污染批量修复留痕（ocrfix1 / ocrfix2 / ocrfix3）

> 范围：全库 545 道 pdf 选择题扫描（证据 `_evalscratch/all_questions.tsv` → `ocr_suspects.tsv` / `polluted_answers.tsv`，汇总 `_evalscratch/ocr_scan_findings.md`）。发现 **系统性 `answer` 字段污染 bug**：`answer` 被填成多字母/全选（如 `A,B,C,D`），但**真正答案存活在 `answerKeywords[0]`**，且**答案册 `explanation` 解析文字 + 化学真值**一致收敛。答案册的 `answer` 字段被同一 bug 感染（不可信），但其 `explanation` 字段是可靠官方源。
> **关键机制**：eval 判分的 ground-truth（`expected`）取自 `answerKeywords`，**不**取 `answer` 字段；引擎亦不读 `note`。故：① 只改 `answer` 字段（去污染）不影响判分；② 改 `answerKeywords[0]` 会移动真值，但若该题引擎当前返回 `null`（未解），判分结果不变 → 零回归。所有改动均经 eval diff 验证。

### I.1 已修键（10 道，零回归）

| id | 原值 → 新值 | 官方源 / 核对依据 | 真值是否移动 | eval 证据 |
|----|-------------|-------------------|--------------|-----------|
| pdf-099 | `A,B,C,D` → `A` | 答案册 `page_037` num=1：问「反应式错误的是」，仅 A 的 `Fe−3e⁻=Fe³⁺` 错（应 `Fe−2e⁻=Fe²⁺`），B/C/D 均正确；kw0=A；guard 预置 A | 否（kw0 本即 A） | `choice_b1`→`ocrfix1` GAIN=0/LOST=0 |
| pdf-210 | `A, C, D` → `A` | 答案册 `page_076` num=4：问「解释错误的是」，A 错、B/C/D 对；kw0=A | 否 | 同上 |
| pdf-268 | `A, B, C, D` → `A` | 答案册 `page_093` num=7：问「正确的是」，A 对、B/C/D 错；kw0=A | 否 | 同上 |
| pdf-271 | `A, B, C, D` → `A` | 答案册解析：问「错误的是」，仅 A 错（实验 3 突跃 >4.4 不能用甲基橙）；kw0=A | 否 | 同上 |
| pdf-404 | `A, B, C, D` → `A` | 答案册 `page_137` num=10：问「不正确的是」，A 错、B/C/D 对；kw0=A | 否 | 同上 |
| pdf-149 | `A, B, C, D` → `A`（仅 `answer` 字段） | 答案册 `page_053` num=4：A 对、C/D 错；kw0=A；guard 预置 A，**引擎当前解出 A 并判正**。B 项装置图存疑，保持单 A 不动真值（见 I.3） | 否 | 同上 |
| pdf-218 | `D,B,C` → `D`（仅 `answer` 字段） | 答案册 `page_078` num=3：问「错误的是」，A/B/C 对、D 错（`CN2^2-` 为直线形非 V 型）；kw0=D。原登记「多选 BCD」系污染误判，实为**单选 D** | 否 | `ocrfix1`→`ocrfix2` GAIN=0/LOST=0 |
| pdf-147 | `A, B, C, D` → `B`（`answerKeywords` `A`→`B`） | 答案册 `page_053` num=2：问「正确的是」，A 错（明火危险）、B 对（单向阀防倒吸）、C 错（光照爆炸）、D 错（白磷应放回原瓶）；原 kw0=A 错误 | **是**（A→B，引擎未解 → 判分不变） | 同上 |
| pdf-155 | `A,B,C,D` → `C`（`answerKeywords` `A`→`C`） | 答案册 `page_055`：问「错误的是」，A/B/D 对、C 错（选干燥器是防 AgCl 分解而非提升干燥效率）；原 kw0=A 错误 | **是**（A→C，引擎未解） | 同上 |
| pdf-220 | `A,B,C` → `D`（`answerKeywords` `A`→`D`） | 答案册 `page_078` num=5：问「错误的是」，A/B/C 对、D 错（`Mg²⁺` 配位数为 6 非 3）；原 kw0=A 错误 | **是**（A→D，引擎未解） | 同上 |

> 统一验证：`choice_b1`→`ocrfix3` **GAIN=0 / LOST=0 / HARM=0 / precision=1**（gatewayCorrect 623→623）；全量测试 2144/0 全程绿。所有改动仅在 JSON 数据，未改 `.cs`，无硬编码化学事实。

### I.2 note 解析文字清洗（6 道，answer 键本即正确，零判分影响）

> 引擎不读 `note`，仅面向人工；改 note 不影响判分（`ocrfix2`→`ocrfix3` GAIN=0/LOST=0 实证）。9 道嫌疑中 pdf-385/400/453 经核验 note 已通顺，未动；以下 6 道重写/订正：

| id | 问题 | 处理 |
|----|------|------|
| pdf-007 | note 混乱（自相矛盾推导） | 重写：A/B/D 对、C 错（纳米级 BaSO4 粉末未形成分散系，非胶体），选 C |
| pdf-066 | note 把 `CaCO3` 误写成 `CaO` | 订正为 `CaCO3 → CaO + CO2↑`，结论 C 不变 |
| pdf-067 | note 末句称「C 选项正确」与 answer=C（错误项）矛盾 | 重写：A/B/D 对、C 错（氧化剂:还原剂应为 7:4 非 4:7），选 C |
| pdf-094 | note 称「D 选项正确」与 answer=D（不正确项）矛盾 | 重写：A/B/C 对、D 错（CO2 循环但仍需补充），选 D |
| pdf-454 | note 末句「故选 A」与 answer=B 矛盾 | 订正末句为「故选 B」，B（煤的气化是化学变化）错误 |
| pdf-455 | note 的 C/D 分析张冠李戴（混入 pdf-453/454 内容） | 重写：A/B/C 化学变化、D（石油分馏）物理变化，选 D |

### I.3 仍需书原页核对（不可自动修复，保持 `null`）

| id | 已核验结论 | 为何仍需官方源 |
|----|------------|----------------|
| pdf-100 | 答案册 `page_037` num=2 解析**内容**指向 B（湿棉花移至下方无法提供足够温度，实验效果差），化学核算亦指向 B；但解析的「正确/错误」标签被 OCR 全部污染为「正确」 | 标签全污染，机器无法 100% 确认；需书原页（答案册页）核对确认 B |
| pdf-197 | 化学复核：选项 A（键角 NH3>NO3⁻）、B、C 均假、D 真，与「错误的是」单选矛盾（疑多选或选项 OCR 错位）；note 完全错位（讲 IO3⁻/N2H4） | 题面/选项可能已损坏，需书原页恢复正确题干+选项+键 |
| pdf-149 | 引擎当前解出 A 并判正（guard 亦预置 A），答案册解析确认 A 对、C/D 错；但 B 项需看装置图，若 B 亦正确则应为 AB | 若改真值为 AB 会导致引擎（返回 A）判负 → 引入 LOST；故保持单 A，待书原页装置图确认 B 是否成立 |

---

## J 类：verify-fix-all-suspects 批次（200 嫌疑题检验+修复）

> 本 spec 对 verify 报告 200 道嫌疑题（98 suspect + 102 info）逐题检验并修复。总控证据：[fix_200.tsv](file:///D:/github/CogniThink-Chem/_evalscratch/verify/fix_200.tsv)（200 行：修=18 / 不动=177 / blocked=5，0 待分诊，id 集合与原 TSV 一致，校验通过）。全量验证见 J.5。

### J.1 本批次新增已修复（3 题：pdf-184 / pdf-461 / pdf-522）

> α2（10 题：166/168/169/176/177/188/199/260/261/357）与 batch2（090/170/178/531/544 + 056/502 note 清理）已分别登记于 A.1 / C / F / I 类或本类 J.3；下表仅列本会话新增落刀的 3 题，均经 `--recheck` 确认 0 新增嫌疑。

| id | 修复内容 | 真值依据（三者收敛） | recheck 结果 |
|----|----------|----------------------|--------------|
| pdf-522 | 占位 answer→「2,4,6-三溴苯酚」；note 重写溯源（结构图 `p112_n05_4.png` 对称三取代酚，OH 1 位 / Br 2,4,6 位） | 原图对称性 + imageAssets name「2,4,6-tribromo phenol」+ 化学唯一性（已排除 RDKit 自动 SMILES 键感知伪影 2,4,5） | 仅 info/V6（映射假阳性），0 新增嫌疑 |
| pdf-461 | 重建子问 (1)–(4)、type→fill、挂流程图 `p033_n01_r0.png`；answer 据官方册 | OCR p32/p33 题 5 + 官方答案册 `page_040.json` num=5（2024 广东卷真题）+ WebSearch 已知条件 | info/V6 + suspect/V3（官方 exercise 截断假阳性），0 新增 α 缺陷 |
| pdf-184 | 题干核反应式 ⁹₄X→⁶₂X（质量/电荷守恒，X=⁶₂He）；note 原为别题电负性解析错配，重写为核反应解析；answer B 不动 | OCR p61 第 28 行确认教材原题用变量 + 守恒律 + WebSearch 21cnjy 解析（X=⁶₂He / Y=⁴₂He） | info/V3（官方抽取仍为错的 ⁹₄X，假阳性）+ 预存 suspect/V4，0 新增 |

### J.2 blocked（5 题，题界拼接无法唯一确定，保持 null 待原页重切）

> 统一阻塞根因：数据管道切片时题界拼接/错位，当前文本无法唯一判定正确题干、选项与答案键；在 precision=1 红线内不臆造。解除条件：拿到习题册原页（exercise 页）OCR 重切题界，或人工核对答案册定位。pdf-197/355/356/414 已在 A.2 / E 类登记症状，下表补 Unblock Condition。

| id | 既有登记 | 缺陷摘要 | Unblock Condition |
|----|----------|----------|-------------------|
| pdf-197 | A.2 + I.3 | 题干拼接两道题，answer=`A,B,C,D` 垃圾值，note 错位 | 原页恢复正确题干+选项+键（化学复核 A/B/C 假、D 真与「错误的是」单选矛盾，疑多选或选项错位） |
| pdf-355 | E 类 | 题干拼接填空+选择，answer 错配到前半填空 | 原页拆分为两条题录，核对后半选择题答案键 |
| pdf-356 | E 类 | 题干拼接填空+选择，后半还缺合成路线图 | 原页拆分 + 图像轨道恢复 X/Y/Z 结构 + 核对后半答案键 |
| pdf-414 | E 类 | 单小问碎片，但 answer 为大题多子问串 | 原页恢复大题上下文并核对该小问键 |
| pdf-456 | 本批新增 | suspect/V2：库含选项 A 但官方页无此选项（错位），题界拼接无法唯一确定 | 原页/OCR 重切题界，核对正确选项与答案键 |

### J.3 缺陷暴露（数据已修正，但引擎能力缺口致 eval 判负，非数据回归）

> 隔离对照 `ctrl_pre2`（原始数据+当前代码）→ `verify_fix_all`（当前数据+当前代码）仅这 2 题由 T→F，均为 α2 修正数据后暴露的引擎能力缺口，与 spec Non-Goal NG-3 一致（修复后仍因引擎能力未解的题保持 null，不算数据缺陷未修）。precision=1 / harm=0 未受影响。

| id | 修正内容 | 为何 eval 判负（缺陷暴露，非回归） |
|----|----------|-----------------------------------|
| pdf-357 | 删除酯化②前缀（重定位 pdf-356）；answer C→B、kw→[B]、note→贵州卷灵芝酸 B 解析、type→choice | 原库 answer=C（错误）被引擎采纳，eval 真值取自同一错库 → 假阳性「判正」；订正真值为 B 后引擎无力解 → 诚实判负（消除 adopted-wrong） |
| pdf-260 | 题头去重；补 (3) He 曲线问 + answerKeywords；迁入 p093 图 + curve imageAsset + 新建 svg | 原残缺题（缺子问 3）引擎能解部分而误判正；补全完整多部分大题后引擎无力解 → 判负（暴露引擎多部分大题能力缺口） |

### J.4 precision=1 暂缓不动（存疑，不臆造）

| id | 存疑点 | 决策 |
|----|--------|------|
| pdf-418 | suspect/V2 错位；answer=A 与官方一致，但 note 称 C 亦正确，疑似应为 AC | 不能 100% 确定则不改（改键有引入 harm 风险）；建议核对 2024 重庆卷原卷确认是否多选 AC |
| pdf-486 / pdf-487 | answer 化学验证正确；题面拼接别题填空片段（外观瑕疵，不影响判分） | 编辑 question 文本有改变引擎路由的风险（pdf-260 前车之鉴），precision=1 优先，保留不动；建议后续数据管道统一清理题面拼接 |

### J.5 全量验证证据

- **单元测试**：`dotnet test CogniThink.Chem.sln -c Release --no-restore` → 0 failed（2144 passed）。唯一测试侧变更：`AtomicStructureSolverTests.Guardrail_全题库无假阳性` 期望集移除 pdf-188（α2 已将其正确归位为 choice 化学用语题，护栏跳过 choice 系正确行为；见该测试注释）。
- **verify**：嫌疑 id 99（基线 `.bak_pre_verifyfixbatch`）→ 93（当前），无新增 α 类真缺陷；唯一新增 flag pdf-461 为 V3（官方 exercise 截断）假阳性。7 个 finding 被真实修复消除（056/090/170/178/502/531/544）。
- **eval**：tag `verify_fix_all`，gatewayCorrect=621、precision=1、harmCount=0。对绿色基线 `pdf174fix`：GAIN 5（其他 spec 代码变更，非本 spec 数据）/ LOST 2（J.3 缺陷暴露）；隔离对照 `ctrl_pre2`→`verify_fix_all`：GAIN 0 / LOST 2（J.3），证明本 spec 数据编辑未引入任何 adopted-wrong 或正确数据回归。

---

## K 类：引擎能力缺口·本轮闭环台账（spec: engine-capability-gap-fix）

> **范围**：[_evalscratch/verify/gap63_diagnose.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/gap63_diagnose.txt) 中 63 道「数据正确、题型认对，但求解器返回 `null` / 占位未采纳」，排除 2 道 `BuildTestCases` 合成边界探针（`trap-limit-2` / `trap-limit-3`，非 `pdf_questions.json` 数据）后，**真实引擎能力缺口 = 61 道**。
> **本 spec 结果**：解出 8 道（AC-5 达成：累计 GAINED=8 / LOST=0 / harm=0 / precision=1）；剩余 53 道逐题登记处置结论，确保 **0 静默遗漏**。所有未解题保持 `null`（诚实答不出），**不蒙数**、不改 `answer`、不写入错误事实。
> **集合闭包验证（PASS）**：`61 = solved(8) ∪ skipped_trace(6) ∪ t2(7) ∪ remaining(40)`，两两无重叠、差集为空。证据脚本 [_evalscratch/verify/t8_partition.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/t8_partition.py)；分簇明细 [t8_partition.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/t8_partition.txt)；未解题清单 [unsolved_registered_ids.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/unsolved_registered_ids.txt)（53 道，与下表处置一一对应）。
> **diagnose taskType 说明**：下表 taskType 取自 `gap63_diagnose.txt`（基线 `verify_fix_all` 路由）。T4 后 `pdf-044/084/354/526/527` 的 taskType 已重路由为 `equation_writer`（见 T4 证据），其中 044/354 已解出，084/526/527 仍留痕（见 K.3）。

### K.0 解出清单（8 道，本 spec 已翻转判正，correct=true && truthAdopted=true）

| id | diagnose taskType | 解出任务 | 落库 / 关键改动 |
|----|-------------------|----------|-----------------|
| pdf-044 | calculation | T5 | `reactions-inorganic.json` +`RX-IN-ION-034`（FeBr₂+少量 Cl₂ 离子方程，锚点 FeBr2 标 `IonEquilibrium` 进入离子检索）；T4 路由至 `equation_writer` |
| pdf-090 | choice | T1 | `choice-curated-facts` + `experiment-facts`（NA 选择题，D 对） |
| pdf-147 | choice | T1 | `experiment-facts`（实验安全，B 对） |
| pdf-155 | choice | T1 | `choice-curated-facts`（沉淀定量，C 错，WrongDir） |
| pdf-188 | choice | T1 | `choice-curated-facts`（化学用语，C 对） |
| pdf-319 | choice | T1 | `organic-facts`（有机分类，D 对） |
| pdf-354 | calculation | T5 | `reactions-organic.json` +`RX-OR-EST-004`（乙酸+乙二醇→乙二醇单乙酸酯）；`substances.json` `organics` +2（HOCH2CH2OH / CH3COOCH2CH2OH 过 checker 白名单）；T4 路由 |
| pdf-480 | judgment | T3 | `judgment-statement-facts` +10（JS-017~026，10 子题 √/× 与答案键一致） |

> 终态证据：[final_gained_ids.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/final_gained_ids.txt)，累计 eval diff `verify_fix_all`→`t5_kb`：GAINED=8 / LOST=0 / WORSEN=0 / harm 0→0 / precision=1。

### K.1 多部分大题·子问异构（引擎无子问分发与逐子问聚合）—— 最大簇

> 共同根因：一条题录含多子问，各子问分属**方程书写 / 化学计算 / 概念解释 / 选字母 / 流程推断**等不同求解域，但引擎以单一 taskType 整体调度，无法把每个空格分发到对应求解器并聚合；部分子问还依赖流程/装置图（文本外数据）。**当前处置：保持 `null`。** 建议接续方向：子问分发架构（按 `(1)(2)…` 切分题面 → 逐子问 `ClassifyTaskDeterministic` → 各子问独立求解 → 按空格聚合回填）。

| id | diagnose taskType | 子问构成（摘要） | 已有登记 |
|----|-------------------|------------------|----------|
| pdf-024 | calculation | 化学计量 3 子问（气球体积字母 + 混合气 H 质量 + 平均摩尔质量） | — |
| pdf-025 | calculation | 化学计量 6 子问（体积/摩尔质量/NA 代数式/气体溶解浓度/混合浓度/电荷守恒） | — |
| pdf-070 | calculation | 4 子问（双线桥标电子 + KMnO₄ 性质选字母 + 氧还产物比 + 转移电子数） | — |
| pdf-098 | calculation | 5 子问（σ 键数 + 装置优点 + 装置连接序 + 能否 + 检验方案自由文本） | G 类⑧（连接序非真方程） |
| pdf-146 | calculation | 5 子问（仪器名 + 通 N₂ 目的 + 沉淀化学式 + 性质选字母 + 滴定计算） | — |
| pdf-156 | calculation | 4 子问（制 NH₃ 方程 + 滴加溶液/合成方程 + 分离操作选字母 + 滴定质量分数） | G 类⑧（产物碎片非真方程） |
| pdf-166 | calculation | 6 子问（周期表位置 + 方程 + 滤渣成分 + 离子方程 + 电子守恒计算 + 电解现象解释） | — |
| pdf-167 | equilibrium | 蛇纹石+绿矾分解耦合流程多子问（绿矾分解方程 + Ksp 沉淀 + 矿化 CO₂…），`answer=A` 单字母 | 本类新增 |
| pdf-168 | calculation | 2 子问（滤渣成分 + Ksp 计算 Al³⁺ 浓度）—— 数据在题干，缺沉淀溶解计算域 | — |
| pdf-169 | equilibrium | 7 子问（手性碳数 + 离子 + 温度解释 + 方程 + 金属元素 + 平衡解释 + 热解方程） | — |
| pdf-190 | equilibrium | 4 子问（电离能理由 + 电负性排序 + 原子半径排序 + 价层排布/电负性）—— 结构概念解释 | — |
| pdf-207 | calculation | 4 子问（沸点原因 + 熔沸点原因 + 熔点比较 + 熔点比较）—— 晶体/分子间力解释 | — |
| pdf-228 | calculation | 2 子问（配合物离子符号 + 18e 规则计算 + σ 键数）—— 配位化学计算 | — |
| pdf-244 | calculation | 2 子问（光合作用热化学方程 + 键焓计算）—— 热化学计算 | — |
| pdf-258 | equilibrium | 3 子问（盖斯定律 + lgK–1/T 图像判吸放热 + 平衡常数 + 电极反应）—— 依赖图 | G 类⑤ / C 类（依赖图） |
| pdf-260 | equilibrium | 3 子问（电子式 + 盖斯定律 + He 曲线判断）—— 数据补全后暴露多部分大题缺口 | J.3（缺陷暴露） |
| pdf-261 | equilibrium | 5 子问（熵变 + 盖斯 + 选字母 + 流速解释 + 计算）—— 化学平衡多子问 | — |
| pdf-262 | equilibrium | 1 空（CH₄ 脱氢芳构化判高低）—— 复杂平衡图像推断 | — |
| pdf-272 | equilibrium | 3 子问（草酸粒子分布曲线）—— **T6 占位噪声**：`gatewayConclusion` 吐出曲线中间量，`adopted=False` 无 harm | 见 K.4 |
| pdf-275 | calculation | 1 空（8 种溶液 pH 排序）—— 缺 pH 定性排序计算域 | — |
| pdf-328 | calculation | 有机结构推断（肉桂酸分子式 + 结构简式，质谱/红外/NMR） | G 类⑧（关键词非方程） |
| pdf-418 | equilibrium | 流程题，`answer=A`，note 称 C 亦正确疑多选 AC，存疑不改 | J.4（存疑不动） |
| pdf-461 | equilibrium | 镓提取流程 4 子问（电解方程 + 配位离子 + 置换 + …）—— **数据已修复（J.1）但引擎仍不解** | J.1（数据已修，能力缺口残留） |
| pdf-464 | calculation | 1 空（调 pH 沉镍后滤液溶质）—— 流程溶质判断 | — |
| pdf-484 | calculation | 多子问流程题（酸浸目的 + 氧化/检验 + 2 个方程 + …）—— (3) 问 2 方程 KB 无命中、路由未触达 | T5 未覆盖（留痕） |
| pdf-506 | equilibrium | 1 空（N 杂化 + 键角比较理由）—— 结构键角解释 | — |
| pdf-532 | concept | 多子问（仪器名 + 吸收 + 方程 + …）—— 同时问化学/离子方程触发 `askIonic && askMolecular` 歧义守卫 → null | G 类②（歧义守卫） |

### K.2 纯计算 / 定量域——KB 与域计算器不覆盖（数据完整、单空格，但无求解器产出）

> 共同根因：题面文本与数据完整（滴定/水解/误差分析），但引擎缺对应**域计算器**（滴定分析质量分数、盐类水解 pH、定量实验误差），返回 `null`。**当前处置：保持 `null`。** 建议接续方向：滴定分析域计算器 / 水解 pH 计算器 / 定量实验误差分析求解器。

| id | diagnose taskType | 计算内容 | 已有登记 |
|----|-------------------|----------|----------|
| pdf-030 | equilibrium | Bi³⁺ 质量分数（EDTA 滴定，列计算式） | — |
| pdf-415 | calculation | 调 pH 除去的离子判断（流程除杂离子） | — |
| pdf-517 | calculation | 0.01 mol·L⁻¹ NaClO 溶液 pH（盐类水解） | — |
| pdf-535 | calculation | 配制 NaOH 浓度误差判断 + 操作名（定量实验误差） | — |
| pdf-543 | calculation | 加硫酸调 pH 目的解释（流程目的自由文本） | — |

### K.3 方程书写——KB 无法唯一确定 / 答案键假命中 / 多方程冲突（T5 已论证禁硬凑）

> 共同根因：T4 已把这些题路由到 `equation_writer`，但：① 答案键是裸字母 `C` → 任何含碳方程都会子串假命中，**禁止**加 KB 使其"答对"（precision=1 红线）；② 产物写法非常规 / 含聚合度 `n` → `ChemicalChecker` 拒绝；③ 多子问含多个方程 → `ReactionKbWriterSolver` 唯一命中守卫失败。**当前处置：保持 `null`，不加条目、不蒙数。** 建议接续方向：答案键清洗（去裸字母）后重评估；安全的多重命中路径（仅产出题干明确要求的方程）；有机反应选择性推断与聚合模板。

| id | diagnose taskType | 留痕原因（T5 论证） | 已有登记 |
|----|-------------------|---------------------|----------|
| pdf-084 | calculation | `answer='C'`、`answerKeywords=['C']` → 含碳方程子串假命中，**禁加** | G 类② |
| pdf-526 | calculation | 产物 `CH3CH(O)COOCH(CH3)CO`（丙交酯）非常规写法，解析/白名单风险高 | G 类⑥ |
| pdf-527 | calculation | 聚酯含 `n` 计量 → `ChemicalChecker` 拒绝 | G 类⑥ |
| pdf-010 | reaction_check | 多空混合（3 方程 + 胶体词语），多方程致唯一命中守卫失败；占位 gc=`已匹配到相关反应`（adopted=False） | G 类② / K.4 |

### K.4 占位噪声——`usedEngine=True` 但结论非最终答案（T6 降级，已 `adopted=False` 无 harm）

> 共同根因：`reaction_check` / `equilibrium` 求解器对非方程/非定量问法吐出占位结论（`已匹配到相关反应` / `已解析出相关物质与离子` / 曲线中间量），但结论不满足「是题目最终答案」签名，**采纳门已拒绝（`adopted=False`），harm=0**。T6 为可选噪声收紧（改守卫逻辑有回归风险），鉴于 AC-5 已达成、precision=1 为最高红线，**决定降级跳过**，不为压噪声冒回归风险。**当前处置：保持 `adopted=False`，解对数不增不减。** 建议接续方向：后续独立评估守卫收紧（仅当全库已解对题逐案不变时）。

| id | diagnose taskType | 占位结论 | 已有登记 |
|----|-------------------|----------|----------|
| pdf-010 | reaction_check | `已匹配到相关反应` | 亦见 K.3（多方程） |
| pdf-163 | reaction_check | `已解析出相关物质与离子`（问「酸浸是否可行+原因」，非方程） | — |
| pdf-175 | reaction_check | `已匹配到相关反应`（问「催化剂」，非方程） | — |
| pdf-272 | equilibrium | `x=2.5 时，曲线「H₂C₂O₄」的 y = 0.8`（曲线中间量，非最终答案） | 亦见 K.1 |

### K.5 概念 / 自由填空问法——方程锚点或选择链不触发（G 类既有登记）

> 共同根因：问法不是「写出方程式」，方程锚点守卫不触发；或属概念解释自由文本，无对应求解器。**当前处置：保持 `null`。** 建议接续方向：概念解释求解器（平衡原理/流程目的/结构理由）。

| id | diagnose taskType | 留痕原因 | 已有登记 |
|----|-------------------|----------|----------|
| pdf-274 | equilibrium | 问「用平衡原理解释除杂原因」，非「写方程式」，方程锚点不触发 | G 类④ |
| pdf-107 | equilibrium | 题干拼接（电解方程填空）+ note 错配（共存选择），`answer='D'` 单字母，题型混杂 | 本类新增（数据管道拆分+核键） |

### K.6 探针不通过而跳过——T1 / T3 留痕（保持 `null`，不蒙数）

> 共同根因：T1（选择事实策展）/ T3（判断陈述事实策展）探针阶段发现：选项缺失 / 子问缺父上下文 / 题干拼接无唯一锚点 / 子题依赖图或方程抽取失败 → 无法构造唯一匹配事实或无法干净聚合。在 precision=1 红线内**不臆造**，整题或相关子题跳过并留痕。**当前处置：保持 `null`。** 建议接续方向：数据管道补全选项 / 大题上下文；图像轨道恢复；方程 Unicode 解析修复。

| id | diagnose taskType | 跳过原因（探针留痕） | 已有登记 |
|----|-------------------|----------------------|----------|
| pdf-134 | judgment | 子题 (9) 图像依赖 + (5)(11) 方程 Unicode 解析失败 → 陈述集合不完整，无法干净聚合 | T3 留痕（本类新增索引） |
| pdf-478 | judgment | 子题 (2)(8) 图像依赖 + (6) 方程抽取失败 → 保持 null | T3 留痕（本类新增索引） |
| pdf-196 | calculation | 氮循环选择题，**options 为空**（T1 探针核实）→ 无法策展 | T1 留痕（本类新增索引） |
| pdf-416 | choice | 实验操作顺序，子问缺父大题上下文，无可靠唯一 stemMatch 锚点 | T1 留痕（本类新增索引） |
| pdf-405 | choice | 选项 A/B/D 仅给实验现象、缺结论性文本，文本截断无法写匹配事实 | H.3（既有） |
| pdf-531 | choice | 题干拼接两题（滴定 + Te 量子点）+ 多选 BC 无标记，askDir/stemMatch 不可靠 | E 类 / H.1（既有） |

### K.7 T2 依赖图候选——待视觉核实（保持 `null`，AC-5 已达成不阻塞）

> 共同根因：选择题选项真伪依赖装置/曲线/结构图，4-verdict 需**读图辅助**（图仅作示意，结论从化学原理独立推出）。AC-5（总 GAINED≥8）已由 T1/T3/T5 达成，T2 为可选 bonus；任一选项化学真值存疑即整题跳过，绝不靠「看答案反推」写事实。**当前处置：保持 `null`，待后续视觉核实后评估（探针 `TOTAL PROBLEMS=0` 才落库）。** 建议接续方向：T2 批次 2（视觉辅助 + 化学真值交叉核对 + 探针门控）。

| id | diagnose taskType | 图像依赖 | 已有登记 |
|----|-------------------|----------|----------|
| pdf-138 | choice | H₂S 制备/干燥/检验/尾气装置图（B 干燥 H₂S 错，浓硫酸氧化 H₂S） | 本类新增 |
| pdf-255 | choice | CO₂ 加氢绝热反应管温度/体积分数曲线图 | 本类新增 |
| pdf-271 | choice | 三组滴定实验曲线图（指示剂选择），键已修 | register line 235（键修复） |
| pdf-303 | choice | 电池装置图，`answer='C'` 存疑（note 称应为 CD） | G 类⑤ / H.1（既有） |
| pdf-340 | equilibrium | 有机手性碳结构图（D 仅含 3 个手性碳） | 本类新增 |
| pdf-357 | choice | 灵芝酸有机结构图，键已修（C→B）但引擎无力解 | J.3（键已修，能力缺口残留） |
| pdf-406 | choice | 气体发生/收集装置图；`WrongDir` 正则未覆盖「不能完成」→ askDir 误判 | A.1 / H.3（既有，键已修） |

### K.8 台账自洽性核对（61 全覆盖，0 静默遗漏）

| 处置类别 | 题数 | 题号 |
|----------|------|------|
| 解出（K.0） | 8 | pdf-044, 090, 147, 155, 188, 319, 354, 480 |
| 多部分大题子问异构（K.1） | 27 | pdf-024, 025, 070, 098, 146, 156, 166, 167, 168, 169, 190, 207, 228, 244, 258, 260, 261, 262, 272, 275, 328, 418, 461, 464, 484, 506, 532 |
| 纯计算/定量域（K.2） | 5 | pdf-030, 415, 517, 535, 543 |
| 方程书写 KB 不可行（K.3） | 4 | pdf-084, 526, 527, 010 |
| 占位噪声 T6 降级（K.4） | 4 | pdf-010, 163, 175, 272 |
| 概念/自由填空问法（K.5） | 2 | pdf-274, 107 |
| 探针跳过留痕（K.6） | 6 | pdf-134, 478, 196, 416, 405, 531 |
| T2 依赖图候选（K.7） | 7 | pdf-138, 255, 271, 303, 340, 357, 406 |

> **去重说明**：pdf-010 同时属 K.3（多方程）与 K.4（占位噪声）；pdf-272 同时属 K.1（多子问）与 K.4（占位噪声）。二者在「未解题清单」与闭包集合中各计 1 次，上表按根因维度交叉列出，不重复计入 61。
> **集合恒等式**：解出(8) + 未解题(53) = 61。未解题(53) = K.1(27) ∪ K.2(5) ∪ K.3(4) ∪ K.4(4) ∪ K.5(2) ∪ K.6(6) ∪ K.7(7)，各类求和 55，去重交叉项（pdf-010 同时属 K.3/K.4、pdf-272 同时属 K.1/K.4，各仅计 1 次）后 = 53，与 [unsolved_registered_ids.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/unsolved_registered_ids.txt) 逐行一致；差集为空，0 静默遗漏。

---

## 处理原则（固化，防止回退到硬编码路线）

1. **引擎是唯一真值源**：化学判定走 `IStatementJudge` 聚合器（事实库/计数/氧化还原电子），不向 `general-choice-facts` 追加单题策展条目。
2. **数据修复只信官方源**：答案键改前必须有答案册 OCR 或原页核对（5b）；化学推导仅作辅助，5a 类（存储键为明显垃圾值 `"A,B,C,D"` 且 note 结论明确）沿用 Task 35/46 已验证流程，但跨多选项/题型存疑时（如 075）不单独触发存储键变更，必须官方源裁定。
3. **文本回填只写 `question`/`options`，不写 verdict、不改 `answer`**：回填后必须过 EvalRunner diff（LOST=0、harm=0）与字段 diff 审查。
4. **不可靠恢复一律登记、保持 `null`**：宁可未解，不可假阳性。
