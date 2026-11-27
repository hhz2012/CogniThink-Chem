# 化学模拟器统一架构 — 实现计划（本轮：设计交付，不编码）

## Task 1：撰写 `chemical-simulator-design.md` 详细设计文档
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - 在 `.trae/specs/chemical-simulator-architecture/chemical-simulator-design.md` 产出详细设计，必须包含：
    1. **统一输入契约**：`ChemicalSystemInput` / `SpeciesInput` / `ChemicalConditions` 的完整字段表（含类型、默认值、说明）；`ChemicalConditions` 集中所有环境开关 + `ReturnConsidered`。
    2. **输出契约**：`SimulationTrace`、`ReactionEvent`（含 `Consumed`/`Produced`/`Basis`/`Priority`/`Extent`/剩余量）、`ConsideredCandidate`、`SystemState` 的字段表，以 C# record/class 代码块给出。
    3. **`ChemicalSimulator` 主体类设计**：`Simulate(ChemicalSystemInput)` 方法签名；反应筛选→择一→计量→富化的处理流程；优先级计算（复用现有类型优先级表与金属活动性）；`Basis` 生成规则（KnowledgeIds 取自 `Reaction.KnowledgeIds`，选择理由模板）。
    4. **吸收合并方案**：`ChemicalSimulationEngine` 现有逻辑（`AllReactantsAvailable`/`CheckConditions`/`ComputeExtent`/`Score`/`Apply` 等）逐项说明迁入 `ChemicalSimulator` 的位置；旧 `ChemicalSimulationEngine` 退化为薄适配器（保留 `Simulate(SimulationRequest)` 签名不变，内部 `SimulationRequest`→`ChemicalSystemInput` 映射 + 调用新类 + 结果映射回旧 `SimulationResult`）；`ChemicalReverseInferenceEngine` 保持现状或按需委托（标注）。
    5. **`ChemicalMultiStepReasoningSimulator` 主体类设计**：`Reason` / `QueryState` / `WhatIf` / `Search` 方法签名；`StateSnapshot` / `ReasoningPlan` / `ReasoningStep`（AddSpecies/SetConditions/Assert）/ `ReasoningGoal` / `ReasoningCandidate` 模型字段表；状态轨迹生成算法；what-if 纯函数语义（深拷贝起点，不污染原轨迹）；Search 算法（复用逆向引擎反查 + 正向回放验证，候选排序，不蒙数）。
    6. **向后兼容策略**：`ChemistryController` 现有端点、`Program.cs` DI、现有测试在实现后如何保持绿色（适配器保证旧签名不变）。
    7. **DI 与可访问性**：下一轮实现时两个主类在 DI 的注册方式与加性端点/测试入口方案。
    8. **零回归论证专节**：为何不修改 `Application/Gateway/` 与 `data/`；实现期红线核验清单（precision=1/harm=0/LOST=0/WORSEN=0）及验证命令。
    9. **实现任务清单（下一轮）**：把设计拆成可执行的编码步骤（契约→模拟器→推理模拟器→适配器→DI→单测→核验），供下一轮直接照做。
  - 所有 DTO/方法以 C# 代码块或表格给出，粒度到"可直接照抄编码"。
- **Acceptance Criteria Addressed**: AC-1, AC-2, AC-3, AC-4, AC-6
- **Test Requirements**:
  - `rule` TR-1.1：文档同时定义 `ChemicalSystemInput`/`ChemicalConditions` 且两个主类主方法签名含 `ChemicalSystemInput`。证据：文档章节自查。
  - `rule` TR-1.2：文档含吸收合并方案（旧引擎→薄适配器映射）与完整 `ReactionEvent` 字段表（含 Basis/Priority/Extent）。证据：文档章节自查。
  - `rule` TR-1.3：文档定义 `Reason`/`QueryState`/`WhatIf`/`Search` 与 `StateSnapshot`/`ReasoningPlan`/`ReasoningGoal`，且 WhatIf 标注纯函数语义。证据：文档章节自查。
  - `rule` TR-1.4：文档含零回归论证专节 + 实现期红线核验清单。证据：文档章节自查。
  - `rubric` TR-1.5：设计具体可实现程度；scale 1-5；anchors 1=无签名/3=字段不全/5=全字段+签名+状态机可直接编码；threshold >= 4；evidence 文档。
