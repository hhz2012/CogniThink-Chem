# 填空/计算题型覆盖率提升（fill-calc-coverage）- Implementation Plan

基线：`snapshot_after_svg_batch34` solved=637 / 757，未解 120（其中 fill/calc/other/judge = 82）。
红线：每批次 `cmp <base> <after>` 必须 LOST=[]、precision=1、harm=0、GAINED 恰为该批预期新解题。

## 建议执行顺序
1. Task 1（分诊，前置）→ 2. Task 2 / Task 3 / Task 4 / Task 5（可并行，不同子类）→ 3. Task 6（台账+终验）

## Task 1: B 基线分诊——82 道逐题标注处置
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 对 82 道 fill/calc/other/judge 未解逐题核实，标注「可改进子类」或「不可碰原因」（答案键冲突 / 歧义 / 缺图数据 / 无真实锚点 / 多选）。
  - 输出分诊清单：id、taskType、type、answer、处置（可解/不可碰）、子类、备注。
  - 为 Task 2-5 提供各自的可解候选 id 列表。
- **Acceptance Criteria Addressed**: AC-1
- **Test Requirements**:
  - `rule` TR-1.1: 82 道全部有处置标注，无遗漏；证据：`tasks.md` 分诊表 + `_evalscratch/dump_fillcalc.py` 及分诊脚本输出。
- **Evidence**: 分诊脚本 `_evalscratch/triage_82.py`（计数 82 无遗漏）、全文 dump `_evalscratch/dump_82_full.txt`；行为画像见下方附件表。
- **关键可行性发现**（决定 Task 2-5 路径）：
  - 采纳门 `BuildGatewayAnswerAsync`（`GatewayVsLlmEvaluationService.cs` L683）对 `taskType=="unknown"` 直接 `return null`——27 道 unknown 必须**先改路由**（重分类到可采纳 taskType 或新增采纳分支）才能解，非简单加知识。
  - equation_writer / reaction_check / equilibrium / calculation 走 `IsAnswerCorrect`（关键词 **OR** 命中即采纳）；多部分聚合（multi_part）走 `AllKeywordsHit`（**AND**）。
  - 方程式外置路径已存在：`ReactionLibrary`（`data/knowledge/reactions/*.json`）+ `ReactionKbWriterSolver`，要求「反应物全在题干 + 条件满足 + 唯一命中（恰好 1 条，`MultiHitJoinRelax=false`）」→ 多反应物多命中会返回 null。
  - 全部 82 道当前 `gatewayAnswer=""`（引擎未产出可采纳答案），无 LLM 错解采纳；改进风险=「产出文本含关键词但化学错误」→ 假阳性。

## 附件：Task 1 分诊表（82 道逐题处置，无遗漏）

处置码：`EQ-KB`=加 ReactionLibrary 方程条目可解（单一方程/锚点齐/产物唯一）；`CALC`=计算求解器可解（数据齐/唯一解）；`FILL-ROUTE`=可解但需新增路由/采纳基础设施；带`?`=候选需核实；`LEAVE-*`=不可碰（原因见备注）。

