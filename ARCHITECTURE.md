# CogniThink-Chem 架构现状与求解栈收敛决策

> 状态：**现状架构文档**（2026-11-22 起为事实来源）
> 与 [docs/](./docs) 的关系：`docs/01-03` 是 v0.2 **历史规划**（已标注为历史态），描述"当初想做什么"；本文件描述"现在实际是什么、后续能力往哪加"。冲突时以本文件与各 spec 为准。
> 关联分析：[.trae/documents/project-structure-analysis-and-roadmap.md](./.trae/documents/project-structure-analysis-and-roadmap.md)

---

## 1. 一句话定位

CogniThink-Chem = **知识驱动的确定性化学求解内核** + **LLM 只做语言层（NL↔结构化 + 话术）** 的高中化学 AI 助教。**化学结论只能由引擎产出，LLM 不得产出化学结论**——这是全项目不可动摇的红线。

当前评估基线：757 道 PDF 真题，解对 621，**precision=1、harm=0**；2144 个单元测试全绿。

---

## 2. 解决方案分层

```
CogniThink.Chem.sln
│
├─ src/CogniThink.Chem.Core        ← 化学原语层 + 结构化推断引擎（真值地基）
│   ├─ Balancing / Parsing / Math(Rational) / Molecules(RDKit)
│   ├─ Structure(PeriodicTable) / Reactions(ReactionLibrary)
│   ├─ Rules(RuleStore + RuleEngine)
│   ├─ Inference/*Engine           ← 12 类结构化推断引擎
│   ├─ Solving/ProblemSolver       ← 基准回归求解编排（见 §3）
│   └─ Explanation(Explainer + GuideGenerator)
│
├─ src/CogniThink.TutorBrain.Api   ← 产品服务层（面向学生/真题/前端）
│   ├─ Application/Gateway/*       ← 求解主入口：网关（见 §3、§4）
│   ├─ Application/Chemistry/*     ← 导学侧辅助引擎（共存检查等）
│   ├─ Application/Reasoning/*     ← 学生作答判分（非题目求解）
│   ├─ Application/Ability/*       ← 能力图谱
│   ├─ Application/Diagnostic/*    ← 诊断教学
│   ├─ Application/Agent/*         ← LLM 代理（转发 + 话术）
│   ├─ Application/Evaluation/*    ← 评估与采纳门控
│   └─ Api/Controllers/*           ← HTTP 入口
│
├─ src/CogniThink.Chem.Cli         ← 演示/自检/导出工具（--solve/--verify-real/--export-web）
├─ src/CogniThink.TutorBrain.EvalRunner  ← 757 题评估运行器（LLM 关闭下跑网关）
├─ src/CogniThink.TutorBrain.PdfOcrRepair
├─ tests/CogniThink.Chem.Tests     (911)
└─ tests/CogniThink.TutorBrain.Tests (1233)
```

**依赖方向**：Api → Core（单向）。Core 不依赖 Api。

---

## 3. 两套求解栈：现状与收敛决策（核心）

项目当前存在**两套并行的求解编排**，这是最关键的结构事实，也是后续所有"能力往哪加"的决策依据。

### 3.1 现状

| 维度 | 栈 A：Core `ProblemSolver` | 栈 B：Api 网关 `LlmGatewayService` |
|------|---------------------------|-----------------------------------|
| 入口 | [ProblemSolver.Solve(ProblemSpec)](./src/CogniThink.Chem.Core/Solving/ProblemSolver.cs) | [LlmGatewayService.ProcessStudentMessageAsync](./src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs)（HTTP [POST /api/gateway/process](./src/CogniThink.TutorBrain.Api/Api/Controllers/GatewayController.cs)） |
| 输入 | 结构化 `ProblemSpec`（人工标注 given/expected） | 自然语言题干文本 |
| 分派 | `ProblemCategory`（12 类） | `taskType`（13 类） |
| 真值来源 | 反应库 + 推断引擎 + 有理计算 | 事实库匹配 + Core 原语求解器 + 答案键门控 |
| 评估集 | 98 基准题 + 27 真题 | 757 PDF 真题 |
| 输出 | `Solution` + 讲解 + 引导 | `GatewayResult`（引擎结论 + LLM 话术） |

**复用关系**：Api 引用 Core，但网关**不调用** Core 的 `ProblemSolver` 或 `Inference/*Engine`。网关只复用 Core 的**底层原语**（`EquationBalancer`、`Parsing`、`Rational`、`PeriodicTable`、`ReactionLibrary`、`RuleStore`（加载 `data/highschool` 事实）、`Molecules`）——网关**不使用** `RuleEngine` 仲裁。在「求解编排」层，两者是两套独立实现。

### 3.2 收敛决策（已确认，2026-11-22）

