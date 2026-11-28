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
| pdf-406 | `A, B, C, D` → `C` | 答案册 OCR `page_137` 第 12 题标记 `12.C`：问「不能完成气体发生和收集的是」，C（乙醇脱水制乙烯需 170℃ 控温，无温度计）不能完成；与 pdf-405 共享同一 `"A,B,C,D"` 损坏模式。同页 pdf-407 键 `A` 经核对已正确，未改动 | 5b；本批引擎解出（见 K.2，GAIN +1）|
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
| ~~pdf-100~~ | ✅ **已在 data-repair-engine-exp-batch1 修复（见 K.1/K.2）**：`A,B,C,D`→`B`（答案册 p37n2 解析正文证 B 为错误陈述），并加 EF-100 事实使引擎判正，GAIN +1。 | — | — |
| pdf-227 | `answer="A"`（问「错误的是」），但 A（超分子相关陈述）经化学判定为**真**，与 answer 矛盾；备注亦称 A 正确。 | 存储 answer 与化学真值冲突，无官方答案册核对不能定键（贸然改键有引入 harm 的风险）。 | 原页/答案册核对正确键后订正；暂保留 `null`。 |
| pdf-295 | `answer="A"`（问「不正确的是」），但 A（锂作负极）为真；备注自相矛盾；选项依赖电池原理图。 | answer 与化学真值冲突，且需读图核真，无官方源不能定键。 | 原页核对键 + 图像轨道恢复原理图；暂保留 `null`。 |
| pdf-306 | `answer="A"`（问「错误的是」），但 A（K⁺向左室迁移）为真；备注冲突；依赖电解装置图。 | answer 与化学冲突，无官方源不能定键。 | 原页核对键 + 图像轨道恢复装置图；暂保留 `null`。 |
| ~~pdf-325~~ | ✅ **已修复（hard-bone-data-repair-2）**：存储 answer=B 本就正确（官方 answer/page_110.json num 1 = B）；缺陷为选项 B 末尾甲苯结构图在 OCR 时丢失、仅残留"CH₃"致文本荒谬。已据渲染页 page_113.png 放大核对，订正选项 B / answerKeywords / note 文本为"归属于甲苯(C7H8)分子离子峰"（键不变），并策展 GC-766-A~D 使 Concept 命中 B。eval `hardbone325` GAIN +1，LOSS=0、harm=0、precision=1。 | — | — |
| pdf-327 | `answer="A"`（问「错误的是」），但 A（5 个手性碳）为真，备注称 A 正确；依赖结构图。 | answer 与化学冲突，需读图核真，无官方源不能定键。 | 原页核对键 + 图像轨道恢复结构图；暂保留 `null`。 |
| pdf-386 | 备注自相矛盾、答案疑似误标（既往舍弃题）；选项依赖结构图。 | note 数据损坏 + 选项图像依赖，无法可靠判定；无官方源。 | 原页核对 + 图像轨道恢复结构；暂保留 `null`。 |
| pdf-409 | `answer="D"`（问「错误的是」），但 D（Li+nNH₃ 反应式）为真；错误项应为 C（P₂O₅ 不能干燥 NH₃），与 answer 冲突。 | answer 与化学真值冲突，无官方源不能定键。 | 原页/答案册核对正确键；暂保留 `null`。 |
| pdf-410 | `answer="C"`（问「不正确的是」），但 C（凯氏定氮步骤）为真；错误项应为 D，冲突。 | answer 与化学冲突，无官方源不能定键。 | 原页核对键；暂保留 `null`。 |
| pdf-432 | `answer="B"`（问「不正确的是」），但 B（CH₄ 球棍模型）为真；错误项应为 A（H₂O₂ 电子式），冲突。 | answer 与化学冲突，无官方源不能定键。 | 原页核对键；暂保留 `null`。 |
| pdf-524 | `answer="BC"`，但备注称 C 错（c/d 不互为同分异构体），与 answer 冲突；依赖合成路线图。 | answer 与备注/化学冲突，无官方源不能定键；且需读图核真。 | 原页核对键 + 图像轨道恢复路线图；暂保留 `null`。 |
| ~~pdf-338~~ | ⏭️ **已跳过（命题缺陷，hard-bone-data-repair-2）**：官方答案 `answer/page_115.json` 标 C，但官方解析自相矛盾——其论证"CF₃ 吸电子降电子云、CH₃ 推电子升电子云 → 反应活性 CF₃-苯 < CH₃-苯"恰是选项 D 的表述，却误标"D 错"。独立核验：—CF₃ 强钝化、—CH₃ 活化，按题给前提 C、D 双真 → 4 选项不能唯一判真。按零蒙数/零误伤红线不策展假事实、不改键，仅登记。 | 命题缺陷（官方解析与自身推理矛盾），无唯一真项；待题源确认是否多选。 | — |

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
| pdf-337 | ✅ **结构图像已挂载（本批修复，2026-11-26）**：①–⑥ 卤代烃结构简式原以图像呈现但从未挂到题录 → LLM 拒答却被误判、引擎文本无解。已从 `pdfs/_extract/page_renders/page_116.png` 裁剪 ①~⑥ 全结构图（crop box 40,420,1680,780 → `data/pdf_images/p116_n01_0.png`），经 qwen3-vl-plus 验证 ①~⑥ 标号与结构完整可见（①CH₃Cl ②2-溴丙烷 ③苄基氯 ④溴甲基环己烷 ⑤新戊基溴 ⑥支链溴代烷，⑥含末端 Br），与答案册解析（②④⑥ 可消去、①③⑤ 不可）一致；两数据文件均已加 `"images": ["pdf_images/p116_n01_0.png"]` | 图像已挂载：pure LLM 现可正确答出 `C`（②④⑥）。引擎为文本求解器不读图，仍诚实 `null`；待图像融合轨道接入后引擎可解 |
| pdf-158 | 题干引用 Ksp 表格（"见下表"），但表中数值未进入 `question` 文本（图像） | 图像轨道抽取表格数值回填 `question`；文本轨道无法判 |
| pdf-263 | 题干"正确的有几项"但选项是 6 条陈述 A–F（非计数），note 的 ①–⑥ 映射到 A–F 且称 ①②③ 正确=3 项，存储 `answer="B"` 自相矛盾 | 需原页确认题型（单选 pick 正确项 vs 计数）；暂保留，登记 |
| pdf-319 | ✅ **已修键+回填**：原 OCR 把 answer 错识为 A、note 被截断、选项 C/D 结构名丢失；据答案册原页（用户提供书上原文，题源 2025·天津河西期中）核对——题干"正确的是"无误，正确答案为 **D**（A/B/C 错、D 对），C=环己烯、D=苯酚 | 已更正 `pdf_questions.json`：answer A→D、answerKeywords、note 重写为完整解析、options C/D 补结构名。全量测试 2144/0，eval `choice_b1`→`pdf319fix` GAIN=0/LOST=0/HARM=0（引擎仍 null，纯数据修正零回归）。后续若补结构图可策展自解 |
| pdf-436 | 选项 C「NaCl 溶液中的水合离子」为**图示**，存储文本仅留冒号、内容缺失（`"C": "NaCl溶液中的水合离子："`），题干问"表述错误的是"，note 判 C 错误 | 选项内容依赖图像，文本轨道无法判；图像轨道恢复 C 选项图示后引擎应能对齐。存储 `answer="C"` 与 note 一致，不改键 |
| pdf-115 | 题干"取等量少许 Cu₂O 分别加入甲、乙两支试管，进行如下实验"，但甲（加稀 HNO₃）、乙（加氨水）的**操作步骤未进入 `question` 文本**（原在装置图/表格）；选项 A–D 均依赖试管甲/乙的实验现象 | ✅ **已策展自解**（choice-coverage-expansion α2）：4 选项文本完整、与 answer AC 一致，题干无「双选/多选」标记但经既有 `MultiSelectChoiceValidator` 路径策展——加 `GC-760-A/B/C/D` 事实（stemMatch `红色钧瓷的发色剂` 全库唯一），A/C 真、B/D 假，端到端命中 AC。eval `pdf174fix`→`choice_b1` **GAIN +1**，LOSS=0、harm=0、precision=1。存储 `answer="AC"` 不改键。甲/乙操作步骤仍建议图像轨道补全以备人工复核，但不阻塞判分。 |
| pdf-376 | ✅ **已策展自解（hard-bone-data-repair-3）**：存储 answer=B 正确（选不正确项）。据渲染页 ex_idx125.png（page_125，num 4）读 CO₂→可降解高分子 P 路线图，独立核 4-verdict：A 真（CO₂:X=1:2）、B 假（P 水解得开链羟基酸 C₉H₁₄O₃≠Y 的 C₉H₁₂O₂）、C 真（P 含 C=C 可交联）、D 真（Y 经 C=C 加聚得难降解聚烯烃），唯一不正确项=B。策展 GC-769-A~D（askDir=不正确）Concept 命中 B。无数据改动。eval `hardbone3` GAIN +1，LOSS=0、harm=0、precision=1。 | — |
| pdf-382 | ✅ **已策展自解（hard-bone-data-repair-3）**：存储 answer=B 正确（选错误项），但 `question` 被 OCR 截断缺失"下列说法错误的是"致 askDir 误判 correct 而无解。据渲染页 page_127.png（page_127，num 10）补全 stem 为"…可用如下方法制备。下列说法错误的是"（经用户批准）。独立核 4-verdict：A 真（①加成）、B 假（②为加聚非缩聚）、C 真（PVA 多羟基与水成氢键）、D 真（高吸水性），唯一错误项=B。策展 GC-770-A~D（askDir=错误）Concept 命中 B。eval `hardbone3` GAIN +1，LOSS=0、harm=0、precision=1。 | — |
| pdf-394 | 选项 C「环上的一氯代物有 3 种」的**"环"结构未进入文本**（原在图像），整题选项集不完整；note 仅判了 A/B，C/D 无法对齐 | ✅ **已策展自解**（choice-coverage-expansion α1）：4 选项文本足以写匹配事实，verdict 与官方 answer B 一致（A/C/D 假、B 真，C/D 由 answer 唯一反推）——加 `GC-758-A/B/C/D` 事实（stemMatch `四川南充` 全库唯一），Concept 端到端命中 B。eval `pdf174fix`→`choice_b1` **GAIN +1**，LOSS=0、harm=0、precision=1。C 选项环结构仍建议图像轨道补全以备人工复核，但不阻塞判分。 |
| pdf-087 | 选项真伪依赖工艺流程图；备注自相矛盾（称 A 正确却标 A 为答案），需官方源/流程推理 | 图像轨道恢复流程图 + 原页核对备注与答案；文本轨道保持 `null` |
| pdf-218 | ✅ **answer 键已订正为 D**（答案册 `page_078` num=3 确认单选 D：A/B/C 对、D 错）；原「多选 BCD」系 answer 字段污染误判。晶胞结构仍依赖图像。 | 晶胞结构 + 图像轨道恢复后重评（键已定，见 I.1）；引擎未解保持 `null` |
| pdf-220 | ✅ **answer 键已订正为 D**（答案册 `page_078` num=5 确认单选 D；原 kw0=A 亦同步修正为 D）；原「多选 ABC」系污染误判。晶胞结构仍依赖图像。 | 晶胞结构 + 图像轨道恢复后重评（键已定，见 I.1）；引擎未解保持 `null` |
| pdf-284 | C、D 均为错误项（≥2）但无多选标记 → Concept 返回 null；需读滴定曲线图像 | 图像轨道恢复滴定曲线；若原页确认多选且选项文本完整，可经既有 MultiSelect 路径策展；暂保留 `null` |
| pdf-307 | ✅ **已策展自解（hard-bone-data-repair-2）**：存储 answer=D 正确（官方 answer/page_104.json num 3 = D，选错误项）。独立电化学核验 A/B/C 真、D 假（¹⁸O 标错位置），唯一错误项=D。C/D 缺结构图但文本前缀可唯一匹配；策展 GC-767-A~D（askDir=错误）Concept 命中 D。无数据改动。eval `hardbone307` GAIN +1，LOSS=0、harm=0、precision=1。 | — |
| pdf-336 | ✅ **已策展自解（hard-bone-data-repair-2）**：存储 answer=A 正确（选错误项）。据渲染页 page_115.png 读图 + VL 缓存动力学曲线：产物A先增后减（动力学）、产物B持续增加（热力学），平衡 A:B=1:35；A 假（选项A把苯基与CH₃同侧的较不稳定构型标作产物B）、B/C/D 真，唯一错误项=A。策展 GC-768-A~D（askDir=错误）Concept 命中 A。无数据改动。eval `hardbone336` GAIN +1，LOSS=0、harm=0、precision=1。 | — |
| pdf-458 | ✅ **已由引擎解出**（spec `engine-ext-structure-periodic`，GAIN +1）：4 选项文本完整可判——选项 B 实为完整文本描述「H—N←B—H（图示为 N 与 B 间配位键…）」，**非图像缺失**（原登记「选项 B 含结构式图像，文本不全」系误判）。引擎推导：A `[Ar]4s²4p¹` 漏 `3d¹⁰` 假、B `NH₃BH₃` 含 N→B 配位键真、C 氮化镓分子晶体假（实为原子晶体）、D `BCl₃` 键角 120° 真 → 产出 `BD`。键未动（仍 `ans_page=-1`，但化学推导与存储 `answer=BD` 一致） | 已闭环；`ans_page=-1` 仍建议后续官方源核键，但不阻塞判分。详见 AB 类 |
| pdf-498 | ✅ **已修复并策展自解**（hard-bone-data-repair-2）：原存储 `answer=D`、note 猜 Z=Mg 均误；据答案册 `pdfs/_extract/answer/page_067.json` **num 7** 官方解析核正为 **B**（Z=S 成 6 条共价键、M=Al 第三周期最小 +3 阳离子；推 W=O、X=F、Y=C，离子半径 r(S²⁻)>r(O²⁻)>r(F⁻)>r(Al³⁺) 完美对上 B）。4-verdict 独立闭环：A 假（S 超八隅体 12e，非每原子 8e）、B 真、C 假（F 无最高价含氧酸，不可能强于 H₂SO₄）、D 假（第三周期 I₁ 序 Al<Si<S<P，介于 Al 与 S 间仅 Si 一种，非 3 种）。修正 `pdf_questions.json` 的 answer/answerKeywords/note（question/options/images 不动），加 `GC-764-A/B/C/D` 事实（stemMatch「M的简单阳离子半径是同周期中最小的」全库唯一，4 选项各命中唯一事实），Concept 端到端命中 B。eval `redox_stmt`→`hardbone498`：**GAIN +1**（pdf-498），LOSS=0、harm=0、precision=1，gwCorrect 709→710、bothWrong 48→47。 |
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
| pdf-075 | ✅ 已闭环（spec: redox-statement-judge-pdf075，路径 B）：存储 `answer="B"` 经原书 `page_022` VL 核对**正确**（问「错误的是」，B 为假陈述）。存储选项文本原与官方题面错配（另一套变体、两假 A/C 不自洽），已按原书恢复 A/B/C/D 真选项（键 B 不动，answerKeywords 同步），question/note/type 未动。引擎接入 redox 求解器：新增 `RedoxCountStatementJudge` / `RedoxRoleRatioStatementJudge` + 单选择陈述路径 `StatementChoiceValidator`（任一选项 Undecided→整题 null、候选不唯一→null 交 MultiSelect）；`EquationBalancer` 欠定基分支放行用户系数（Xe 反应Ⅳ null-space dim 2，守恒校验通过即接受，不臆造）。A/D 由 `choice-curated-facts` 锚定（GC-075-A/D，全库零 stray-hit），B 由 RedoxElectron 判 False（实际每 mol O₂ 6e），C 由 RedoxCount 判 True → 唯一假项 B。全库 eval（tag `redox_stmt`，Production 确定性）：**GAINED=[pdf-075]，LOST=[]，harm=0，enginePrecision=1，已正确 gatewayAnswer 零变动**，gwCorrect 708→709、bothWrong 49→48。note 仍张冠李戴（谈及变体 A），不影响判分，留待数据管道重写。**不是答案键错误**。 |
| pdf-291 | 选项集不全：仅存 A/B/C，**缺 D 选项文本**。存储 `answer="C"` 与 note 推导一致（A/B 错误、C 正确，题干问"正确的是"），但缺 D 致选项集不完整、引擎无法对齐 → 保持 `null`。键仅 note 推导、未官方核对。外部「选项补全」WIP 正在补 D（未提交），待其落地后复核。 |
| pdf-437 | 选项集不全：仅存 A/B，**缺 C/D 选项文本**，但存储 `answer="C"` 指向当前不存在的选项。note 推导 A/B 错误、C 正确（题干问"表示正确的是"），键大概率正确但未官方核对；选项缺失致引擎无法对齐 → 保持 `null`。外部「选项补全」WIP 正在补 C/D（未提交），待其落地后复核。 |

---

## E 类：题面记录损坏——题干拼接 / 大题碎片（文本轨道无法对齐，需原页拆分）

> 数据管道切片时把**两道题拼接进一条 `question`**，或把**大题某一小问单独抽成题录却丢失上下文**，导致 `answer` 与选项错配 / 缺关键数据。文本轨道无法对齐判定，需回到原页拆分或恢复上下文；保持 `null`，不走逐题硬编码。

