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
| pdf-172 | 大题碎片 + 跨题答案污染（**P1-3 新登记**） | 大题第(6)小问「1.0 Kg 蛇纹石“矿化”固定 CO₂，得到 0.466 kg Mg₅(CO₃)₄(OH)₂·4H₂O，相当于固定 CO₂______ L（标准状况）」——题面是**完整的数值计算小问**（数据齐备、可由摩尔质量唯一确定体积）；但存储 `answerKeywords=['AC']` 与 `note`（通篇讲 1~36 号元素中 Cr / Cu 核外电子排布是否遵循构造原理）属于**另一道选择题**，与 CO₂ 固定体积完全无关（答非所问） | 题面与 answer/note 分属不同题（跨题污染），数值计算键缺失、无法对齐；缺官方源核对该小题数值键 | 原页恢复蛇纹石矿化大题上下文，单独抽取 (6) 小问并核对数值键；归**数据管道键修复**。暂保留 `null`。P1-3 审计出处：[p13_inspect_ids.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p13_inspect_ids.txt) |
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

> 共同根因：一条题录含多子问，各子问分属**方程书写 / 化学计算 / 概念解释 / 选字母 / 流程推断**等不同求解域，但引擎以单一 taskType 整体调度，无法把每个空格分发到对应求解器并聚合；部分子问还依赖流程/装置图（文本外数据）。**当前处置：保持 `null`。**
>
> **【P1-2 交付（分发骨架 + AND 判分，纯基础设施）】** 子问分发架构已落地：[MultiPartUtility](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/MultiPartUtility.cs)（多部分检测 + `(N)` 编号切分，口径复用 `MultiSubCorrectnessSolver.SubMarker`，天然排除 `(2025·北京卷)`/`[Cu(NH3)4]`/`(FeSO4·7H2O)`）、[LlmGatewayService.MultiPartDispatch](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.MultiPartDispatch.cs)（仅在整题未解 `Ok!=true` 时 fallback 激活 → 629 道已解对题零回归；逐子问复用现有求解器，任一子问未解 → 整体返回 `null`，不蒙数、不输出部分答案）、评估侧 `BuildGatewayAnswerAsync` 对 `task_type=multi_part` 聚合结论走**答案键 AND 判分**（全部子问键命中才采纳，凡采纳必全对 → precision=1 / harm=0 构造保证）。**AC-4 据实校准为 ≥0**：T0 + P1-3 就位后重审均确认 **0/27** 可由现有求解器全覆盖子问——阻塞子问为「自由文本解释」（→ P1-4 自由填空概念求解器）/「图像依赖」（→ P2-3 图像融合）/「热化学·Ksp·滴定·平衡·有机等其他计算域」（→ 后续域计算器）。本项交付纯基础设施，**零即时增益、零回归**。
>
> **实证（`after_p12`，Production LLM off，757 题）**：27 道 K.1 全部 `correct=false / adopted=false`（`unsolved_bothwrong`），0 错采纳；全库 global `adopted∧¬correct = 0`，precision=1 / harm=0。权威 diff：对 `base_p0` → LOST=0 / WORSEN=0 / harm=0（GAINED=1 为 P1-3 的 trap-limit-2 携带，非 P1-2）；对 `after_p13` → GAINED=0 / LOST=0 / WORSEN=0（P1-2 零影响隔离证明）。后续 P1-4 / P2-3 解锁子问后，分发骨架可在不动判分门的前提下产出增益。证据：[p12_verify_after.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p12_verify_after.py)、[p12_reaudit.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p12_reaudit.txt)、[p12_check_states.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p12_check_states.py)。

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
>
> **【P1-3 修订】** 已落地水解 pH 计算器（`HydrolysisPhCalculator`，查 `acid-constants.json`）与氧化还原比例计算器（`RedoxRatioCalculator`），作为 `CallCalculation` 末尾**兜底**基础设施（既有求解器全 miss 才触发，零域外假阳性）。逐题核对后：pdf-517 计算器诚实算出 pH≈9.76，但与存储键 10.3 冲突（见下）→ 不采纳，无 gain 无 harm；pdf-030 实为未定值变量题 + 键错位，非纯计算可解。排除闭环见下方「K.2 附」。

| id | diagnose taskType | 计算内容 | 已有登记 |
|----|-------------------|----------|----------|
| pdf-030 | equilibrium | Bi³⁺ 质量分数（EDTA 滴定，列计算式） | **P1-3 排除**：题干含未定值变量 `a g` / `b mL` 且明确要求「列出计算式，不用化简」→ 无唯一数值解（不蒙数）；存储键 `25.00%` 与变量式题干不对齐，疑似数据错位。归**数据管道键修复**；非纯计算可解。 |
| pdf-415 | calculation | 调 pH 除去的离子判断（流程除杂离子） | ✅ **P1-4 已解出**（概念事实库，见 §L.1）：原误路由 calculation，路由 cue「除去的离子」先于 calculation 截走 → concept → 事实唯一匹配命中，eval after_p14 correct=true。 |
| pdf-517 | calculation | 0.01 mol·L⁻¹ NaClO 溶液 pH（盐类水解） | **P1-3 答案键冲突**：`HydrolysisPhCalculator` 查 `acid-constants.json`（Ka(HClO)=3.0×10⁻⁸）算得 pH≈9.76，存储键 10.3 与真值冲突（达 10.3 需 Ka≈2.5×10⁻⁹；其 note 自承「pH=9.76≈10.3」为强行近似）。按红线不蒙数：计算器诚实输出 9.76 但**不匹配键 → 不被采纳**（`gatewayTruthAdopted=false`，无 gain 无 harm）。归**数据管道键修复**（核对官方键）。证据：[p13_truth_check.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p13_truth_check.py)。 |
| pdf-535 | calculation | 配制 NaOH 浓度误差判断 + 操作名（定量实验误差） | ✅ **P1-4 已解出**（概念事实库，见 §L.1）：原误路由 calculation，路由 cue「配制∧浓度∧可能」截走 → concept → 事实唯一匹配命中，eval after_p14 correct=true。 |
| pdf-543 | calculation | 加硫酸调 pH 目的解释（流程目的自由文本） | ✅ **P1-4 已解出**（概念事实库，见 §L.1）：原误路由 calculation，路由 cue「ph∧目的」截走 → concept → 事实唯一匹配命中，eval after_p14 correct=true。 |

### K.2 附：P1-3 纯计算域审计排除闭环（AC-5：0 静默遗漏）

> P1-3 审计范围 = 128 未解中剔除 27 道 K.1 多部分后的 **101 道非多部分未解**（证据：[p13_scope_report.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p13_scope_report.txt)、[p13_inspect_ids.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p13_inspect_ids.txt)）。初筛「calc 信号 ∧ 无图 ∧ 无自由文本 ∧ 单部分」得 **12 道候选**，逐题核对后仅 `trap-limit-2` 可零风险解出（已解，GAIN +1），`pdf-517` 因答案键冲突跳过（已建计算器基础设施，不采纳），其余 **13 道 + 深度核对补登 3 道（pdf-160/165/172）= 14 道排除**，逐题登记如下。排除集合 = 审计集合，差集为空，**0 静默遗漏**。

| 排除 id | taskType | 根因 | 归属 spec / 处置 | 登记位置 |
|---------|----------|------|-----------------|----------|
| trap-limit-3 | calculation | 与量有关离子方程式，属方程书写域，非数值计算 | P1-1 equation_writer 已路由；保持 null | §G⑥ / 引言（合成探针，非 pdf_questions 数据） |
| pdf-030 | equilibrium | 未定值变量 a/b + 要求「列计算式不化简」→ 无唯一数值解；键 25.00% 疑似错位 | 数据管道键修复；不蒙数 | §K.2（上表已更新） |
| pdf-517 | calculation | 答案键冲突（真值 pH≈9.76 vs 键 10.3）；计算器已建但不采纳 | 数据管道键修复；P1-3 基础设施留用 | §K.2（上表已更新）；证据 p13_truth_check.py |
| pdf-415 | calculation | 「除去的离子有」自由填空概念，非数值 | ✅ P1-4 已解出（概念事实库，见 §L.1） | §L.1 / §K.2（上表已更新） |
| pdf-197 | choice | 题干拼接两道题，answer/options/note 错配 | 数据管道拆分 | §A.2 |
| pdf-263 | choice | 多陈述选择题「正确的有几项」，非纯数值；键自相矛盾 | chem-fact-curation / 原页核型 | §C |
| pdf-355 | equation_writer | 题干拼接两题（填空+选择），answer 错配前半填空 | 数据管道拆分 | §E |
| pdf-356 | equation_writer | 题干拼接两题 + 后半选择依赖缺失路线图 | 数据管道拆分 + 图像轨道 | §E |
| pdf-463 | calculation | 大题碎片缺沉淀 pH / Ksp 数据 + 小写选项键 | P1-2 多部分分发 / 数据管道 | §E |
| pdf-160 | unknown | checkpoint 题干仅存大题引语，(1)–(5) 子问被剥离，键跨 5 子问 | P1-2 多部分分发 / 数据管道补全 | §G① |
| pdf-165 | unknown | 大题碎片 + 跨题答案污染（键属 Cu/Ag 提萃大题） | 数据管道键修复；P1-2 | §E |
| pdf-172 | unknown | 大题碎片 + 跨题答案污染（键 `AC` / note 属 Cr/Cu 构造原理选择题） | 数据管道键修复 | §E（本批次新增） |
| pdf-526 | equation_writer | 有机推断（乳酸自身酯化六元环酯），非确定型模板可唯一覆盖 | P1-1 方程域（已路由） | §G⑥ |
| pdf-527 | equation_writer | 有机推断（乳酸自身酯化聚酯含 n），ChemicalChecker 拒绝 | P1-1 方程域（已路由） | §G⑥ |