> 本决策已于 **2026-11-22 经用户确认**（roadmap §6#1）。回退成本为零——本决策仅涉及文档与后续落位指引，**不改任何代码、不迁移任何数据**。

**决策：网关（栈 B）是面向学生与真题的主求解入口；Core `ProblemSolver`（栈 A）定位为「结构化基准回归引擎」。**

具体含义：

1. **新增学生可感知的求解能力，一律落在网关（栈 B）**，并最大化复用 Core 原语；不新增 Core `ProblemSolver` 的题类。
2. **Core `ProblemSolver` 只做两件事**：(a) 守护 98 基准 + 27 真题的推断正确性回归；(b) 当某推断能力（如平衡三段式、约束传播）可被网关复用时，抽取为 Core 内的共享服务供网关调用——**而不是在网关重写一套**。
3. **不做的事**（本期）：不强行把网关改造成调用 Core `ProblemSolver`；不把 757 题迁成 `ProblemSpec`；不合并两套评估集。收敛是「落位纪律」而非「代码重构」。

**为什么是这个方向**（而非反过来）：
- 网关已对齐 757 真题的真实形态（NL 输入、OCR 噪声、答案键门控），是离产品最近的一层；学生端与前端要接的也是它。
- Core 推断引擎的价值在「确定性推断正确性」，应由基准题守护，而非直接暴露给 NL 噪声。
- 反向收敛（网关能力下沉到 Core）会把 NL 解析、taskType 路由、答案键门控等产品关注点污染进领域层，破坏 Core 的纯净性。

### 3.3 每类题型「在哪维护」速查

| 能力 | 维护位置 | 说明 |
|------|----------|------|
| 选择/判断事实匹配 | 网关 `Application/Gateway/*Validator` + `data/highschool/*.json` | 事实只进 JSON，不进 `.cs` |
| 配平 / 半反应 / 晶胞 / 盖斯 等确定性计算 | 网关 `*Solver`，底层调 Core 原语 | 若 Core 已有等价能力，优先复用而非重写 |
| 平衡 / 离子共存 / 推断 等结构化推理 | Core `Inference/*Engine`（真值），网关按需调用 | 网关不得绕过 Core 自行重写推断逻辑 |
| 新题型求解器 | 先在网关新增 `taskType` 路由 + 求解器；若需推断，下沉 Core 原语 | 遵循 §3.2 纪律 |
| 基准回归（98+27） | Core `ProblemSolver` + `ProblemSolverTests` | 只减不增题类 |

### 3.4 规则/事实体系分工（P3-2，2026-11-22）

两套规则/事实体系**同域不同角色、非冗余**（维护型所有权视图与 KP 交叉引用见 [rule-fact-ownership.md](./tools/data-assets/rule-fact-ownership.md) / [rule-fact-crossref.json](./tools/data-assets/rule-fact-crossref.json)）：

| 资产 | owner | 角色 | 网关是否消费 |
|------|-------|------|--------------|
| `data/rules/rules.json`（61 条 if-then 规则） | Core | 仲裁/基准回归规则库（if-then + conflictGroup，`knowledgeId` 外键指向 knowledge.json） | **否**（网关不加载 rules.json、不调用 `RuleEngine.Decide`） |
| `data/rules/rule-tests.json`（9 条） | Core | 规则回归用例 | 否 |
| `data/highschool/*.json`（24 文件） | 网关为主，Core 复用子集 | 网关事实库（扁平 `match`/`stemMatch`） | 是（15 处 `RuleStore.Query`） |

- 网关只复用 Core 的 `RuleStore`（加载 `data/highschool` 事实），**不使用** `RuleEngine` 仲裁。
- `rules.json` 的 61 条规则全部带有效 `knowledgeId`（0 dangling，实测），覆盖 26/50 个 KP；24 个 KP 无规则（由 highschool 事实覆盖，或为内容缺口——属后续策展 spec）。
- **三文件名碰撞**（`ions`/`substances`/`solubility`）在 `data/highschool/`（Core）与 `src/CogniThink.TutorBrain.Api/data/chemistry/`（网关 `ChemicalKnowledgeBase`）各有一份，schema 与消费者均不同——**不合并**，逐根标注归属。
- 新增能力落位纪律：网关事实进 `data/highschool/*`；Core 仲裁规则进 `data/rules/rules.json` 且必须带有效 `knowledgeId`（不编造）。

---

## 4. 求解主链路（网关，栈 B）

