# 引擎能力缺口补强（P0/P1 批次）— Product Requirements Document

## Overview
- **Summary**：在 pdf-278 多部分分发已落地（引擎 649/757）的基线上，继续修复「引擎待补强 63 题」计划（[引擎待补强63题逐题分析与补强计划.md](file:///d:/github/CogniThink-Chem/引擎待补强63题逐题分析与补强计划.md)）中的 P0/P1 项：扩展/新建确定性窄域求解器，使引擎能解出更多「LLM 答对、引擎答错」的题目，同时严守零假阳性、零 harm、零回归红线。
- **Purpose**：把"引擎已有能力但未触发"（R1）和"窄域确定性可解"（R2）的题目从 LLM 领先转为引擎正确，提升引擎在确定性窄域的覆盖率；对已解题加鲁棒性守卫防回退；对数据缺陷/图像依赖/多部分耦合无法解的题诚实登记、保持 null。
- **Target Users**：化学引擎开发者、评估与回归责任人。

## Goals
- G1：d5-1 / d5-2（STP 物质状态陷阱）、d7-4（手性碳）**当前已解出**，加 taskType 无关的鲁棒性守卫 + 全库回归测试，确保不因 LLM taskType 漂移回退。
- G2：修复 pdf-262 题库数据缺陷（题干去重、答案键校正），并新建通用 ΔG=ΔH−TΔS 自发性求解器覆盖自包含子问。
- G3：新建「已知 pH + Ksp → 残留离子浓度」通用求解器，覆盖 pdf-168 子问(3) 的 Ksp 计算。
- G4：扩展 VSEPR/键角触发，使 pdf-506 的杂化/键角比较子问能路由到 VseprSolver（不被兄弟子问的「水解」关键词错误抢占为 equilibrium）。
- G5：扩展限量离子方程式求解器覆盖「等物质的量 NaHCO3 + Ba(OH)2」（产物唯一），解出 trap-limit-3。
- G6：为 pdf-272 补全「曲线交点 pH → Ka=10⁻ᵖᴴ」推理链（资产存在），并尝试整题多部分解出。
- G7：对 P2（常见反应方程生成器 / 燃烧法测分子式 / concept_fact 窄事实策展）产出可行性结论，不贸然提交求解器。
- G8：全量 757 题回归验证引擎层 LOST=0 / harm=0 / precision=1，27 道多部分守卫题 0 回退。

## Non-Goals
- 不强行解出图像依赖（装置图/结构简式/质谱图）、开放论述、无唯一解的题——保持 null，诚实登记。
- 不改任何已解题的行为或判决字段；不放松采纳门控。
- 不为单题硬编码解法（所有新增必须是通用规则 / 外置事实表）。
- P2 仅做可行性验证，不提交未被证明零假阳性的求解器。
- 不修改 data-defect-register.md 以外的登记类文档的事实陈述（数据修复仅限 pdf_questions.json 的题干/答案键校正，依项目记忆 L45）。

## Background & Context
- 计划文档数据源自 [snapshot_engine_vs_llm_20261126.json](file:///d:/github/CogniThink-Chem/_evalscratch/snapshot_engine_vs_llm_20261126.json)（引擎 643），**早于当前代码**：d5-1/d5-2/d7-4/pdf-278 已由 routing_focused / pdf-278 多部分分发解出，当前快照 `pdf278_final`（引擎 649）中这 4 题均为 gatewayCorrect=True。本 spec 以 `pdf278_final` 为真实基线。
- 各目标题当前状态（已探针核实，见 [_evalscratch/probe_targets.py](file:///d:/github/CogniThink-Chem/_evalscratch/probe_targets.py) 等）：
  - d5-1 / d5-2 / d7-4：**已解出**（concept_fact），但触发依赖 LLM 把 taskType 归到 concept/unknown/calculation 分支；若归为 equilibrium 则对应求解器不可达 → 鲁棒性风险。
  - pdf-168：未解，taskType=equilibrium，多部分（子问 2/3）；引擎 Ksp 仅覆盖沉淀转化比值（[AqueousEquilibriumEngine.cs](file:///d:/github/CogniThink-Chem/src/CogniThink.Chem.Core/Inference/AqueousEquilibriumEngine.cs)），不覆盖「pH → 残留离子浓度」。
  - pdf-262：未解，**数据缺陷**：题干被重复拼接 3 次、answer='低' 与 4 子问不匹配、子问(2)(4) 依赖图片表格。确认为 2024 贵州卷第 17 题（官方 PDF 可获取）。
  - pdf-272：未解，多部分 3 子问，唯一 C 类；曲线资产 SVG 存在（[pdf-272_p097_n11_1_curve.svg](file:///d:/github/CogniThink-Chem/pdfs/vector_svg/pdf-272_p097_n11_1_curve.svg)），曲线读数能读出点，但缺「交点 → Ka」推理。
  - pdf-506：未解，多部分 3 子问；因子问(3)含「水解」被 `ClassifyTaskDeterministic` 整条规则链强制归 equilibrium，阻塞 VSEPR。
  - trap-limit-3：未解，合成用例（计入 757 eval，不在 pdf_questions.json），产物唯一。
- 红线（项目记忆，必须遵守）：precision=1 / harmCount=0 / 引擎层 LOST=0；无唯一解不蒙数（返回 null）；不硬编码单题解法；引擎是唯一真值源，pureLlm 波动不计入代码回归。

## Functional Requirements
- **FR-1（鲁棒性守卫）**：STP 物质状态陷阱（d5-1/d5-2）与手性碳判定（d7-4）的触发改为 taskType 无关——即使 LLM 把 taskType 误归为 equilibrium/vsepr 等分支，也能命中对应求解器；不抢占任何已解题结论。
- **FR-2（pdf-262 数据修复）**：在 pdf_questions.json 中对 pdf-262 去除重复拼接的题干、校正答案键（以 2024 贵州卷官方答案为准）；不修改引擎逻辑。
- **FR-3（ΔG 自发性求解器）**：新建通用求解器，对「ΔH、ΔS、T 已知，判断能否自发」问法计算 ΔG=ΔH−TΔS 并按符号判定 能/不能；缺数据 / 单位不可解析 / 多解时返回 null。接入计算链（不影响既有 Hess 等求解器）。
- **FR-4（Ksp-pH 浓度求解器）**：新建通用求解器，对「调 pH=x，已知 Ksp，求 c(Mⁿ⁺)」计算 c = Ksp / [OH⁻]ⁿ（或 [H⁺] 相关式）；缺 Ksp/pH/物种价态 / 不唯一时返回 null。
- **FR-5（VSEPR 触发扩展）**：修正 `ClassifyTaskDeterministic` 中「水解」等 equilibrium 线索对含键角/杂化问题的抢占，使键角比较与杂化子问能进入 vsepr 分支；扩展 VseprSolver 覆盖比较理由题；域外/不唯一返回 null。
- **FR-6（限量离子方程式）**：扩展 `ProductWriterSolver`（或等价通用链）覆盖「等物质的量」反应物比例的离子方程式书写，仅当产物唯一时输出，否则 null；解出 trap-limit-3。
- **FR-7（曲线→Ka 推理链）**：对曲线资产读出的 δ-pH 交点（c(HA)=c(A⁻)）推导 Ka=10⁻ᵖᴴ；仅当交点可精确定位时输出，否则 null。结合方程书写尝试 pdf-272 整题解出。
- **FR-8（P2 可行性）**：调研常见反应方程生成器、燃烧法测分子式、concept_fact 窄事实策展的可行性与零假阳性风险，产出 go/no-go 结论；无证明不提交求解器。
- **FR-9（诚实登记）**：每个目标题在 spec/tasks 中记录最终处置（解出 / 数据缺陷已修 / 多部分耦合未解 / 缺资产未解 / P2 暂缓），0 静默遗漏。

## Non-Functional Requirements
- **NFR-1**：全量 757 题 eval，引擎层 `gatewayCorrect True→False` LOST=0、truthAdopted 回退=0、harm=0、precision=1。
- **NFR-2**：不蒙数——每个新求解器在无唯一确定性解时返回 null，并有单测覆盖 null 分支。
- **NFR-3**：不硬编码单题——化学事实外置 JSON / 通用规则，新增能力对域外题返回 null（全库扫描 newlyFiredWrong=0）。
- **NFR-4**：Release 构建通过，全量单元测试 0 失败。
- **NFR-5**：数据修复仅限 pdf_questions.json 的题干/答案键校正（依项目记忆 L45），不动引擎核心逻辑。
- **NFR-6**：27 道已解多部分守卫题（verify_mp27）全部保持判正，0 回退。

## Constraints
- **Technical**：Release 构建（`dotnet build/test -c Release`）；eval 用 `EvalRunner` 断点续跑 + `cmp.py` / `diff_engine.py` / `verify_mp27.py`；`$env:ASPNETCORE_ENVIRONMENT="Development"`。
- **Business**：零假阳性优先于覆盖率——宁可少解、不可错解。
- **Dependencies**：pdf-262 数据修复依赖官方 2024 贵州卷答案获取；pdf-272 依赖曲线资产能被 eval 加载。

## Assumptions
- A1：`pdf278_final` 快照（引擎 649）为当前代码有效基线，作为本 spec 回归对比基准。
- A2：trap-limit-3 等物质的量 NaHCO3+Ba(OH)2 产物唯一（HCO3⁻+Ba²⁺+OH⁻=BaCO3↓+H2O），可由守恒配平验证。
- A3：pdf-262 子问(1) 自包含可解（ΔH1、ΔS、T 均在题干文本中），子问(2)(4) 依赖图片表格数据，本 spec 不强求整题判正（诚实登记）。

## Acceptance Criteria

### AC-1: d5-1/d5-2/d7-4 鲁棒性守住
- **Type**: `rule`
- **Given**: 当前代码 d5-1/d5-2/d7-4 gatewayCorrect=True
- **When**: 加入 taskType 无关守卫后跑全量 eval，并加单测强制 taskType=equilibrium 时这 3 题仍能命中求解器
- **Then**: 全量 eval 中 3 题仍 gatewayCorrect=True；单测断言 equilibrium 分支下不返回 null
- **Pass Condition**: 3 题 eval 判正 + 鲁棒性单测通过
- **Evidence**: 全量 checkpoint 记录 + 新增单测文件

### AC-2: pdf-262 数据修复 + ΔG 求解器
- **Type**: `rule`
- **Given**: pdf-262 当前题干重复拼接、answer='低' 错配
- **When**: 从官方源校正 pdf_questions.json 并新建 ΔG 求解器
- **Then**: pdf-262 题干去重、答案键与官方一致；ΔG 求解器对 ΔG<0 返回「能」、ΔG>0 返回「不能」、缺数据返回 null；pdf-262 最终状态被诚实记录（整题判正 或 因子问(2)(4) 图片依赖保持 null 但子问(1) 可解）
- **Pass Condition**: 数据修复已落库且经官方核对；ΔG 单测覆盖正/负/null 三分支；pdf-262 处置有记录
- **Evidence**: pdf_questions.json diff + ΔG 单测 + tasks 处置记录

### AC-3: Ksp-pH 浓度求解器
- **Type**: `rule`
- **Given**: pdf-168 子问(3) 需 c(Al³⁺)=Ksp/[OH⁻]³
- **When**: 新建求解器并接入
- **Then**: 对 pH=6、Ksp(Al(OH)3)=1.3×10⁻³³ 输出 1.3×10⁻⁹；缺 Ksp/pH/价态返回 null；pdf-168 最终状态诚实记录（整题判正 或 因子问(2) 流程推理未解保持 null）
- **Pass Condition**: 求解器单测断言 1.3e-9 与 null 分支；pdf-168 处置有记录
- **Evidence**: 单测 + tasks 记录

### AC-4: VSEPR 触发扩展
- **Type**: `rule`
- **Given**: pdf-506 被「水解」抢占为 equilibrium
- **When**: 修正分类抢占并扩展 VseprSolver
- **Then**: 含键角/杂化的子问进入 vsepr 分支且求解器命中；域外返回 null；全库已解题 taskType 零漂移（无 LOST）；pdf-506 最终状态诚实记录
- **Pass Condition**: pdf-506 相关子问路由到 vsepr 且有结论；diff_engine 无 LOST；pdf-506 处置有记录
- **Evidence**: 单测 + diff_engine 输出 + tasks 记录

### AC-5: trap-limit-3 解出
- **Type**: `rule`
- **Given**: trap-limit-3 产物唯一
- **When**: 扩展限量方程求解器
- **Then**: trap-limit-3 全量 eval gatewayCorrect 翻正为 True；非唯一限量场景返回 null
- **Pass Condition**: 全量 checkpoint 中 trap-limit-3 gatewayCorrect=True；null 分支单测存在
- **Evidence**: 全量 checkpoint 记录 + 单测

### AC-6: pdf-272 曲线→Ka 推理链
- **Type**: `rule`
- **Given**: pdf-272 曲线资产存在，Ka1=1.0×10⁻¹.²
- **When**: 补全交点 pH→Ka 推理 + 尝试整题
- **Then**: 能从曲线交点精确读出 pH 时输出 Ka=10⁻ᵖᴴ（对 pdf-272 得 10⁻¹.²）；交点不可定位时返回 null；pdf-272 最终状态诚实记录（整题判正 或 因子问/资产加载问题保持 null）
- **Pass Condition**: 推理单测断言交点→Ka；pdf-272 处置有记录
- **Evidence**: 单测 + tasks 记录

### AC-7: P2 可行性结论
- **Type**: `rule`
- **Given**: P2 三项探索方向
- **When**: 完成可行性调研
- **Then**: 每项产出 go/no-go + 零假阳性风险评估；未通过验证的不提交求解器
- **Pass Condition**: spec/tasks 中 P2 每项有明确结论与理由
- **Evidence**: tasks 记录 / 调研笔记

### AC-8: 全量回归红线
- **Type**: `rule`
- **Given**: 基线 pdf278_final（引擎 649）
- **When**: 全部任务完成后跑全量 757 eval
- **Then**: `diff_engine` 引擎层 LOST=0、truthAdopted 回退=0、harm=0、precision=1；`verify_mp27` 27/27 PASS；全量单测 0 失败
- **Pass Condition**: diff_engine 报告 LOST=[]、harm=0；verify_mp27 全 PASS；dotnet test 0 failed
- **Evidence**: diff_engine / verify_mp27 输出 + 测试结果

### AC-9: 诚实覆盖登记
- **Type**: `rubric`
- **Dimension**: 每个目标题的处置可追溯性与完整性
- **Scale**: 1-5
- **Anchors**: 1 = 多题无处置记录 / 静默遗漏；3 = 大部分有记录但缺理由；5 = 每题（d5-1/d5-2/d7-4/pdf-168/pdf-262/pdf-272/pdf-506/trap-limit-3 + P2 三项）都有「解出/受阻原因/证据」完整记录，0 静默遗漏
- **Pass Threshold**: >= 4
- **Evidence**: spec.md / tasks.md / review.md 处置记录

## Open Questions
- [x] pdf-262 处置 → 用户决定：先修数据 + 建 ΔG 求解器（接受图片子问可能未解）。
- [x] d5/d7 已解 → 用户决定：加鲁棒性守卫 + 回归测试。
- [x] 本轮范围 → 用户决定：全量可行 + 诚实登记，引擎层零回归零假阳性为硬红线。
- [ ] pdf-168 子问(2) 滤渣成分（Fe2O3/SiO2）是否存在可复用的流程推理/事实能力，需在实现期核查；若不可解则 pdf-168 整题保持 null（诚实登记）。
- [ ] pdf-506 子问(3)（配位键 + 杂化 sp3→sp2）VseprSolver 能否全覆盖，需在实现期核查；若不可解则 pdf-506 整题保持 null。
- [ ] pdf-272 曲线资产在 eval 中是否能加载到 imageAssets（当前 checkpoint 显示 imageAssets=False），需在实现期核查；若不可加载则 pdf-272 保持 null。