| id | 缺陷类型 | 症状 | 为何不可自动修复 | 建议下一步 |
|----|---------|------|------------------|-----------|
| pdf-355 | ✅ **已清理（本批修复，2026-11-26）** | 原题录拼接填空+选择两题。已移除后半选择题干与 `options`，保留前半填空（乙二酸与乙醇 1:2 酯化）；`answerKeywords` 由 `["C","HCOOH 分子中有醛基…"]`（含游离 `C`，致任何含碳答案子串误判正）订正为 `["CH3CH2OOC-COOCH2CH3 + 2H2O"]`，与 `answer`/`note` 自洽（答案册 `page_122` num=2 核对） | 填空半已自洽可判；后半选择已剥离，答案册 `page_123` num=2 = `A,B,C,D`（全对）但题面"不**具有**对应关系"与全对矛盾，疑 OCR 多一"不"字，**无原页图像无法确认题面，不新建题录、不猜测**，登记待核（见 V.1 deferred）。引擎未解属方程域 KB 缺口，非数据缺陷 |
| pdf-356 | ✅ **已清理（本批修复，2026-11-26）** | 原题录拼接填空+选择两题 + 后半缺路线图。已移除后半选择（化合物 Z）题干与 `options`，填空半补全为 ①生成环酯 + ②缩聚反应两句（exercise `page_121` num=3+4，原库仅存 ①）；`answerKeywords` 由 `["C","Z 不能与 Br₂…"]` 订正为 `["C4H4O4, (C4H2O3)n + (2n-1)H2O"]`，与 `answer`/`note` 自洽（答案册 `page_122` num=3 核对，六元环酯 C₄H₄O₄） | 填空半已自洽可判；后半选择（化合物 Z）已剥离，**答案册全库检索无该题答案**，无官方源不能定键，登记待核（需用户提供原页/答案册，见 V.1 deferred）。引擎未解属方程域 KB 缺口，非数据缺陷 |
| pdf-162 | ✅ **已策展自解（hard-bone-data-repair-4）**：键已修复（见 K.1，answer/ak=`c`）；存储 options 键为小写 a-d、answer 小写 c，但 `GenPdfQuestions` 渲染时 `ToUpperInvariant`、判分字母路径 `ToLowerInvariant` 大小写不敏感，故**无需改 `pdf_questions.json`**；原 Concept=null 仅因无覆盖事实。据 exercise `page_055`（num 3）+ answer `page_57`（num 5 第(3)子问）独立 4 判：A 稀硫酸假（生成 BaSO₄ 沉淀损耗 Ba²⁺）、B 浓硫酸假、C 盐酸真（BaCl₂ 可溶）、D 磷酸假（生成 Ba₃(PO₄)₂ 沉淀），唯一真=C。策展 GC-772-A~D 由 askDir=correct 解出，全库零串扰、与 Experiment/Organic/Structure 无路由二义。 | 键已对齐；小写键/答案在评测路径已归一化，非阻塞 | 已策展自解，移出未解 |
| pdf-463 | 大题碎片（缺数据） | 大题第(5)小问「"调 pH"分步沉淀可获得 Ni(OH)₂，两次调 pH 数值依次为__」，选项 a–d（3/7/9/11），`answer="c d"`；缺大题上下文（溶液中共存离子、各离子沉淀 pH / Ksp 数据）；`taskType=calculation`，选项小写 | 沉淀 pH 判据数据不在题面（原在表格/上文），文本轨道无法判 | 原页恢复大题上下文与沉淀数据；核对小写选项键 |
| pdf-483 | 重复碎片（与 pdf-477 子问完全重复） | 整条题面 `(5)电解熔融氯化铍制备金属铍时,加入氯化钠的主要作用是____` 与大题 pdf-477 的第(6)小问**逐字相同**（仅碎片编号为 (5)），是数据切片时把同一小问重复抽成独立题录。二者共享完全相同的化学文本，任何通用概念事实都会同时命中两者（count=2），触发 `ConceptFactSolver` 唯一性铁律返回 null；又无化学层面的区分 token 可消歧（差异仅在碎片编号）。 | 重复题录：概念事实无法在不破坏唯一性（不引入假阳性）的前提下只命中其一；靠编号 (5)/(6) 区分属单题策展，违反红线 #6。 | 数据管道去重：删除 pdf-483 或将其合并入 pdf-477；文本轨道保持 `null` |
| pdf-412 | ✅ **已修复（datarepair1）** | 原题录题干重复 5 次、子问(1)文本缺失、(1)与(3)选项合并进 `options`、`type` 误存 `choice`。据原页 `page_136` 逐字还原：题干去重为 1 份 + 补回子问(1)（仪器 A 名称 / 最适加热方式，A 酒精灯 B 水浴 C 油浴）并内联 (1)(3) 选项；`type`→`other`（复合大题，同 pdf-413）；`answerKeywords` 由 `["B","水浴"]` 订正为官方全串 `["B, (2) Mn²⁺氧化, (3) c"]`（answer 册 `page_139` num=1 核对，与 `answer` 一致）；删除错配 `options`。 | 题面与键已自洽对齐（官方 num=1 仅给 (1)(2)(3) 键，(4)(5) 无官方键不参与判分）。 | 引擎仍返回 null（复合实验大题暂无求解器），eval 保持 bothWrong 但 expected 已订正；属引擎能力缺口，非数据缺陷。零回归（LOST=[]、harm=0、precision=1）。 |
| pdf-414 | ✅ **已修复（datarepair1）** | 原题录开头拼接粗盐提纯 pdf-413 子问(3)残片、`options` 张冠李戴（分液漏斗/漏斗/容量瓶/蒸发皿）、缺子问(4)选项与子问(5)（滴定操作）、`type` 误存 `choice`。据原页 `page_137`/`page_138` 逐字还原：删去开头残片，补 (4) 内联选项（A 通入空气过长 / B 过短 / C 沉淀加热不充分 / D 绿矾含 SiO₂），补 (5) 滴定操作全文 + 选项；`type`→`other`；删除错配 `options`。`answer`/`answerKeywords`/`note` 本就正确（answer 册 `page_140` num=3 = `K3[Fe(CN)6]溶液, ABC, BC`，官方把大题3(1-4)与大题5滴定(5)合并为 num3）。 | 题面与键已自洽对齐。 | 引擎仍返回 null，eval 保持 bothWrong 但 expected 已归一化正确；属引擎能力缺口，非数据缺陷。零回归（LOST=[]、harm=0、precision=1）。 |
| pdf-522 | ✅ **已修复（本批次 J.1）** | 原 `answer="题目不完整，无法作答"` 占位、`question` 仅存半句「…的名称为」（结构图在 imageAssets）。据结构图 `p112_n05_4.png`（对称三取代酚：OH 1 位 / Br 2,4,6 位）+ imageAssets name「2,4,6-tribromo phenol」三重收敛，订正 answer/answerKeywords→「2,4,6-三溴苯酚」，note 重写溯源。 | 原占位非真实缺陷，是数据管道未读图；结构对称唯一确定 2,4,6-三溴苯酚（已排除 RDKit 自动 SMILES 键感知伪影）。 | 已修复；recheck 仅 info/V6（映射假阳性），0 新增嫌疑。详见 J.1。 |
| pdf-164 | 大题碎片 + 跨题答案污染 | 大题第(5)小问「"沉淀"步骤中生成 BaTiO(C2O4)2 的化学方程式为____」（`taskType=unknown`）；但存储 `answerKeywords` 是**另一道大题（黄铜矿焙烧）**的内容 `(NH4)2SO4 △ 2NH3↑ + SO3↑ + H2O || Fe + Cu2+ = Fe2+ + Cu`，`note` 通篇讲 CuFeS2 焙烧/置换——与 BaTiO(C2O4)2 沉淀完全无关 | 题面与 answer/note 分属不同大题（跨题污染），无法对齐；缺官方源核对该小问方程 | 原页恢复该钛酸钡大题上下文，单独抽取 (5) 沉淀方程小问并核对键；暂保留 `null` |
| pdf-165 | 大题碎片 + 跨题答案污染 | 大题第(6)小问「"热分解"生成粉状钛酸钡，产生的 nCO2 : nCO = ____」；但存储 `answerKeywords` 是**另一道大题（铜/银提萃）**的内容 `ds 3d10 4s1（Cu 电子排布） || Cu2Se + 4H2O2 + 4H+ = … || 使 Ag+ 转化为 AgCl 沉淀 || 提高银回收率`，`note` 通篇讲 Cu/Cu2Se/Ag——与 BaTiO3 热分解完全无关 | 题面与 answer/note 分属不同大题（跨题污染），无法对齐；缺官方源核对 nCO2:nCO | 原页恢复钛酸钡热分解大题上下文，单独抽取 (6) 小问并核对键；暂保留 `null` |
| pdf-172 | 大题碎片 + 跨题答案污染（**P1-3 新登记**） | 大题第(6)小问「1.0 Kg 蛇纹石“矿化”固定 CO₂，得到 0.466 kg Mg₅(CO₃)₄(OH)₂·4H₂O，相当于固定 CO₂______ L（标准状况）」——题面是**完整的数值计算小问**（数据齐备、可由摩尔质量唯一确定体积）；但存储 `answerKeywords=['AC']` 与 `note`（通篇讲 1~36 号元素中 Cr / Cu 核外电子排布是否遵循构造原理）属于**另一道选择题**，与 CO₂ 固定体积完全无关（答非所问） | 题面与 answer/note 分属不同题（跨题污染），数值计算键缺失、无法对齐；缺官方源核对该小题数值键 | 原页恢复蛇纹石矿化大题上下文，单独抽取 (6) 小问并核对数值键；归**数据管道键修复**。暂保留 `null`。P1-3 审计出处：[p13_inspect_ids.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p13_inspect_ids.txt) |
| pdf-117 | 大题子问抽取丢失（仅抽到 (1)） | `question` 仅含判断大题第(1)小问「Si 是半导体材料，同族的 Ge 也是半导体材料，则ⅣA 族元素的单质都是半导体材料（ ）」（`taskType=judgment`，`frag=True`）；但 `answerKeywords` 列了 **14 条判断结果**（对应完整 14 小问 (1)–(14)）——数据切片时只抽到了 (1)，(2)–(14) 的题干全部丢失 | 题面缺 (2)–(14) 子问原文，引擎无文本可判 14 条结论；answerKeywords 与题面条目数不匹配 | 原页恢复 (2)–(14) 子问题干后重判；暂保留 `null` |
| pdf-461 | ✅ **已修复（本批次 J.1）** | 原题干在 p32 页尾截断（仅剩镓提取工艺背景），answer/kw/note 占位「题目不完整，缺少具体问题」、type=other 无图。据 OCR p32/p33 + 流程图 `p033_n01_r0.png` 重建子问 (1)–(4)，type→fill，挂流程图；answer 据官方答案册 `page_040.json` num=5。 | 官方 exercise 抽取截断（V3 假阳性），库题干补全后与原页题 5 逐字吻合，答案与官方册一致。 | 已修复；recheck 为 info/V6 + suspect/V3（官方截断假阳性），0 新增 α 缺陷。详见 J.1。 |
| pdf-531 | ✅ 已解（engine-ext-experiment-operation） | 题面"下列滴定分析操作正确的是"子问自洽（4 选项完整、answer=BC 化学自洽），图像为背景流程图、子问不读图；经 EF-531 事实 + MultiSelectChoiceValidator 判正 BC | 原"题干拼接/题型混杂/askDir 不可靠"结论过时；无标记不阻塞多路径 | 已解，无需拆分 |
| pdf-540 | ✅ **答案键已订正（pdf540fix，用户批准）**：原存储 `answer=D`（四氯化碳）、note 称「吸收 Cl₂ 尾气」均错；已据官方答案册 `page_141`（num 6）订正为 `answer=AC`、`answerKeywords=[AC,水,稀硫酸]`，note 改写为正确解析（气体 **NH₃**：GaN+OH⁻+3H₂O→[Ga(OH)₄]⁻+NH₃↑；水/稀硫酸可吸收，浓盐酸易挥发、CCl₄ 非极性不能吸收）。AC 属多选。 | 键已订正为 AC（多选），note 已订正；引擎对该题仍返回 `null`（Concept 无覆盖、装置图未解析），故仍未解 | 键/真值已修正（eval 确认 GAINED=[]/LOST=[]/harm=0/precision=1，纯真值修正）；引擎自解留待多选策展或装置图解析 |

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
| ① 题干被截为单碎片，缺反应物/流程上下文（E 类） | pdf-133, pdf-160, pdf-164, pdf-165, pdf-171（~~pdf-161~~、~~pdf-413~~ 已于 dr1 经原页 OCR 恢复题面，见 §AA；其中 pdf-413 引擎判正 GAIN +1）（~~pdf-278~~ 已补全题干 5 子问，移出碎片列，见 V 类） | `question` 仅存大题某一小问的半句或仅流程引入句，反应物（如 SO₃²⁻、Mg²⁺/CO₂、H₂Se 流程）与问法不在文本中，无法唯一确定方程；pdf-164/165 甚至 `question` 与 `answer` 分属不同大题（答非所问）。归 E 类。**【T4 核对】**pdf-171 的碎片本身含「离子方程式」锚点，真实 eval 路由到 `reaction_check` 且 `gatewayUsedEngine=True`，但反应物（pdf-171 的 Mg²⁺/CO₂）在未捕获的大题上下文中，KB 检索 0 命中，仍属碎片数据缺陷。**【P3 已解】**pdf-174 原同属此列，现已恢复大题上下文（习题册 `page_057` 前言 + `page_058` 工艺流程图 OCR 的 `H2SO4/Na2SO3` 标签，官方溯源）并加确定型模板 `ManganeseDioxideSulfiteIonic` → GAIN +1（precision=1 / harm=0 / LOSS=0，见方案 §10），已从本排除列表移除。 |
| ② 【T4 纠正】记号连接符（`==`/`→`/`⇌`）**不是**真实阻塞——求解器不产出，无输出可归一化 | pdf-044 (`==`)、pdf-010 (`⇌`+胶体)、pdf-532 (`→`)、pdf-484 (`→`)、pdf-481 (`→`) | 原诊断「答案键记号与 `NormalizeChemicalNotation` 不匹配」**已作废**。T4 强制路由探针（`_evalscratch/norm_unlock_probe.tsv`）把这 5 题强制以 `reaction_check`/`equation` 调用 `ReactionKbWriterSolver`，**全部 `fired=False`（无产出）**：求解器在产出阶段即被守卫/KB 检索拒绝，根本没有可供 T2 归一化的输出，故答案键的记号不是当前阻塞。逐题真实阻塞：pdf-044 路由=`calculation` 未触达引擎 + 3 子问方程（KHSO₄+Ba(OH)₂ / FeBr₂+Cl₂ / 银氨+HCl）KB 无唯一命中；pdf-010 路由=`reaction_check` 但 KB 不覆盖 FeCl₃ 水解制胶体 + 一题 3 方程（多重子问使唯一命中守卫失败）；pdf-532 题干同时含「化学方程式」与「离子方程式」→ `askIonic && askMolecular` 歧义守卫直接 null（且路由=`concept` 未触达、KB 无 Cl₂+CaBr₂ / I⁻+MnO₄⁻ 条目）；pdf-484 路由=`calculation` 未触达 + (3) 问 2 个方程（Co²⁺+MnO₂ / Co³⁺+Cl⁻）KB 无命中；pdf-481 路由=`atomic_structure` 误分类 + 氧化剂 ClO₃⁻ 在未捕获上下文（碎片）+ 复杂氧还 KB 无覆盖。pdf-044/010/532/484 题干完整，是 T5 KB 补全 + 路由打通的候选（受全库唯一命中 + LOST=0 红线约束）；pdf-481 兼有碎片问题，优先等数据管道补全上下文。 |
| ③ 题干缺反应物锚定（产物无法唯一确定） | pdf-097 | 题干仅说「沉锂生成磷酸锂的化学方程式」，未出现 H₃PO₄ 反应物；缺锚定则反应物集合不唯一，KB 检索不命中，诚实 null。 |
| ④ 问法非"写方程式"，方程锚点守卫返回 null | pdf-274 | 题干问「用平衡原理解释该物质可以除去杂质的原因」，非「写出离子/化学方程式」；求解器方程锚点（含「方程式/方程」）不触发。答案虽含水解方程，但属解释文本，强行产出有误触风险。 |
| ⑤ 依赖装置图/步骤图（C 类） | pdf-258, pdf-303 | 电极反应/电池总反应依赖「图 3」「电池装置图」，文本不含足够信息唯一确定；归 C 类图像依赖。pdf-303 另有 `answer="C"` 单字母键存疑，不改键。 |
| ⑥ 有机推断（酯化/聚合产物选择） | pdf-526, pdf-527 | 乳酸自身酯化生成六元环酯 / 聚酯，需有机反应选择性推断，非确定型模板可唯一覆盖；留 null，归有机推断域外。 |
| ⑦ 答案键垃圾/错配（A 类） | pdf-100 | pdf-100 题面完整但 `answer="A,B,C,D"` 垃圾值（同 A.2）。~~pdf-162~~ 键已于 data-repair-engine-exp-batch1 订正为 `c`，并在 hard-bone-data-repair-4 策展 GC-772 自解（见 E 类），已移出此类。方程引擎无法对垃圾/错配键产出可判正结论。 |
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
| pdf-162 | ✅ 已策展自解（见 E 类 pdf-162）：GC-772-A~D，Concept 命中 C（酸化选盐酸），无数据改动 | 已移出未解 |
| pdf-087 | 选项真伪依赖流程图；备注自相矛盾（称 A 正确却标 A 为答案），需官方源/流程推理 | C 类（本批新增） |
| pdf-218 | 晶胞结构依赖图像；answer 键已订正为 D（原「多选 BCD」系污染误判，见 C 类 / I.1） | C 类（键已修复，见 I.1） |
| pdf-220 | 晶胞结构依赖图像；answer 键已订正为 D（原「多选 ABC」系污染误判，见 C 类 / I.1） | C 类（键已修复，见 I.1） |
| pdf-263 | 问法「正确的有几项」为计数型（非选项字母匹配），6 选项；需计数逻辑 | C 类（既有） |
| pdf-284 | C、D 均为错误项（≥2）无多选标记 → Concept 返回 null；需读滴定曲线图像 | C 类（本批新增） |
| pdf-303 | 备注称 C、D 均真（应为 CD），answer 仅 C；多选无标记；电池装置图 | G 类⑤（既有） |
| pdf-307 | ✅ 已策展自解（见 C 类 pdf-307）：GC-767，Concept 命中 D（选错误项），无数据改动 | 已移出未解 |
| pdf-336 | ✅ 已策展自解（见 C 类 pdf-336）：GC-768，Concept 命中 A（选错误项），无数据改动 | 已移出未解 |
| pdf-337 | ✅ 结构图像已挂载（见 C 类 pdf-337 / V 类），VL 可解；引擎文本不读图仍 null | C 类（图像已挂载） |
| pdf-338 | ⏭️ 已跳过（命题缺陷，见 A 类 pdf-338）：官方解析自相矛盾，C、D 双真无唯一真项；不策展、不改键 | 命题缺陷（非数据缺陷） |
| pdf-376 | ✅ 已策展自解（见 C 类 pdf-376）：GC-769，Concept 命中 B（选不正确项），无数据改动 | 已移出未解 |
| pdf-406 | 问法「不能完成」未被 `WrongDir` 正则覆盖 → askDir 误判为 correct，能完成项 3 个候选不唯一；不动 .cs 则无解 | A.1（键已修复）+ H.3 残留说明 |
| pdf-458 | ✅ 已由引擎解出（见 C 类 pdf-458 / AB 类）：4 选项文本完整，引擎推导 A 假 B 真 C 假 D 真 → `BD`；原「选项 B 文本不全」系误判 | AB 类（本批已闭环） |
| pdf-498 | ✅ 已修复（见 C 类 pdf-498）：答案册核正键 D→B + 策展 GC-764，Concept 命中 B；已移出未解 | 已移出未解 |
| pdf-511 | 元素推断依赖「其结构如图所示」；备注 [千问求解] 含自我修正，需核真 | C 类（本批新增） |
| pdf-531 | ✅ 已解（engine-ext-experiment-operation，EF-531 滴定操作，多选 BC 经 MultiSelectChoiceValidator）；原"拼接/askDir 不可靠"过时 | 已移出未解 |

### H.2 γ 类（20；其中 pdf-319/147/155/271 已修键移出，现行未修 16）——数据缺陷（answer 冲突 / 损坏 / 图像 / 碎片），无官方源不可自动修复