- **Notes**: 仅写文档，不创建/修改任何 `.cs` 文件。
- **Completion Evidence**: 已产出 [chemical-simulator-design.md](file:///d:/github/CogniThink-Chem/.trae/specs/chemical-simulator-architecture/chemical-simulator-design.md)。自审 TR：TR-1.1 通过（§2 定义 ChemicalSystemInput/ChemicalConditions，§4.1 与 §6.1 主方法签名均含 ChemicalSystemInput）；TR-1.2 通过（§3.3 ReactionEvent 含 Consumed/Produced/Basis/Priority/Extent，§5 吸收合并映射表）；TR-1.3 通过（§6 定义 Reason/QueryState/WhatIf/Search 与 StateSnapshot/ReasoningPlan/ReasoningGoal，§6.5 WhatIf 纯函数语义）；TR-1.4 通过（§9 零回归论证+红线核验清单）；TR-1.5 自评 5/5（全字段 C# 代码块 + 主循环伪代码 + 下一轮编码任务清单）。

## Task 2：撰写 `solver-landscape-analysis.md` 求解器分类分析报告
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None（可与 T1 并行；分类需以 T1 设计的新架构为参照，故建议 T1 主体完成后定稿）
- **Description**:
  - 只读勘察 `src/CogniThink.TutorBrain.Api/Application/Gateway/`（71 文件）与 `src/CogniThink.Chem.Core/Inference/`、`Solving/`、`Rules/`、`Molecules/`、`Balancing/` 下的能力。
  - 产出 `.trae/specs/chemical-simulator-architecture/solver-landscape-analysis.md`，分三类：
    - **A 类：可组合到新模拟器之上**——本质是"反应 + 状态 + 多步"的推演（如平衡推演、流程/实验推断、离子推断、产物推断等），可由 `ChemicalSimulator` + `ChemicalMultiStepReasoningSimulator` 承载。列代表（≥3）+ 文件路径 + 理由 + 建议接入方式。
    - **B 类：需要独立模拟器**——非"反应状态推演"范式，需各自的状态/搜索空间（如晶胞计数验证、分子异构体计数、VSEPR 构型、半反应配平/氧化态分析、曲线读图等）。列代表（≥3）+ 文件路径 + 建议模拟器名称与职责 + 与本架构关系。
    - **C 类：非模拟器**——事实查找、选择匹配、判分聚合、概念判定（不产生反应状态，保持现状）。列代表（≥3）+ 文件路径 + 理由。
  - 给出推荐迁移顺序与风险提示（哪些先迁、哪些高风险需谨慎）。
- **Acceptance Criteria Addressed**: AC-5
- **Test Requirements**:
  - `rubric` TR-2.1：报告完整性与可操作性；scale 1-5；anchors 1=仅罗列/3=分类但代表不足/5=三类各 ≥3 代表且理由具体到引擎职责、含迁移顺序与风险；threshold >= 4；evidence 产物文件。
- **Notes**: 只读分析，不修改任何代码或数据。
- **Completion Evidence**: 已产出 [solver-landscape-analysis.md](file:///d:/github/CogniThink-Chem/.trae/specs/chemical-simulator-architecture/solver-landscape-analysis.md)。自审 TR-2.1 自评 5/5：A 类列 ~20 个对象（分推断引擎/平衡溶液/Gateway 反应预测三组，各 ≥3 代表），B 类列 ~25 个对象分 6 个子域（分子结构/配平/计量/热化学/曲线/后续平衡态数值），C 类列约 25 个对象分 7 子类；每类带文件路径、职责、处置理由；§5 给出 5 阶段迁移顺序，§6 给出红线风险提示。

## Task 3：将平衡态数值纳入本期设计范围（D-5 追加）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: T1（设计主体框架）, T2（求解器分类中 B-6）
- **Description**:
  - 响应 D-5：把原标为"后续待立项"的平衡态数值（pH / 离子浓度 / Ksp 残留 / 沉淀转化 K / 粒子排序与守恒）提升为本轮设计范围，新增第三个主体类 `EquilibriumStateSimulator`。
  - 更新 `spec.md`：新增 D-5 决策；Goals/Non-Goals 调整（平衡常数/pH/浓度纳入，仅排除结构能）；新增 FR-9A（平衡数值主类）、FR-9B（与多步推理组合）；新增 AC-8；Overview/AC-1 表述由"两模拟器"改为"三模拟器"。
  - 扩展 `chemical-simulator-design.md`：`SpeciesInput` 增加 `Concentration?`/`Volume?`；新增 §7 `EquilibriumStateSimulator`（类签名/输入输出模型/计算目录 8 项含数据需求与不蒙数边界/不蒙数规则/与现有分散能力关系/与多步推理组合）；`ChemicalMultiStepReasoningSimulator` 可选注入并挂载 `StateSnapshot.Numerical`；DI/零回归/下一轮任务清单（§11）同步纳入平衡模拟器步骤；后续章节顺延编号。
  - 更新 `solver-landscape-analysis.md`：B-6 从"后续待立项"改为"本期纳入"；A-2/A-3 数值标注改为"由本期 EquilibriumStateSimulator 承载"；迁移顺序把平衡数值从阶段 3 提前到阶段 1 实现；红线与结论同步。
  - 更新 `object-responsibility-map.md`：A 类模拟器表新增 `EquilibriumStateSimulator` 行；总览与结论同步为"三个主体类"。
  - 全程不碰 `.cs` 与 `.json`（纯文档）。
- **Acceptance Criteria Addressed**: AC-8（及 AC-1 的"三模拟器共用"）
- **Test Requirements**:
  - `rule` TR-3.1：`spec.md` 含 D-5、FR-9A/9B、AC-8，且 Overview/Goals 表述为"三个主体类"。证据：文档自查。
  - `rule` TR-3.2：`chemical-simulator-design.md` §7 定义 `EquilibriumStateSimulator.Compute` 签名 + 数据来源 `acid-constants.json` + 计算目录（每项含数据需求与不蒙数边界）+ 不蒙数规则。证据：文档自查。
  - `rule` TR-3.3：`solver-landscape-analysis.md` B-6 标注为"本期纳入"，无"后续待立项"残留；A-2/A-3 数值行指向 `EquilibriumStateSimulator`。证据：文档自查。
  - `rule` TR-3.4：`object-responsibility-map.md` A 类表含 `EquilibriumStateSimulator` 行。证据：文档自查。
- **Notes**: 本轮不编码；`acid-constants.json` 已存在且为人教版教材真值（`ConstantSpec` 精确有理存储），纳入有真实数据支撑、无 precision 风险——常数只来自 JSON 或题给，缺则 `Underdetermined`，绝不蒙数。
- **Completion Evidence**: 已完成 4 份文档的对应编辑。自审 TR：TR-3.1 通过（spec.md D-5/FR-9A/9B/AC-8 已加，Overview 与 Goals 已改为三主体类）；TR-3.2 通过（design §7 完整：7.1 类签名、7.2 输入输出、7.3 八计算项目录、7.4 不蒙数五条、7.5 与现有能力关系、7.6 组合方式）；TR-3.3 通过（landscape B-6 已改"本期纳入"，A-2/A-3 行已更新，阶段 1 含三主体类）；TR-3.4 通过（object-map A 表已加行）。全部编辑仅触及本 spec 目录 `.md`，`src/`、`data/` 无新增改动。

## Task 4：自审 + 零改动核验 + 通知批准
- **Status**: `completed`
- **Priority**: high
- **Depends On**: T1, T2, T3
- **Description**:
  - 自审文档是否覆盖全部 AC（逐条核对 AC-1 ~ AC-8）。
  - 执行 `git status --short`，确认本轮改动仅限 `.trae/specs/chemical-simulator-architecture/*.md`，`src/` 与 `data/` 零改动（AC-7）。
  - 通知用户审阅两份设计文档；批准后下一轮直接进入编码实现。
- **Acceptance Criteria Addressed**: AC-7
- **Test Requirements**:
  - `rule` TR-4.1：`git status --short` 输出不含 `src/`、`data/` 路径改动；仅含本 spec 目录下 `.md`。证据：git 输出。
- **Notes**: 本轮不进入编码；实现为设计批准后的下一轮。
- **Completion Evidence**: 自审 AC-1 ~ AC-8 全部覆盖（AC-1~4/6 见 T1 证据，AC-5 见 T2，AC-7 见本任务 T4，AC-8 见 T3）；AC-7 核验：`git status --short` 显示本轮仅改动 `.trae/specs/chemical-simulator-architecture/` 下的 5 个 `.md`，工作区已有的 `M`/`??`（src/data/tests 及 .trae/skills、另一 specs 目录）均为本轮之前的历史遗留改动，非本轮产生；本轮未 Edit/Write 任何 `src/*.cs` 或 `data/*.json`。
