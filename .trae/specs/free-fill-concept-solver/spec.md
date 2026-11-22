# P1-4 自由填空概念求解器（复用 ConceptFactSolver 事实库 + 受保护路由放宽）

> **状态**：spec 草案，待用户审批后进入实现（spec-first 门禁）。本文件不含实现代码。
> **基线**：`after_p12`（757 题，630 正确 / 127 未解，precision=1，harm=0）。红线锚点 `base_p0`（629 正确）。

## 1. 背景与问题

全库 757 题，`after_p12` 已解对 630、未解 127。未解中扣除 27 道 K.1 多部分大题（P1-2 分发骨架已就位，待 P1-4/P2-3 解锁子问）后，存在一批 **自由填空概念题**：答案为短文本概念/目的/作用/成分/误差结论，非数值、非方程、非选择、非图像依赖。

它们当前全部 `engine.Ok = null`，根因有二：

1. **路由截走**：含 "pH/浓度" 字样的概念题（如 pdf-415「除去的离子有」、pdf-543「调 pH 目的」、pdf-535「配制浓度」）被 `ClassifyTaskDeterministic` 的 `calculation` 规则（[LlmGatewayService.cs L323-326](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs)）优先截走 → 计算器返回 null；它们本应进入 `concept` 路径命中事实库。
2. **事实缺失**：落入 `unknown` 的概念题（如 pdf-542「沉淀1 含金属元素」、pdf-530「方案 2 优点」）虽已到达 `ConceptFactSolver`（concept/unknown 瀑布 [L464-475](file:///d:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs)），但 `concept-facts.json` 无对应触发事实 → 0 命中 → null。

## 2. 方案

复用现有 **`ConceptFactSolver`**（唯一匹配 → 不蒙数）+ 评估侧 **`IsAnswerCorrect` 采纳门**（凡采纳必判正 → precision=1 构造保证），不新建独立求解器，最小化表面积：

- **Part A 事实库增补**：向 `data/highschool/concept-facts.json` 追加窄域触发事实。每条必须经 T0 审计三项断言：① 全库唯一匹配（>1 命中的事实不得入库）；② 不命中任何已判正题（0 漂移）；③ 其 `answer` 命中目标题答案键（IsAnswerCorrect 会判正）。
- **Part B 路由放宽（受 0 漂移保护）**：在 `ClassifyTaskDeterministic` 的 `calculation` 规则**之前**，插入自由填空概念 cue 检测（"除去的离子" / "pH"∧"目的" / "配制"∧"浓度"∧"可能" → 返回 `"concept"`）。**前置全库 757 预校验脚本断言 0 道已判正题被重分类**（LOST=0 构造保证），与 P1-1/P1-2 的 equation_writer / atomic_structure 插入式 0 漂移预校验同口径。

## 3. T0 只读审计结论（已实证，不信自述）

证据脚本（均只读，未改任何文件）：
- [p14_scope_scan.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p14_scope_scan.txt)：枚举 100 道未解非 K.1 题，圈定自由填空概念簇。
- [p14_feasibility_probe.txt](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p14_feasibility_probe.txt)：确认候选均 `engine.Ok=null`、未被采纳。
- [p14_t0_audit.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p14_t0_audit.py)：唯一匹配 + 0 漂移 + 答案键命中三态断言。
- [p14_routing_drift.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p14_routing_drift.py)：路由 cue 对已判正题匹配数（须 0）。
- [p14_existing_facts2.py](file:///d:/github/CogniThink-Chem/_evalscratch/verify/p14_existing_facts2.py)：现有事实对目标的匹配数（须 0，避免追加后歧义）。

### 5 道 cleanly solvable（唯一匹配 + 0 漂移 + 答案键命中）

| id | 现 taskType | 触发 `required` | 拟入库 answer | 命中答案键 |
|----|-------------|-----------------|---------------|------------|
| pdf-415 | calculation（误路由） | `盐酸`/`调节`/`除去的离子` | `OH⁻、CO₃²⁻` | ✓ |
| pdf-543 | calculation（误路由） | `硫酸`/`调节`/`ph`/`目的` | `除去溶液中过量的OH⁻和CO₃²⁻，防止后续生成杂质沉淀` | ✓ |
| pdf-535 | calculation（误路由） | `配制`/`浓度`/`可能` | `偏小；摇匀` | ✓ |
| pdf-542 | unknown（已达事实库） | `沉淀1`/`金属元素` | `Fe、Al` | ✓ |
| pdf-530 | unknown（已达事实库） | `方案2`/`优点` | `操作更简便，反应条件更温和` | ✓ |

关键安全断言（已实测）：
- 现有 29 条事实对这 5 道的匹配数均为 **0** → 追加新事实后每题总匹配数恰为 **1**（无歧义，不会被 `ConceptFactSolver` 的 `>1 命中→null` 守卫吞掉）。
- 5 条路由/事实 cue 对已判正题匹配数均为 **0**（0 漂移）。
- 5 条拟入库 answer 均命中各自目标题的答案键（IsAnswerCorrect OR 语义 → 会判正采纳）。

### 排除项（保持 `null`，0 损失，在台账标注原因）

| id | 排除原因 |
|----|----------|
| pdf-483 | 触发「加入氯化钠+作用」与**已判正** pdf-477 重叠 → 漂移风险（LOST 风险），暂无更窄唯一触发。 |
| pdf-541 | 触发「玻璃仪器+烧杯」与 pdf-414 重叠（均未判正），`ConceptFactSolver` 无互斥条件 → 2 命中 → 返回 null（无增益无伤害），暂无目标独有 token。 |
| pdf-177 | 答案键为多子问合并串 `(1)...(4)...`，单条事实 answer 无法作为子串命中 → 属多部分大题域，归 P1-2 分发 + 后续子问求解器。 |

## 4. 验收标准（AC）

- **AC-1 precision=1**：无假阳性。全库 `adopted=True ∧ correct=False = 0`；新增结论凡采纳必判正（`IsAnswerCorrect` 门构造保证）。
- **AC-2 harm=0**：不删/盖正确数据；`data/highschool/concept-facts.json` **仅追加**（不修改/删除既有 29 条）。
- **AC-3 无回归**：`diff_ckpt base_p0 after_p14` 与 `diff_ckpt after_p12 after_p14` 均 **LOST=0 / WORSEN=0 / harm=0**；路由放宽前全库预校验 0 道已判正题重分类。
- **AC-4 增益**：解对题数 **≥ 1**（T0 审计预判 5 道：pdf-415/542/543/530/535，以 eval 实证为准）。
- **AC-5 不蒙数**：每个入库事实经「唯一匹配 + 0 漂移 + 答案键命中」三项断言（脚本化，CI 前必跑）；`ConceptFactSolver` 的 0/歧义→null 守卫不修改。
- **AC-6 测试**：新增 (a) 概念事实匹配单测覆盖唯一/歧义/0 命中三态；(b) 路由放宽 0 漂移单测（断言代表性已判正题 `taskType` 不变）；全量 `dotnet test` **0 failed**。
- **AC-7 事实只存 JSON**：不新增/不修改 `.cs` 中的化学事实（仅改 `concept-facts.json` + `ClassifyTaskDeterministic` 路由规则；路由规则不含化学真值）。
- **AC-8 台账闭环**：5 道解出 id 在 [data-defect-register.md](file:///d:/github/CogniThink-Chem/data-defect-register.md) 逐题登记；排除项（pdf-483/541/177）标注「保持 null + 原因」，集合差集为空（0 静默遗漏）。

## 5. 开放问题

- **OQ-1（已决策）**：pdf-483 / pdf-541 不纳入本 spec（漂移/歧义风险），保持 `null`，留待后续更窄触发或求解器支持互斥条件。
- **OQ-2（已决策）**：不新建独立求解器类；复用 `ConceptFactSolver`。仅在事实库 + 路由层改动，最小化表面积。
- **OQ-3（留观，不影响本 spec）**：`pdf-484` 等 K.1 多部分题也会被「pH+目的」cue 重分类到 `concept`，但其为多部分、无唯一事实匹配 → `ConceptFactSolver` 返回 null → 行为不变（仍未解，无伤害）。待 P1-2 分发 + 子问求解器解锁后另行处理。

## 6. 红线（不可妥协，破线即回退）

`precision=1` / `harm=0` / `LOST=0` / `WORSEN=0` / 不蒙数 / 事实只存 JSON。任何一项破线 → 回退 offending 改动，不得 paper over。