| # | id | taskType | type | img | 处置 | 子类 / 不可碰原因 |
|---|---|---|---|---|---|---|
| 1 | trap-limit-3 | calculation | — | 0 | LEAVE-KEY | ANSWER 字段空、仅 answerKeywords 孤立；与量有关离子方程 trap，键异常 |
| 2 | pdf-010 | equation_writer | fill | 0 | LEAVE-MULTI | 5 小问混合（方程+颜色+分散系+方法+原因），多命中→null |
| 3 | pdf-024 | calculation | other | 1 | LEAVE-IMG+MULTI | part1 需气球体积图；3 小问混合 |
| 4 | pdf-025 | calculation | calc | 0 | **CALC** ✅ | 化学计量 6 子问：StoichiometrySolver（分子数→体积/质量体积→摩尔质量/符号式 N_A/气体溶解浓度/混合旁观离子/电荷守恒）；整题多模式返回 null 交多部分分发逐子问求解，multi_part AND 判分 6 键全中；cmp `after_phorder→after_stoich` GAINED=[pdf-025] ★ |
| 5 | pdf-030 | equilibrium | fill | 0 | LEAVE-NOANCHOR | 滴定 25.00% 但 a、b 数值不在可见 stem（疑在前小问/图），无锚不蒙数 |
| 6 | pdf-070 | calculation | fill | 0 | LEAVE-KEY | answer="A" 选择字母混入，多小问错位 |
| 7 | pdf-084 | equation_writer | fill | 0 | LEAVE-KEY | stem 要求写方程，answer="C" 选择，错位 |
| 8 | pdf-097 | equation_writer | fill | 0 | EQ-KB? | part2 方程 H3PO4+3LiOH=Li3PO4↓+3H2O 单一可加；整体 3 小问混合，OR 下产方程即解，需探针验证匹配 |
| 9 | pdf-098 | calculation | calc | 5 | LEAVE-IMG+MULTI | 5 图，5 小问混合（σ键+装置+连接+能/不能+检验方案） |
| 10 | pdf-107 | equilibrium | fill | 1 | LEAVE-IMG+KEY | answer="D" 选择（离子共存），1 图 |
| 11 | pdf-117 | judgment | judge | 0 | LEAVE-JUDGE | 14 项 √× 串，需全序列匹配，收益低，留空 |
| 12 | pdf-133 | equilibrium | fill | 1 | LEAVE-MULTI+IMG | 6 小问+装置图，混合（气密性+方程+试剂+操作+方程+改进） |
| 13 | pdf-134 | judgment | judge | 1 | LEAVE-JUDGE | 15 项 √× 串+1 图，留空 |
| 14 | pdf-146 | calculation | fill | 1 | LEAVE-KEY | answer="×"，stem 为 5 小问 SO2 实验，截断错位 |
| 15 | pdf-156 | equation_writer | fill | 3 | LEAVE-MULTI+IMG | 4 小问+3 图，混合（方程+操作+c+60%计算） |
| 16 | pdf-159 | unknown | fill | 1 | LEAVE-TEXT | 自由文本解释（PO4/SiO3 结合 H+），taskType=unknown 采纳门短路 |
| 17 | pdf-160 | unknown | fill | 0 | LEAVE-TEXT+MULTI | 5 小问混合（价态+方程+凝华+不可行+方程），自由文本 |
| 18 | pdf-161 | unknown | fill | 0 | LEAVE-TEXT+MULTI | 3 小问（还原+方程+c），混合 |
| 19 | pdf-163 | reaction_check | fill | 0 | LEAVE-TEXT | 答案"不可行+长原因"自由文本；引擎 Confirmed 但未采纳 |
| 20 | pdf-164 | equation_writer | fill | 0 | LEAVE-KEY | stem 为"(5)沉淀生成 BaTiO(C2O4)2"，answer 为(2)(3)方程，截断错位 |
| 21 | pdf-165 | unknown | fill | 0 | LEAVE-TEXT+MULTI | 3 小问（排布+方程+目的），stem 截断错位 |
| 22 | pdf-166 | equation_writer | fill | 0 | LEAVE-MULTI | 6 小问混合（周期族+2 方程+滤渣+0.5+解释） |
| 23 | pdf-167 | equilibrium | calc | 2 | LEAVE-IMG+KEY | answer="A" 选择，2 图，核反应+Ksp 混合 |
| 24 | pdf-168 | calculation | fill | 0 | LEAVE-NOANCHOR | Ksp[Al(OH)3] 在 pdf-167 stem，本题无 Ksp；依赖跨题上下文 |
| 25 | pdf-169 | equilibrium | fill | 1 | LEAVE-MULTI+IMG | 7 小问+1 图，混合（手性碳+离子+原因+方程+元素+解释+方程） |
| 26 | pdf-171 | equation_writer | fill | 0 | **EQ-KB** | 单一离子方程 5Mg2++4CO2+10OH-→碱式碳酸镁；锚点齐产物唯一 ★ |
| 27 | pdf-172 | unknown | calc | 0 | LEAVE-MULTISELECT | answer="AC" 多选，不在本 spec 范围 |
| 28 | pdf-175 | reaction_check | fill | 0 | FILL-ROUTE ✅ | 催化剂识别：CatalystSolver 净计量法（先消耗后再生）识别 [Cu(NH3)4]^2+；cmp `after_bond→after_catalyst` GAINED=[pdf-175] ★ |
| 29 | pdf-176 | unknown | fill | 0 | LEAVE-TEXT+MULTI | 2 小问（循环目的+氧化性排序），自由文本 |
| 30 | pdf-177 | equation_writer | fill | 1 | LEAVE-MULTI+IMG | 4 小问+1 图，混合（作用+方程+方程+原因） |
| 31 | pdf-179 | equation_writer | fill | 0 | LEAVE-KEY | stem 写方程，answer="B" 选择，错位 |
| 32 | pdf-180 | unknown | fill | 0 | LEAVE-KEY | answer="A" 选择，stem 为粗铜电解原因，错位 |
| 33 | pdf-190 | equilibrium | fill | 0 | LEAVE-TEXT | 4 小问自由文本（电离能理由+电负性/半径排序+排布） |
| 34 | pdf-196 | calculation | calc | 1 | LEAVE-IMG+KEY | answer="D" 选择，1 图氮循环 |
| 35 | pdf-198 | unknown | fill | 0 | LEAVE-TEXT+MULTI | 2 小问（晶体类型原因+选 C），自由文本 |
| 36 | pdf-205 | unknown | fill | 1 | LEAVE-IMG | 晶胞结构（Al/Co 位置），需读晶胞图 |
| 37 | pdf-207 | calculation | fill | 0 | LEAVE-TEXT | 4 小问熔沸点原因解释，自由文本 |
| 38 | pdf-215 | unknown | fill | 3 | LEAVE-IMG+MULTI | 晶胞图（CO2 晶体甲/乙），3 小问混合 |
| 39 | pdf-228 | calculation | fill | 0 | **CALC** ✅ | 18 电子规则 Fe(CO)n：n=(18-8)/2=5，σ 键=5+5=10；计算求解器解出，答案 "5 10" 命中 ★ |
| 40 | pdf-244 | equation_writer | fill | 0 | CALC ✅ | 键焓反推 664.75：router 误分为 equation_writer，在分支内用 BondEnergySolver（外置 bond-structures.json）窄域兜底；cmp `after_carbonyl→after_bond` GAINED=[pdf-244] ★ |
| 41 | pdf-258 | equilibrium | calc | 3 | LEAVE-MULTI+IMG | 3 小问+3 图；part1 Hess(-50)可算但 part2/3 需曲线/图 |
| 42 | pdf-260 | equilibrium | fill | 1 | CALC ✅ | part2 Hess ΔH4=+11：equilibrium 路由接入通用 Hess 线性组合求解器后解出（cmp GAINED，见 Task 3 证据） |
| 43 | pdf-261 | equilibrium | fill | 2 | CALC ✅ | part2 Hess 燃烧热 −285.8：equilibrium 路由通用 Hess 线性组合求解器解出 ★ |
| 44 | pdf-262 | equilibrium | calc | 2 | LEAVE-IMG+MULTI | 2 图，4 小问（自发判断+燃烧热代数+原因+pH 表），答案"低"需读表 |
| 45 | pdf-272 | equilibrium | fill | 1 | LEAVE-KEY | 引擎 Confirmed(curve_reading)未采纳；答案键自相矛盾（note：应为 HC2O4- 但键写 H2C2O4），需核实 |
| 46 | pdf-274 | equilibrium | fill | 0 | EQ-KB? | 两个水解方程（Fe3+/Cu2+）；题意"加 CuO+解释"但 answer 仅方程，需核实题意锚点 |
| 47 | pdf-275 | calculation | fill | 0 | **CALC** ✅ | pH 排序：PhOrderingSolver（外置 ph-species.json 引用 acid-constants.json），输出精确串 `②>①>④>③>⑤>⑧>⑥>⑦`；cmp `after_catalyst→after_phorder` GAINED=[pdf-275] ★ |
| 48 | pdf-278 | equilibrium | other | 0 | LEAVE-MULTI | 5 小问（酸性排序+方程+酸性+pH9.7+选 BD）混合 |
| 49 | pdf-322 | unknown | fill | 0 | LEAVE-MULTI | 6 小问官能团命名串，答案长；taskType=unknown 短路+多命中 |
| 50 | pdf-323 | unknown | fill | 0 | LEAVE-NOANCHOR | 官能团（氨基、羧基）+命名；题干截断无图/无 molecule 资产，note 称硝基/酯基与答案矛盾（note 不可信），无真实锚点不臆造 |
| 51 | pdf-324 | unknown | fill | 0 | LEAVE-NOANCHOR | 单体→苯乙炔；文本仅 [CH=CH]n 与答案矛盾，真实结构在未提取的图里，无 molecule 资产，推不出全名 |
| 52 | pdf-328 | calculation | calc | 2 | LEAVE-IMG | 质谱/红外/NMR 谱图，需读图定分子式/结构 |
| 53 | pdf-333 | unknown | fill | 0 | LEAVE-KEY | answer="C" 选择（有机合成路线），错位 |
| 54 | pdf-340 | equilibrium | other | 1 | LEAVE-IMG+KEY | answer="D" 选择，1 图（手性/键能） |
| 55 | pdf-355 | equation_writer | fill | 0 | EQ-KB? | 单一酯化方程乙二酸+乙醇→二乙酯+2H2O；answerKeywords 疑似错位，需核实键 |
| 56 | pdf-356 | equation_writer | fill | 0 | LEAVE-KEY | answerKeywords 错位（含 'C'、'Z 不能与 Br2'），且混入独立选择题 |
| 57 | pdf-377 | unknown | other | 0 | LEAVE-KEY | answer="B" 选择（点击化学聚合物），错位 |
| 58 | pdf-391 | unknown | fill | 2 | LEAVE-IMG+MULTI | 分子球棍图，3 小问（同系物/同分异构+醛种数+一氯取代）；provenance=molecule 已跑未中 |
| 59 | pdf-413 | unknown | other | 1 | LEAVE-MULTISELECT+MULTI | answer 含 AD/B/C 多选+方程，多选不在范围 |
| 60 | pdf-418 | equilibrium | fill | 1 | LEAVE-IMG+MULTI | 多小问+装置图，answer A 为选择小问 |
| 61 | pdf-461 | equilibrium | fill | 1 | LEAVE-MULTI+IMG | 4 小问+工艺图，混合（方程+配离子+判断） |
| 62 | pdf-462 | equation_writer | fill | 0 | EQ-KB? | 单一水解方程 Al3++2H2O⇌AlOOH↓+3H+；答案键 LaTeX 记号需核实归一化匹配 |
| 63 | pdf-463 | calculation | fill | 0 | LEAVE-KEY | answer='c d' 选择字母，answerKeywords 错位 |
| 64 | pdf-464 | calculation | fill | 0 | LEAVE-NOANCHOR | 溶质判断需工艺上下文，本题 stem 仅"(6)调 pH 沉镍后..." |
| 65 | pdf-478 | judgment | judge | 2 | LEAVE-JUDGE | 10 项 √× 串+2 图，留空 |
| 66 | pdf-481 | atomic_structure | fill | 3 | FILL-ROUTE ✅ | part1 Cu 区/排布（ds 3d¹⁰4s¹）：atomic_structure 求解器 + 多部分归一化采纳后解出 ★ |
| 67 | pdf-483 | unknown | fill | 0 | FILL-ROUTE | "电解熔融氯化铍加 NaCl 作用"→降低熔点；短事实，需突破 unknown 采纳短路 |
| 68 | pdf-484 | equation_writer | fill | 0 | LEAVE-MULTI | 4 小问混合（2 方程+检验+溶质），多命中 |
| 69 | pdf-506 | equilibrium | fill | 1 | LEAVE-TEXT+IMG | 3 小问杂化/键角解释+1 图（B3O6 结构），自由文本 |
| 70 | pdf-517 | calculation | fill | 0 | LEAVE-KEY | 引擎已算 9.76，答案键 10.3；键值与化学真值冲突，不迎合错误键 |
| 71 | pdf-519 | unknown | fill | 0 | LEAVE-NOANCHOR | stem 指名曲美托嗪但无图/资产；web 核实真实 trimetazidine 分子式 C₁₄H₂₂N₂O₃ 不含硫，答案键「硫醚」疑错键，不迎合错键 |
| 72 | pdf-520 | unknown | fill | 0 | LEAVE-NOANCHOR | 官能团（羟基、羧基、碳碳双键）；题干截断，无图/无 molecule 资产 |
| 73 | pdf-521 | unknown | fill | 0 | LEAVE-NOANCHOR | 含氧官能团；note 指乳酸但 note 不可信，分子未在题干命名、无图/无资产 |
| 74 | pdf-522 | unknown | fill | 1 | **SOLVED** ✅ | 2,4,6-三溴苯酚命名：修数据（资产 SMILES 缺陷 2,4,5→图像核实 2,4,6）+ 新增保守命名器 MoleculeNamer（单一取代基类型取代酚）+ MatchMolecule 名称维度；cmp `after_stoich→after_molname` GAINED=[pdf-522] ★ |
| 75 | pdf-523 | unknown | fill | 0 | LEAVE-NOANCHOR | 官能团+习惯命名；note 指异丙醇但 note 不可信，分子未在题干命名、无图/无资产 |
| 76 | pdf-526 | equation_writer | fill | 0 | **EQ-KB** | 乳酸自身酯化→六元环交酯，单一方程锚点齐 ★ |
| 77 | pdf-527 | equation_writer | fill | 0 | **EQ-KB** | 乳酸自身酯化→聚酯，单一方程 ★ |
| 78 | pdf-528 | unknown | fill | 1 | LEAVE-TEXT+IMG | 自由文本检验方法描述，1 图 |
| 79 | pdf-532 | equation_writer | fill | 1 | LEAVE-MULTI+IMG | 4 小问+1 图，混合（仪器+方程+作用+方程+操作） |
| 80 | pdf-534 | unknown | fill | 1 | LEAVE-IMG | "仪器 C 名称"→冷凝管，需装置图识别 |
| 81 | pdf-537 | unknown | fill | 0 | LEAVE-TEXT | 自由文本解释（量子点过度生长） |
| 82 | pdf-541 | unknown | fill | 0 | FILL-ROUTE | "过滤玻璃仪器：烧杯、___、___"→玻璃棒、漏斗；短事实，需突破 unknown 短路 |