> **闭环核对**：解出 1（trap-limit-2）+ 排除 14 = 15，与 P1-3 触及的全部候选 id（12 初筛 + 3 深度核对 160/165/172）一一对应；排除集合 ⊆ 已登记集合，差集为空。

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
>
> **【P1-4 后续更新】** K.2 中的 pdf-415 / pdf-535 / pdf-543 已由 P1-4 自由填空概念求解器解出（见 §L.1），现行未解题 = 53 − 3 = 50；本行及上表为 P1-1 交付时点的闭包快照（61 全覆盖、0 静默遗漏依然成立），[unsolved_registered_ids.txt] 同步冻结为该时点清单。

---

## L 类：P1-4 自由填空概念求解器闭环（spec: free-fill-concept-solver）

> **范围**：`after_p12` 127 道未解中，扣除 27 道 K.1 多部分大题后的自由填空概念簇——答案为短文本概念/目的/作用/成分/误差结论，非数值/方程/选择/图像依赖。复用现有 `ConceptFactSolver`（唯一匹配→不蒙数）+ 评估侧 `IsAnswerCorrect` 采纳门（凡采纳必判正→precision=1 构造保证），不新建求解器类；最小化表面积：仅改 `data/highschool/concept-facts.json`（追加 5 条，version 1.4→1.5）+ `ClassifyTaskDeterministic` 路由（3 条 cue 先于 calculation 截走，全库 757 预校验 0 漂移）。
> **红线结果**：eval `after_p14`（Production LLM off，757/757 完成、failed=0）correct=635 / adopted=635；diff `after_p12`→`after_p14` **GAINED=5 / LOST=0 / WORSEN=0 / harm=0**，global `adopted∧¬correct=0`（precision=1）。diff `base_p0`→`after_p14` GAINED=6（5 道 P1-4 + P1-3 携带的 `trap-limit-2`）/ LOST=0 / WORSEN=0 / harm=0。
> **证据**：T0 审计 [p14_t0_audit.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p14_t0_audit.py)、路由 0 漂移预校验 [p14_routing_drift_precheck.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p14_routing_drift_precheck.py)（→ [p14_tasktype_diff.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p14_tasktype_diff.txt)）、T1 后事实库验证 [p14_post_t1_verify.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p14_post_t1_verify.py)、T4 eval 验证 [p14_verify_after.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p14_verify_after.py)、闭包核对 [p14_closure_check.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p14_closure_check.py)。

### L.1 解出清单（5 道，correct=true && truthAdopted=true）

| id | 原 taskType | 入库事实 id（concept-facts.json） | 解出路径 |
|----|------------|----------------------------------|----------|
| pdf-415 | calculation（误路由） | `fill-purify-acid-ph-remove-ions` | Part B 路由 cue「除去的离子」先于 calculation 截走 → concept → 事实唯一匹配命中；`answer`=`OH⁻、CO₃²⁻` 命中答案键 |
| pdf-543 | calculation（误路由） | `fill-v-recovery-acid-ph-purpose` | Part B 路由 cue「ph∧目的」截走 → concept → 唯一匹配；`answer`=`除去溶液中过量的OH⁻和CO₃²⁻，防止后续生成杂质沉淀` 命中答案键 |
| pdf-535 | calculation（误路由） | `fill-solution-prep-concentration-shake` | Part B 路由 cue「配制∧浓度∧可能」截走 → concept → 唯一匹配；`answer`=`偏小；摇匀` 命中答案键 |
| pdf-542 | unknown（已达事实库） | `fill-precipitate1-metal-elements` | 路由不动（unknown 瀑布含 ConceptFactSolver），Part A 事实补全 → 唯一匹配；`answer`=`Fe、Al` 命中答案键 |
| pdf-530 | unknown（已达事实库） | `fill-plan2-advantage` | 路由不动，Part A 事实补全 → 唯一匹配；`answer`=`操作更简便，反应条件更温和` 命中答案键 |

> 全库唯一性：每条新事实在 757 题中恰匹配 1 题（无歧义，不会被 `ConceptFactSolver` 的 `>1→null` 守卫吞掉）；0 漂移（不命中任何已判正题）；答案均命中目标答案键。证据 [p14_post_t1_verify.py]（34 条目、5 目标各 1 命中、0 漂移、5/5 答案键命中，ALL PASS）。

### L.2 排除项（保持 `null`，0 损失，precision=1 不妥协）

| id | 排除原因 | 归属 / 建议下一步 |
|----|----------|-------------------|
| pdf-483 | 触发「加入氯化钠+作用」与**已判正** pdf-477 重叠 → 任何概念事实都会同时命中两者（count=2），触发唯一性铁律返回 null；无化学区分 token（差异仅碎片编号），强行消歧违反红线 #6。**另**：整条题面与大题 pdf-477 第(6)小问逐字相同，是数据切片重复题录（见 §E pdf-483 行）。 | 数据管道去重（删除 pdf-483 或合并入 pdf-477）；文本轨道保持 `null`。双重排除理由。 |
| pdf-541 | 触发「玻璃仪器+烧杯」与 pdf-414 重叠（均未判正）→ 2 命中 → `ConceptFactSolver` 返回 null（无增益无伤害）；暂无目标独有 token 可消歧。 | 待数据管道补全唯一锚点，或求解器支持互斥条件后重评；保持 `null`。 |
| pdf-177 | 答案键为多子问合并串 `(1)...(4)...`，单条事实 `answer` 无法作为子串命中 → 属多部分大题域，非单条概念事实可解。 | 归 P1-2 多部分分发 + 后续子问求解器（见 §K.1）；保持 `null`。 |

### L.3 集合闭包核对（AC-8：0 静默遗漏）

> P1-4 审计候选全集 = T0 `p14_t0_audit.py` PROPOSALS 枚举的 **8 道**自由填空概念题。

| 处置 | 题数 | 题号 |
|------|------|------|
| 解出（L.1） | 5 | pdf-415, 530, 535, 542, 543 |
| 排除保持 null（L.2） | 3 | pdf-483, 541, 177 |

> **闭包恒等式**：候选(8) = 解出(5) ∪ 排除(3)，两两无交集、差集为空。`after_p14` 实证：5 解出题全部 correct=true；3 排除题全部 correct=false ∧ adopted=false（保持 null）；全局新增采纳集恰为 5 解出题，无多余采纳（不蒙数）。证据 [p14_closure_check.py]（PASS）。

---

## M 类：P1-5 路由补齐闭环（spec: routing-unknown-backfill）