```
学生题干文本（+ 配图资产）
  │
  ▼ ① 解析层：ParseStudentInputAsync
  │   LLM 把 NL → EngineRequest（task_type + reaction/known/options）
  │   失败 → FallbackParse（关键词）
  │   最后 ApplyDeterministicTaskType 用规则链强制覆盖 task_type（消除漂移）
  │
  ▼ ② 分类：ClassifyTaskDeterministic → 13 类 taskType
  │   choice/judgment/ion_check/balance/equilibrium/atomic_structure/
  │   calculation/reaction_check/structure_verify/vsepr/titration/concept/unknown
  │
  ▼ ③ 分发 + 多层守卫：CallEngineAsync（命中即返回，未命中回退，不蒙数）
  │   equilibrium → 曲线判定 → 概念事实 → 图/曲线/分子资产 → 晶胞/半反应
  │   → 原子结构/VSEPR/概念事实/陷阱 → 方程书写兜底 → concept/unknown 早返回 → switch
  │
  ▼ ④ 求解器/校验器（Application/Gateway/，55 文件）
  │   事实校验器（FactChoiceMatcher 共享内核）/ 陈述判断器（JudgeStatement）/ 算法求解器
  │
  ▼ ⑤ 采纳门控：BuildGatewayAnswerAsync（在 Evaluation 层）
  │   calculation 直接采纳数值；choice 需含选项字母；
  │   atomic/vsepr/concept_fact/judgment/equilibrium 仅采纳答案键校验通过的结论；
  │   balance/reaction_check 排除占位结论；其余 → 返回 null（诚实答不出）
  │   ★ 凡采纳必判正 → precision=1 是构造保证，不是统计结果
  │
  ▼ ⑥ GenerateReplyAsync：LLM 把引擎结论转述为教学话术（不改结论）
```

详见 [网关主链路源码](./src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs) 与 [采纳门控源码](./src/CogniThink.TutorBrain.Api/Application/Evaluation/GatewayVsLlmEvaluationService.cs)。

---

## 5. 数据资产分层

| 数据 | 位置 | 消费者 | 状态 |
|------|------|--------|------|
| 事实库（24 文件） | `data/highschool/*.json` | 网关事实校验器（经 `RuleStore`） | **活跃主真值** |
| 反应库 + 知识点 | `data/knowledge/` | Core 推断 + 网关书写求解器 | 活跃 |
| 旧规则库（37 条 L0–L4） | `data/rules/rules.json` | Core `RuleEngine` | 网关基本不直接消费（待 P3-2 归一） |
| 基准题（12 类 98 题） | `data/benchmark/problems/` | Core `ProblemSolver` | 活跃（基准回归） |
| 真题（27） | `data/real-exam/real-exam.json` | Core `--verify-real` | 活跃（基准回归） |
| PDF 真题（757） | `src/CogniThink.TutorBrain.Api/data/pdf_questions.json` | 网关 EvalRunner | 活跃（主评估集） |

数据纪律：化学事实只存 JSON，不硬编码进 `.cs`；事实落库必经探针门控（`TOTAL PROBLEMS=0` 才允许提交）与 NoOverlap 硬门。

---

## 6. 工程红线（不可动摇）

1. **precision=1 / harm=0**：凡被引擎采纳的结论必须判正；宁可返回 null（答不出）也不蒙数。
2. **LLM 不产出化学结论**：只做 NL↔结构化 与 话术转述；任何化学真值必须来自引擎/事实库。
3. **确定性分类覆盖 LLM**：`ClassifyTaskDeterministic` 能判出的 taskType 强制覆盖 LLM 值，保证 eval 可复现。
4. **零误伤优先于多解对**：新增能力前先确认不破坏已解对的题（全量回归 diff）。
5. **事实不进代码**：化学事实只在 JSON；`.cs` 只放算法与契约。

---

## 7. 文档导航

- **现状架构**：本文件（`ARCHITECTURE.md`）
- **结构分析与路线图**：[.trae/documents/project-structure-analysis-and-roadmap.md](./.trae/documents/project-structure-analysis-and-roadmap.md)（P0–P3 全计划）
- **已交付能力索引**：[.trae/specs/README.md](./.trae/specs/README.md)（各 spec 记录已落地能力）
- **历史规划（v0.2，已标注历史态）**：[docs/01-overall-plan.md](./docs/01-overall-plan.md)、[docs/02-modules.md](./docs/02-modules.md)、[docs/03-tasks.md](./docs/03-tasks.md)、[docs/decisions.md](./docs/decisions.md)
- **Core 推断引擎设计**：[.trae.documents/chem-inference-tutor-engine.md](./.trae.documents/chem-inference-tutor-engine.md)
- **能力缺口台账**：[data-defect-register.md](./data-defect-register.md)

---

## 8. 决策状态与回退

- 本文件 §3.2 的收敛方向已于 **2026-11-22 经用户确认**（roadmap §6#1）；P3-2 已落位「规则/事实体系分工」（§3.4）。后续落位均遵循该决策。
- 回退：若否决该方向，只需删除/改写本文件 §3.2、§3.3、§3.4，不涉及代码与数据迁移。
- 后续每个新立项（P1-2 及以后）的 spec 都必须在开头声明其落位遵循本文件 §3.3。