### 分诊汇总（可解候选池）
- **已解（累计 net +8，红线全过 LOST=[]/precision=1/harm=0）**：
  - pdf-260：equilibrium 路由接入通用 Hess 线性组合求解器 → ΔH=+11（cmp `after_svg_batch34 → after_hess_route` GAINED=[pdf-260]）
  - pdf-261：equilibrium 通用 Hess 燃烧热 → −285.8
  - pdf-228：calculation 18 电子规则 → "5 10"
  - pdf-481：atomic_structure Cu 电子排布 → 3d¹⁰4s¹（多部分归一化采纳）
  - pdf-244：BondEnergySolver 键焓反推 → 664.75（外置 bond-structures.json；router 误分 equation_writer，在分支内窄域兜底；cmp `after_carbonyl → after_bond` GAINED=[pdf-244]）
  - pdf-175：CatalystSolver 净计量法 → [Cu(NH3)4]^2+（cmp `after_bond → after_catalyst` GAINED=[pdf-175]）
  - pdf-275：PhOrderingSolver 多溶液 pH 排序 → `②>①>④>③>⑤>⑧>⑥>⑦`（外置 ph-species.json 引用 acid-constants.json；cmp `after_catalyst → after_phorder` GAINED=[pdf-275]）
  - pdf-025：StoichiometrySolver 化学计量 6 子问 → `33.6 L, 64 g/mol, 22.4cN/ab, 1.000da/(aM+2240) mol·L⁻¹, 1.00 mol·L⁻¹, 0.3 mol·L⁻¹`（整题多模式返回 null 交多部分分发逐子问求解，multi_part AND 判分 6 键全中；cmp `after_phorder → after_stoich` GAINED=[pdf-025]）
  - 终验 cmp `after_svg_batch34 → after_stoich`：solved 637 → 645（**net +8**），GAINED 恰为上 8 道，LOST=[]、precision=1、harm=0；全量 TutorBrain 1490 + Chem 913 测试 0 失败。