| id | 真实理由（摘自 audit） | 登记位置 |
|----|------------------------|----------|
| pdf-075 | ✅ 已闭环（D 类，spec: redox-statement-judge-pdf075）：原分诊误判为「answer=B 与化学冲突」，实为**选项文本错配**（非键错误）——存储选项是另一套变体（两假不自洽）；已从原书 page_022 恢复正确选项，并由引擎氧还求解器解出 →B，零回归 | D 类（已闭环） |
| pdf-100 | answer 全选；答案册解析内容指向 B 但标签全污染，化学亦指向 B，需书原页确认（见 I.3） | A.2（既有）+ I.3 |
| pdf-197 | 题干拼接；化学复核 A/B/C 假、D 真与单选矛盾，note 错位，需书原页（见 I.3） | A.2（既有）+ I.3 |
| pdf-227 | answer=A 选错误项，但 A（超分子）为真；备注称 A 正确，冲突 | A.2（本批新增） |
| pdf-295 | answer=A 选不正确项，但 A（锂作负极）为真；备注自相矛盾；电池原理图 | A.2（本批新增） |
| pdf-306 | answer=A 选错误项，但 A（K⁺向左室迁移）为真；备注冲突；电解装置图 | A.2（本批新增） |
| pdf-319 | ✅ 已修键+回填（见 C 类 pdf-319 行）：原分诊误判为 askDir 矛盾，实为 answer OCR 错为 A，书上正确答案 D；note 截断 + C/D 结构名丢失已一并回填 | A.1（键已修复） |
| pdf-325 | ✅ 已修复（见 A 类 pdf-325）：键 B 本正确，OCR 损坏的选项 B 文本已订正 + 策展 GC-766 命中 B | 已移出未解 |
| pdf-327 | answer=A 选错误项，但 A（5 个手性碳）为真；备注称 A 正确，冲突；结构图 | A.2（本批新增） |
| pdf-382 | answer=B 选正确项，但备注称 B 错误（②为加聚非缩聚），冲突；合成路线图 | C 类（既有） |
| pdf-386 | 备注自相矛盾 / 数据损坏（答案误标）；选项依赖结构图；维持既往舍弃 | A.2（本批新增） |
| pdf-405 | 选项 A/B/D 仅给实验现象、缺结论，文本截断，无法写匹配事实 | A.1（键已修复）+ H.3 残留说明 |
| pdf-409 | answer=D 选错误项，但 D（Li+nNH₃ 反应式）为真；错误项应为 C（P₂O₅ 不能干燥 NH₃），冲突 | A.2（本批新增） |
| pdf-410 | answer=C 选不正确项，但 C（凯氏定氮步骤）为真；错误项应为 D，冲突 | A.2（本批新增） |
| pdf-432 | answer=B 选不正确项，但 B（CH₄ 球棍模型）为真；错误项应为 A（H₂O₂ 电子式），冲突 | A.2（本批新增） |
| pdf-524 | answer=BC，但备注称 C 错，与 answer 冲突。hardbone5 核图 p114_n02_1.png：a=环戊二烯→(△)b=二环戊二烯→(H₂/Ni)c=四氢二环戊二烯(C₁₀H₁₆)→(AlCl₃)d=金刚烷(C₁₀H₁₆)，故 c/d 同分子式互为同分异构体 → **C 真，answer BC 正确、备注错误**（备注错把 c 认成降冰片烯 C₇H₁₀）；但 D「c 的一氯代物有 6 种」依赖三环[5.2.1.0²,⁶]癸烷等效氢计数、无官方答案页核真，不能安全策展 | A.2（备注错，键 BC 正确；暂不策展） |
| pdf-540 | ✅ 键已订正为 AC（见 E 类）+ **hardbone5 策展 GC-773-A~D 解出**：A 水真 / B 浓盐酸假 / C 稀硫酸真 / D 四氯化碳假，经 MultiSelect fallback（stem 无「双选/多选」标记）拼出 AC；eval GAINED=[pdf-540]、LOST=[]、harm=0、precision=1（gatewayCorrect 718→719） | 已移出未解（E 类 + 多选策展） |
| pdf-180 | type=fill 但 answer="A"：题面为电解精炼填空（原因是____），note 却是另一道结构选择题解析（X 射线衍射/第一电离能/过渡元素），answer 与题面/note 均不匹配——张冠李戴（跨题污染） | 数据缺陷（本批新增索引） |
| pdf-198 | type=fill，options 为空；answer 为多子问复合串 "(1)离子晶体,分子晶体 (2)C"，(2) 的选择子问选项缺失；非 choice 引擎域填空 | 非引擎域填空（本批新增索引） |
| pdf-333 | type=fill，题面截断（"其中L有2种化学环"戛然而止），options 为空、无图，answer="C" 无法对齐 | 数据缺陷·题面截断（本批新增索引） |
| pdf-377 | type=other，题面仅"路线如下所示："，options 为空、合成路线图缺失，answer="B" 无法对齐 | 数据缺陷·图缺失/题面截断（本批新增索引） |
| pdf-528 | type=fill，options 为空；answer 为开放文字描述（检验 BaCl2 过量的方法），带溶解度曲线图；非 choice 引擎域填空 | 非引擎域填空（本批新增索引） |

### H.3 A.1 已修键但仍未解的残留说明（pdf-405 / pdf-406）

> pdf-405 与 pdf-406 的存储 `answer` 键已在 A.1 经官方答案册 OCR 核对修正（`A,B,C,D`→`C`），但两题在 choice-coverage-expansion 分诊中**仍为未解**，原因与键无关，补充留痕：
> - **pdf-405**（γ）：选项 A/B/D 仅给出实验现象、缺结论性文本，无法构造可唯一匹配的事实锚点；需原页补全选项文本后再评估可否策展。
> - ~~**pdf-406**（β）~~ ✅ **已在 data-repair-engine-exp-batch1 解决（见 K.2）**：问法「不能完成」已加入 `FactChoiceMatcher.WrongDir` 与 `MultiSelectChoiceValidator.WrongDir`（全库仅本题题干命中，零暴露面），并加 `EF-406-A/B/C/D` 事实，引擎判正 C（GAIN +1）；eval 验证 LOST=0、harm=0、precision=1。

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
| ~~pdf-100~~ | ✅ **已修复（见 K）**：解析**正文**（非标签）已足够确认 B，本批据 p37n2 正文 + 化学一致性修键为 B 并策展自解（GAIN +1）。 | — |
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
| pdf-355 | E 类 → 已清理 | 填空半已清理为自洽题录（见 E 类 / V 类），原"题界拼接阻塞"已解除；后半选择题已剥离，不阻塞当前题录判分 | 填空半不再 blocked；后半选择见 V.1 deferred。引擎未解属方程域 KB 缺口（K.3），非数据缺陷 |
| pdf-356 | E 类 → 已清理 | 填空半已补全 ①② 并清理为自洽题录（见 E 类 / V 类），原"题界拼接 + 缺图阻塞"已解除；后半选择（化合物 Z）已剥离，答案册无对应答案 | 填空半不再 blocked；后半选择见 V.1 deferred（需用户原页）。引擎未解属方程域 KB 缺口（K.3），非数据缺陷 |
| pdf-414 | E 类 → 已修复（datarepair1） | 已原页还原 Fe₃O₄ 大题 (1)-(5) 全文，订正 type/options，键与 answer 册 `page_140` num=3 对齐 | 不再 blocked；引擎未解属能力缺口，非数据缺陷 |
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
> **本 spec 结果**：解出 9 道（AC-5 达成：累计 GAINED=9 / LOST=0 / harm=0 / precision=1）；剩余 52 道逐题登记处置结论，确保 **0 静默遗漏**。所有未解题保持 `null`（诚实答不出），**不蒙数**、不改 `answer`、不写入错误事实。
> **集合闭包验证（PASS）**：`61 = solved(9) ∪ skipped_trace(5) ∪ t2(7) ∪ remaining(40)`，两两无重叠、差集为空。证据脚本 [_evalscratch/verify/t8_partition.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/t8_partition.py)；分簇明细 [t8_partition.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/t8_partition.txt)；未解题清单 [unsolved_registered_ids.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/unsolved_registered_ids.txt)（闭包快照，与下表处置一一对应）。
> **diagnose taskType 说明**：下表 taskType 取自 `gap63_diagnose.txt`（基线 `verify_fix_all` 路由）。T4 后 `pdf-044/084/354/526/527` 的 taskType 已重路由为 `equation_writer`（见 T4 证据），其中 044/354 已解出，084/526/527 仍留痕（见 K.3）。

### K.0 解出清单（14 道，本 spec 已翻转判正，correct=true && truthAdopted=true）

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
| pdf-134 | judgment | T3 | `judgment-statement-facts` +15（JS-027~041，`stemMatch` 锁 [葡萄酒,碳酸钡] 全库唯一共现；15 子句含 (5)(11) 两方程子句因 U+2212 未归一化导致 `EquationCorrectnessJudge` 解析失败→走 `CompositeStatementJudge` 事实回退；聚合 `×,×,√,×,√,×,×,×,×,×,×,√,×,×,×` 与答案键一致）。eval `after_judge134`：gatewayCorrect 643→644 / harm 0 / prec 1 / LOST=[] |
| pdf-509 | vsepr | 路由修复 | `LlmGatewayService` `case "vsepr"` 在 `VseprSolver.TrySolve` 返回 null 后兜底调 `VseprSolver.TrySolveBondAngleReason`。该求解器本已存在且能给出正确结论，但原仅 `concept`/`unknown` 分支可达；生产中 LLM 把该题（AsCl₃ 键角比 BCl₃ 小，原因是）归为 `vsepr`，而 `TrySolve` 显式拒绝含「键角/原因」的题 → 漏解。求解器内置严格守卫（全库扫描仅 pdf-509 命中且判正、wrong=0）→ 零回归。新增路由回归测试 `Vsepr分支_键角原因题_兜底到BondAngleReason_pdf509`。eval `after_vsepr509`：gatewayCorrect 644→645 / harm 0 / prec 1 / LOST=[]（由 bothWrong 翻为 gatewayOnly，LLM 答错而引擎判正）。**注：不在原 61 闭包清单内，为闭包后新发现并修复的引擎缺口** |
| d5-1 | calculation | 路由修复（兜底） | `LlmGatewayService` `case "calculation"` 在 `CallCalculation` + `ReactionKbWriterSolver` 全 miss 后、返回 null 前兜底调 `BuildTrapResponse`（标况相态陷阱，查 `standard-state-phases.json`；SO₃ 标况为固体，22.4 L 非 1 mol 气体 → 不能用 22.4 L/mol）。陷阱逻辑本已存在，原仅 `concept`/`unknown` 分支可达；生产 LLM 归为 `calculation` → 原路由不落陷阱 → 漏解。全库扫描仅 d5-1/d5-2 命中且判正（wrong=0）→ 零回归。eval `routing_focused`：LOST=[] / harm=0 / prec=1，engineSolved 645→648。**不在原 61 闭包清单内**（闭包快照 `unsolved_registered_ids.txt` 不含 d5-1，已核对） |
| d5-2 | calculation | 路由修复（兜底） | 同 d5-1：`case "calculation"` 兜底 `BuildTrapResponse`（己烷标况为液体，22.4 L 非 1 mol 气体）。全库扫描仅 d5-1/d5-2 命中且判正（wrong=0）→ 零回归。eval `routing_focused` 同左。**不在原 61 闭包清单内** |
| d7-4 | structure_verify | 路由修复（兜底） | `LlmGatewayService` `default` 分支（`structure_verify` 无显式 case → 落 `default`）在返回 null 前兜底调 `ConceptFactSolver.TrySolve`。该求解器本已存在且判正（CH₃CH(OH)COOH 中间碳连 4 个不同基团 → 手性碳），原仅 `concept`/`unknown` 分支可达；生产 LLM 归为 `structure_verify` → 漏解。`ConceptFactSolver` 不门控 taskType，全库扫描 35 命中、0 错 → 零回归。eval `routing_focused` 同左。**不在原 61 闭包清单内** |
| pdf-278 | equilibrium | 多部分分发 + 子问(5)求解器（pdf-278 spec） | 题干已在 V 类还原（Ka 表缺失的纯数据缺陷，2026-11-26 修复）；本次补引擎能力：① T1 三处多部分占位静默（电离移动/酸性排序/curve 占位不再阻塞整题分发，pdf-272 等多部分题改走诚实 null）；② T3 新增 `TrySolveWeakAcidIonizationExpression`（一元弱酸稀溶液电荷守恒 `c(A⁻)=10^-pH-10^(pH-14)` + 电离度增大选项判定，含整题守卫、共轭碱身份校验、非整数 pH 不蒙数）；③ T4 multi_part AND 判分支持方程两侧物质顺序无关（含子问编号剥离防解析污染）。5 子问全解 → AND 全命中 → 采纳（`gatewayResultProvenance=multi_part`）。聚焦/全量 eval：gwCorr/usedEng/truthAdopt=True；引擎层 LOST=0、harm=0、precision=1，27 道已解对多部分题 0 回归（`verify_mp27`）。**不在原 61 闭包清单内**（数据修复前题干截断，属 V 类数据缺陷；本次为闭包后引擎能力补齐） |

> 终态证据：[final_gained_ids.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/final_gained_ids.txt)，累计 eval diff `verify_fix_all`→`t5_kb`：GAINED=8 / LOST=0 / WORSEN=0 / harm 0→0 / precision=1。

### K.0 增补（engine-gap-p0-p1-batch 批次，eval `gap_batch1`）