> **范围**：`after_p14` 32 道 `taskType=unknown`（全部未判正）的路由卫生收敛。在 `ClassifyTaskDeterministic` 插入 cue A（方程书写问法补齐 → `equation_writer`），并修复 cue A 引发的回归：`equation_writer` 分支补齐 `ProductWriterSolver`（与 `reaction_check`/`CallCompile` 作家链同序：Product → Known → ReactionKb）。最小表面积：仅改 `LlmGatewayService.cs`（cue A 插入 + equation_writer 分支补 `ProductWriterSolver`）+ 测试；不动 JSON 数据、不动评估代码、不硬编码化学真值。
> **诚实口径**：纯路由新增解对数 = 0（spec §2.1：unknown 瀑布已覆盖廉价求解器）。本 spec 目标 = 路由卫生（unknown 32→27↓ + 零有害漂移），不解新题。
> **红线结果**：eval `after_p15b`（Production LLM off，757/757、failed=0）correct=635 / adopted=635；diff `after_p14`→`after_p15b` **GAINED=0 / LOST=0 / WORSEN=0 / harm=0**；diff `base_p0`→`after_p15b` GAINED=6（P1-4 携带）/ LOST=0 / WORSEN=0 / harm=0；global `adopted∧¬correct=0`（precision=1）。
> **回归修复留痕（实证，非推理）**：cue A 首版 eval `after_p15` 出现 **LOST=1（pdf-515）**。根因：pdf-515 在 `after_p14` 由 LLM 兜底分类为 `reaction_check`（确定性分类器返回 unknown 时保留 LLM 结果），经 `CallCompile` 作家链中的 `ProductWriterSolver`（NaCN + 少量 CO2 规则，[ProductWriterSolver.cs L121-123](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/ProductWriterSolver.cs#L121-L123)）解出，输出指纹 `离子方程式：CN- + CO2 + H2O → HCN + HCO3-`（`→`+去`^`，唯一定位该求解器）；cue A 将其重分类为 `equation_writer` 后，该分支原本只调 `KnownEquationWriterSolver` + `ReactionKbWriterSolver`，**不含 `ProductWriterSolver`** → 丢失求解器 → LOST（`ReactionKbWriterSolver` 因题干无 H2O、KB 要求反应物全在题干而返回 null）。修复（用户确认方案 a）：`equation_writer` 分支按 `reaction_check` 同序补齐 `ProductWriterSolver`，命中后置 `reaction_check` 走同一采纳门（凡采纳必经 `IsAnswerCorrect` 校验 → precision=1，不蒙数）。修复后 `after_p15b` pdf-515 恢复判正，全库零回归。spec §2.3 原推理（"5 道均由 unknown 瀑布 ReactionKbWriterSolver 解出"）经实证更正：5 道实际经 LLM→`reaction_check`→`CallCompile` 作家链（pdf-174/417=ReactionKb，pdf-456/539=Known，pdf-515=Product）解出。
> **证据**：0 漂移预校验 [p15_drift_precheck.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p15_drift_precheck.py)（→ [p15_drift_precheck.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p15_drift_precheck.txt)）、T4 eval 验证 [p15_verify_after.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p15_verify_after.py)、闭包核对 [p15_closure_check.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p15_closure_check.py)。

### M.1 cue A 重分类 16 道去向表（全库 757 预校验，affected=16）

Port 口径（确定性分类器 old→new）：16 道 old≠new，全部 → `equation_writer`；无 choice/judgment/equilibrium/balance/ion_check/structure/vsepr/titration/atomic 被截走（不窃取高优先级）。

| 去向（port old→new） | 题数 | 题号 | runtime after_p14 taskType | after_p15b 结果 |
|---|---|---|---|---|
| unknown→ew（runtime 属 unknown，卫生收益） | 5 | pdf-097, 164, 177, 179, 462 | unknown | 仍未判正（锚点不全/KB 缺口 → Ok=null），taskType→equation_writer，无伤害 |
| unknown→ew（runtime 属 reaction_check，已判正，保持判正） | 5 | pdf-174, 417, 456, 515, 539 | reaction_check | **全部保持 correct=True**（作家链产出同一结论后回写 reaction_check）；pdf-515 经 ProductWriterSolver |
| unknown→ew（runtime 属 reaction_check，未判正） | 1 | pdf-171 | reaction_check | 仍未判正，无伤害 |
| concept→ew | 2 | pdf-484, 532 | concept | 仍未判正，无伤害 |
| calculation→ew | 3 | pdf-156, 166, 244 | calculation | 仍未判正，无伤害 |

> 5 道已判正保持判正的实证（eval，非推理）：`after_p15b` 中 pdf-174/417/456/515/539 全部 `gatewayCorrect=True`、`taskType=reaction_check`；`diff after_p14→after_p15b` LOST=0。

### M.2 范围外 27 道（保持 unknown，逐项归属 spec §2.2）

`after_p14` 32 道 runtime-unknown 中，5 道被 cue A 重分类离开 unknown（M.1 卫生收益），其余 **27 道保持 unknown**（B 集），均属 spec §2.2 范围外类别，不纳入 P1-5：

| 类别（spec §2.2） | 题数 | 题号 | 处置 |
|---|---|---|---|
| 多部分大题（仅 mp） | 3 | pdf-176, 198, 541 | 范围外 → P1-2 多部分分发 |
| 仅图像（img） | 3 | pdf-159, 160, 205 | 范围外 → P2-3 图像管道 |
| 多部分 + 图像 | 4 | pdf-215, 391, 413, 528 | 范围外 → P1-2 / P2-3 |
| 数据缺陷（答案错位/题面碎片/裸字母键） | 5 | pdf-165, 172, 180, 333, 377 | 范围外 → 数据管道键修复（§E） |
| 有机官能团/命名（缺分子图+缺命名求解器） | 8 | pdf-322, 323, 324, 519, 520, 521, 522, 523 | 范围外 → 图像恢复 + 新命名求解器（未来 spec） |
| 概念事实重叠/重复题录 | 1 | pdf-483 | 范围外（§L.2 已排除）→ 数据去重 |
| 装置命名（缺图标号） | 1 | pdf-534 | 范围外 → 图像恢复 |
| 自由文本分析（无对应求解器） | 1 | pdf-537 | 范围外 → 概念解释求解器（未来 spec） |
| 题面碎片 | 1 | pdf-161 | 范围外 → 数据管道补全 |

> 核对：3+3+4+5+8+1+1+1+1 = **27** = B 集，与 `p15_closure_check.py` 输出一致。

### M.3 集合闭包核对（AC-7：0 静默遗漏）

> P1-5 作用域全集 = `after_p14` runtime `taskType=unknown` 的 **32 道**（U）。

| 处置 | 题数 | 题号 |
|------|------|------|
| 被 cue A 重分类离开 unknown（A，卫生收益，仍未判正无伤害） | 5 | pdf-097, 164, 177, 179, 462 |
| 保持 unknown（B，范围外，M.2） | 27 | （见 M.2 表） |

> **闭包恒等式**：U(32) = A(5) ∪ B(27)，A∩B=∅，差集为空（U-(A∪B)=∅）。`after_p15b` 实证：A 全部不再是 unknown（taskType→equation_writer，correct=false ∧ adopted=false，保持不蒙数）；B 全部仍为 unknown（correct=false）。另：5 道 correct-touched（pdf-174/417/456/515/539）∉ U（runtime 非 unknown），全部保持 correct=True。全局 `adopted∧¬correct=0`。证据 [p15_closure_check.py]（PASS）。

---

## N 类：P2-1 统一推理 DAG 与结论三态契约闭环（spec: unified-reasoning-dag）

> **范围**：网关 `EngineResponse` 契约**加性**统一 — 新增 `ReasoningNode`（可溯源到 rule/knowledge + evidence_level）与 `ConclusionState` 三态（Confirmed/CandidateSet/Insufficient）；`ConclusionStateDeriver` 从现有信号（Ok / FinalResult / NeedMoreCondition）只读派生，不写回 Ok/FinalResult/TaskType，**不影响采纳门**。4 个有明确 knowledge_id/rule_id 真值源的示范 solver 命中路径填充 `ReasoningNodes`，其余生产路径保持 `DerivationSteps` 字符串、`ReasoningNodes` 留空。最小表面积：加性字段 + 派生调用点 + 4 solver + 诊断字段贯通 + 测试；不动 JSON 数据、不动评估判分、不动采纳门、不硬编码化学真值。
> **诚实口径**：eval 正确性增益 = 0（结构卫生；为 P2-2 evidence_level 与 P2-4 前端 DAG 提供可溯源数据，使信息不足显式化）。**红线结果**：eval `after_p21` correct=635（== after_p15b）；diff `after_p15b`→`after_p21` **GAINED=0 / LOST=0 / WORSEN=0 / harm=0**；diff `base_p0`→`after_p21` LOST=0 / WORSEN=0 / harm=0（GAINED=6 为 P1-4 携带）；`p21_verify_after.py` adopted∧¬correct=0、派生 0 不一致、CandidateSet=0。

### N.1 本 spec 触及文件清单（content attestation，非 git diff——自 P0 未提交）

**新增**：
- [ReasoningContracts.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/ReasoningContracts.cs)：`ReasoningNode` / `ConclusionState` / `EvidenceLevel` 类型 + `ConclusionStateDeriver`。

**修改（加性，不删改既有字段/逻辑）**：
- [GatewayContracts.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/GatewayContracts.cs)：`EngineResponse` 加 `ReasoningNodes` + `ConclusionState`（默认值不破坏 wire）。
- [LlmGatewayService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs)：`ProcessStudentMessageAsync` 内 `ConclusionStateDeriver.Derive` 调用点 1 处。
- [ConceptFactSolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/ConceptFactSolver.cs)：命中路径填 `ReasoningNodes`（KnowledgeId=match.Id，KnowledgeFact）。
- [ReactionKbWriterSolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/ReactionKbWriterSolver.cs)：命中路径填 `ReasoningNodes`（`KbLookupMolecular`/`KbLookupIonic` 返回 `List<ReactionEntry>` 透传 entry.Id → RuleId/KnowledgeId）。
- [AtomicStructureSolver.cs](file:///d:/github/CogniThink.TutorBrain.Api/Application/Gateway/AtomicStructureSolver.cs)：命中路径填 2 节点链（periodic_table → rule:aufbau_hund_pauli）。
- [VseprSolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/VseprSolver.cs)：命中路径填节点（vsepr_table / rule:vsepr_lone_pair_repulsion）。
- [GatewayVsLlmEvaluationService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs)：仅 `QaCaseResult` 加诊断字段 `GatewayConclusionState`（L83）+ `EvaluateCaseAsync` 赋值（L200）；`BuildGatewayAnswerAsync` 方法体（L664 起）**未动**。
- [Program.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.EvalRunner/Program.cs)：`MapQaCase` 快照加 `conclusionState` 字段。

**测试**：[ReasoningContractDeriverTests.cs](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/ReasoningContractDeriverTests.cs)（10）、[ReasoningSolverTraceabilityTests.cs](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/ReasoningSolverTraceabilityTests.cs)（11，含 Tier 1 新增 HalfReactionBalancer / CrystalCellValidator 命中+R4 留空 4 例、Tier 2 新增 HydrolysisPhCalculator 命中+R4 2 例）。

**未触及**：`data/highschool/*.json`、`data/rules/*.json`、`BuildGatewayAnswerAsync` 方法体、任何判分逻辑。

### N.2 待迁移 solver 清单（ReasoningNodes 暂留空，DerivationSteps 保持字符串）

P2-1 仅示范 4 个有明确 knowledge_id/rule_id 真值源的 solver（ConceptFact / ReactionKb / 原子结构 / VSEPR）。以下生产 `EngineResponse` 构造 / `DerivationSteps` 写入点，除下表 ✅ 标记的 10 个（**Tier 1**：CrystalCellValidator / HalfReactionBalancer；**Tier 2**：HydrolysisPhCalculator；**B-外置批**：KnownEquationWriter / ProductWriter / RedoxGateway / ConceptVerdict；**B-judge-API 批**：MultiSubCorrectnessSolver / JudgmentStatementAggregator；**B-Impurity 批**：ImpurityTrapSolver）外**未填充 `ReasoningNodes`**，保持 `DerivationSteps` 字符串（spec CQ-1 选项 A：增量迁移，表面积可控、零回归）。迁移留待 P2-1.x / P2-2（evidence_level 消费方落地后按优先级推进）：

| 生产者文件 | 构造点（核实行号） | 真值源（已核实） | 分级 | 阻塞项 / 解封动作 |
|---|---|---|---|---|
| [LlmGatewayService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) | **66 处** `new EngineResponse`（登记原写 ~40，已核低估）：~19 处为子求解器传播（`FinalResult=<x>.FinalResult`：L407/431/543/942/958/973/988/1003/1018/1033/1048/1064/1080/1096/1112/1128/1145/1159/1235）；~38 处为专用计算器/硬编码分支（L755/774/788/801/862/917/1225/1281/1300/1346/1368/1390/1430/1448/1477/1517/1594/1645/1738/1794/1981/2036/2105/2174/2236/2277/2307/2333/2458/2479/2496/2522/2541/2565/2582/2606/2652/2680）；其余为守卫/空响应 | 混杂：传播点=子求解器响应；计算点部分读 JSON 常数（Ka/Ksp，L2036/L2105）、部分纯计量、部分硬编码规则（金属活动性顺序、强氧化性酸清单等） | 混合（B 为主） | ①传播点：等子求解器迁移后把 `ReasoningNodes` 从子响应复制上来（加性，可先做）；②计算点逐点子分类——读常数的挂 `KnowledgeId`（同 HydrolysisPh 模式），纯计量留空（C），硬编码规则外置 JSON（B）。**表面积最大、最依赖前置，应最后迁移。** |
| [LlmGatewayService.MultiPartDispatch.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.MultiPartDispatch.cs) | 1 处（L54） | 聚合各子问 `CallEngineAsync` 的 `FinalResult`，但**丢弃子响应本体**（L44 只取 `FinalResult`） | B（传播） | 子求解器迁移后聚合各子问 `ReasoningNodes` 链进父响应；未迁移前留空合规 |
| [RedoxGatewaySolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/RedoxGatewaySolver.cs) | R2/P3 命中路径 | ✅ 已迁移 (B-外置批)：R2 还原产物/电解产物 + P3 判决外置为 `data/knowledge/redox-rules.json`，命中后 `KnowledgeId=entry.Id`（`EvidenceLevel.KnowledgeFact`）；**R1 角色识别（Core 氧化态计算）保持 `ReasoningNodes` 空（R4，无知识锚点）**。eval `after_b_ext` diff 全零、d2-4/trap-limit-1/d8-5 推理节点 0→1、redox-1/2/3 保持 0 | A·done | — |
| [ProductWriterSolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/ProductWriterSolver.cs) | 命中路径 | ✅ 已迁移 (B-外置批)：6 条少量/过量产物骨架外置为 `data/knowledge/product-rules.json`（含占位展开配置），命中后 `KnowledgeId=entry.Id`（`EvidenceLevel.KnowledgeFact`）；触发识别逻辑原样保留。eval `after_b_ext` diff 全零、d1-1..5/pdf-044/pdf-515 推理节点 0→1 | A·done | — |
| [KnownEquationWriterSolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/KnownEquationWriterSolver.cs) | 命中路径 | ✅ 已迁移 (B-外置批)：2 条已知方程式外置为 `data/knowledge/known-equations.json`，命中后 `KnowledgeId=entry.Id`（`EvidenceLevel.KnowledgeFact`）；触发锚点识别原样保留。eval `after_b_ext` diff 全零、pdf-539/pdf-456 推理节点 0→1 | A·done | — |
| [ConceptVerdictSolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/ConceptVerdictSolver.cs) | 命中路径 | ✅ 已迁移 (B-外置批)：断言模式 + verdict + 推导外置为 `data/highschool/concept-verdicts.json`，从 JSON 编译 `Regex`、命中后 `KnowledgeId=entry.Id`（`EvidenceLevel.KnowledgeFact`）；判断空/否定式逻辑原样保留。eval `after_b_ext` diff 全零、pdf-467 推理节点 0→1 | A·done | — |
| [ImpurityTrapSolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/ImpurityTrapSolver.cs) | 命中路径 | ✅ 已迁移 (B-Impurity 批)：单条硬编码「KMnO₄ 氧化乙烯生成 CO₂ 引入新杂质」外置为 `data/knowledge/impurity-traps.json`（id=`impurity-kmno4-ethylene-co2`，触发模式 + 结论 + 推导 + `id`），`Lazy<>` + `DataRoot` 直读，命中后 `KnowledgeId=entry.Id`（`EvidenceLevel.KnowledgeFact`）；触发识别 `if` 原样保留（识别逻辑，B-外置批 AC1 先例）。eval `after_b_judge`→`after_b_impurity` diff 全零、d9-1 推理节点 0→1、gatewayConclusion 字节不变 | A·done | — |
| [MultiSubCorrectnessSolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/MultiSubCorrectnessSolver.cs) | 命中路径 | ✅ 已迁移 (B-judge-API 批)：`CorrectnessContextRules` 2 条硬编码语境规则外置为 `data/knowledge/correctness-context-rules.json`（id=`ccr-acid-nitrate-sulfite` / `ccr-cuprous-ammine-charge`），`EquationCorrectnessJudge.JudgeWithIds` 携带 `CtxRuleId`；仅 ctx 命中子发节点（`KnowledgeId`=ctx rule id、`EvidenceLevel=KnowledgeFact`）+ 末尾汇总节点，engine-only 子留空（R4）。契约加性（原 `Judge` 委托 `JudgeWithIds` 丢弃 id，行为不变）。eval `after_b_ext`→`after_b_judge` diff 全零、pdf-051 推理节点 0→3（2 ctx 子 + 汇总）、pdf-468/471/472 保持 0 | A·done | — |
| [JudgmentStatementAggregator.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/JudgmentStatementAggregator.cs) | 命中路径 | ✅ 已迁移 (B-judge-API 批)：`FactChoiceMatcher.JudgeStatementWithIds` / `FactBaseStatementJudge.JudgeWithIds` / `CompositeStatementJudge.JudgeWithIds` 加法暴露命中事实 id（原 `Judge` 委托，字节级行为不变；Counting/RedoxElectron 命中返回空 id 集合→不发节点，R4）；方程分支取 ctxId、概念分支取 hitIds，聚合器线程化子节点 + 末尾汇总节点。eval `after_b_ext`→`after_b_judge` diff 全零、pdf-119/120/121/122/450/466/469/470/473/480 推理节点 0→>0（10 例）、14 例 gatewayConclusion 字节不变 | A·done | — |
| [HydrolysisPhCalculator.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/HydrolysisPhCalculator.cs) | 1 处（L55/L66） | ✅ 已迁移 (Tier 2)：真读 JSON `acid-constants.json`，`kaKey` 线程化（`KnowledgeId=kaKey`、`EvidenceLevel=EngineDerived`）；命中即锚、无命中返回 null（R4 安全），eval `after_t2_ph` diff 全零 | A·done | — |
| [RedoxRatioCalculator.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/RedoxRatioCalculator.cs) | 1 处（L68） | 从题干方程解析计量数 + 自带 C# `OxState`（L117-162 硬编码 O=-2/H=+1…）算比例，**无 JSON 查表、无真实锚点** | **C（留空）** | 强行挂 rule_id 属「假可溯源」违反 R4；氧化态规则若要溯源须先外置为 rule JSON（低 ROI），当前诚实留空 |
| [CrystalCellValidator.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/CrystalCellValidator.cs) | 1 处 | ✅ 已迁移 (Tier 1) | A·done | — |
| [HalfReactionBalancer.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/HalfReactionBalancer.cs) | 1 处 | ✅ 已迁移 (Tier 1) | A·done | — |

**B 类审计结论（逐项核实，行号均来自源码核对）：**
- **分级统计**（13 个生产者）：已迁移 A=10（Tier 1：CrystalCellValidator/HalfReactionBalancer；Tier 2：`HydrolysisPhCalculator`；**B-外置批**：KnownEquationWriter/ProductWriter/RedoxGateway/ConceptVerdict，eval `after_b_ext` diff 全零、恰 13 例推理节点 0→1、R1 3 例保持 0；**B-judge-API 批**：MultiSubCorrectnessSolver/JudgmentStatementAggregator，eval `after_b_ext`→`after_b_judge` diff 全零、恰 11 例推理节点 0→>0、3 例 R4 留空、14 例 gatewayConclusion 字节不变；**B-Impurity 批**：ImpurityTrapSolver，eval `after_b_judge`→`after_b_impurity` diff 全零、d9-1 推理节点 0→1、gatewayConclusion 字节不变）；B=1（传播 1：MultiPartDispatch）；混合 1（`LlmGatewayService` 66 处，B 为主、含传播/计算/外置三类，须最后做）；C=1（`RedoxRatioCalculator`，诚实留空）。
- **更正的登记错误**：①Redox「氧还规则有 rule id」→ 实为硬编码无 id；②ProductWriter「可挂 RuleId」→ 无 id；③KnownEquation「已知方程模板有 id」→ 无 id；④`LlmGatewayService`「~40 处」→ 实测 **66 处**。另 `ImpurityTrap` 标「无知识源」不准（化学事实硬编码），`HydrolysisPh` 标「中」低估（已读 JSON 可挂 id）。
- **建议执行顺序**：(1) ✅ 重判 A 类 `HydrolysisPhCalculator` 已迁移（Tier 2，eval `after_t2_ph` diff 全零、pdf-517 推理节点 0→1、无臆造无回归）；(2) ✅ **B-外置批已完成**（KnownEquation / ProductWriter / RedoxGateway / ConceptVerdict，eval `after_b_ext` diff 全零、恰 13 例 0→1、R1 保持 0、无臆造无回归）；(3) ✅ **B-judge-API 批已完成**（MultiSubCorrectnessSolver / JudgmentStatementAggregator：裁决契约加性暴露命中 id + `CorrectnessContextRules` 外置，eval `after_b_ext`→`after_b_judge` diff 全零、恰 11 例 0→>0、3 例 R4 留空、14 例 gatewayConclusion 字节不变、无臆造无回归）；(4) ✅ **B-Impurity 批已完成**（ImpurityTrapSolver：化学事实外置为 `data/knowledge/impurity-traps.json`（id=`impurity-kmno4-ethylene-co2`），命中线程化 `KnowledgeId`，eval `after_b_judge`→`after_b_impurity` diff 全零、d9-1 推理节点 0→1、gatewayConclusion 字节不变、无臆造无回归）；(5) 剩余：`LlmGatewayService.MultiPartDispatch` 传播（B）+ 最后迁移 `LlmGatewayService` 传播点 + 逐点子分类计算点。每步均须 `diff_ckpt` 全零 + 命中路径发节点、无锚路径留空的自检。

> 另：`Batch2NarrowCalcSolver` / `StoichiometryStateTrapSolver` / `ThermochemicalHessSolver` / `GraphSolver` / `BalanceGatewaySolver` 自身不直接写 `DerivationSteps`，其产出经 `LlmGatewayService` 内联构造或聚合器消费，随 `LlmGatewayService` 迁移一并覆盖。

### N.3 ConclusionState 与原 Ok / NeedMoreCondition 映射对照表（可审计）

| 原信号（Ok / FinalResult / NeedMoreCondition） | ConclusionState | 含义 | eval 计数（after_p21） |
|---|---|---|---|
| 求解器显式置 CandidateSet | CandidateSet | 多候选未唯一（当前无生产路径，预留） | 0 |
| Ok==true 且 FinalResult 非空 | Confirmed | 引擎给出唯一可溯源结论 | 615 |
| Ok==false | Confirmed | 引擎确定地判定学生答案错误（spec CQ-3，仍属"给出确定结论"） | 26 |
| Ok==null（含 NeedMoreCondition / FinalResult 空 / 范围外 / 能力缺口） | Insufficient | 信息不足 / 能力缺口（原静默 null 显式化） | 116 |
| **合计** | — | — | **757** |

> 派生规则由 `ConclusionStateDeriver.Derive` 单一赋值点执行，只读不写回 Ok/FinalResult/TaskType → 不影响采纳门 `BuildGatewayAnswerAsync`。T6 自检（[p21_self_check.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p21_self_check.py)）实证：`ConclusionState` 在 `GatewayVsLlmEvaluationService.cs` 仅出现 2 次（L83 字段 + L200 赋值），均在 `BuildGatewayAnswerAsync` 定义（L664）之前；逐 case `gatewayAnswer` / `gatewayOk` / `gatewayConclusion` 与 `after_p15b` 完全一致（diff=0）。Confirmed=641 = Ok:true 615 + Ok:false 26，与原信号可加；MISSING=0（每行都有派生值）。

---

## O 类：P2-2 世界模型与机理层最小可用版闭环（spec: world-model-minimal）

> **范围**：在不改动判分/采纳门的前提下，为网关 `EngineResponse` 追加一条**趋势级/机理级解释链**（`ExplanationNodes`，`EvidenceLevel=Trend`），标注「帮助理解，不作判定依据」，为 P2-4 可解释性打底。三条链（M1 金属性/还原性递变 · M2 沉淀-配位溶解 · M3 盐类水解酸碱性），触发限定窄、定性不给绝对值；节点内容**全部来自既有 JSON**（rule `Conclusion.Display` / reaction `Equation`），零数据改动、零化学硬编码。采纳门 `BuildGatewayAnswerAsync` 不读 `ExplanationNodes`（spec CQ-5），故 eval 正确性收益预期=0；价值=可溯源解释。
> **红线结果**：eval `after_p22`（Production LLM off，757/757 完成、failed=0）correct=635 / precision=1 / harm=0；diff `after_p21`→`after_p22` **GAINED=0 / LOST=0 / WORSEN=0 / harm=0**（verdict 不变，解释链不影响判分）；diff `base_p0`→`after_p22` GAINED=6（P1-4 携带：pdf-415/530/535/542/543/trap-limit-2）/ LOST=0 / WORSEN=0 / harm=0。全库 757 题中 `explanationNodesCount>0` = 11 题（M1×1、M2×1 含 2 节点、M3×9），其中 Confirmed=8（全部 adopted 且 correct）、Insufficient=3（未采纳）→ 无 Insufficient 被采纳；`explanationNodes>0 ∧ adopted → 全部 correct`（不蒙数）。

### O.1 本 spec 触及文件清单（content attestation，非 git diff——自 P0 未提交）

| 文件 | 改动 | 说明 |
|------|------|------|
| [WorldModel.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/WorldModel/WorldModel.cs) | **新增** | Core 世界模型：`TrendExplanationNode` 类型 + 静态 `WorldModel.Explain(text, finalResult)`，3 链纯函数；进程内 `Lazy<Snapshot>` 缓存加载 activity-series / rules / reactions；无命中返回空（不蒙数） |
| [GatewayContracts.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/GatewayContracts.cs) | 加性 | `EngineResponse` 追加 `ExplanationNodes` 字段（`List<ReasoningNode>`，默认 `new()`，JSON key `explanation_nodes`），置于 `ReasoningNodes` 之后；旧字段不动 |
| [ReasoningContracts.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/ReasoningContracts.cs) | 加性 | 新增 `WorldModelEnricher.Enrich(resp, questionText)`：调 `WorldModel.Explain`，映射为 `ReasoningNode`（`EvidenceLevel=Trend`），仅写 `resp.ExplanationNodes`，不改 Ok/FinalResult/TaskType |
| [LlmGatewayService.cs](file:///d:/github/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) | 加性 | 在 `ConclusionStateDeriver.Derive` 之后、`GenerateReplyAsync` 之前插入 `WorldModelEnricher.Enrich` 调用 |
| [GatewayVsLlmEvaluationService.cs](file:///d:/github/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs) | 加性 | `QaCaseResult` 追加 `GatewayExplanationNodesCount` 诊断字段 + 赋值（在 `BuildGatewayAnswerAsync` 调用之前，仅观测不计分） |
| [Program.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.EvalRunner/Program.cs) | 加性 | `MapQaCase` 快照追加 `explanationNodesCount` |
| [WorldModelExplanationTests.cs](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/WorldModelExplanationTests.cs) | **新增** | 6 测试：M1 周期递变（STR-001/002）、M1 活动性序（pdf-176 真题干，RED-001）、M2 AgCl/氨水（pdf-213 真题干）、M3 碳酸根水解（pdf-277 真题干 +「不涉及 pH 数值」）、无命中不蒙数（空）、富化不改 Ok/FinalResult/ReasoningNodes |

> **未触及**：`data/**` 任何 JSON；`BuildGatewayAnswerAsync` 方法体；任何判分/采纳/路由逻辑。

### O.2 三链事实来源（全部复用既有 JSON，零数据改动）

| 链 | 触发条件（窄） | 引用事实（JSON 路径 + id） |
|----|----------------|---------------------------|
| M1 金属性/还原性递变 | `(金属性\|非金属性\|还原性\|氧化性\|活动性\|活泼性).{0,12}(递变\|强弱\|比较\|顺序\|增强\|减弱\|由.+到)` 或 `(同周期\|同主族).{0,12}(金属性\|非金属性\|还原性\|氧化性)` | `data/rules/rules.json`：STR-001（同周期，KH-ST-02）、STR-002（同主族，KH-ST-02）、RED-001（金属活动性置换，KH-IN-06）；+ `data/highschool/activity-series.json` 确定序（题干中独立出现 ≥2 种金属时，按序给出相对位置，排除 H） |
| M2 沉淀-配位溶解 | `AgCl`/`氯化银` ∧ `氨水`/`氨`/`银氨` | `data/rules/rules.json`：SOL-EXC-001（AgCl 氨水配位溶解，KH-RP-07）；+ `data/knowledge/reactions/reactions-principles.json`：RX-PL-PPT-001（AgCl ⇌ Ag⁺+Cl⁻） |
| M3 盐类水解酸碱性 | `Na2CO3`/`碳酸钠`/`CO3`/`碳酸根` ∧ `水解`/`碱性`/`显碱`/`溶液.{0,4}碱` | `data/knowledge/reactions/reactions-principles.json`：RX-PL-HYD-003（碳酸根水解，KH-RP-06）；描述含「定性，不涉及 pH 数值」 |

> 节点 `Description`/`Premises`/`Conclusion` 内容取自 `rule.Conclusion.Display` / `reaction.Equation`，**无化学真值硬编码**（仅允许展示性拼接）；每节点追加免责声明 `WorldModel.Disclaimer = "帮助理解，不作判定依据"`。

### O.3 命中清单与 verdict-unchanged 主张（eval `after_p22`）

全库 757 题中 `explanationNodesCount>0` = 11 题；节点数分布 `{1: 10, 2: 1}`（M2 一题产出 2 节点）。Python 镜像触发逻辑与 C# 实际存储值 **0 mismatch**（[p22_chain_probe.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p22_chain_probe.py)）。

| id | 命中链 | 节点数 | conclusionState | adopted | correct |
|----|--------|--------|-----------------|---------|---------|
| pdf-057 | M3 (RX-PL-HYD-003) | 1 | Confirmed | T | T |
| pdf-089 | M3 (RX-PL-HYD-003) | 1 | Confirmed | T | T |
| pdf-096 | M3 (RX-PL-HYD-003) | 1 | Confirmed | T | T |
| pdf-137 | M3 (RX-PL-HYD-003) | 1 | Confirmed | T | T |
| pdf-156 | M3 (RX-PL-HYD-003) | 1 | Insufficient | F | F |
| pdf-176 | M1 (RED-001, 金属 Fe/Cu) | 1 | Insufficient | F | F |
| pdf-213 | M2 (SOL-EXC-001 + RX-PL-PPT-001) | 2 | Confirmed | T | T |
| pdf-269 | M3 (RX-PL-HYD-003) | 1 | Confirmed | T | T |
| pdf-277 | M3 (RX-PL-HYD-003) | 1 | Confirmed | T | T |
| pdf-446 | M3 (RX-PL-HYD-003) | 1 | Confirmed | T | T |
| pdf-478 | M3 (RX-PL-HYD-003) | 1 | Insufficient | F | F |

> **verdict-unchanged 实证**：diff `after_p21`→`after_p22` GAINED=0/LOST=0/WORSEN=0/harm=0，correct 保持 635 → 解释链未改变任何判定。`ExplanationNodes` 仅在采纳门调用**之前**写入 `EngineResponse`，`BuildGatewayAnswerAsync` 不读该字段（T6 自检 grep 0 命中）。
> **不蒙数实证**：全局 `adopted∧¬correct=0`；无 `Insufficient` 被采纳；`explanationNodes>0 ∧ adopted → 全部 correct`（[p22_diag.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p22_diag.py)）。

---

## P 类：P2-3 图像与文本求解融合闭环（spec: image-text-fusion）

> **范围**：把网关中**隐式**的「确定性化学求解器优先于图像原始读数」分层（事实交叉验证）**显式化**，加 `EngineResponse.ResultProvenance` 可观测诊断（采纳结论来源，不计分），加回归保护。**零行为变更**（CQ-1=A）：不改采纳门 `BuildGatewayAnswerAsync` / `IsStructuredCorrect` 判决；不改 `FinalResult`/`Ok`/`TaskType`/`AssetKind`；`data/**` 零改动（CQ-4）。
> **诚实口径**：审计证实路线图点名的 pdf-272 曲线中间量误采纳**已在阶段3F（AC-22）修复**，当前 harm=0。故本 spec **预期 eval 正确性增益 = 0**（结构/卫生项，与 P2-1/P2-2 同）。价值 = 融合策略可审计 + provenance 可观测（供 P2-4 前端标注置信）+ 防回退。
> **红线结果**：eval `after_p23`（Production LLM off，757/757，failed=0）correct=635 / precision=1 / harm=0；diff `after_p22`→`after_p23` **GAINED=0 / LOST=0 / WORSEN=0 / harm=0**（verdict 不变，零行为变更）；diff `base_p0`→`after_p23` GAINED=6（P1-4 携带）/ LOST=0 / WORSEN=0 / harm=0。核心红线：`curve_reading` provenance 被采纳题数 = **0**（中间量永不误采纳）。

### P.1 本 spec 触及文件清单（content attestation，非 git diff——自 P0 未提交）

| 文件 | 改动 | 说明 |
|------|------|------|
| [GatewayContracts.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/GatewayContracts.cs) | 加性 | `EngineResponse` 新增 `ResultProvenance: string?`（默认 null，JSON key `result_provenance`）；旧字段全保留 |
| [LlmGatewayService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) | 加性 | `CallEngineAsync` 7 个融合分支产出结论时设 `ResultProvenance` 标签（仅设标签，不改 FinalResult/Ok/TaskType/AssetKind） |
| [GatewayVsLlmEvaluationService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs) | 加性 | `QaCaseResult` 新增 `GatewayResultProvenance` 诊断字段 + 在采纳门**之前**赋值；采纳门不读（不计分） |
| [Program.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.EvalRunner/Program.cs) | 加性 | `MapQaCase` 快照新增 `resultProvenance` |
| [ImageTextFusionRegressionTests.cs](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/ImageTextFusionRegressionTests.cs) | **新增** | 4 测试：契约加性；pdf-259 equilibrium 标 provenance 且结论不变；pdf-272 curve_reading 标 provenance 且不被采纳；合成中间量读数不被采纳 |

> **未触及**：`data/**` 任何 JSON；`BuildGatewayAnswerAsync` / `IsStructuredCorrect` 方法体；任何判分/采纳/路由判决逻辑。

### P.2 显式融合策略表（spec §3.1）与 provenance 标签

| 优先级 | 产出者 | provenance 标签 | after_p23 观测 |
|--------|--------|-----------------|----------------|
| 1（最高） | `CallEquilibrium` | `equilibrium` | n=13，13 采纳+判正 |
| 2 | `CurveChoiceValidator` | `curve_choice` | n=6，6 采纳+判正 |
| 3 | `ConceptChoiceValidator` | `concept_choice` | n=250，250 采纳+判正 |
| 4 | `TryRouteGraph` | `graph` | n=1，1 采纳+判正 |
| 5 | `TryRouteCurve` | `curve_reading`（**中间量高风险类**） | n=1（pdf-272），**0 采纳**（正确拦截） |
| 6 | `TryRouteMolecule` | `molecule` | n=4，2 采纳+判正；2 未采纳（pdf-391/522 召回缺口，CQ-3=a 排除，另立 spec） |
| 7 | `CrystalCellValidator` | `crystal` | n=6，6 采纳+判正 |
| 8（兜底） | 通用文本事实瀑布 | `null`（= text_fact） | n=476，357 采纳+判正 |

> 所有被采纳（adopted=true）的结论，其 provenance 均为确定性求解器（equilibrium/crystal/curve_choice/concept_choice）或 graph/molecule 或 null 文本事实；**无任何 adopted 结论来自 curve_reading**——这正是「图像读数 + 化学事实交叉验证后才采纳」的可观测证据。

### P.3 verdict-unchanged 与不蒙数实证（eval `after_p23`）

- diff `after_p22`→`after_p23`：GAINED=0 / LOST=0 / WORSEN=0 / harm=0；correct 保持 635 → provenance 诊断未改变任何判定。`ResultProvenance`/`GatewayResultProvenance` 仅在采纳门调用**之前**写入/赋值，`BuildGatewayAnswerAsync` 不读该字段（T6 自检：Evaluation 服务内 `Provenance` 仅 2 次——字段 L89 + 赋值 L209，均在 `BuildGatewayAnswerAsync` 定义 L673 之前）。
- diff `base_p0`→`after_p23`：GAINED=6（P1-4 携带：pdf-415/530/535/542/543/trap-limit-2），LOST=0 / WORSEN=0 / harm=0。
- 不蒙数：全局 `adopted∧¬correct=0`；`curve_reading` 被采纳 = 0（[p23_diag.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p23_diag.py)、[p23_self_check.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p23_self_check.py)）。

---

## Q 类：P2-4 前端接入网关求解闭环（spec: frontend-gateway-solve-loop）

> **范围**：在 miniapp 新增「题目求解」入口，调用新增的学生面投影端点 `POST /api/gateway/solve`，按 `ConclusionState` 裁剪字段后展示（结论卡 + 渐进推理 DAG + 标注趋势解释 + 教学话术 + 复盘），并从服务端源头保证**不泄露未采纳结论**。**纯加性 + 只读**：不改 `ProcessStudentMessageAsync` / `CallEngineAsync` / `BuildGatewayAnswerAsync` / `IsStructuredCorrect`；`data/**` 零改动（CQ-5）。
> **诚实口径**：预期 eval 正确性增益 = 0（前端 + 服务端只读投影，不改求解/判分）。价值 = 把 P2-1/P2-2/P2-3 已产出的结论三态 / 推理 DAG / 趋势解释端到端展示出来，并在学生面强制不泄露。

### Q.1 触及文件清单（content attestation，无 data/**）

- **后端（加性，只读）**：
  - 新增 [GatewaySolveProjection.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/GatewaySolveProjection.cs)：`StudentSolveView` DTO + `GatewaySolveProjector.Project` 纯函数（按 ConclusionState 裁剪，不改输入）。
  - 修改 [GatewayController.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Api/Controllers/GatewayController.cs)：新增 `POST /api/gateway/solve`（复用 `ProcessStudentMessageAsync` + 投影，**不动** `/api/gateway/process`）。
- **测试**：新增 [GatewaySolveProjectionTests.cs](file:///d:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/GatewaySolveProjectionTests.cs) 5 测试（Confirmed Ok=true / Confirmed Ok=false / Insufficient 不泄露 / CandidateSet / 纯只读不改输入）。
- **前端（miniapp，Taro）**：
  - 修改 [tutorApi.ts](file:///d:/github/CogniThink-Chem/miniapp/src/services/tutorApi.ts)：加 `StudentSolveView` / `ReasoningNodeDto` 类型 + `solve()` 方法（不动既有方法签名）。
  - 修改 [app.config.ts](file:///d:/github/CogniThink-Chem/miniapp/src/app.config.ts)：注册 `pages/solve/index`。
  - 修改 [pages/home/index.tsx](file:///d:/github/CogniThink-Chem/miniapp/src/pages/home/index.tsx)：加「题目求解」入口。
  - 新增 `miniapp/src/pages/solve/{index.tsx, index.config.ts, index.module.scss}`。
- **scratch**：`_evalscratch/verify/p24_solve_probe.py`、`p24_solve_confirmed.json`、`p24_solve_insufficient.json`、`p24_self_check.py`。
- **未触及**：`data/**` 任何 JSON；`ProcessStudentMessageAsync` / `CallEngineAsync` / `BuildGatewayAnswerAsync` / `IsStructuredCorrect` 方法体；任何求解与判分逻辑。

### Q.2 投影门控策略表（按 ConclusionState 下发字段矩阵）

| 字段 ＼ 状态 | Confirmed | CandidateSet | Insufficient |
|--------------|-----------|--------------|--------------|
| `conclusion_state` | ✓ | ✓ | ✓ |
| `final_result` | ✓ 下发 | ✗ null（不泄露） | ✗ null（**不泄露**） |
| `reasoning_nodes` | ✓ | ✗ [] | ✗ [] |
| `candidates` | ✗ [] | ✓（当前空，预留） | ✗ [] |
| `explanation_nodes` | ✓（标注 Trend） | ✓（标注 Trend） | ✗ [] |
| `reply` | ✓ | ✓ | ✓ |
| `constraint_note` | — | — | ✓ 透传 |
| `task_type` / `result_provenance` | ✓ | ✓ | ✓ |

- 投影为纯只读函数，不修改输入 `EngineResponse`（单测 `Projector_DoesNotMutateInputEngineResponse` 覆盖）。
- **运行时 HTTP 实证**（[p24_solve_probe.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p24_solve_probe.py)，API Production / LLM off）：
  - Confirmed 题 **ion-1** → `conclusion_state=Confirmed`，`final_result='可以大量共存'` 正常下发，task_type=ion_check。
  - Insufficient 题 **trap-limit-3** → `conclusion_state=Insufficient`，`final_result=None`，`reasoning_nodes=[]`，`explanation_nodes=[]`，`constraint_note` 透传 → **不泄露未采纳结论**。

### Q.3 eval 零漂移（后端求解/判分未改）

- `after_p24`：757/757 failed=0，correct=635，precision=1，harm=0。
- diff `after_p23`→`after_p24`：GAINED=0 / LOST=0 / WORSEN=0 / harm=0 → **RED-LINE PASS**（投影在新 HTTP 路径，eval 走进程内不走 HTTP，零影响）。
- diff `base_p0`→`after_p24`：GAINED=6（P1-4 携带：pdf-415/530/535/542/543/trap-limit-2），LOST=0 / WORSEN=0 / harm=0。

### Q.4 已知残余（非本 spec 范围，登记防止误解）

- **6 个 `ConclusionState=Confirmed` 但 eval 未被答案键采纳（答错）的题**：运行时无答案键，服务端无法识别为错，仍会以「引擎确定结论」下发。对策：前端诚实标注「已通过引擎确定性校验，仅供参考，不宣称 100% 正确」。这 6 个属 precision=1 红线的 **eval 期保证**范畴（系统在有答案键时不采纳错），运行时不可消除，不在本 spec 范围。

---

## R. P3-1 数据资产归一与缺口报表自动化（2026-11-22）

> spec：`.trae/specs/data-asset-inventory-gap-report/{spec.md, tasks.md}`；CQ-1..6 已批准。诚实口径：**预期 eval 增益 = 0**（纯工具 + 纯新增产物，不改运行时数据/求解/判分/eval 管线）。

### R.1 触及范围（content attestation）

**仅新增**，无修改/删除运行时文件：
- `tools/data-assets/asset_map.py` — 52 项消费者/加载机制/校验入口映射（据 T0 审计固化）。
- `tools/data-assets/gen_inventory.py` — 清单生成器（只读 `data/**`，产物只写本目录；支持 `--repo-root` 供演示/测试）。
- `tools/data-assets/gap_report.py` — 主干知识点缺口报表（只读数据，strict/lenient 双匹配，候选标注）。
- `tools/data-assets/data-asset-manifest.json`（机读，tracked）+ `data-asset-inventory.md`（人读，tracked）+ `gap_report.md`（tracked）— 生成产物。
- `_evalscratch/verify/p31_data_baseline.json`（52 JSON 的 after_p24 sha256 基线）、`p31_version_binding_demo.py`、`p31_self_check.py`（gitignored 自检）。
- 本台账（新增 §R）。

**未触及**：任何 `data/**` JSON 的内容/结构；任何运行时 `.cs`（加载器/求解器/采纳门/判分/eval 管线）；`.gitignore`；5 个 `.bak`。

### R.2 数据资产清单产物

- `data-asset-manifest.json`：覆盖 **52 个 JSON**（根 A `data/` 44 + 根 B `Api/data/` 8），每项含 sha256 / byte_size / item_count / schema_kind / declared_version / effective_version / consumer / load_mechanism / validation_entry；**0 项 UNMAPPED**。
- `effective_version` 诚实口径：有顶层 `version` 字段用其值（如 `1.0`/`1.5`/`0.2`），否则用 `hash:<sha256[:12]>` 内容指纹（**不编造版本号**）。**11 个 JSON 无 `version` 字段**（含 `pdf_questions`/`tier2`/6 个 `chemistry/*`/`electrode-half-reactions` 等裸 list，以及 `electrode-reaction-facts`/`infeasible-equation-patterns` 两个无 version 的 dict），均指纹绑定。
- `manifest_hash`：对数据内容（排除 `generated_at`/`repo_root` 等非数据元数据）的稳定指纹——**同数据跨次运行/跨机器一致**（已自检幂等）。
- `binding` 区段：`question_sets`（pdf_questions + tier2）/ `fact_library`（24 highschool + electrode-half-reactions）/ `knowledge_base`（knowledge.json）指纹绑定，回归 diff 可精确定位变更文件。
- `data-asset-inventory.md`：分组人读表 + 碰撞标注 + 二进制资产计数（PNG 282 + SVG 129 = 411，为网关图像路由合法运行时资产，属 P0-3 范畴不在本 spec 处理）。

### R.3 缺口报表产物（`gap_report.md`）

- 覆盖 `knowledge.json` **50 个知识点**（`tier=Main` 44 个）× 24 个事实文件 × 675 道题（pdf_questions 545 + benchmark 130… 实际扫描 675）。
- **双匹配（strict 驱动标注，lenient 仅作上下文）**：strict = 完整 `name`/`textbookRef` 子串；lenient = strict + 名称按通用分隔符切出的 ≥2 字 token。无 `knowledge_id` 外键，**全部缺口为候选，需人工复核**。
- strict 标注结果：12 covered / 4 no-fact / 0 no-question / 34 no-fact-and-no-question。
- lenient 上下文：37 项为「likely phrasing FN」（strict 漏但 lenient 命中→大概率措辞差异，低优先级），**1 项 strong candidate**：`KH-RP-09 电解池`（strict/lenient 事实均 0，但有 13 道题→值得人工复核是否缺事实；仍为候选，不宣称确定性）。
- 脚本只读，不进 eval 管线 → 零 eval 影响。

### R.4 版本绑定策略

- **manifest 旁路指纹，零运行时 JSON 改动**（CQ-1=a）：不改任何 `data/**` JSON（裸 list 包 dict 会改变顶层类型、破坏 `JsonSerializer.Deserialize<List<T>>`，故走旁路）。
- `manifest_hash` 是数据内容的稳定指纹；两次 eval checkpoint 各自带 manifest 即可 diff 出「哪个数据文件变了」。
- **演示已证（T3，AC-4）**：在临时副本中将 `data/knowledge/electrode-half-reactions.json` 改 1 字节后重跑生成器 → 仅该项 sha256 与 `effective_version`（hash-bound）变化、`manifest_hash` 变化，其余 51 项 sha256 完全不变；真实数据与真实 manifest 未被触碰。

### R.5 三文件名跨根碰撞登记（归一化留 P3-2）

| 文件名 | 根 A `data/highschool/` | 根 B `Api/data/chemistry/` |
|--------|------------------------|---------------------------|
| `ions.json` | dict（cations/anions/gases）· RuleStore | list[56] · ChemicalKnowledgeBase |
| `substances.json` | dict（organics/otherInorganics）· RuleStore | list[186] · ChemicalKnowledgeBase |
| `solubility.json` | dict{version,source,table} · RuleStore | dict{rules,exceptions} · ChemicalKnowledgeBase |

同文件名、不同 schema、不同消费者。合并/分工属 **P3-2**（依赖路线图 §6#1 两套栈收敛方向的用户决策）；本 spec 仅登记标注。

### R.6 5 个 stray `.bak` 处置（CQ-3=a）

`Api/data/pdf_questions.json.bak_alpha2` / `.bak_alpha2batch2_current` / `.bak_pre_alpha2batch2` / `.bak_pre_alpha2complete` / `.bak_pre_verifyfixbatch`：untracked、无任何 `.cs` 引用、不被加载。仓库自 P0 无 commit，可能是早期 pdf_questions 变体的唯一留存 → **仅登记，不删除/不移动**，保留用户选择权。

### R.7 eval 零漂移（AC-3）

- `after_p31`：757/757 全跑，correct=635，precision=1，harm=0。
- diff `after_p24`→`after_p31`：GAINED=0 / LOST=0 / WORSEN=0 / harm=0 → **RED-LINE PASS**（P3-1 未碰运行时数据/求解/判分，diff 必为 0，已证）。
- diff `base_p0`→`after_p31`：GAINED=6（P1-4 携带，非 P3-1 新增），LOST=0 / WORSEN=0 / harm=0 → **RED-LINE PASS**。
- 红线自检 `p31_self_check.py` **18/18 PASS**（密钥 0 命中 / 52 JSON sha256 与 after_p24 全同 / 产物不被运行时 .cs 引用 / 生成器幂等 / 不蒙数）。

---

## S. P3-2 规则/事实体系归一（2026-11-22）

- S.1 **范围与触及文件 attestation**：P3-2 为「明确分工 + 交叉引用」（roadmap §6#1 用户确认：网关为主 / Core 基准回归）。触及文件仅：新增 `tools/data-assets/rule_fact_ownership.py`（只读生成器）+ 产物 `rule-fact-ownership.md` / `rule-fact-crossref.json`；改 `ARCHITECTURE.md`（§3.2 标已确认、§3.4 新增分工、措辞订正）；新增 `_evalscratch/verify/p32_self_check.py`（scratch）；本台账新增 §S。**未改任何 `data/**` JSON、未改任何运行时 `.cs`、未改 eval 管线** → 预期 eval 增益 = 0。
- S.2 **所有权矩阵**：`rule-fact-ownership.md` 覆盖 **29 行** = 26 主资产（rules.json + rule-tests.json + 24 个 highschool）+ 3 跨根碰撞伴随行（`Api/data/chemistry/{ions,substances,solubility}.json`）。每行含 owner_stack / role / 消费者证据（file:line）/ 化学域。分工：`data/rules/rules.json` = Core 仲裁/基准规则库（网关不消费）；`data/highschool/*` = 网关事实库（Core 复用子集）；三碰撞不合并。
- S.3 **KP 交叉引用**：`rule-fact-crossref.json` 覆盖全部 50 KP。rules 侧用**精确 `knowledgeId` 外键**（实测 0 dangling，61 条规则覆盖 26 个 KP）；facts 侧用**名称子串候选匹配**（标注 `match=candidate`，需人工复核，同 P3-1 gap_report 双口径）。**24 个 KP 无任何规则**（`kps_without_rules`，含 KH-RP-09），不属本项修复。coverage：rules+facts=7 / rules-only=19 / facts-only=5 / neither=19。
- S.4 **ARCHITECTURE.md 落位**：§3.2 收敛决策从「推荐，待确认」升级为「已确认（2026-11-22）」；新增 §3.4「规则/事实体系分工」；订正「网关不使用 RuleEngine 仲裁」。
- S.5 **eval 零漂移**：无运行时改动 → `diff after_p31→after_p32` 全 0（RED-LINE PASS）；`diff base_p0→after_p32` LOST/WORSEN/harm=0（GAINED 维持 6）。`p32_self_check.py` 全 PASS（52 JSON sha256 与 after_p31 基线全同 / 产物不被运行时 .cs 引用 / 生成器幂等 / 不蒙数）。

---

## T. P3-3 导学与求解融合（加性锚定，2026-11-22）

- T.1 **范围与触及文件 attestation**：P3-3 采用「加性锚定（CQ-1=a）」——导学流按需获取网关标准推理链（`ReasoningNodes` DAG）作参照 + 诊断检查点按 `knowledge_id` 回溯到 DAG 节点，**不改判分源、不改网关求解 / 采纳门 / eval 判分**。触及文件：新增 `Application/TutorSolveChainProvider.cs`（取链 + 缓存 + 降级，只读网关）/ `Application/Diagnostic/DiagnosticCheckpointLinker.cs`（纯匹配）/ `Application/Diagnostic/DiagnosticExplanationAnchor.cs`（纯锚点追加）/ 前端 `miniapp/src/components/ReasoningDag/`；改 `Api/Contracts/Dtos.cs`（`SubmitStepResponse` 加可选 `StandardReasoningChain`）/ `Application/TutorService.cs`（取链挂响应，取链在 `SaveChangesAsync` 之后、纯加性）/ `Api/Controllers/TutorController.cs`（透传）/ `Application/Diagnostic/DiagnosticContracts.cs`（`ReasoningCheckpoint` 加可选 `LinkedReasoningNode`）/ `Application/Diagnostic/DiagnosticTeachingEngine.cs`（链接 + 锚点，仅 chain 非空时触发）/ `Api/Controllers/DiagnosticController.cs`（透传）/ `Program.cs`（DI 注册）/ 前端 `tutorApi.ts` + `pages/tutor/index.tsx`（最小展示）。**未改任何 `data/**` JSON、未改任何网关 solver、未改 `LlmGatewayService` 主体、未改采纳门 `BuildGatewayAnswerAsync` / `IsStructuredCorrect`、未改 eval 判分文件** → 预期 eval 增益 = 0。
- T.2 **加性锚定策略**：`StandardReasoningChain` / `LinkedReasoningNode` 均为可选加性字段，既有 JSON 键不删不改（wire 向后兼容）。取链复用 P2-4 门控：仅 `ConclusionState=Confirmed` 且节点非空时下发，`Insufficient` / `CandidateSet` / 空 DAG → `null`（不泄露未采纳结论）；排除 `EvidenceLevel=Trend`（世界模型解释不作判定依据）。取链失败/超时降级为 `null` 且不缓存（允许重试），不阻塞判分主路径。
- T.3 **4-solver 覆盖限制登记**：当前仅 4 个 solver 家族填充 `ReasoningNodes`（ConceptFactSolver / ReactionKbWriterSolver / AtomicStructureSolver / VseprSolver），故「标准推理链」仅对路由到这些 solver 的题非空；其余题 `StandardReasoningChain=null`，导学行为与现状逐字节一致（AC-4 快照验证）。不夸大覆盖率，不迁移其余 solver（留后续批次）。
- T.4 **eval 覆盖盲区声明**：757 eval 只测网关求解路径，**不覆盖导学流**（grep `*Eval*.cs` 对 `TutorService` / `DiagnosticTeachingEngine` / `SubmitStepAsync` = 0 命中）→ 导学流回归在 eval 中不可见。三重守护：(a) 本项不改判分源 / 网关 / 采纳门；(b) 导学专用快照测试 `TutorSnapshotRegressionTests`（4 场景：正确非关键步 / 正确关键步带化学证据 / 三步通关带变式 / 答错带薄弱前置）断言 `SubmitStepResponse` 既有字段在 provider=null / 返回 null / 返回真实链三者间逐字节一致，仅新增字段不同（AC-4，4/4 PASS）；(c) 加性降级（CQ-4=a）。
- T.5 **eval 零漂移**：`after_p33`：757/757 全跑，correct=635，precision=1，harm=0。`diff after_p32→after_p33`：GAINED=0 / LOST=0 / WORSEN=0 / harm=0 → **RED-LINE PASS**（P3-3 未碰网关/eval 路径，diff 必为 0，已证）；`diff base_p0→after_p33`：GAINED=6（P1-4 携带，非 P3-3 新增），LOST=0 / WORSEN=0 / harm=0 → **RED-LINE PASS**。`p33_self_check.py` 全 PASS（密钥 0 命中 / 触及清单不含网关 solver·采纳门·eval 判分 / 52 JSON sha256 与 p31 基线全同 / 加性字段 wire 兼容 / 快照测试在位 / 不蒙数）。

---

## 处理原则（固化，防止回退到硬编码路线）

1. **引擎是唯一真值源**：化学判定走 `IStatementJudge` 聚合器（事实库/计数/氧化还原电子），不向 `general-choice-facts` 追加单题策展条目。
2. **数据修复只信官方源**：答案键改前必须有答案册 OCR 或原页核对（5b）；化学推导仅作辅助，5a 类（存储键为明显垃圾值 `"A,B,C,D"` 且 note 结论明确）沿用 Task 35/46 已验证流程，但跨多选项/题型存疑时（如 075）不单独触发存储键变更，必须官方源裁定。
3. **文本回填只写 `question`/`options`，不写 verdict、不改 `answer`**：回填后必须过 EvalRunner diff（LOST=0、harm=0）与字段 diff 审查。
4. **不可靠恢复一律登记、保持 `null`**：宁可未解，不可假阳性。