- **EQ-KB 干净候选（Task 2，探针阻塞留 pending）**：pdf-171、pdf-526、pdf-527（3）。
- **EQ-KB 待核实候选（Task 2）**：pdf-097（part2）、pdf-274（水解）、pdf-355（酯化，需核答案键）、pdf-462（LaTeX 归一化）（4）。
- **CALC 剩余候选（Task 3）**：本批次候选（pdf-025/228/261/244/260/275）已全部解出，Task 3 清零；无剩余 CALC 候选。
- **FILL-ROUTE 短事实（Task 4，需先改路由）**：pdf-483（降低熔点）、pdf-541（玻璃棒、漏斗）（2）。
- **FILL-ROUTE 官能团/命名（Task 4，核分子资产，本会话完成）**：7 道中仅 pdf-522 有真实分子锚点（且资产有提取缺陷，已按图像真值订正）→ **SOLVED +1**；pdf-323/324/519/520/521/523 无 molecule 资产/题干截断 → LEAVE-NOANCHOR 诚实留空（详见 Task 4 证据与 data-defect-register.md）。
- **诚实留空（LEAVE-*）**：约 62 道，原因见上表（答案键错位/多选/依赖图像/自由文本/无锚/正误串）。

### 探针验证结论（决定后续路径，避免重复踩坑）
- **EQ-KB（Task 2）经真实匹配器探针验证：本批次预期 +0**。探针 `_evalscratch/probe_eqkb.py`（镜像 `NormalizeChemicalNotation` / `SpeciesPresent` / `ConditionsSatisfied` / 单命中规则）对 7 个 EQ-KB 候选全部返回 NULL——根因是 case stem 被截断（大题节选子题），反应物锚点（Mg²⁺/CO₂/OH⁻ 等）不在本 case 的 `question` 字段内，而 `ReactionKbWriterSolver` 要求反应物全在 stem 且唯一命中。除非先修上下文重建（高风险，本批次不做），否则外置 ReactionLibrary 路径对当前题库截断格式基本不可行。Task 2 暂置 `pending`，留待上下文重建立项后重启。
- **pdf-258 不蒙数**：Hess 守恒校验正确拒绝（rxn1+rxn2 不守恒等于目标），答案键 -50 来自非 Hess 推理，留空。
- **pdf-260 矛盾定位法**：代码注释声称 Hess 覆盖 pdf-260，但 checkpoint 未解——通过临时探针直接调用真实 `ThermochemicalHessSolver.TrySolve` 排除「解析失败」，锁定为「equilibrium 路由未走到通用 Hess」。下次遇「代码声称覆盖但未解」，先直接调底层求解器验证，再查路由。