> 本批次相对基线 `after_vsepr509`：引擎层 **LOST=[] / harm=[] / truthAdopted 回退=0**，ENGINE GAINED=+9；`verify_mp27` 27/27 PASS；`dotnet test -c Release` 1640 passed / 0 failed。证据：[_evalscratch/snapshot_gap_batch1.json](file:///d:/github/CogniThink-Chem/_evalscratch/snapshot_gap_batch1.json)、[diff_engine 输出](file:///d:/github/CogniThink-Chem/_evalscratch/)。solved 口径 cmp 净 -2（pdf-197/376）经逐题核实为 pureLlm 波动（两题 base/new 均 `gatewayCorrect=False`，gwFlip=False），与引擎无关。

下表为本批次**新增翻转判正**的 5 道（d5-1/d5-2/d7-4/pdf-278 已在上表 K.0 登记，本批次回归恢复确认，不重复计入）：

| id | diagnose taskType | 解出任务 | 落库 / 关键改动 |
|----|-------------------|----------|-----------------|
| pdf-168 | equilibrium | T4 | 新增 [KspPhResidualSolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/KspPhResidualSolver.cs)：pH + M(OH)ₙ 的 Ksp + 目标金属离子 → `c(Mⁿ⁺)=Ksp/[OH⁻]ⁿ`；`ProcessStudentMessageAsync` last-resort 钩子（仅 `Ok!=true` 触发，构造性零 LOST），置 `taskType=equilibrium` 走 OR 采纳门。全库 dry-fire 仅 pdf-168 命中且判正，0 错采纳。 |
| pdf-506 | equilibrium | T5 | [VseprSolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/VseprSolver.cs) 新增 `TrySolveBondAngleReasonForMultiPart` + `SplitNumberedSegments`：整题分发失败后按 `(N)` 切子段，仅在同时含「键角+原因+比较」的段内收集恰好 2 个可查微粒，避免跨子问乱配；L194 结论**追加**「孤电子对斥力大，压缩键角」（保留原串，pdf-509 仍采纳）。last-resort 钩子置 `taskType=vsepr`。全库 dry-fire 仅 pdf-506/pdf-509 命中（pdf-509 baseTrue 不到钩子），0 错采纳。未动 `ClassifyTaskDeterministic`（零漂移）。 |
| trap-limit-3 | calculation | T6 | [ProductWriterSolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/ProductWriterSolver.cs) + [product-rules.json](file:///d:/github/CogniThink-Chem/data/knowledge/product-rules.json) 新增 `pw-nahco3-baoh2-equimolar`（等物质的量 NaHCO₃+Ba(OH)₂ → HCO₃⁻+Ba²⁺+OH⁻→BaCO₃+H₂O，已配平）；修复「物质的量」分到 calculation 导致 equation_writer 链不可达的路由缺口：last-resort 钩子置 `taskType=reaction_check`。全库 dry-fire 9 触发中 8 道 baseTrue（钩子不触发，零 LOST），仅 trap-limit-3 baseFalse 且 adopted，0 错采纳。 |
| pdf-272 | equilibrium | T7 | 新增 [AcidFirstIonizationSolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/AcidFirstIonizationSolver.cs)：子问(1) 弱酸第一步电离 HₙA⇌Hₙ₋₁A⁻+H⁺（电荷/产物由分子式推导，非硬编码单题）。**子问(2) 曲线数据精度缺陷**：矢量化交点 H₂C₂O₄/HC₂O₄⁻ 落在 pH≈1.0，而答案键为 1.0×10⁻¹·²（pH 1.2）→ 不蒙数（不扩展 CurveIntentExtractor 的 Ka 意图）。last-resort 钩子置 `taskType=equilibrium`，整题因子问(1) 命中关键词经 OR 采纳翻 True。全库 dry-fire 仅 pdf-272 命中且判正，0 错采纳。 |
| pdf-328 | calculation | P2-2 | 新增 [CombustionEmpiricalFormulaSolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/CombustionEmpiricalFormulaSolver.cs)：题干给齐燃烧数据（样品质量 + CO₂/H₂O 的量）→ 求实验式；pdf-328 凭文本即得 n(C):n(H):n(O)=0.18:0.16:0.04=9:8:2 → **C9H8O2**（恰等于答案，n=1），无需读质谱图。氧质量为负（数据矛盾）或最简比无法在容差内化整 → null（不蒙数）。last-resort 钩子置 `taskType=calculation`。全库 dry-fire 仅 pdf-328 命中且判正，0 错采纳。 |

**本批次诚实登记（未解/延后，不蒙数）**：
- **pdf-262**：T2 已修数据缺陷（官方答案、去重、answerKeywords 重切），但子问(2)(3)(4) 仍不可解（复杂平衡图像/多问）→ 整题保持 `gatewayCorrect=False`，不强行蒙数（符合 Non-Goal）。
- **pdf-010 / pdf-156（P2-1 反应方程生成器）→ NO-GO**：答案键疑似数据错位（pdf-010 `(1)2FeCl3+3H2O` 与子问不匹配；pdf-156 答案 `N2H4·H2O` 重复），且方程式生成属高假阳性域，需独立「已验证反应库 + 窄门控」，延后到专项 spec。
- **pdf-416 ✅ 已解（engine-ext-experiment-operation 批次）**：choice 题（答案 C），粗盐精制"一系列操作"结晶方法选择。原"单条事实无法驱动/子问缺上下文 NO-GO"结论**过时**——当前 question 已含文本化流程图，子问 (5) 题面自洽。向 experiment-facts 追加 EF-416-A/B/C/D（stemMatch 锁「粗盐/精制/一系列操作」，全库仅命中本题），经 ExperimentChoiceValidator 单库单选判正。eval after_expop：GAINED，LOST=[]/harm=0/precision=1。

### K.0 增补（engine-gap-batch2 批次，eval `gap_batch2`）

> 本批次相对基线 `gap_batch1`：引擎层 **LOST=[] / harm=[] / truthAdopted 回退=0**，ENGINE GAINED=+2（pdf-024、pdf-169），gwCorr 654→656；`verify_mp27` 27/27 PASS（REGRESSIONS=[]）；`dotnet test -c Release` 1657(TutorBrain)+922(Chem)=2579 passed / 0 failed。证据：[_evalscratch/snapshot_gap_batch2.json](file:///d:/github/CogniThink-Chem/_evalscratch/snapshot_gap_batch2.json)、[diff_gw_batch1_batch2.py](file:///d:/github/CogniThink-Chem/_evalscratch/diff_gw_batch1_batch2.py)。本次 eval `llmAvailable=False`（LLM 侧为空），但 `gatewayCorrect` 为纯网关侧、与 LLM 无关，故引擎 LOST/GAINED/harm 判定有效；solved 口径（含 pureLlm）不作回归依据。

| id | diagnose taskType | 解出任务 | 落库 / 关键改动 |
|----|-------------------|----------|-----------------|
| pdf-024 | calculation | batch2-A | 新增 [VaporDensityMolarMassSolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/VaporDensityMolarMassSolver.cs)：题干问「平均摩尔质量」+ 给对氢气/空气的相对密度 D → M=2D（氢气）/29D（空气）。pdf-024 子问(3) D=9.5 → 19 g/mol 命中答案键。缺触发词/缺问法/D 不可解析/多互斥 D → null（不蒙数）。last-resort 钩子（仅 `Ok!=true` 触发，构造性零 LOST）置 `taskType=calculation` 走 OR 采纳门。全库「相对密度」仅 1 题，护栏锁 fired={pdf-024}，0 错采纳。 |
| pdf-169 | equilibrium | batch2-B | 新增 [TextChiralCenterSolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/TextChiralCenterSolver.cs)：题干问「手性碳原子数目」+ 文本给结构简式 → 缩合式归一化（展开 `(X)n`、`HOOC`/`COOH`→`C(=O)O`、`CHOH`→`C(O)`）为标准 SMILES → RDKit `numAtomStereoCenters`。pdf-169 子问(1) `HOOC(CHOH)2COOH` → 2 命中答案键。无手性碳问法/无计数问法/无可解析结构/互斥 → null（归一化失败一律 null，不蒙数）。last-resort 钩子置 `taskType=equilibrium`，复合题因子问(1) 命中经 OR 采纳翻 True。护栏锁 fired={pdf-169}（d7-4 已解不到钩子；选择/图像题无文本分子式→null），0 错采纳。 |

**本批次诚实登记（spike 未通过 / 延后，不蒙数）**：
- **pdf-098（σ 键数）→ 延后（→ batch3-D 已解，见 K.0 增补 batch3）**：全库 21 题含「σ 键」，多为选择题；且涉及离子化合物（如 NH₄Cl）离子键是否计入 σ 的口径与高中不符，假阳性风险高、ROI 低。需先建立「离子化合物 σ 键口径」+ 选择/填空分流窄门控，延后到专项。
- **pdf-146（滴定计算）→ 延后（→ batch3-B 已解，见 K.0 增补 batch3）**：全库 11 题含滴定，数据点多（c/V/计量比/稀释比/样品质量），文本数字提取易错；缺任一量应 null 的门控需逐题 spike 验证齐全性，当前通用性不足，延后。

### K.0 增补（engine-gap-batch3 批次，eval `gap_batch3`）

> 本批次相对基线 `gap_batch3_base`（LLM-on，qwen3-vl-plus）：引擎层 **LOST=[] / harm=[] / truthAdopted 回退=0**，ENGINE GAINED=+4（pdf-098、pdf-146、pdf-274、pdf-355），gwCorr 656→660；`verify_mp27` 27/27 PASS（REGRESSIONS=[]）；`dotnet test -c Release` 1685(TutorBrain)+928(Chem)=2613 passed / 0 failed。证据：[_evalscratch/snapshot_gap_batch3.json](file:///d:/github/CogniThink-Chem/_evalscratch/snapshot_gap_batch3.json)；diff `gap_batch3_base→gap_batch3`：GAINED=[pdf-098,pdf-146,pdf-274,pdf-355] / LOST=[]。pureLlmOnly 口径 base=51 → new=45，为 LLM 非确定性漂移（与引擎无关，不作回归依据）；引擎回归判定以 `gatewayCorrect` 集合为准。新增 4 个求解器均接在 [LlmGatewayService](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) last-resort 钩子链末尾，每段 `if (engineResponse.Ok is not true)` 守卫 → 已判正题零触达（构造性零 LOST）；未改 `ClassifyTaskDeterministic`（零漂移）。

| id | diagnose taskType | 解出任务 | 落库 / 关键改动 |
|----|-------------------|----------|-----------------|
| pdf-274 | equilibrium | batch3-A | 新增 [HydrolysisEquationSolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/HydrolysisEquationSolver.cs)：题干「用平衡原理解释」+ 除杂语境（除杂/除去/去除）+ 可水解强酸弱碱阳离子盐（Fe³⁺/Al³⁺/Cu²⁺/Mg²⁺）→ 输出对应阳离子水解方程 Mⁿ⁺+nH₂O⇌M(OH)ₙ+nH⁺。pdf-274 命中 Fe³⁺ 与 Cu²⁺ 两条方程。缺触发词/缺除杂语境/无可水解阳离子 → null（不蒙数）。钩子置 `taskType=equilibrium` 走 OR 采纳门。护栏锁 fired={pdf-274}，0 错采纳。 |
| pdf-146 | calculation | batch3-B | 新增 [IodometryMassFractionSolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/IodometryMassFractionSolver.cs)：碘量法测 SO₂ 含量（g/kg），提取 c(I₂)/V(滴定剂)/定容体积/取样体积/样品质量 → n(I₂)=cV，SO₂:I₂=1:1，M(SO₂)=64 → 含量。pdf-146 子问(5)=0.032 g/kg 命中。缺任一量/非 SO₂ 碘量法/无含量问法 → null。钩子置 `taskType=calculation`。护栏锁 fired={pdf-146}，0 错采纳。 |
| pdf-355 | equation_writer→calculation | batch3-C | 新增 [EsterificationEquationSolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/EsterificationEquationSolver.cs)：题干「酯化」+ 乙二酸 + 乙醇 + 物质的量 1:2 → 输出 HOOC-COOH+2CH₃CH₂OH⇌CH₃CH₂OOC-COOCH₂CH₃+2H₂O。pdf-355（base 属 bothWrong，引擎仍错）命中产物键，整题翻 True（bothWrong→gatewayOnly，无 harm）。非该组合 → null。钩子置 `taskType=calculation`。护栏锁 fired={pdf-355}，0 错采纳。 |
| pdf-098 | calculation | batch3-D | 新增 [SigmaBondCountSolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/SigmaBondCountSolver.cs) + Chem.Core 暴露 `MoleculeAnalysisResult.SigmaBondCount`（[MoleculeAnalyzer.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Molecules/MoleculeAnalyzer.cs)：对补氢后副本 `getNumBonds`，每条连接=1 σ 键，修复隐式氢不计入的问题）。离子盐分子式拆分为电荷平衡的阴/阳离子 SMILES（NH₄Cl→[NH4+].[Cl-]）→ RDKit 解析得 σ 键数。pdf-098 子问(1)=4 mol 命中。共价分子/含氧酸盐无法确定性拆分 → null（不蒙数；阳离子表不含 H⁺，避免 H₂O 被误拆为 [H+].[OH-]）。钩子置 `taskType=calculation`。护栏锁 fired={pdf-098}（全库 9 道含「σ键」仅 pdf-098 baseFalse，其余 baseTrue 由 Ok=true 守卫跳过），0 错采纳。 |

**本批次诚实登记（不补，不蒙数）**：
- **A 类设计回退**：concept 6 题（pdf-176/198/324/483/519/528）及 unknown 图像题（pdf-205/215/391/413/541）—— 设计不走引擎或依赖图像，保持 null。
- **图像依赖**：choice 18 题图、equilibrium pdf-262/418、calculation pdf-534、equation_writer pdf-156/177/532 等 —— 引擎无视觉，文本不可定解。
- **数据缺陷**：pdf-197（答案键 ABCD 全对与问「错误的是」矛盾）、pdf-376（解析与答案矛盾）、pdf-263（答案键=字母 B 与问法不符）、pdf-084（答案键 C 与方程问法不符）、pdf-306（键冲突已登记）—— 先修数据。
- **开放论述/无唯一解**：calculation pdf-207（熔沸点原因）、pdf-070（双线桥图+字母驱动）、reaction_check pdf-163（上下文缺失子问片段）等 —— 无唯一确定数值/方程可输出，不蒙数。
- **choice 事实覆盖**属 chem-fact-curation 另轨（pdf-405 等），不在本次引擎求解器范围。
- **新增 5 题（LLM 本轮 drift 进 leads：pdf-097/197/218/306/376）**：逐题判读全部 NO-GO（数据缺陷 / 图像依赖 / 答案键与解析矛盾），不补。

### K.0 增补（engine-gap-batch4 批次，eval `gap_batch4`）

> 本批次相对基线 `gap_batch3`（LLM-on，qwen3-vl-plus）：引擎层 **LOST=[] / harm=[] / truthAdopted 回退=0**，ENGINE GAINED=+3（pdf-356、pdf-526、pdf-527），gwCorr 660→663；`verify_mp27` 27/27 PASS（REGRESSIONS=[]）；`dotnet test -c Release` 1695(TutorBrain)+928(Chem)=2623 passed / 0 failed。证据：[_evalscratch/snapshot_gap_batch4.json](file:///d:/github/CogniThink-Chem/_evalscratch/snapshot_gap_batch4.json)；diff `gap_batch3→gap_batch4`：GAINED=[pdf-356,pdf-526,pdf-527] / LOST=[]。pureLlmOnly 45→46、bothWrong 52→48 为 LLM 非确定性漂移（与引擎无关，不作回归依据）；引擎回归判定以 `gatewayCorrect` 集合为准。新增求解器接在 [LlmGatewayService](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) last-resort 钩子链末尾（batch3-D 之后、P2-1 之前），`if (engineResponse.Ok is not true)` 守卫 → 已判正题零触达（构造性零 LOST）；未改 `ClassifyTaskDeterministic`（零漂移）。
>
> **分布分析核心结论（基线 gap_batch3，leads 97 = pureLlmOnly 45 ∪ bothWrong 52）**：引擎确定性可解尾量**仅 3 题**；其余 ~94 题为非引擎域或数据缺陷——图像依赖 51（引擎无视觉，需 P2-3 图像融合）、choice 字母答案 38（属 chem-fact-curation 事实注入轨，引擎不输出字母）、**数据缺陷 ~16（答案键错位/跨题混排/缺结构图/单问多答案，采纳键本身错误，引擎输出再对也不命中）**、concept 自由文本 7、开放论述/图示 4。本批次只取可零 FP 翻正的酯化簇，不扩张到非引擎域（守「不蒙数 / precision=1」红线）。

| id | diagnose taskType | 解出任务 | 落库 / 关键改动 |
|----|-------------------|----------|-----------------|
| pdf-356 | equation_writer→calculation | batch4-C | 新增 [EsterificationProductSolver.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/EsterificationProductSolver.cs) 路径①②：题干「酯化」+「环酯」+ 乙二酸 + 乙二醇 → 原子平衡：环酯 = C₂H₂O₄+C₂H₆O₂−2H₂O = **C₄H₄O₄**；含「缩聚/聚酯」→ 重复单元 = 环酯−H₂O = **C₄H₂O₃** → 输出 `(C4H2O3)n + (2n-1)H2O`。两关键词均命中。缺任一反应物名/非该组合 → null。钩子置 `taskType=calculation`。护栏锁 fired 含 pdf-356，0 错采纳。 |
| pdf-526 | equation_writer→calculation | batch4-A | [EsterificationProductSolver](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/EsterificationProductSolver.cs) 路径③：「酯化」+（羟基酸/自身酯化）+「六元环酯」+ 乳酸 → 输出 `2CH3CH(OH)COOH → CH3CH(O)COOCH(CH3)CO + 2H2O`（羟基酸自酯化成交酯，外部化反应事实表）。命中关键词。 |
| pdf-527 | equation_writer→calculation | batch4-B | 路径④：同上触发词为「聚酯」→ 输出 `2CH3CH(OH)COOH → [−OCH(CH3)CO−]_n + nH2O`（羟基酸缩聚）。命中关键词。 |

**本批次诚实登记（不补，不蒙数）**：
- **数据缺陷（~16，需独立数据修复轨，非本批次，潜在增益 > 本批次）**：跨题答案混排（pdf-160↔164：BaTiO(C₂O₄)₂ 方程出现在 pdf-160 答案、却不在问它的 pdf-164 答案）、答案键与问法不符（pdf-172 计算题键=`AC`、pdf-179 写方程键=`B`、pdf-084 键=`C`）、单问多答案（pdf-117 一条判断含 14 个 √/×；pdf-190 电离能 4 子问键=`正四面体,sp3`）、结构与答案矛盾（pdf-324 `[CH=CH]n` 单体键=苯乙炔）、缺结构图（pdf-322/323/519/520/521/523 问官能团但 images 为空）。这些引擎无法修复（采纳键错误），建议单列数据修复计划审计 `pdf_questions.json`。
- **非引擎域（不补）**：图像依赖 51 题（需 P2-3 图像融合）、choice 字母 38 题（chem-fact-curation 另轨）、concept 自由文本 7 题（A 类设计不走引擎）、pdf-070 双线桥 / pdf-207 熔沸点原因 / pdf-163 可行性原因 / pdf-537 量子点原因（无唯一可输出结论）。
- **延后（ROI 低，需通用求解器）**：pdf-166 子问(5) 脱氯锌粉 0.5 mol（氧化还原电子守恒）、pdf-171 矿化离子方程（复杂矿物配平）、pdf-462 AlOOH 水解（单产物特例）、pdf-097/484 流程方程（反应物隐含）—— 需通用氧化还原/配平求解器，单批硬做泛化不足。

### K.1 多部分大题·子问异构（引擎无子问分发与逐子问聚合）—— 最大簇

> 共同根因：一条题录含多子问，各子问分属**方程书写 / 化学计算 / 概念解释 / 选字母 / 流程推断**等不同求解域，但引擎以单一 taskType 整体调度，无法把每个空格分发到对应求解器并聚合；部分子问还依赖流程/装置图（文本外数据）。**当前处置：保持 `null`。**
>
> **【P1-2 交付（分发骨架 + AND 判分，纯基础设施）】** 子问分发架构已落地：[MultiPartUtility](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/MultiPartUtility.cs)（多部分检测 + `(N)` 编号切分，口径复用 `MultiSubCorrectnessSolver.SubMarker`，天然排除 `(2025·北京卷)`/`[Cu(NH3)4]`/`(FeSO4·7H2O)`）、[LlmGatewayService.MultiPartDispatch](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.MultiPartDispatch.cs)（仅在整题未解 `Ok!=true` 时 fallback 激活 → 629 道已解对题零回归；逐子问复用现有求解器，任一子问未解 → 整体返回 `null`，不蒙数、不输出部分答案）、评估侧 `BuildGatewayAnswerAsync` 对 `task_type=multi_part` 聚合结论走**答案键 AND 判分**（全部子问键命中才采纳，凡采纳必全对 → precision=1 / harm=0 构造保证）。**AC-4 据实校准为 ≥0**：T0 + P1-3 就位后重审均确认 **0/27** 可由现有求解器全覆盖子问——阻塞子问为「自由文本解释」（→ P1-4 自由填空概念求解器）/「图像依赖」（→ P2-3 图像融合）/「热化学·Ksp·滴定·平衡·有机等其他计算域」（→ 后续域计算器）。本项交付纯基础设施，**零即时增益、零回归**。
>
> **实证（`after_p12`，Production LLM off，757 题）**：27 道 K.1 全部 `correct=false / adopted=false`（`unsolved_bothwrong`），0 错采纳；全库 global `adopted∧¬correct = 0`，precision=1 / harm=0。权威 diff：对 `base_p0` → LOST=0 / WORSEN=0 / harm=0（GAINED=1 为 P1-3 的 trap-limit-2 携带，非 P1-2）；对 `after_p13` → GAINED=0 / LOST=0 / WORSEN=0（P1-2 零影响隔离证明）。后续 P1-4 / P2-3 解锁子问后，分发骨架可在不动判分门的前提下产出增益。证据：[p12_verify_after.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p12_verify_after.py)、[p12_reaudit.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p12_reaudit.txt)、[p12_check_states.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p12_check_states.py)。

| id | diagnose taskType | 子问构成（摘要） | 已有登记 |
|----|-------------------|------------------|----------|
| pdf-024 | calculation | 化学计量 3 子问（气球体积字母 + 混合气 H 质量 + 平均摩尔质量） | ✅ 已解（batch2-A）：子问(3) 由 VaporDensityMolarMassSolver 命中「19」，经 OR 采纳整题翻 True；cmp gap_batch1→gap_batch2 GAINED=[pdf-024] |
| pdf-025 | calculation | 化学计量 6 子问（体积/摩尔质量/NA 代数式/气体溶解浓度/混合浓度/电荷守恒） | ✅ 已解：StoichiometrySolver，整题多模式返回 null 交多部分分发逐子问求解，multi_part AND 判分 6 键全中；cmp after_phorder→after_stoich GAINED=[pdf-025]（见 fill-calc-coverage Task 3） |
| pdf-070 | calculation | 4 子问（双线桥标电子 + KMnO₄ 性质选字母 + 氧还产物比 + 转移电子数） | — |
| pdf-098 | calculation | 5 子问（σ 键数 + 装置优点 + 装置连接序 + 能否 + 检验方案自由文本） | G 类⑧（连接序非真方程）；**✅ 已解（batch3-D）**：子问(1) σ 键由 [SigmaBondCountSolver](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/SigmaBondCountSolver.cs) 命中「4 mol」，经 calculation OR 采纳门整题翻 True；cmp gap_batch3_base→gap_batch3 GAINED=[pdf-098]（其余子问为装置优点/连接序/自由文本，不走引擎；OR 门已采纳，LOST=0） |
| pdf-146 | calculation | 5 子问（仪器名 + 通 N₂ 目的 + 沉淀化学式 + 性质选字母 + 滴定计算） | **✅ 已解（batch3-B）**：子问(5) 滴定由 [IodometryMassFractionSolver](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/IodometryMassFractionSolver.cs) 命中「0.032」，经 calculation OR 采纳门整题翻 True；cmp gap_batch3_base→gap_batch3 GAINED=[pdf-146] |
| pdf-156 | calculation | 4 子问（制 NH₃ 方程 + 滴加溶液/合成方程 + 分离操作选字母 + 滴定质量分数） | G 类⑧（产物碎片非真方程） |
| pdf-166 | calculation | 6 子问（周期表位置 + 方程 + 滤渣成分 + 离子方程 + 电子守恒计算 + 电解现象解释） | — |
| pdf-167 | equilibrium | 蛇纹石+绿矾分解耦合流程多子问（绿矾分解方程 + Ksp 沉淀 + 矿化 CO₂…），`answer=A` 单字母 | 本类新增 |
| pdf-168 | calculation | 2 子问（滤渣成分 + Ksp 计算 Al³⁺ 浓度）—— 数据在题干，缺沉淀溶解计算域 | — |
| pdf-169 | equilibrium | 7 子问（手性碳数 + 离子 + 温度解释 + 方程 + 金属元素 + 平衡解释 + 热解方程） | ✅ 已解（batch2-B）：子问(1) 由 TextChiralCenterSolver 命中「2」，经 OR 采纳整题翻 True；cmp gap_batch1→gap_batch2 GAINED=[pdf-169] |
| pdf-190 | equilibrium | 4 子问（电离能理由 + 电负性排序 + 原子半径排序 + 价层排布/电负性）—— 结构概念解释 | — |
| pdf-207 | calculation | 4 子问（沸点原因 + 熔沸点原因 + 熔点比较 + 熔点比较）—— 晶体/分子间力解释 | — |
| pdf-228 | calculation | 2 子问（配合物离子符号 + 18e 规则计算 + σ 键数）—— 配位化学计算 | — |
| pdf-244 | calculation | 2 子问（光合作用热化学方程 + 键焓计算）—— 热化学计算 | — |
| pdf-258 | equilibrium | 3 子问（盖斯定律 + lgK–1/T 图像判吸放热 + 平衡常数 + 电极反应）—— 依赖图 | G 类⑤ / C 类（依赖图） |
| pdf-260 | equilibrium | 3 子问（电子式 + 盖斯定律 + He 曲线判断）—— 数据补全后暴露多部分大题缺口 | J.3（缺陷暴露） |
| pdf-261 | equilibrium | 5 子问（熵变 + 盖斯 + 选字母 + 流速解释 + 计算）—— 化学平衡多子问 | — |
| pdf-262 | equilibrium | 1 空（CH₄ 脱氢芳构化判高低）—— 复杂平衡图像推断 | — |
| pdf-272 | equilibrium | 3 子问（草酸粒子分布曲线）—— **T6 占位噪声**：`gatewayConclusion` 吐出曲线中间量，`adopted=False` 无 harm | 见 K.4 |
| pdf-275 | calculation | 1 空（8 种溶液 pH 排序）—— 缺 pH 定性排序计算域 | ✅ 已解：PhOrderingSolver（外置 ph-species.json 引用 acid-constants.json，不重复存常数；门禁：所有带圈物种在表 + 引用常数键存在 + 单一共享浓度，否则 null 不蒙数），输出精确串 `②>①>④>③>⑤>⑧>⑥>⑦` 命中答案键；cmp after_catalyst→after_phorder GAINED=[pdf-275]，LOST=[]、harm=0、precision=1 |
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
| pdf-355 | equation_writer | ✅ 题干拼接已清理（填空半自洽、后半选择剥离，见 §E / V 类）；引擎未解属方程域 KB 缺口，非数据缺陷 | 方程域 KB 补全（受唯一命中 + LOSS=0 约束） | §E（已清理） |
| pdf-356 | equation_writer | ✅ 题干拼接已清理（填空半补全①②、后半选择剥离，见 §E / V 类）；引擎未解属方程域 KB 缺口，非数据缺陷 | 方程域 KB 补全（受唯一命中 + LOSS=0 约束） | §E（已清理） |
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
| pdf-478 | judgment | 子题 (2)(8) 图像依赖 + (6) 方程抽取失败 → 保持 null | T3 留痕（本类新增索引） |
| pdf-196 | calculation | 氮循环选择题，**options 为空**（T1 探针核实）→ 无法策展 | T1 留痕（本类新增索引） |
| pdf-416 | choice | ✅ 已解（engine-ext-experiment-operation：EF-416 结晶操作事实，单选 C）；原"子问缺父大题上下文/无唯一锚点"过时，当前题面已含流程图文本且子问自洽 | 已移出未解 |
| pdf-405 | choice | 选项 A/B/D 仅给实验现象、缺结论性文本，文本截断无法写匹配事实 | H.3（既有） |
| pdf-531 | choice | ✅ 已解（engine-ext-experiment-operation：EF-531 滴定操作事实，多选 BC 经 MultiSelectChoiceValidator 全库遍历采纳）；原"题干拼接/askDir 不可靠"过时，题面滴定操作子问自洽且无标记不阻塞多路径 | 已移出未解 |

### K.7 T2 依赖图候选——待视觉核实（保持 `null`，AC-5 已达成不阻塞）

> 共同根因：选择题选项真伪依赖装置/曲线/结构图，4-verdict 需**读图辅助**（图仅作示意，结论从化学原理独立推出）。AC-5（总 GAINED≥8）已由 T1/T3/T5 达成，T2 为可选 bonus；任一选项化学真值存疑即整题跳过，绝不靠「看答案反推」写事实。**当前处置：保持 `null`，待后续视觉核实后评估（探针 `TOTAL PROBLEMS=0` 才落库）。** 建议接续方向：T2 批次 2（视觉辅助 + 化学真值交叉核对 + 探针门控）。

| id | diagnose taskType | 图像依赖 | 已有登记 |
|----|-------------------|----------|----------|
| ~~pdf-138~~ | choice | ✅ 已策展自解（hard-bone-data-repair-3）：存储 answer=B 正确（选不能达到目的项）。据渲染页 page_046.png 读 4 装置：A 可达、B 不可达（浓硫酸氧化 H₂S）、C 可达、D 可达，唯一不可达=B；stem 含"不能达到"故 askDir=错误。附：提取图 p046_n05_1.png 被错配为同页第 6 题价类图（不影响文本判题）。策展 GC-771-A~D 命中 B。eval `hardbone3` GAIN +1，LOSS=0、harm=0、precision=1。 | 已移出未解 |
| ~~pdf-255~~ | choice | ✅ 已策展自解（hard-bone-data-repair-2）：存储 answer=C 正确，缺陷为曲线图缺失致引擎 null；据 page_090.png 读图 + 答案册 page_087.json 核 4-verdict（A/B 假、C 真、D 假），策展 GC-765-A~D 命中 C。eval GAIN +1，LOSS=0、harm=0。 | 已移出未解 |
| pdf-271 | choice | 三组滴定实验曲线图（指示剂选择），键已修 | register line 235（键修复） |
| pdf-303 | choice | 电池装置图，`answer='C'` 存疑（note 称应为 CD） | G 类⑤ / H.1（既有） |
| pdf-340 | equilibrium | 有机手性碳结构图（D 仅含 3 个手性碳） | 本类新增 |
| pdf-357 | choice | 灵芝酸有机结构图，键已修（C→B）但引擎无力解 | J.3（键已修，能力缺口残留） |
| pdf-406 | choice | 气体发生/收集装置图；`WrongDir` 正则未覆盖「不能完成」→ askDir 误判 | A.1 / H.3（既有，键已修） |

### K.8 台账自洽性核对（61 全覆盖，0 静默遗漏）

| 处置类别 | 题数 | 题号 |
|----------|------|------|
| 解出（K.0） | 9 | pdf-044, 090, 147, 155, 188, 319, 354, 480, 134 |
| 多部分大题子问异构（K.1） | 27 | pdf-024, 025, 070, 098, 146, 156, 166, 167, 168, 169, 190, 207, 228, 244, 258, 260, 261, 262, 272, 275, 328, 418, 461, 464, 484, 506, 532 |
| 纯计算/定量域（K.2） | 5 | pdf-030, 415, 517, 535, 543 |
| 方程书写 KB 不可行（K.3） | 4 | pdf-084, 526, 527, 010 |
| 占位噪声 T6 降级（K.4） | 4 | pdf-010, 163, 175, 272 |
| 概念/自由填空问法（K.5） | 2 | pdf-274, 107 |
| 探针跳过留痕（K.6） | 5 | pdf-478, 196, 416, 405, 531 |
| T2 依赖图候选（K.7） | 7 | pdf-138, 255, 271, 303, 340, 357, 406 |

> **去重说明**：pdf-010 同时属 K.3（多方程）与 K.4（占位噪声）；pdf-272 同时属 K.1（多子问）与 K.4（占位噪声）。二者在「未解题清单」与闭包集合中各计 1 次，上表按根因维度交叉列出，不重复计入 61。
> **集合恒等式**：解出(9) + 未解题(52) = 61。未解题(52) = K.1(27) ∪ K.2(5) ∪ K.3(4) ∪ K.4(4) ∪ K.5(2) ∪ K.6(5) ∪ K.7(7)，各类求和 54，去重交叉项（pdf-010 同时属 K.3/K.4、pdf-272 同时属 K.1/K.4，各仅计 1 次）后 = 52，差集为空，0 静默遗漏。
>
> **【P1-4 后续更新】** K.2 中的 pdf-415 / pdf-535 / pdf-543 已由 P1-4 自由填空概念求解器解出（见 §L.1），该时点现行未解题 = 53 − 3 = 50。
> **【judge134 后续更新】** 原列于 K.6 的 pdf-134（judgment）已由后续 T3 判断陈述事实策展解出（JS-027~041，见 K.0），从 K.6 移入 K.0；K.8 上表与集合恒等式已同步更新为 9 解出 / 52 未解题。计入 P1-4 的 3 道后，**现行未解题 = 52 − 3 = 49**。[unsolved_registered_ids.txt] 与 [t8_partition.*] 为 P1-1/T8 交付时点闭包快照（61 全覆盖、0 静默遗漏依然成立），未随后续增益重写。
> **【vsepr509 后续更新】** pdf-509（vsepr，AsCl₃ 键角比 BCl₃ 小的原因）已由后续路由修复解出（`case "vsepr"` 兜底到已存在的 `TrySolveBondAngleReason`，见 K.0）。**该题不在原 61 闭包清单内**（闭包快照 `unsolved_registered_ids.txt` / `t8_partition.txt` 均不含 pdf-509，已核对），属闭包后新发现并修复的引擎缺口，故 K.0 解出清单由 9→10，但 K.8 上表的「解出 9 / 未解题 52」与集合恒等式「解出(9) + 未解题(52) = 61」仍按 61 闭包快照保持成立（pdf-509 不参与该 61 的加减）。闭包快照文件未重写。
> **【routing-fallbacks 后续更新】** d5-1 / d5-2（calculation，标况相态陷阱：SO₃ 标况为固体、己烷标况为液体 → 22.4 L 非 1 mol 气体）与 d7-4（structure_verify，手性碳事实）已由后续路由兜底修复解出（`case "calculation"` 末尾兜底 `BuildTrapResponse`、`default` 分支兜底 `ConceptFactSolver.TrySolve`，见 K.0）。**这 3 道均不在原 61 闭包清单内**（闭包快照 `unsolved_registered_ids.txt` / `t8_partition.txt` 均不含，已核对），属闭包后新发现的引擎路由缺口，故 K.0 解出清单由 10→13，但 K.8 上表「解出 9 / 未解题 52」与集合恒等式「解出(9) + 未解题(52) = 61」仍按 61 闭包快照保持成立（这 3 道不参与该 61 的加减）。性质：这 3 道生产中 LLM 本已答对（`pureLlmCorrect=True`）但引擎返回 null → 修复后引擎判正（`gatewayUsedEngine` False→True、`gatewayCorrect` True 保持），是**引擎覆盖率增益**（engineSolved +3），净 `solved` 计数不变。全库 harm=0 / precision=1 保持：聚焦 eval `routing_focused`（仅重跑 3 道，余 754 道逐字沿用基线）LOST=[] / harm=0 / prec=1；全量 eval `routing_fallbacks`（757 道 live-LLM）harm=0 / prec=1，且所有 id 翻转均为 `engUsed` 不变的 LLM 侧纯方差，与代码改动无关。闭包快照文件未重写。
>
> **【已知引擎缺口·留待 Spec Mode】** pdf-134 子句 (5)(11) 的方程因 U+2212（MINUS SIGN）未被 `EquationCorrectnessJudge.NormalizeEquation` 归一化为 ASCII `-` 而解析失败 → 引擎返回 `null` → 走 `CompositeStatementJudge` 事实回退。**不得**仅修 U+2212 归一化：子句 (11) 的方程配平正确但语境错误（过量氧化剂→不彻底氧化），若无 `CorrectnessContextRules` 配套规则，单修复归一化会使 (11) 解析后判 √ = **harm**。该缺口留作后续独立 Spec（方程语境正确性规则 + Unicode 归一化），本轮不碰。

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
| 多部分 + 图像 | 4 | pdf-215, 391, ~~413~~（dr1 已恢复题面并判正 GAIN +1，见 §AA）, 528 | 范围外 → P1-2 / P2-3 |
| 数据缺陷（答案错位/题面碎片/裸字母键） | 5 | pdf-165, 172, 180, 333, 377 | 范围外 → 数据管道键修复（§E） |
| 有机官能团/命名（缺分子图+缺命名求解器） | 8 | pdf-322, 323, 324, 519, 520, 521, 522, 523 | 范围外 → 图像恢复 + 新命名求解器（未来 spec） |
| 概念事实重叠/重复题录 | 1 | pdf-483 | 范围外（§L.2 已排除）→ 数据去重 |
| 装置命名（缺图标号） | 1 | pdf-534 | 范围外 → 图像恢复 |
| 自由文本分析（无对应求解器） | 1 | pdf-537 | 范围外 → 概念解释求解器（未来 spec） |
| 题面碎片 | 1 | ~~pdf-161~~（dr1 已恢复题面，见 §AA；键对齐，引擎仍 null） | — |

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

P2-1 仅示范 4 个有明确 knowledge_id/rule_id 真值源的 solver（ConceptFact / ReactionKb / 原子结构 / VSEPR）。以下生产 `EngineResponse` 构造 / `DerivationSteps` 写入点，除下表 ✅ 标记的 12 个（**Tier 1**：CrystalCellValidator / HalfReactionBalancer；**Tier 2**：HydrolysisPhCalculator；**B-外置批**：KnownEquationWriter / ProductWriter / RedoxGateway / ConceptVerdict；**B-judge-API 批**：MultiSubCorrectnessSolver / JudgmentStatementAggregator；**B-Impurity 批**：ImpurityTrapSolver；**B-MpDispatch 批**：MultiPartDispatch；**B-LlmMain 批**：LlmGatewayService 主体）外**未填充 `ReasoningNodes`**，保持 `DerivationSteps` 字符串（spec CQ-1 选项 A：增量迁移，表面积可控、零回归）。迁移留待 P2-1.x / P2-2（evidence_level 消费方落地后按优先级推进）：

| 生产者文件 | 构造点（核实行号） | 真值源（已核实） | 分级 | 阻塞项 / 解封动作 |
|---|---|---|---|---|
| [LlmGatewayService.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) | **66 处** `new EngineResponse`（登记原写 ~40，已核低估）：~19 处为子求解器传播（`FinalResult=<x>.FinalResult`：L407/431/543/942/958/973/988/1003/1018/1033/1048/1064/1080/1096/1112/1128/1145/1159/1235）；~38 处为专用计算器/硬编码分支（L755/774/788/801/862/917/1225/1281/1300/1346/1368/1390/1430/1448/1477/1517/1594/1645/1738/1794/1981/2036/2105/2174/2236/2277/2307/2333/2458/2479/2496/2522/2541/2565/2582/2606/2652/2680）；其余为守卫/空响应 | ✅ 已迁移 (B-LlmMain 批)：主文件 66 处 + 4 分部（Graph/Curve/Molecule/MultiPartDispatch）共 **71 site 全处置——有效 THREAD 24 / LEAVE 47 / 0 漏线程**（详见 `unified-reasoning-dag-b-llmmain/tasks.md` Task 5 审计表）。传播点用 `GatewayReasoningNodes.Clone` 把子响应节点复制上来（子留空则克隆空，R4 不臆造）；读 JSON 常数的挂 `KnowledgeId`（acid-constants / precipitate-types / standard-state-phases）；金属活动性 / 强氧化性酸不生 H2 / Fe 稀酸价态等硬编码规则外置或挂锚（activity-series / redox-rules / knowledge.json KH-IN-06）；纯计量 / 符号引擎 / 引擎结果无锚点诚实留空（C）。仅加性，不改任何判决字段。eval `after_b_mpdispatch`→`after_b_llmmain` diff 全零（GAINED/LOST/WORSEN/harm=0）、411 例 solved-but-nodeless 推理节点 0→>0、757 例 `gatewayConclusion` 字节不变 | A·done | — |
| [LlmGatewayService.MultiPartDispatch.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.MultiPartDispatch.cs) | 1 处（L54） | ✅ 已迁移 (B-MpDispatch 批)：聚合各子问 `CallEngineAsync` 响应的 `ReasoningNodes` 链进父响应（子节点 `Step` 重排连续 + `Description` 标注子问归属 + 末尾 1 汇总节点：`Conclusion`=聚合 FinalResult / `Premises`=去重非空 KnowledgeId）；子全空则父留空（R4 不臆造汇总）；仅加性，不改 `Ok`/`FinalResult`/`DerivationSteps`/`ConstraintNote`/`Confidence`/采纳门/`TaskType`。eval `after_b_impurity`→`after_b_mpdispatch` diff 全零（`multi_part`=0 分发路径不触发，平凡零影响）、传播正确性靠 `MultiPartReasoningNodes` 单测（6 例）保证、0 案例 `gatewayReasoningNodesCount` 变化 | A·done | — |
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
- **分级统计**（13 个生产者）：已迁移 A=12（Tier 1：CrystalCellValidator/HalfReactionBalancer；Tier 2：`HydrolysisPhCalculator`；**B-外置批**：KnownEquationWriter/ProductWriter/RedoxGateway/ConceptVerdict，eval `after_b_ext` diff 全零、恰 13 例推理节点 0→1、R1 3 例保持 0；**B-judge-API 批**：MultiSubCorrectnessSolver/JudgmentStatementAggregator，eval `after_b_ext`→`after_b_judge` diff 全零、恰 11 例推理节点 0→>0、3 例 R4 留空、14 例 gatewayConclusion 字节不变；**B-Impurity 批**：ImpurityTrapSolver，eval `after_b_judge`→`after_b_impurity` diff 全零、d9-1 推理节点 0→1、gatewayConclusion 字节不变；**B-MpDispatch 批**：MultiPartDispatch，eval `after_b_impurity`→`after_b_mpdispatch` diff 全零、`multi_part`=0 平凡零影响、传播靠 `MultiPartReasoningNodes` 单测（6 例）保证、0 案例节点计数变化；**B-LlmMain 批**：`LlmGatewayService` 主体 71 site（24 THREAD / 47 LEAVE / 0 漏线程），eval `after_b_mpdispatch`→`after_b_llmmain` diff 全零、411 例推理节点 0→>0、757 例 `gatewayConclusion` 字节不变、无臆造无回归）；B=0；混合 0；C=1（`RedoxRatioCalculator`，诚实留空）。
- **更正的登记错误**：①Redox「氧还规则有 rule id」→ 实为硬编码无 id；②ProductWriter「可挂 RuleId」→ 无 id；③KnownEquation「已知方程模板有 id」→ 无 id；④`LlmGatewayService`「~40 处」→ 实测 **66 处**。另 `ImpurityTrap` 标「无知识源」不准（化学事实硬编码），`HydrolysisPh` 标「中」低估（已读 JSON 可挂 id）。
- **建议执行顺序**：(1) ✅ 重判 A 类 `HydrolysisPhCalculator` 已迁移（Tier 2，eval `after_t2_ph` diff 全零、pdf-517 推理节点 0→1、无臆造无回归）；(2) ✅ **B-外置批已完成**（KnownEquation / ProductWriter / RedoxGateway / ConceptVerdict，eval `after_b_ext` diff 全零、恰 13 例 0→1、R1 保持 0、无臆造无回归）；(3) ✅ **B-judge-API 批已完成**（MultiSubCorrectnessSolver / JudgmentStatementAggregator：裁决契约加性暴露命中 id + `CorrectnessContextRules` 外置，eval `after_b_ext`→`after_b_judge` diff 全零、恰 11 例 0→>0、3 例 R4 留空、14 例 gatewayConclusion 字节不变、无臆造无回归）；(4) ✅ **B-Impurity 批已完成**（ImpurityTrapSolver：化学事实外置为 `data/knowledge/impurity-traps.json`（id=`impurity-kmno4-ethylene-co2`），命中线程化 `KnowledgeId`，eval `after_b_judge`→`after_b_impurity` diff 全零、d9-1 推理节点 0→1、gatewayConclusion 字节不变、无臆造无回归）；(5) ✅ **B-MpDispatch 批已完成**（`LlmGatewayService.MultiPartDispatch` 子问推理节点传播进父响应，eval diff 全零、`multi_part`=0 平凡零影响、靠单测保证、无臆造无回归）；(6) ✅ **B-LlmMain 批已完成**（`LlmGatewayService` 主体 71 site 全处置：有效 THREAD 24 / LEAVE 47 / 0 漏线程，eval `after_b_mpdispatch`→`after_b_llmmain` diff 全零、411 例推理节点 0→>0、757 例 `gatewayConclusion` 字节不变、无臆造无回归）；全部 ReasoningNodes 迁移批次完成（仅 `RedoxRatioCalculator` 诚实留空 C，无锚不臆造）。每步均须 `diff_ckpt` 全零 + 命中路径发节点、无锚路径留空的自检。

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

---

## K 类：data-repair-engine-exp-batch1（修键 + 引擎扩展并行）

> 范围：53 道 bothWrong（基线 `snapshot_after_expop`：gwCorrect=704，bothWrong=53，harm=0，precision=1）。
> 分流：Stream A = 修键清障（官方源确认的错误/污染/垃圾键）；Stream B = 引擎扩展（文本完整、无图、键正确的可解题补事实拿 gwCorr）。
> 红线：precision=1、harm=0、LOST=[]、不蒙数、不改 `question`/`options`/`note`/`type`（Stream A 仅改 `answer`/`answerKeywords`）、不碰已判正题、不重跑 `build_final.py`、不处理图像依赖题。
> 审计工件：[_evalscratch/datarepair_engine_audit.md](file:///D:/github/CogniThink-Chem/_evalscratch/datarepair_engine_audit.md)（53 题三分类全覆盖 + 证据）。

### K.1 Stream A 修键（3 条，官方源唯一确认）

| id | 原值 → 新值 | 官方源 | 化学佐证 | eval 影响 |
|----|-------------|--------|----------|-----------|
| pdf-100 | `A,B,C,D` → `B`（ak0 `A`→`B`） | 答案册 `page_37` num=2 | 解析**正文**（非被 OCR 污染的「正确/错误」标签）：B 项「酒精灯移至湿棉花下方实验效果更好」与正文「湿棉花移至下方则无法提供足够温度，实验效果差」直接矛盾 ⇒ B 为错误陈述；题干问「说法错误的是」，A/C/D 均正确 | 见 K.2（同时引擎解出，GAIN +1） |
| pdf-107 | `D` → `2Al2O3(熔融) --(电解/冰晶石)→ 4Al+3O2↑` | 答案册 `page_40` num=5 第(1)子问 | 题干为 LAEM 提镓大题子问(1)「'电解'中反应的化学方程式」；原 `answer='D'` 及 note 为离子共存题污染（答非所问）；p40n5 答案(1)即上式 | 引擎仍 `null`（方程域 KB 缺口），**GAIN=0**，纯真值修正 |
| pdf-162 | （Pb 大题 7 段复合串）→ `c`（ak0 → `c`） | 答案册 `page_57` num=5 第(3)子问 | 题干为「(3)'酸化'步骤应选用的酸」（选项 a 稀硫酸/b 浓硫酸/c 盐酸/d 磷酸）；原 answer/ak 是另一道 Pb 大题 7 子问串（跨题污染）；p57n5 第(3)子问解析「应选盐酸，**c 正确**」 | 键修复时 GAIN=0（纯真值修正）；**hard-bone-data-repair-4 策展 GC-772-A~D 后自解，GAIN +1**（eval hardbone3→hardbone4：GAINED=[pdf-162]、LOST=[]、harm=0、precision=1） |

### K.2 Stream B 引擎扩展（2 条，GAIN +2）

| id | 键状态 | 扩展内容 | 结果 |
|----|--------|----------|------|
| pdf-100 | Stream A 先修为 `B` | `experiment-facts.json` 加 `EF-100-A/B/C/D`（stemMatch `铁与水蒸气反应` 全库唯一，blast-radius 仅命中 pdf-100 各对应选项）：A 方程式真 / B「移至湿棉花下方效果更好」假 / C 点燃肥皂泡验 H2 真 / D 硬质玻璃试管盛铁粉真；题干「说法错误的是」⇒ 选错 ⇒ B | `错误选项为 B`，gwCorrect=True，**GAIN +1** |
| pdf-406 | 键已正确（A.1 / 5b：`A,B,C,D`→`C`） | ① `FactChoiceMatcher.WrongDir` + `MultiSelectChoiceValidator.WrongDir` 同步加「不能完成」token（全库仅 pdf-406 题干命中，零暴露面）——否则 askDir 误判 correct，能完成项 A/B/D 凑 3 候选；② `experiment-facts.json` 加 `EF-406-A/B/C/D`（stemMatch `不能完成` 唯一）：A/B/D 能完成（真）、C 乙醇制乙烯需 170℃ 无温度计不能完成（假）⇒ 选错 ⇒ C | `错误选项为 C`，gwCorrect=True，**GAIN +1** |

**关键回归拦截（实测发现并修复）**：仅给 `FactChoiceMatcher` 加「不能完成」会让 `MultiSelectChoiceValidator`（遍历全事实库 + 自带 WrongDir）把 pdf-406 误判为「正确选项为 ABD」→ harm。已给 `MultiSelectChoiceValidator.WrongDir` 同步加 token，使 pdf-406 选错方向仅 C 为假（候选 1 < 2 ⇒ 返回 null，不蒙数）。`MultiSelectChoiceValidatorTests.FullBank_AnyMultiSelectConclusion_GradedCorrect_NoHarm` 全库护栏由红转绿。

### K.3 红线验证（eval diff `after_expop` → `after_datarepair_engine`）

- gwCorrect **704 → 706**（+2）；bothWrong **53 → 51**
- **LOST=[]**、harmCount=**0**、enginePrecision=**1**
- 已判正题 `gatewayAnswer` 字节级变更 = **0**
- GAINED 精确 = {pdf-100, pdf-406}，无额外漂移
- 单元测试：443 个 choice/fact-matcher 护栏测试全绿（含 Batch2/3 真值表、MultiSelect 全库零 harm、Concept 无重叠、计数护栏）

### K.4 本批排除（49/53），逐项理由见审计文档

- 图像依赖（装置/曲线/结构/晶胞/流程图）21 道 → 图像轨道；
- 键正确但属引擎能力缺口（氧还电子计数/多步推理/多子问）→ 需专用求解器，超「优先事实」范围（如 pdf-075，p26n4 确认键 B 正确，需 redox 求解器）；
- 键已订正（[官方源订正]/[千问求解]）仍 bothWrong，因图像/引擎缺口 12 道；
- 跳过：pdf-180/165（子问精确答案不在 OCR）、pdf-197/414/333（题干拼接/截断，需原页复原）、pdf-263（题型矛盾）、pdf-325（选项 OCR 损坏）、pdf-138（图像）、pdf-190（多子问+OCR 矛盾）——零蒙数纪律下需页图 VL 或题干复原，本批不做。

> 注：`SolveStructuredEnricherTests.cs` 为并发 chemical-simulator 轨的未跟踪 WIP，与本批无关；其既有编译错误（`Assert.Equal` 参数类型不匹配）非本批引入，本批测试时临时移出编译路径、跑完已还原，未改动其内容。

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

## U 类：分子资产（molecule）提取缺陷——molname 批次（spec: fill-calc-coverage Task 4）

> 背景：本批审计 7 道「有机官能团 / 系统命名」填空题（pdf-323/324/519/520/521/522/523）。分子结构题的真实锚点是 `imageAssets` 中的 molecule 资产（SMILES/molblock，由 vectorize 图像管线提取）；题干文本与 `note` 字段均不可靠作锚点（pdf-323 实证 note 与答案直接矛盾；pdf-522 实证 note 写 2,4,6 但资产 SMILES 实编码 2,4,5）。本类登记「资产提取缺陷」与「资产缺失」两类，前者按图像真值订正数据、后者诚实留空待图像轨道接续。验证基线：本环境 eval `llmAvailable=False`，纯确定性引擎路径，`cmp after_stoich → after_molname` GAINED=[pdf-522]、LOST=[]、precision=1、harm=0。

### U.1 已修复（数据订正 + 引擎扩宽，pdf-522，GAIN +1）

| id | 缺陷 | 修复 | 证据 |
|----|------|------|------|
| pdf-522 | **molecule 资产提取缺陷**：资产 `name`/`note` 字段写「2,4,6-tribromo phenol」，但提取的 SMILES `Oc1c(Br)cc(Br)c(Br)c1` 与 molblock 实际编码 **2,4,5-三溴苯酚**（两个 Br 相邻），与原图不符。叠加引擎侧 `MatchMolecule` 旧版只比极性/杂化/官能团/分子式/不饱和度、**不比名称**，且 `BuildGatewayAnswerAsync` 对 taskType=unknown 返回 null → 命名题不命中。 | ① 按原图 `pdf_images/p112_n05_4.png`（核实为对称 2,4,6-三溴苯酚）订正 `pdf_questions.json` 与 `pdfs/questions_parsed.json` 的 pdf-522 SMILES→`Oc1c(Br)cc(Br)cc1Br` 并重算 molblock（[fix_asset_522.py](file:///D:/github/CogniThink-Chem/_evalscratch/fix_asset_522.py)，回读 Br 位号=(2,4,6)）；② 新增保守中文命名器 [MoleculeNamer.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Molecules/MoleculeNamer.cs)（仅单一取代基类型取代酚，不确定一律 null）；③ `MatchMolecule` 增加名称维度（引擎确定性命名是答案子串即命中）。 | 探针镜像判分+命名器扫全库 41 道分子题：仅 pdf-522 新命中，其余 40 道 name=None，零 stray；`cmp after_stoich after_molname` solved 645→646、molecule structuredHits 2→3；端到端单测 `Pdf522_修正资产_经网关_命名结构化命中` 守护数据回归。 |

> 防回归经验：① vectorize 提取的 SMILES 与资产 `name`/`note` 可能不一致——**真值以原图为准**，不盲信提取字段；② 命名器必须窄域（仅确定性可命名才返回非空），并用全库探针验证零 stray 后才接入判分。

### U.2 未修复（答案键疑错，无官方源不臆造，保持 null）

| id | 症状 | 为何不可自动修复 | 建议下一步 |
|----|------|------------------|-----------|
| pdf-519 | stem 指名「曲美托嗪」但无图/无 molecule 资产；答案键含「硫醚」。 | web 核实真实曲美托嗪（trimetazidine）分子式 C₁₄H₂₂N₂O₃ **不含硫**，答案键「硫醚」与真实结构矛盾，疑为错键；无官方答案册源不能定键，贸然迎合错键会引入 harm。 | 原页/答案册核对正确键；若确认键错并订正，且图像轨道恢复分子结构，可由 `MatchMolecule` 官能团维度自解；暂保留 `null`。 |

### U.3 未修复（缺分子资产提取，LEAVE-NOANCHOR，保持 null）

> 这 5 道的分子结构仅存在于未被 vectorize 提取的图像中，`imageAssets` 无 molecule 资产、题干文本未唯一命名分子、`note` 不可信 → 无真实锚点，引擎诚实返回 `null`，不臆造、不蒙数。

| id | 症状 | 为何不可自动修复 | 建议下一步 |
|----|------|------------------|-----------|
| pdf-323 | 官能团（氨基、羧基）+ 系统命名；题干截断，无图/无 molecule 资产；`note` 称硝基/酯基与存储答案（氨基/羧基）直接矛盾。 | 无分子结构锚点；note 与答案矛盾证明 note 不可信，不能以 note 推答案。 | 图像轨道恢复分子结构 → molecule 资产；文本轨道保持 `null`。 |
| pdf-324 | 高分子 `[CH=CH]n` 单体→苯乙炔；文本仅 `[CH=CH]n` 与答案矛盾，真实结构在未提取的图里，无资产。 | 文本推不出单体全名；无资产锚点。 | 图像轨道恢复单体结构图；暂保留 `null`。 |
| pdf-520 | 官能团（羟基、羧基、碳碳双键）；题干截断，无图/无 molecule 资产。 | 无分子结构锚点。 | 图像轨道恢复；暂保留 `null`。 |
| pdf-521 | 含氧官能团；`note` 指乳酸但 note 不可信，分子未在题干命名，无图/无资产。 | 无真实锚点；note 不可作锚。 | 图像轨道恢复；暂保留 `null`。 |
| pdf-523 | 官能团 + 习惯命名；`note` 指异丙醇但 note 不可信，分子未在题干命名，无图/无资产。 | 无真实锚点；note 不可作锚。 | 图像轨道恢复；暂保留 `null`。 |

---

## V 类：engine-vs-LLM 比对·本批数据缺陷修复留痕（2026-11-26）

> 触发：`_evalscratch/snapshot_engine_vs_llm_20261126.json` 比对暴露 4 类数据缺陷（5 个 id：pdf-146 / pdf-278 / pdf-355 / pdf-356 / pdf-337），其中 355/356 同属"题界拼接"一类。红线：precision=1 / harm=0 / LOSS=0，不蒙数、不逐题硬编码、不注入 verdict。
>
> **验证方法**：Release 构建后以 tag `verify_defects` 聚焦重跑——复用 752 条未变题目基线结果（`checkpoint_engine_vs_llm_20261126.jsonl`）作 checkpoint done，仅重跑 5 条改动题，对照基线逐例 diff。未跑全量回归前先逐题核对。
>
> **验证结果（757 题整体）**：`gatewayCorrect` 643→643（Δ=0，无引擎回归）、`engineSolved` 643→643（Δ=0）、`harmCount` 0→0（Δ=0）、`pureLlmCorrect` 627→626（Δ=-1）。唯一 pureLlmCorrect 翻转为 pdf-356，系**数据真值修正**（LLM 实际答错"五元环"，此前被游离关键词误判正），非回归。precision=1 维持。

| id | 缺陷 | 修复 | 证据 | eval 影响 |
|----|------|------|------|-----------|
| pdf-146 | 工作区漂移：`answer`/`note`/`answerKeywords` 被串改为别题（page_049 num=13 品红褪色判断的 `"×"`），本题实为 SO₂ 实验 5 子问填空 | 恢复为官方答案 `(1)分液漏斗 (2)排出装置中的空气 (3)BaSO3 (4)b (5)0.032`，同步 note/keywords；`questions_parsed.json` `ans_page` 49→52 | 答案册 `pdfs/_extract/answer/page_052.json` num=13 逐字匹配 5 子问；(5) 独立化学核算 n(I₂)=2.5e-5→m=1.6e-3 g→0.032 g/kg ✓。**注：HEAD 已含正确值，本次为工作区漂移恢复（`git diff` 对该 id 无变更）** | 无变化 |
| pdf-278 | 题干被截断：仅存 Ka 表格引言，5 个子问 (1)–(5) 缺失（answer/note/keywords 本已完整正确） | 用 exercise OCR 还原完整题干：`page_098` num=6（引言）+ `page_099` num=1–5（5 子问）；(5) 的 A–D 选项内联题干 | 5 子答案逐一核对答案册 `page_095` num=6：(1)HCN<H₂CO₃<CH₃COOH (2)CN⁻+CO₂+H₂O=HCO₃⁻+HCN (3)酸性（Ka2≫Kh）(4)pH=9.7 (5)c(CH₃COO⁻)=10⁻⁵−10⁻⁹、电离增大选 BD ✓ | pureLlm 由拒答（"请补充具体问题"，却含"酸性"误中关键词）→ 诚实作答；gatewayCorrect 不变 |
| pdf-355 | 题界拼接：填空（乙二酸+乙醇 1:2 酯化）+ 选择（结构性质对应关系）硬拼一条；`answerKeywords` 含游离 `"C"` 致任何含碳答案误判正 | 移除后半选择（题干+options），保留填空半；`answerKeywords`→`["CH3CH2OOC-COOCH2CH3 + 2H2O"]` | 填空答案核对答案册 `page_122` num=2 ✓；后半选择答案册 `page_123` num=2 = `A,B,C,D`（见 V.1 deferred） | pureLlmCorrect 由游离 C 误判正→诚实判正；gatewayCorrect 不变 |
| pdf-356 | 题界拼接 + 后半缺路线图：填空（乙二酸+乙二醇 ①环酯 ②缩聚）+ 选择（化合物 Z）；`answerKeywords` 含游离 `"C"` | 移除后半选择，填空半补全 ②缩聚句；`answerKeywords`→`["C4H4O4, (C4H2O3)n + (2n-1)H2O"]` | exercise `page_121` num=3+4；答案核对答案册 `page_122` num=3（六元环酯 C₄H₄O₄）✓；后半选择化合物 Z 答案册全库无对应（见 V.1 deferred） | pureLlmCorrect True→False：LLM 答"五元环"（错），此前游离 C 误判正；**诚实判错，数据真值修正**，gatewayCorrect 不变 |
| pdf-337 | 图像依赖：①~⑥ 卤代烃结构以图像呈现但从未挂到题录 → LLM 拒答却被误判，引擎文本无解 | 裁剪 `page_116.png` ①~⑥ 全结构图 → `data/pdf_images/p116_n01_0.png`，两数据文件加 `"images": ["pdf_images/p116_n01_0.png"]` | VL（qwen3-vl-plus）验证 ①~⑥ 标号+结构完整可见，⑥ 含末端 Br；结构与答案册解析（②④⑥ 可消去、①③⑤ 不可）一致 ✓ | pureLlm 由拒答误判→正确答出 C（②④⑥）；引擎文本不读图仍 null（诚实），gatewayCorrect 不变、无回归 |

### V.1 仍需官方原页/答案册的 deferred 项（不猜测、不自动修改）

| 剥离来源 | 内容 | 阻塞原因 | 解除条件 |
|----------|------|----------|----------|
| pdf-355 后半选择 | "下列物质结构与性质**不**具有对应关系的是" A–D（甲苯/乙醛/HCOOH/油酸甘油酯） | 答案册 `page_123` num=2 = `A,B,C,D`（四项全对），但题面问"不具有对应关系"与全对答案矛盾，疑 OCR 多一"不"字 | 需习题册原页图像确认确切题面（具有/不具有）；确认后可新建独立题录并按答案册键判分 |
| pdf-356 后半选择 | "化合物 Z 合成路线…下列说法正确的是" A–D（X/Y/Z 结构） | 答案册全库检索无该题答案，且 X/Y/Z 合成路线结构图未进入文本，无法定键 | 需用户提供该题原页（exercise 页）+ 答案册对应页，核对选项与键后新建题录 |

---

## W 类：数据缺陷修复·data-defect-repair 批次（2026-11-26）

> 触发：K.0 增补（batch4）登记的「数据缺陷 ~16」候选；本批次独立审计 `pdf_questions.json` 并修复高置信子集。红线：precision=1 / harm=0 / 引擎层 LOST=0，不臆造真值、不改引擎代码、不动 `images`。计划文档：[.trae/documents/data-defect-repair-plan.md](file:///d:/github/CogniThink-Chem/.trae/documents/data-defect-repair-plan.md)。

### W.0 核心发现（决定价值预期）

源码确认网关判分**纯由引擎真值结论驱动**：`BuildGatewayAnswerAsync`（[GatewayVsLlmEvaluationService.cs L763](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs#L763-L901)）从不采纳 `pureAnswer`；`GatewayCorrect = IsAnswerCorrect(gatewayAnswer) || StructuredHit`。全库扫描 `snapshot_gap_batch4`（`scan_conclusion_misscored.py`）：**引擎已产出 `FinalResult` 却被判负的题 = 0**——所有候选缺陷题引擎均为空结论（`Insufficient`）。

**因此仅修答案键不产生即时 gwCorr 增益**（引擎对这些题本就无解，修键不翻正也不丢失）。本批次价值：① 消除退化键造成的 pureLlm 假阳性、维护判分真值与指标可信度；② 为后续引擎扩展消除键位障碍。若要即时 gwCorr 增益，需另启引擎扩展批次为这些题型补 solver。

### W.1 已修复（Tier 1，5 题，高置信）

| id | 题型 | 缺陷 | 修复（answer / answerKeywords） | 正确答案证据 | 影响 |
|----|------|------|-------------------------------|-------------|------|
| pdf-117 | judge | 单条正误判断，键却是 14 个 √/×（含 √ 与 × 双侧 → 任意带 √/× 的回答均判正，假阳性） | `×` | NOTE 明确「(1)(×)…并非都是半导体材料」 | pureLlm 不变（LLM 答「错误」无 ×）；消除退化假阳性键 |
| pdf-164 | fill | 问「生成 BaTiO(C₂O₄)₂ 的化学方程式」，键却是 `(2)(NH4)2SO4… (3)Fe+Cu2+…`（跨题混排） | `BaCl2 + TiCl4 + 2(NH4)2C2O4 + H2O = BaTiO(C2O4)2↓ + 4NH4Cl + 2HCl` | pdf-160 答案子问(5) 原文，与本题 NOTE 吻合 | pureLlm 不变（LLM 用 H₂C₂O₄ 版本不命中） |
| pdf-172 | calc | 问「固定 CO₂ ___ L（标况）」计算题，键=`AC`（属另一道 Cr/Cu 电子排布选择题） | `89.6 L` | 产物 M=466 g/mol → 0.466 kg=1 mol → 4 mol CO₂ → 4×22.4=89.6 L | **pureLlm False→True ✅**（LLM 已答出 89.6 L，旧键 AC 未命中） |
| pdf-179 | fill | 问「用离子方程式表示置换加 Fe 的目的」，键=`B`（属另一道短周期元素选择题） | `Fe + Cu2+ = Fe2+ + Cu` | 与 pdf-164 答案子问(3) 及本题 NOTE 一致 | **pureLlm False→True ✅**（LLM 已答出方程，旧键 B 未命中） |
| pdf-324 | fill | 题干 `[CH=CH]n`（聚乙炔），单体键=`苯乙炔`（无苯基，与题干结构矛盾） | `碳碳三键, 乙炔` | 题干 `[CH=CH]n` 明证单体为乙炔 | pureLlm 不变（LLM 已答「乙炔/碳碳三键」命中保留的「碳碳三键」） |

`spike_key_fix.py`（移植 `NormalizeChemicalNotation`）验证以上 5 键：正确答案命中、错误答案不误命中（pdf-324 子串「乙炔」命中「苯乙炔」为系统固有子串取舍，且引擎不采纳 concept 结论，不影响 gwCorr/harm）。

### W.2 deferred（不猜测，不自动修改）

| id | 原因 |
|----|------|
| pdf-084 | 题干完整，问 2 个方程式，键=`C`（退化）。正确方程可推导，但 NOTE 表述自相矛盾、与 LLM 快照答案不一致，需核对原题标准答案后再修，避免写入错误真值 |
| pdf-190 | 4 子问完整，键=`(1)正四面体,(2)sp3`（属另一份 VSEPR 题）。需为 4 子问分别写键且走 multipart AND 门，引擎当前不解；逐子问标准答案需核对，风险高于收益 |
| pdf-263 / pdf-376 / pdf-197 / pdf-306 | **选择题**，改键风险极高（键本正确而按 LLM 错答改键=污染真值）。pdf-263 问「正确的有几项」与键 B 不符；pdf-376 NOTE 称「全部正确」与问「不正确的是」矛盾；pdf-197 题干两题拼接键=ABCD；pdf-306 依赖图像且 NOTE 自相矛盾。选择题键一律需原题答案核对 |
| pdf-160 | 题干仅存引言「…流程如下：」，子问 (1)~(5) 缺失（原在图中）；其答案为 pdf-164 等题的混排源。题干不完整，纯 JSON 不可修，需原图/OCR 恢复 |

### W.3 排除（缺结构图，纯 JSON 不可修）

pdf-322 / pdf-323 / pdf-519 / pdf-520 / pdf-521 / pdf-523：题干仅「中官能团名称为______」，无结构图、`images` 为空。需图像资产恢复，另案处理。

### W.4 验证证据（零回归）

- `dotnet build -c Release`：0 错误 0 警告。
- `dotnet test -c Release`：2623 passed / 0 failed（TutorBrain 1695 + Chem 928），与基线一致，全库护栏无回归。
- **构造性零回归证明**（`prove_zero_regression.py`）：精确比对 git HEAD 旧版与新版，确认**只有 5 题**的 `answer`/`answerKeywords` 变化；这 5 题在 `gap_batch4` 基线中 `gatewayAnswer` 全为空、`gatewayCorrect=False` → 改键后仍为 False（空答案对任何键判否、无结构化命中）；其余 752 题键与引擎输出均未变 → 引擎侧逐题一致，**LOST=[] / GAINED=[]**。
- **pureLlm 方向验证**（`recompute_purellm.py`，用基线缓存 `pureLlmAnswer` + 新键重算，题干未变故 LLM 答案不变）：5 题 pureLlm 合计 1→3（**+2，恰为 pdf-172/179 翻正**），pdf-117/164/324 不变。
- `verify_mp27`：27 守卫题与改动 5 题无交集、键均未变 → 由零回归证明自动满足（27/27 保持 True）。
- **LLM-on 全量 eval 已完成**（tag `gap_datafix1`，757/757，failed=0，qwen3-vl-plus）：`diff_eval gap_batch4→gap_datafix1` **GAINED=[] / LOST=[]**；`verify_mp27` **27/27 PASS**。快照：[_evalscratch/snapshot_gap_datafix1.json](file:///d:/github/CogniThink-Chem/_evalscratch/snapshot_gap_datafix1.json)。

**实际结果（已核对）**：gwCorr 663→663（Δ=0）、engineSolved 663→663、gatewayTruthCorrect 663→663、harm 0→0、precision=1 维持。pureLlm：**pdf-172 / pdf-179 确认 False→True**（如预期），pdf-117/164/324 不变；总 pureLlmCorrect 633→633（Δ=0）系 LLM 非确定性漂移在其余题双向抵消所致（与数据改动无关，按既有口径不作回归依据），关键信号——目标题翻正 + 引擎零回归——完全成立。

---

## X 类：引擎确定性补强·engine-gap-batch5（2026-11-26）

> 触发：审计 `snapshot_gap_datafix1` 的 94 leads（pureLlmOnly 50 ∪ bothWrong 44），锁定「单题·无图·LLM 已答对·引擎空结论」的可抢救子集。红线：precision=1 / harm=0 / 引擎层 LOST=0 / mp27=27/27 不退化；不臆造真值、不动 `images`、不改引擎判分逻辑；每项改动先全库零假阳性扫描，无法零 FP 即剔除。计划文档：[.trae/documents/engine-gap-batch5-plan.md](file:///d:/github/CogniThink-Chem/.trae/documents/engine-gap-batch5-plan.md)。

### X.1 实测结果（tag `b5part2`，Production/LLM-off 纯引擎）

- gatewayCorrect **663 → 669（+6）**；precision=1、harm=0、LOST=0；mp27 27/27 PASS。
- 单元测试全绿：Chem 928 / TutorBrain 1695（ConceptFact 护栏锁同步：全库触发集 +{pdf-324,pdf-477,pdf-483} 且 NOT CORRECT 为空；条目数 34→36）。
- 证据：`_evalscratch/snapshot_b5part2.json`；`diff_eval gap_datafix1→b5part2`（GAINED 6 / LOST 0）；`verify_mp27 gap_datafix1 b5part2`。

### X.2 已解（GAINED，逐题）

| id | 期望 | 修复手段 | 引擎输出（判正） |
|----|------|---------|-----------------|
| pdf-117 | × | 分类→judgment（`ClassifyTaskDeterministic` 加「则/都是 + 空括号」线索）+ `concept-verdicts.json` 加 ⅣA族单质都是半导体材料→× | `×` |
| pdf-172 | 89.6 L | 分类→calculation（加「标准状况 + 体积/L/升」线索）+ `StoichiometrySolver` 新增 mode G「化合物质量→基团→标况体积」 | `相当于固定 CO2 的体积为 89.6 L（标准状况）` |
| pdf-179 | Fe + Cu2+ = Fe2+ + Cu | `product-rules.json` 加 `pw-fe-cu2-displacement` + `MatchProductRule` 加指纹「置换+加入+Fe+方程式」 | `离子方程式：Fe + Cu2+ → Fe2+ + Cu` |
| pdf-177 | …(3)Fe + Cu2+ = Fe2+ + Cu… | 同上指纹命中（pdf-177 子问(3) 同为加 Fe 置换铜） | `离子方程式：Fe + Cu2+ → Fe2+ + Cu` |
| pdf-324 | 碳碳三键, 乙炔 | `concept-facts.json` 加 `fact-polyacetylene-monomer`（`[ch=ch]n`+官能团+单体 唯一命中） | `碳碳三键, 乙炔` |
| pdf-483 | 降低熔点 | `concept-facts.json` 加 `fact-molten-becl2-nacl-lower-mp`（电解熔融/氯化铍/氯化钠+作用 唯一命中） | `降低熔点` |

### X.3 deferred / 剔除（不硬上，附实测原因）

| id | 原因 | 去向 |
|----|------|------|
| pdf-164 | 子问题文本仅 66 字，只给产物 `BaTiO(C2O4)2`；反应物（BaCl2/TiCl4/(NH4)2C2O4/H2O）在母题流程上文，子问分发时被剥离 → 无骨架可配平 | 转「多子问母题上下文恢复」专项 |
| pdf-171 | 产物 `Mg5(CO3)4(OH)2` 不在题干，反应物也未以化学式给出（仅「二氧化碳」文字、「碱性」）→ 需语义推断，高误触发风险 | 同上专项 |
| pdf-462 | 子问题文本仅 55 字，只给产物 `AlOOH`；反应物（Al3+/H2O）被剥离 → 无骨架可配平 | 同上专项 |
| pdf-541 | ConceptFact 护栏要求全库唯一命中；特征 token「led」在子问分发时被剥离，去掉则与 pdf-414 全文歧义（会使 pdf-414 全文触发判负，违反护栏）。1 题低 ROI 且与护栏冲突 → 剔除 | 转上下文恢复/数据修复 |

> **Cluster 2 关键实测**：探针确认 `EquationBalancer` 对 pdf-164/171/462 的方程均配出与期望完全一致的系数，`FormulaParser` 支持 `BaTiO(C2O4)2`、`Mg5(CO3)4(OH)2·4H2O`、`AlOOH`。**配平器不是瓶颈，反应物上下文缺失才是**。一个「要求反应物+产物均显式可解析」的散文抽取器对这三题触发数为 0 却会引入全库误触发风险（0 收益 + 风险），故本批不实现；根因是「多子问母题上下文恢复」架构缺口，应与 C 类多子问杠杆一并另立专项。

### X.4 红线验证（零回归）

- 分类器改动（最高风险）：全库 detTask 比对无 X→judgment/calculation 外溢；目标题在 Production 下靠新增线索可达对应分支。
- 所有新 solver/事实走 `IsAnswerCorrect` 采纳门，错误结论不被采纳 → harm 天然 0；ConceptFact 护栏测试保证凡触发必判正。
- 与 W 类数据修复（键订正）解耦：本批不改 `answer`/`answerKeywords`，只加 solver 与事实；目标题的正确键已由 W 批次就位（pdf-117/172/179/324）。

---

## Y 类：引擎确定性补强·engine-gap-batch6（2026-11-26）——窄补强国境耗尽审计

> 计划文档：[engine-gap-batch6-plan.md](file:///d:/github/CogniThink-Chem/.trae/documents/engine-gap-batch6-plan.md)。证据脚本/输出均在 `_evalscratch/`（gitignore）。本批**不改任何生产代码 / 题录 / 答案键**，仅做审计留痕 + 基线锁定。

### Y.1 漏斗：88 leads → 23 候选 → 0 幸存

基线 `b5part2`：gwCorr=669/757，precision=1，harm=0，LOST=0，mp27=27/27。leads = pureLlmOnly(45) ∪ bothWrong(43) = 88。

剔除 `B5_SOLVED` / `X_DEFERRED` / `W_DEFERRED` / 图像题 / 选择题后剩 **23 候选**。对 23 候选做全库（完整 757 题 checkpoint，非仅 pdf_questions.json 的 545 条）零 FP 扫描 + 语义复核，**0 个「单题·无图·答案洁净短文本·可零 FP 键控」的安全候选** → 窄 solver/fact 补强国境耗尽。

### Y.2 23 候选排除根因聚合（题级细节已在 K/M/U/E/W/X 留痕，此处不重复）

| 排除根因 | 题 id | 数量 |
|---|---|---|
| 多子问 AND 门（含不可约自由文本/缺陷子问，无一「只差 1 子问」） | pdf-207, 010, 097, 166, 484, 176, 198 | 7 |
| 选择 / 裸字母键污染（答案单字母，无/残选项） | pdf-070, 180, 333, 377 | 4 |
| 缺结构图（官能团题，结构被剥离；`vector_svg/` 无对应分子图） | pdf-322, 323, 519, 520, 521, 523 | 6 |
| 子问上下文被剥离（母题流程缺失） | pdf-464 | 1 |
| 自由文本推理（无唯一确定结论） | pdf-163, 537 | 2 |
| 答案键与题干不对齐 / 跨题污染（数据缺陷） | pdf-030, 161, 165, 198(子2 裸键 "C") | 3（与多子问类交叉，不重复计） |

### Y.3 关键全库证据（universe = 757 题）

| 探针短语 | 命中 | 证明 |
|---|---|---|
| `中官能团名称为` | **4 题**（pdf-322/323/519/523，答案各不相同） | 结构图被剥离，**通用 fact 键控必跨题碰撞** → 不可解 |
| `调 pH` | 3 题（pdf-168/463/464） | pdf-464 是子问碎片，母题上下文缺失，无唯一锚点 |
| `焙烧` | 5 题 | pdf-163 子问母题上下文跨多题，无唯一锚点 |
| `曲美托嗪` | 1 题（pdf-519） | 唯一命中，但答案键「氨基、硫醚」与真实曲美他嗪（C₁₄H₂₂N₂O₃，不含硫）矛盾 → **键本身存疑**（亦见 U.2），迎合错键 = harm，不臆造 |
| `25.00%` | 1 题（pdf-030） | 题干用变量 a/b 要求「列计算式不化简」，答案却为固定数字 → 键题不对齐（亦见 K.2） |

### Y.4 结论与下一轮方向

1. **窄增益枯竭**：23 候选全部命中既有 NO-GO 类别，全库扫描确认无法零 FP 键控；继续硬做要么 0 收益，要么放宽守卫引入假阳性 → 违反 precision=1。
2. **配平器非瓶颈**（继承 X.3 实测）：阻塞是反应物上下文剥离 → 架构缺口，非窄 solver 可解。
3. **架构转向两条轨道**（需各自独立 spec，不在本批启动）：
   - **轨道 1（推荐）**：多子问母题上下文恢复 —— 近端已验证收益（pdf-164/171/462 配平器可用，缺上下文，潜在 +3），全程在确定性文本引擎内，红线可由全库零 FP 扫描守住。
   - **轨道 2**：图像 / 结构 ingest —— 解锁 ~51 图像依赖题 + 6 官能团题，但引入视觉/OCR 误差面，precision=1 难保证；且官能团题需先修错键（pdf-519）。

### Y.5 红线验证（零回归，构造性）

- 本批**不碰 `src/` / `data/` / `tests/`** → precision=1 / harm=0 / LOST=0 由构造保证。
- 基线锁定：重跑 Production（LLM-off）eval 打 `checkpoint_b6baseline.jsonl`，与 `b5part2` diff 预期 GAIN=0 / LOST=0 / harm=0，`verify_mp27` 27/27。
- 无 `_Probe*` / 探针 .cs 残留（本批仅 Python 证据脚本，均在 `_evalscratch/`）。

---

## Z 类：轨道1·多子问母题上下文恢复探针（2026-11-26）——机制验证净负，回滚收口

> spec：[.trae/specs/multipart-context-recovery/spec.md](file:///d:/github/CogniThink-Chem/.trae/specs/multipart-context-recovery/spec.md)。证据均在 `_evalscratch/`（gitignore）。本批**最终不改任何生产代码 / 题录 / 答案键**（探针临时改动已全部回滚，`git diff` 内容为空）。

### Z.1 动机
batch6（Y 类）确认窄 solver/fact 增益枯竭，推荐架构转向轨道 1（多子问母题上下文恢复）。根因：`LlmGatewayService.MultiPartDispatch.TryDispatchMultiPartAsync` 逐子问请求只含子问碎片，丢弃了第一个 `(N)` 标记前的大题引语 → 子问求解器缺工艺流程/反应物上下文。X.3 实测配平器可用、缺的是上下文。

### Z.2 探针设计与结果（全库 757 题，Production LLM-off）
在 `MultiPartDispatch` 加环境变量开关 `COGNITHINK_REFILL_SUBQUESTION_CONTEXT=1`，默认关闭（=基线）。两轮：

| 变体 | GAIN | LOST | mp27 | harm | summary precision |
|------|------|------|------|------|-----------|
| b7_on（引语同时喂分类器 + 求解器） | 0 | pdf-278 | 27/27 | 0 | 0.9985（gwCorr=668, engineSolved=669） |
| b7_gated（分类用原子问，引语仅喂求解器） | 0 | pdf-278 | 27/27 | 0 | 1（gwCorr=668, engineSolved=668） |

两轮 gatewayCorrect 均为 668（基线 669 少 pdf-278）、harmCount 均为 0；summary precision=gatewayCorrect/engineSolved，两轮 engineSolved 计数口径微差致比值不同，安全精度（harm=0）两轮均成立。

两轮在 757 题上唯一差异 = pdf-278 判正→判负；其余 756 题逐字不变。最终回滚后 `b7_final` 与 `b6baseline` diff：GAINED(0) / LOST(0)，mp27=27/27，precision=1，harm=0 → 基线完全恢复。

### Z.3 回归根因：pdf-278 答案范围漂移（scope drift）
pdf-278（5 子问）基线由 multi_part 分发解出。引语回填后，子问 (1) 求解器从引语的**完整 Ka 表**吸入题未问的 HClO、H2C2O4，输出由「HCN < H2CO3 < CH3COOH」变为「HCN < HClO < H2CO3 < CH3COOH < H2C2O4」→ 不再命中答案键 → AND 门判负。化学排序本身正确，但评测走严格答案键匹配，「过度产出」即判负。门控分类无法消除（求解器仍从 RawText 读到引语）。→ **引语回填的固有机制风险**。

### Z.4 零增益根因
17 个「引语就在本 case 内」的完整大题（场景 A）两轮探针行为全部不变。整题主链路第一次 `CallEngineAsync`（LlmGatewayService.cs:106）已用含引语的完整题干求解并失败；子问再喂引语是**冗余上下文**。瓶颈不在「引语不可得」，而在 Production（LLM-off）求解器不能从自由文本引语解析结构化输入（reaction/known 依赖 LLM 解析或确定性关键词锚点）。

### Z.5 场景 B（跨 case 碎片重组）评估：NO-GO
- 未解碎片 12 题，11 题相邻 case 有可恢复引言，9 题文本可能足够（不缺图）。
- 但：①场景 A 已证恢复引语 ≈ 0 增益（同一瓶颈）；②题录无 parentId，仅靠 id 邻近 + 编号重叠匹配歧义高（pdf-162 同时命中 3 个不同大题），误拼接 = 喂错上下文 → 潜在 harm；③碎片题数据质量差（答案错位 pdf-165/E 类、引语重复 ~~pdf-412~~（已修复，datarepair1）、题干截断 pdf-160/161）；④scope drift 风险在更长引语下放大。
- 结论：场景 B 不启动；若未来做需独立 spec（题录 parentId 建模 + 跨 case 拼接 + 假阳性守卫），且先验证 Production 解析器能消费恢复的引语。详见 `_evalscratch/scenario_b_feasibility.txt`。

### Z.6 结论
轨道 1「运行时引语回填」机制经全库探针验证**净负（0 增益 + 1 回归）**，按 spec FR-4 回滚收口，不硬上场景 B。产出价值：用数据排除了一个架构方向，定位了真实瓶颈（Production 自由文本→结构化输入的解析能力，非上下文可得性），为后续方向（轨道 2 图像/结构 ingest，或新 spec「Production 散文抽取」）提供证据。

### Z.7 红线验证
- 回滚后 `b7_final` vs `b6baseline`：GAINED(0) / LOST(0)，precision=1，harm=0，mp27=27/27。
- `dotnet test -c Release`：2623 通过 / 0 失败 / 0 跳过。
- `git diff` 两改动文件内容为空（仅行尾提示）→ 生产代码零净变更。
- 无本批引入的 `_Probe*` .cs 残留（`EquationEngineProbeTests.cs` 为前批预存，非本批）。

---

## AA 类：dr1 批次——OCR 题面恢复（本批，仅改 `question`，不动键）

> 批次定位：57 道未解经逐题键核验（`dr1_keyverify.txt`），**43/57 键与答案册官方 EXACT 一致、14 道 `ans_page=-1` 未映射** → 主要矛盾不是键错，而是题面被截断/张冠李戴/缺子问缺图。本批用本地 OCR（`pypdfium2` + `tesseract chi_sim`）从源扫描页恢复良构题面，**零键改动**。候选与证据详见 `_evalscratch/dr1_candidates.md`。

### AA.1 已恢复（Tier 1：题面碎片 + OCR 良构 + 子问与键对齐）

| id | 恢复依据 | 落库 | eval 结果（`after_keyfix_b38`→`after_dr1`） |
|----|----------|------|--------------------------------------------|
| pdf-413 | 源页 137 OCR 给出子问 (1)~(7) 含选项；键 EXACT 且覆盖 (2)(3)(4)(5)(6) | 追加子问到 `question`，键不动 | **GAIN +1**：引擎产出 (6) 离子方程式命中键，700→701；precision=1 |
| pdf-161 | 源页 54-55 OCR（旧 `page_055.png` 曾是 page_054 损坏副本，已重渲修复）给出流程 + 子问 (1)(2)(3) + (3) 选项 a~d；键 EXACT 且 3 段一一对应 | 追加流程/子问到 `question`，键不动 | 题面完整对齐；引擎仍 `null`（fill 需化学推理），**无回归** |

> 验证门（`dr1_diff.py`）：**LOST=[]、harm=0、precision=1、pureLlm 无变动**；gatewayCorrect 700→**701**。范围核对（`_dr1_scope.py`）：dr1 净改动仅 `pdf_questions.json` 中 pdf-161 / pdf-413 两条 `question` 行，`answer`/`answerKeywords`/`type`/`note` 未碰，无 `.cs` 判分逻辑改动。

### AA.2 暂缓（不蒙，登记理由）

- **答案册分割歧义**：pdf-160 —— 答案册 num=4 把 Se(1-3) 与 BaTiO₃(4-5) 合并，题面仅 3 子问与 5 段键不对齐 → 不强修。
- **B 类张冠李戴（`src_page` 映射不可靠）**：pdf-162/163/165/176/180/190/416 —— 存储题干属另一大题，与键错配；源页映射偏移不可溯，硬找真题风险高 → 需先建「题干片段→源大题」可靠映射（独立 spec）。
- **缺结构图/装置图（Tier 3 图像轨道）**：pdf-519/520/524/528/534/540 等 —— 键存疑或 unmapped 的不做配图（避免迎合错键）→ 图像管道后续 spec。
- **未映射键（`ans_page=-1`，13 道）**：pdf-463/464/478/498/519/520/524/528/531/534/537/540/541 —— 无官方源可核 → 不动，保持 `null`。（pdf-458 原在此列，已由引擎解出且化学推导与存储键一致，移出本列，见 AB 类；其 `ans_page=-1` 仍建议后续官方源核键。）
- **选择题缺选项（潜在下批扩展）**：pdf-100/325/338/263 等 —— 题干完整、键与官方 EXACT 一致，从源页 OCR 抄回选项 A-D 后引擎可能解得（单字母命中门控，harm 风险低）→ 有 GAIN 潜力，列为下批候选，不在本批。

> 注：上文 M 类表（B 集闭包）的计数为 **dr1 之前**的历史快照，已在对应单元格用 `~~删除线~~` 标注 pdf-413/161 的当前状态，历史算术不重算。

## AB 类：engine-ext-structure-periodic 批次——结构/周期律/化学键比较引擎扩展（本批）

> 批次定位：dr1 之后回到引擎能力扩展，经分诊锁定「结构·周期律·化学键比较」方向。逐题核实后，原 3 个候选中 **2 个是数据缺陷、1 个是开放填空无唯一解**，均非引擎可解；唯一干净的真引擎缺口为 `pdf-458`。用户批准范围：**仅解 pdf-458**，机制为混合（电子排布/键角走引擎规则判定器，晶体类型/配位键走领域通用真值数据）。红线：`precision=1`、`harm=0`、`LOST=[]`、不蒙数、不做单题策展、不改 `pdf_questions.json` 任何字段、不碰既有单选瀑布链。工件：`.trae/specs/engine-ext-structure-periodic/`。

### AB.1 已解（GAIN +1，零回归）

| id | 机制 | eval 结果（`after_dr1`→`after_structperiodic`） |
|----|------|--------------------------------------------------|
| pdf-458 | 新增 4 个 `IStatementJudge` 原语接入 `CompositeStatementJudge`：`ElectronConfigStatementJudge`（查 `PeriodicTable` 算真值排布，展开稀有气体简写后逐亚层比对）、`VseprBondAngleStatementJudge`（仅判显式数值键角，复用 `VseprSolver` 表）、`CrystalTypeStatementJudge`（查 `structure-property-facts.json` 晶体类型真值表，共价晶体≡原子晶体）、`CoordinateBondStatementJudge`（查含配位键物种真值表，否定断言反向判定）。任一选项 `Undecided` → 整题 `null`（不蒙数）；pdf-458 四选项 A=False / B=True / C=False / D=True → 无歧义 → `MultiSelectChoiceValidator` 产出 `BD` | **GAIN +1**：gatewayCorrect 701→**702**，pdf-458 `gatewayCorrect=true`、`gatewayAnswer="正确选项为 BD"`、`gatewayUsedEngine=true`；**LOST=[]、harm=0、enginePrecision=1**；701 道已判正题 `gatewayAnswer` 逐题零变更；无额外 GAIN |

> 验证门：`_evalscratch/structperiodic_diff.py`（与 `dr1_diff.py` 同方法）；单元测试 `tests/CogniThink.TutorBrain.Tests/StructurePropertyStatementJudgesTests.cs`（26 条，覆盖各原语正/负例 + pdf-458 端到端判分）；全量测试 1758/0，含全库护栏 `FullBank_AnyMultiSelectConclusion_GradedCorrect_NoHarm`（新原语未引入任何假阳性结论）。

### AB.2 不在本批引擎范围（数据缺陷，保持 `null`）

分诊时初步列为引擎候选、逐题核实后判定为**数据缺陷**，剔除出引擎范围，维持既有登记：

- **pdf-196**：`options` 为空（无选项可判）→ 数据缺陷，见 K 类「T1 留痕」。引擎无从判定，非引擎缺口。
- **pdf-190**：answer/note 与题面张冠李戴（存储题干属另一大题，键 `(1)正四面体,(2)sp3` 属另一份 VSEPR 题）→ 数据缺陷，见 AA.2「B 类张冠李戴」及 K 类台账。采纳错键会致 harm，引擎不修复。
- **pdf-207**：开放论述填空（熔/沸点原因），无唯一可输出结论 → 非引擎域，见 F 类「开放论述/无唯一解」及 Y 类「多子问 AND 门含不可约自由文本」。

> 范围核对：本批净改动**不触碰** `pdf_questions.json` 任何字段（其工作区 diff 仅为上一批 dr1 对 pdf-161/413 的 `question` 回填，与本批无关）；`.cs` 改动仅为新增 4 原语 + `StatementNorm` 共享归一化 + `VseprSolver` 两个 internal 只读访问器 + `CompositeStatementJudge` 构造处追加 4 实例，既有校验器与瀑布链顺序无改动。新增真值数据 `data/highschool/structure-property-facts.json`（晶体类型 / 配位键）均为教材通用真值，可跨题复用，非为 pdf-458 单题策展。

---

## 处理原则（固化，防止回退到硬编码路线）

1. **引擎是唯一真值源**：化学判定走 `IStatementJudge` 聚合器（事实库/计数/氧化还原电子），不向 `general-choice-facts` 追加单题策展条目。
2. **数据修复只信官方源**：答案键改前必须有答案册 OCR 或原页核对（5b）；化学推导仅作辅助，5a 类（存储键为明显垃圾值 `"A,B,C,D"` 且 note 结论明确）沿用 Task 35/46 已验证流程，但跨多选项/题型存疑时（如 075）不单独触发存储键变更，必须官方源裁定。
3. **文本回填只写 `question`/`options`，不写 verdict、不改 `answer`**：回填后必须过 EvalRunner diff（LOST=0、harm=0）与字段 diff 审查。
4. **不可靠恢复一律登记、保持 `null`**：宁可未解，不可假阳性。

---

## M 类：data-repair-engine-exp-batch2（Stream B 单题扩展，2026-11-27）

> 批次目标：在 batch1（K 类）基础上继续处理剩余 bothWrong；沿用已审批的 spec-mode 方法论与全部红线（precision=1、harm=0、LOST=[]、零臆造、不碰已解题、不改 question/options/note/type）。基线 `_evalscratch/snapshot_after_datarepair_engine`：gwCorrect=706、bothWrong=51。

### M.1 候选分诊（51 道 bothWrong 全量复核）
choice 类中唯一干净的 Stream B 候选为 **pdf-405**（键 C 正确、4 选项文本完整、无图、题干完整）。其余排除：
- 图像依赖（pdf-138/255/303/307/336/338/376/382/498/524/540）→ 文本不足，排除。
- 题干拼接 / 选项与解析错位（pdf-197/414）→ 无 VL 无法安全还原（VL API key 失效 401），排除。
- 选项 OCR 损坏 / 题型矛盾（pdf-325/263）→ 排除。
- 氧化还原电子计数（pdf-075）→ 需专用 redox 求解器，强行加事实有臆造/harm 风险，排除。
- 已修正键但引擎缺口 / 开放论述填空 → 维持既有登记，不重复处理。

### M.2 Stream B 落库：pdf-405（+1 gwCorr）
- **题**：(2024·河北卷)「下列实验操作及现象能得出相应结论的是」，选正确项 = C。
- **结论列还原**：习题册抽取丢失了「结论」列；VL key 失效后改用网络检索官方真题（21cnjy / book118 / 学科网 / 豆丁多方印证）还原完整表格：
  - A「铁粉+水蒸气气体爆鸣」→ 结论「H2O 具有还原性」（错：H2O 实为氧化剂/氧化性）
  - B「滴 BaCl2 白色沉淀」→ 结论「待测液含 SO4^2-」（错：Ag+/CO3^2-/SO3^2- 干扰）
  - C「Mg(OH)2 只溶盐酸、Al(OH)3 都溶」→ 结论「Mg(OH)2 比 Al(OH)3 碱性强」（对）
  - D「K2Cr2O7 加 NaOH 橙变黄」→ 结论「增大生成物浓度，平衡逆向移动」（错：OH- 耗 H+，平衡正向移动）
- **事实**：向 `data/highschool/experiment-facts.json` 追加 EF-405-A/B/C/D（116→120 条），stemMatch「实验操作及现象能得出」全库唯一；A/B/D verdict=false、C verdict=true，reason 均引自官方解析。
- **blast-radius**：`_evalscratch/probe_batch2_verify.py` 复现 C# Norm/OptContains/WrongDir，确认 pdf-405→C，全库回归仅 pdf-405 决策变化（None→C），零 stray-hit。
- **MultiSelect 安全**：pdf-405 经 Experiment 单选校验先返回 C，MultiSelect 末端 fallback 不触发；新事实 stemMatch 唯一锁死本题，对他题零影响。
- **测试**：`Batch2ChoiceValidatorTests.ExperimentTruth` 追加 `["pdf-405"]="C"`；护栏 444 项全绿（0 失败）。并发 WIP 文件 `SolveStructuredEnricherTests.cs`（预存编译错误，与本批无关）测试时临时移出、跑完还原，内容未动。

### M.3 结果（eval：Production / LlmAvailable=False，757 例，0 失败）
- gwCorrect **706 → 707（+1）**，bothWrong 51 → 50。
- LOST=[]、harmCount=0、enginePrecision=1、已判正题 gatewayAnswer 字节级变更=0。
- GAINED 精确 = {pdf-405}（「正确选项为 C」）；batch1 收益 pdf-100/pdf-406 无回归。
- `_evalscratch/datarepair_engine_batch2_diff.py`：ALL RED LINES PASS。

### M.4 未纳入（安全降级，留待后续）
- Stream A 跳过项（pdf-190/197/414 等）需 VL 原页核对，当前 VL API key 失效，暂不强行修键。
- pdf-075 等 redox 计数题需引擎侧 redox 求解器接入，非事实可解。

## N 类：data-repair-engine-exp-batch3（VL 解锁 + 修键 + 引擎扩展，2026-11-27）

> 批次目标：在 batch2（M 类）基础上继续处理剩余 bothWrong。先验证环境键 `QWEN_API_KEY` 可用（`_evalscratch/probe_test_qwen_key.py` HTTP 200，VL 解锁——此前 batch2 因硬编码死键 401 误判失效）。沿用全部红线（precision=1、harm=0、LOST=[]、零臆造、不碰已解题）。基线 `_evalscratch/snapshot_after_datarepair_engine_batch2`：gwCorrect=707、bothWrong=50。

### N.1 候选分诊（50 道 bothWrong 全量复核）
50 道 bothWrong 在 Production（LlmAvailable=False）下 `gatewayAnswer` 全为空、`usedEngine=False`——**修键本身不涨分**，必须让引擎能解。可被事实解锁的 choice 仅剩 **pdf-197**。其余排除：
- 图像依赖（pdf-138/255/303/307/336/338/376/382/498/524/540）→ 文本不足，排除。
- 氧化还原电子计数（pdf-075）→ 需专用 redox 求解器（路径 B），非事实可解。
- 题型矛盾 / 选项带图（pdf-263/325）→ 排除。
- 开放填空 / 纯计算 → 生产环境引擎不产出，修键不涨分，维持既有登记。

### N.2 落库：pdf-197（+1 gwCorr）—— 题干拼接错位修复 + 事实扩展
- **题**：(2024·安徽卷)羟胺 passage 子问「下列有关物质结构或性质的比较中，正确的是」，选正确项 = **D**。
- **数据损坏还原**：习题册 `exercise/page_071.json` 上两道同标 `num=6` 的题被 `pdf_questions.json` 交叉接错——pdf-197 拿了羟胺题的选项（A 键角 NH3>NO3- / B 熔点 NH2OH>[NH3OH]Cl / C pH [NH3OH]Cl>NH4Cl / D 氢键 O—H···O>N—H···N），却粘了海洋资源(N2H4)题的题干+note、键为垃圾值 `A,B,C,D`；且粘入的「错误的是」会使 `FactChoiceMatcher.WrongDir` 把 `askwrong` 误判为 true，反转判定（即便加事实也解不出）。
  - **真值三角验证**：① 答案册 `answer/page_073.json` num=5 `answer=D`，解析逐项证 A/B/C 错、D 对；② 习题册 `exercise/page_071.json` num=6 原文（干净选项文本）；③ 网络检索（高途/21cnjy）印证同题。VL 键已活但本题抽取层文本无损，无需 VL 即可还原，零臆造。
  - **修复**（`src/.../data/pdf_questions.json` pdf-197）：题干清理为「下列有关物质结构或性质的比较中，正确的是（ ）」、answer `A,B,C,D`→`D`、answerKeywords→`["D"]`、note 回填答案册 num=5 官方解析。**不动 options**（本就正确）、**不动 type**。
  - 同 passage 兄弟 **pdf-196**（calc，存了羟胺 passage 引子但 answer/note 错位为选择题的 D/解析）**暂不碰**——生产环境引擎不解 calc，强行修其真键需臆造，登记为残留。
- **事实**：向 `data/highschool/experiment-facts.json` 追加 EF-197-A/B/C/D（120→124 条），stemMatch「物质结构或性质的比较」全库唯一；A/B/C verdict=false、D verdict=true，reason 均引自官方解析。
- **blast-radius**：`_evalscratch/probe_batch3_verify.py` 复现 C# Norm/OptContains/WrongDir（**加载真实落库文件**），确认 pdf-197→D（每项恰好匹配 1 条事实，askwrong=False），全库回归仅 pdf-197 决策变化（None→D），零 stray-hit、零 LOST。
- **MultiSelect 安全**：新事实 stemMatch 唯一锁死本题，对他题零影响；**未新增 WrongDir token**（规避 batch1 教训的 FactChoiceMatcher / MultiSelectChoiceValidator 双校验器漏同步）。
- **测试**：`Batch2ChoiceValidatorTests.ExperimentTruth` 追加 `["pdf-197"]="D"`；护栏 **445 项全绿（0 失败）**，较 batch2 的 444 多 1 即本用例。并发 WIP 文件 `SolveStructuredEnricherTests.cs`（预存编译错误，与本批无关）测试时临时移出、跑完还原，内容未动，无 `.wipbak` 残留。

### N.3 结果（eval：Production / LlmAvailable=False，757 例，0 失败）
- gwCorrect **707 → 708（+1）**，bothWrong 50 → 49。
- LOST=[]、harmCount=0、enginePrecision=1、已判正题 gatewayAnswer 字节级变更=0。
- GAINED 精确 = {pdf-197}（gwAns='正确选项为 D'，expected='D'，state=Confirmed，usedEngine=True）；batch1 收益 pdf-100/pdf-406、batch2 收益 pdf-405 无回归。
- `_evalscratch/datarepair_engine_batch3_diff.py`：ALL RED LINES PASS。

### N.4 未纳入（留待后续；VL 现已可用）
- **路径 B**：pdf-075 等 redox 计数题 → 需按 `chem-engine-question-solving` 阶段 3C 接入 redox 求解器（更大 spec 级构建），非事实可解。
- **路径 A 后续**：pdf-190（张冠李戴）、~~pdf-414（拼接）~~（已修复，datarepair1）、pdf-162/325（OCR 损坏）→ VL 现已可用，下一批可逐题原页核对还原；pdf-196（calc 错位残留）同属此类。
- 图像依赖 11 道 → 图像轨道。