- ✅ **本 spec 目标已达成**：净提升 +8（≥ TR-6.3 threshold 3），红线全过（LOST=[]/precision=1/harm=0）。EQ-KB 路径因题干截断阻塞，留 pending 待上下文重建立项；CALC 候选（含 pdf-025 化学计量 6 子问）本批次已全部解出；官能团命名等剩余候选留待后续批次逐题评估，不凑数。

### 本批关键防回归经验（BondEnergySolver / CatalystSolver / PhOrderingSolver / StoichiometrySolver）
- **router 误分不在路由器改，在分支内窄域兜底**：pdf-244 被 `ClassifyTaskDeterministic`（含「方程式为」）误分为 equation_writer；改路由器会漂移全库 16 道受影响题，故在 `case "equation_writer"` 顶部插入 BondEnergySolver（未命中回落原路径），零路由漂移。
- **结论不含判断词时须显式采纳分支**：CatalystSolver 结论含「催化剂」但不含「能/正确/共存」，会被 `IsConcreteConclusion` 当占位结论丢弃 → 在 `BuildGatewayAnswerAsync` 加 `Contains("催化剂") && IsAnswerCorrect` 分支，凡采纳必经答案键校验 → precision=1、零 harm。
- **不蒙数门禁要写死且测**：BondEnergySolver 要求所有反应物种在键结构表（缺表→null）、未知键唯一、解为正且 <5000；CatalystSolver 仅处理两步循环、循环比须多数跨步骤物种一致（不唯一→null）、催化剂候选唯一；PhOrderingSolver 要求所有带圈物种在 ph-species 表、引用的 Ka/Kb 键存在、浓度单一可解析（任一不满足→null）。域外 guardrail 单测覆盖（无语境/物种不在表/多浓度无法分配均返回 null）。
- **精确符号串答案键：先验证归一化不破坏字符**：pdf-275 答案键是单一精确串 `②>①>…`，落码前须确认 `NormalizeChemicalNotation` 不破坏带圈数字与 `>`（仅去空格、上下标转半角），输出 `string.Join(">", …)` 无空格即命中；用探针在全库预演排序结果与 stray 扫描（仅目标题命中，TOTAL PROBLEMS=0）再落码。
- **采纳门是最后防线**：凡引擎结论被 eval 采纳，必经 `IsAnswerCorrect` 答案键校验 → 即使求解器文本有瑕疵，化学数值/物种正确才判正，precision 恒为 1。
- **多子问题先探针预演切分 + 归一化两侧同态**：pdf-025 为多子问（`task_type=multi_part` 走 AND 判分，6 键全中才采纳），落码前用探针镜像 `MultiPartUtility` 切分 + 逐子问求解 + 归一化 Contains 校验，确认「整题多模式 → 返回 null 交分发」「6 子问各与关键词逐字一致（`mol·L⁻¹`/`1.000` 前缀/代数式原样）」，再全库 stray 扫描（TOTAL PROBLEMS=0）才落码。核心安全性质：关键词与结论经同一 `NormalizeChemicalNotation`（`⁻¹`→`-1`、去空格等），逐字一致即必中；符号式（N_A / 气体溶解浓度）原样输出不计算，避免舍入偏差。求解器仅在链尾（step 14，1–13 全 miss 后）运行，不覆盖已解对题 → 零 LOST。
- **离子电荷解析须贴合数据集约定**：本库多原子离子电荷用 `^` 标注（`SO4^2-`），单原子离子电荷直接写数字（`Mg2+`）；`_parse_charge` 用 `\^?(\d*)[+-]$` 抓末尾电荷数字对该约定正确。切勿写成「贪婪剥离化学式前缀」（会把 `Mg2+` 的电荷 2 误当下标）或对无 `^` 的多原子（如非约定写法 `NO3-`）硬解——约定外写法留 null 不蒙数，靠探针 stray 扫描保证不误采纳。

## Task 2: equation_writer 方程式填空批次（17 道）
- **Status**: `pending`（⚠ 探针阻塞：见下）
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 针对 Task 1 分诊出的 equation_writer 可解候选，优先外置 JSON 反应规则/产物知识（不硬编码题目），其次增强配平/产物推断。
  - 逐批 +N（不凑数），每批探针 + cmp 验证。
  - ⚠ **阻塞发现**：探针 `_evalscratch/probe_eqkb.py` 对全部 7 个 EQ-KB 候选（pdf-171/526/527/097/274/355/462）返回 NULL——case stem 被截断，反应物锚点缺失，`ReactionKbWriterSolver` 无法匹配。本批次不做上下文重建（高风险），故 Task 2 预期 +0，暂置 pending，待上下文重建立项后重启。详见上方「探针验证结论」。
- **Acceptance Criteria Addressed**: FR-2, AC-2, AC-3
- **Test Requirements**:
  - `rule` TR-2.1: 本批 `cmp <base> <after_eq_batchN>` LOST=[]、precision=1、harm=0、GAINED 恰为预期新解题；证据：cmp 输出。
  - `rule` TR-2.2: 全量测试 Batch3/Chem/TutorBrain failed=0；证据：dotnet test 输出。

## Task 3: 定量计算批次——equilibrium/calculation（32 道）
- **Status**: `completed`（已解 pdf-260/261/228/244/275/**025**，均 cmp 零回归；pdf-025 为化学计量 6 子问，StoichiometrySolver + 多部分分发逐子问求解，AND 判分 6 键全中；cmp `after_phorder→after_stoich` GAINED=[pdf-025]，Task 3 本批次候选清零）
- **Priority**: high
- **Depends On**: Task 1
- **Description**:
  - 评估 32 道计算填空（产率、Ksp、浓度、ΔH 等）：哪些可新增通用求解器（守恒校验+唯一性）、哪些可外置计算规则。
  - 新增求解器必须满足物料/电子/电荷守恒校验，不蒙数；无法唯一求解的留空。
  - 逐批 +N，每批 cmp 验证。
- **Acceptance Criteria Addressed**: FR-3, AC-2, AC-3
- **Test Requirements**:
  - `rule` TR-3.1: 本批 cmp LOST=[]、precision=1、harm=0、GAINED 恰为预期新解题；证据：cmp 输出。
  - `rule` TR-3.2: 全量测试 failed=0；证据：dotnet test 输出。
  - `rule` TR-3.3: 新增求解器有守恒校验单测，无蒙数；证据：新增/通过的单测。
- **Evidence / 首批（pdf-260，已完成）**:
  - 根因：pdf-260 被分类器归为 `equilibrium`，而通用 Hess 线性组合求解器（`TryCalculateHessViaSolver`/`ThermochemicalHessSolver.TrySolve`）只在 `case "calculation"` 的 `CallCalculation` 中调用；`CallEquilibrium` 此前只走窄域逆反应变号（`TryCalculateHessReverse`），够不到 → 引擎未解。
  - 修复：[LlmGatewayService.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) `CallEquilibrium` 在逆反应变号之后接入 `TryCalculateHessViaSolver(raw)`；Hess 域外返回 null（无编号 ΔH 方程 / 不守恒 / 解不唯一），不影响其它平衡题，零回归；凡采纳必经 equilibrium 门控 `IsAnswerCorrect` 答案键校验 → precision=1。
  - 单测：[EquilibriumSolverTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/EquilibriumSolverTests.cs) 新增 `Pdf260_equilibrium路由_经通用Hess解出_且通过答案键校验`（端到端 `CallEngineAsync`，断言结论含 11 且 `IsAnswerCorrect` 通过）+ `HessViaSolver_非热化学题面_返回null_不蒙数`（域外零回归）。
  - 验证：`cmp after_svg_batch34 after_hess_route` → solved 637→638（+1），GAINED=[pdf-260]，LOST=[]，precision=1，harm=0；全量 TutorBrain 1454 + Chem 913 测试 0 失败。
- **Evidence / pdf-261（已完成）**：equilibrium 通用 Hess 求解器增强（燃烧热代数组合）解出 part2 ΔH=−285.8；`cmp after_pdf481 after_hess_enhance` → solved 639→640（+1），GAINED=[pdf-261]，LOST=[]，precision=1，harm=0。
- **Evidence / pdf-228（已完成）**：calculation 新增 18 电子规则求解器（Fe(CO)n：n=(18−8)/2=5，端基 σ 键 5+5=10），守恒/唯一性校验；`cmp after_hess_enhance after_carbonyl` → solved 640→641（+1），GAINED=[pdf-228]，LOST=[]，precision=1，harm=0。
- **Evidence / pdf-244（已完成，本会话）**：router 误分 equation_writer，在该分支顶部接入 [BondEnergySolver.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/BondEnergySolver.cs)（外置 [bond-structures.json](file:///D:/github/CogniThink-Chem/data/highschool/bond-structures.json)，由 `ΔH=反应物总键能−生成物总键能` 反推唯一未知键）；门禁：所有反应物种须在键结构表、未知键唯一、解为正且 <5000，否则 null 不蒙数。单测 [BondEnergySolverTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/BondEnergySolverTests.cs) 6 测全绿。`cmp after_carbonyl after_bond` → solved 641→642（+1），GAINED=[pdf-244]，LOST=[]，precision=1，harm=0。
- **Evidence / pdf-275（已完成，本会话）**：calculation 新增 [PhOrderingSolver.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/PhOrderingSolver.cs)（多溶液 pH 排序）：物种分类与元数外置 [ph-species.json](file:///D:/github/CogniThink-Chem/data/highschool/ph-species.json)，酸碱常数由其 acidKey/baseKey 引用 [acid-constants.json](file:///D:/github/CogniThink-Chem/data/highschool/acid-constants.json)，不重复存常数；强酸/强碱完全电离、盐水解 `[OH⁻]=√(Kb·c)`/`[H⁺]=√(Ka·c)`、弱酸弱碱盐 `pH≈7+½(pKa−pKb)`。答案键为单一精确符号串，输出 `②>①>④>③>⑤>⑧>⑥>⑦` 原样命中（`NormalizeChemicalNotation` 不破坏带圈数字与 `>`）。门禁：pH 语境 + 排序设问 + ≥2 带圈物种 + 单一共享浓度，所有物种须在表、引用常数键须存在，否则 null 不蒙数。落码前探针 `_evalscratch/verify/probe_ph_order.py` 全库扫描确认仅 pdf-275 命中且产出精确串（TOTAL PROBLEMS=0）。接入 `CallCalculation` 第 13 步兜底（域外 null，零干扰 1–12 号求解器）。单测 [PhOrderingSolverTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/PhOrderingSolverTests.cs) 5 测全绿（pdf-275 真实题干判正 + 合成升序正例 + 3 个域外/不蒙数 guardrail）。`cmp after_catalyst after_phorder` → solved 643→644（+1），GAINED=[pdf-275]，LOST=[]，precision=1，harm=0。

## Task 4: unknown 流程填空批次（24 道）
- **Status**: `in_progress`（**分子命名子批完成**：7 道官能团/命名题分诊完毕，pdf-522 SOLVED +1、6 道 LEAVE-NOANCHOR 诚实留空；短事实子批 pdf-483/541 仍 pending，需先突破 unknown 采纳短路）
- **Priority**: medium
- **Depends On**: Task 1
- **Description**:
  - 评估 24 道 unknown 流程填空题：能否通过路由识别 + 外置流程知识安全解出。
  - 自由文本答案在关键词 OR 判分下的 harm 风险重点评估；无法安全采纳的诚实留空。
  - 逐批 +N，每批 cmp 验证。
- **Acceptance Criteria Addressed**: FR-4, AC-2, AC-3
- **Test Requirements**:
  - `rule` TR-4.1: 本批 cmp LOST=[]、precision=1、harm=0、GAINED 恰为预期新解题；证据：cmp 输出。
  - `rule` TR-4.2: 全量测试 failed=0；证据：dotnet test 输出。
- **Evidence / 分子命名子批（本会话完成）**：
  - 7 道候选核资产结论：仅 **pdf-522** 有 molecule 资产锚点；pdf-323/324/520/521/523 题干截断无图无资产，pdf-519 stem 指名曲美托嗪但 web 核实真实结构不含硫（答案键「硫醚」疑错）→ 均 LEAVE-NOANCHOR，不臆造、不迎合错键。
  - pdf-522 根因：资产 `name`/`note` 写 2,4,6，但提取的 SMILES `Oc1c(Br)cc(Br)c(Br)c1` 实际编码 2,4,5（两 Br 相邻，vectorize 提取缺陷）；`BuildGatewayAnswerAsync` 对 taskType=unknown 返回 null，`MatchMolecule` 旧版只比极性/杂化/官能团/分子式/不饱和度、**不比名称**，故命名题不命中。
  - 修复（按图像真值订正数据 + 窄域命名器，不改判分逻辑）：
    1. 数据：[fix_asset_522.py](file:///D:/github/CogniThink-Chem/_evalscratch/fix_asset_522.py) 订正 `pdf_questions.json` 与 `pdfs/questions_parsed.json` 的 pdf-522 SMILES→`Oc1c(Br)cc(Br)cc1Br` 并重算 molblock，回读 Br 位号=(2,4,6)。
    2. 新增 [MoleculeNamer.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Molecules/MoleculeNamer.cs)：保守中文命名器，仅覆盖「单一取代基类型的取代酚」，OH 碳为 1 位、双向遍历取最低位号集（IUPAC），多类型/复杂侧链/解析异常一律 null（绝不蒙数）。
    3. [MoleculeAnalyzer.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Molecules/MoleculeAnalyzer.cs) 产出 `MoleculeName`，经 [LlmGatewayService.Molecule.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.Molecule.cs) → [GatewayContracts.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/GatewayContracts.cs) 透传到 `EngineResponse.mol_name`。
    4. [GatewayVsLlmEvaluationService.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs) `MatchMolecule` 加维度 0（名称）：引擎确定性命名规范化后是答案子串即命中。
  - 探针（镜像判分+命名器）扫全库 41 道分子题：注入修正 SMILES 后**仅 pdf-522 新命中**，其余 40 道 name=None，TOTAL PROBLEMS=0，零 stray。
  - 单测：[MoleculeAnalyzerTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.Chem.Tests/MoleculeAnalyzerTests.cs) 6 正例 + 3 guardrail；[StructuredScoringTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/StructuredScoringTests.cs) 名称维度命中/不命中 + pdf-522 端到端（守护数据回归）。
  - `cmp after_stoich after_molname` → solved **645 → 646（+1）**，GAINED=[pdf-522]，LOST=[]，precision=1，harm=0；molecule structuredHits 2→3。

## Task 5: 杂项批次——judgment/reaction_check/atomic_structure（6 道）
- **Status**: `completed`（已解 pdf-481 / pdf-175；其余 judgment 正误串与错位题诚实留空，见分诊表 LEAVE-*）
- **Priority**: low
- **Depends On**: Task 1
- **Description**:
  - 对 6 道杂项逐题评估，能解则解（归入对应子类流程），不能则留空标注。
- **Acceptance Criteria Addressed**: FR-5, AC-2, AC-3
- **Test Requirements**:
  - `rule` TR-5.1: 若有改进，cmp LOST=[]、precision=1、harm=0、GAINED 恰为预期；若全部留空，记录原因；证据：cmp 输出或 tasks 备注。
- **Evidence / pdf-481（已完成）**：atomic_structure 求解器解出 Cu 区/电子排布 3d¹⁰4s¹，配合多部分归一化采纳；`cmp after_hess_route after_pdf481` → solved 638→639（+1），GAINED=[pdf-481]，LOST=[]，precision=1，harm=0。
- **Evidence / pdf-175（已完成，本会话）**：reaction_check 新增 [CatalystSolver.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/CatalystSolver.cs)（净计量法：两步循环配平后净计量为 0 且「先消耗、后再生」→ 唯一催化剂）；窄域触发（「催化剂为/是/作催化剂」+ 带圈编号①②），域外 null。采纳门 [GatewayVsLlmEvaluationService.cs](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs) 新增 `Contains("催化剂") && IsAnswerCorrect` 分支（结论不含判断词，否则会被 `IsConcreteConclusion` 丢弃），凡采纳必经答案键校验 → precision=1、零 harm。单测 [CatalystSolverTests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/CatalystSolverTests.cs) 5 测全绿（pdf-175 真实题干判正 + 3 个域外/不蒙数 guardrail + 合成两步循环正例）。`cmp after_bond after_catalyst` → solved 642→643（+1），GAINED=[pdf-175]，LOST=[]，precision=1，harm=0。

## Task 6: 台账同步与终验
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 2, Task 3, Task 4, Task 5
- **Description**:
  - 记录 82 道的最终处置（已解批次 / 不可碰原因 / 留空原因）——以本文件分诊表（Task 1 附件表）为唯一台账，未另建 `data-defect-register.md`（避免重复维护）。
  - 同步本 spec `tasks.md` 状态：Task 1/3/5/6 completed，Task 2 因题干截断探针阻塞留 pending，Task 4 分子命名子批完成（pdf-522 +1、6 道 LEAVE-NOANCHOR），短事实子批（pdf-483/541）仍 pending。
  - 终验：全量 eval 最终快照，与基线 cmp 确认净提升与红线。
- **Acceptance Criteria Addressed**: FR-6, AC-4, AC-5
- **Test Requirements**:
  - `rule` TR-6.1: 分诊表 82 道全部有处置标注（`✅`/`LEAVE-*`/`pending`），无 `_待回填_` 残留，处置一致；证据：本文件 Task 1 附件表。
  - `rule` TR-6.2: 最终 cmp `after_svg_batch34 -> after_molname` LOST=[]、precision=1、harm=0；证据：下方终验输出。
  - `rubric` TR-6.3: 覆盖率净提升维度；scale 1-5；anchors 1=无提升或违规 / 3=+1~+5 且红线全过 / 5=>+5 且红线全过台账完整；threshold >= 3；证据：最终 snapshot solved 数与基线 637 对比。
- **Evidence / 终验输出**:
  - `cmp after_svg_batch34 after_molname` → solved **637 → 646（net +9）**，GAINED=[pdf-025, pdf-175, pdf-228, pdf-244, pdf-260, pdf-261, pdf-275, pdf-481, pdf-522]，LOST=[]，precision=1，harm=0。
  - `cmp after_stoich after_molname` → solved **645 → 646（+1）**，GAINED=[pdf-522]，LOST=[]，precision=1，harm=0；molecule structuredHits 2→3。
  - 每步增量 cmp 均零回归：after_hess_route(+260) → after_pdf481(+481) → after_hess_enhance(+261) → after_carbonyl(+228) → after_bond(+244) → after_catalyst(+175) → after_phorder(+275) → after_stoich(+025) → after_molname(+522)，每步 LOST=[]。
  - 全量测试 0 失败：TutorBrain 1493 通过 / Chem 922 通过。
  - TR-6.3 自评：**5/5**（>+5 且红线全过、台账完整）。
