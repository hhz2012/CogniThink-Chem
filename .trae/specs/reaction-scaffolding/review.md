# 反应脚手架式拆解（reaction-scaffolding）独立评审

- **评审日期**：2026-11-23
- **评审范围**：轴 1（反应讲解侧）—— Task 0–5 / 批次 1–4，对应验收标准 **AC-1 ~ AC-5**。
- **不在本次评审范围**：轴 2（解题侧 A–F 交互升级）—— Task 6–9 / 批次 5–6，对应 **AC-6 ~ AC-9**，尚未开始，留待后续独立评审。本结论不代表 AC-6~AC-9 通过。
- **评审性质**：只读独立评审，不改代码；结论基于源码检查、运行时断言、预览截图与工具输出。

## 证据清单

| 证据 | 位置 / 说明 |
|------|-------------|
| 模型注册表 | [miniapp/src/engine/reactionSolveModels.ts](../../../miniapp/src/engine/reactionSolveModels.ts) |
| 实例化引擎 | [miniapp/src/engine/reactionNarration.ts](../../../miniapp/src/engine/reactionNarration.ts) |
| 反应数据子集 | [miniapp/src/data/reactions/](../../../miniapp/src/data/reactions/)（inorganic / redox / electrochem / equilibrium / organic） |
| 数据出口与分组 | [miniapp/src/data/reactions/index.ts](../../../miniapp/src/data/reactions/index.ts) |
| 类型定义 | [miniapp/src/types/reaction.ts](../../../miniapp/src/types/reaction.ts) |
| 反应卡页 UI | [miniapp/src/pages/reaction/index.tsx](../../../miniapp/src/pages/reaction/index.tsx) + [index.module.scss](../../../miniapp/src/pages/reaction/index.module.scss) |
| 首页入口 | [miniapp/src/pages/home/index.tsx](../../../miniapp/src/pages/home/index.tsx) |
| 覆盖率与实例化计数 | 运行时脚本（esbuild 打包执行 `narrateReaction` 全量遍历）输出，见下 |
| 预览截图 | [evidence/task4-home-entry.jpg](./evidence/task4-home-entry.jpg)、[evidence/task4-reaction-detail-RX-IN-ION-012.jpg](./evidence/task4-reaction-detail-RX-IN-ION-012.jpg) |
| 构建日志 | `miniapp/.pai/pai.log`（PAI 云预览） |
| 类型诊断 | `GetDiagnostics` 工具输出 |

### 运行时计数（TR-5.1 / AC-3 证据）

以 esbuild 打包、node 执行的只读脚本遍历 `REACTIONS` 全集并对每条每步调用 `narrateReaction(step, entry)`，结果：

```
total reactions: 39
with solveData:  39   → coverage 100.0%
missing solveData ids: (none)

total steps across all entries: 174
steps returning instantiated (non-null): 174  → 100.0%
entries with zero instantiated steps: (none)
```

各 kind 分布（kind | 条数 | 带 solveData | 实例化步数/总步数）：

```
reaction-combination                 化合反应      3  3  12/12
reaction-combustion                  燃烧反应      2  2   8/8
reaction-decomposition               分解反应      2  2   8/8
reaction-displacement                置换反应      3  3  12/12
reaction-double-displacement         复分解反应     2  2   8/8
reaction-electrode                   电极反应      2  2   8/8
reaction-ion-equilibrium             离子平衡      2  2   8/8
reaction-redox                       氧化还原反应    3  3  12/12
reaction-thermochemical              热化学反应    2  2   8/8
reaction-organic-substitution        有机 · 取代    2  2  10/10
reaction-organic-addition            有机 · 加成    2  2  10/10
reaction-organic-elimination         有机 · 消去    2  2  10/10
reaction-organic-oxidation           有机 · 氧化    2  2  10/10
reaction-organic-reduction           有机 · 还原    2  2  10/10
reaction-organic-esterification      有机 · 酯化    2  2  10/10
reaction-organic-hydrolysis          有机 · 水解    2  2  10/10
reaction-organic-polyaddition        有机 · 加聚    2  2  10/10
reaction-organic-polycondensation    有机 · 缩聚    2  2  10/10
```

> 说明：`reaction-generic`（降级兜底模型）在注册表中存在（3 步），但前端子集中无条目归入 generic——所有 39 条均命中具体 kind，符合「无真值/未知类型降级不崩」的安全设计。

## 逐项验收

### AC-1：反应模型注册表覆盖完整 —— **PASS**

- **要求**：10 个 `ReactionType` 主类型 + 9 个有机子类型均有条目，每条目步骤非空（≥3 步）。
- **证据**：`REACTION_SOLVE_MODELS` 注册表共 **19 个 kind**：化合 / 分解 / 置换 / 复分解 / 燃烧 / 氧化还原 / 电极 / 离子平衡 / 热化学（9 个主类，其中第 10 主类「有机」由其 9 个子类型条目代表）+ 9 个有机子类型（取代/加成/消去/氧化/还原/酯化/水解/加聚/缩聚）+ `reaction-generic` 兜底。每 kind 步数：9 个无机主类各 4 步，9 个有机子类型各 5 步（含共享前置 0a/0b 认官能团/定反应类型），generic 3 步——**全部 ≥3**。
- **结论**：PASS。10 主类型（有机以 9 子类型覆盖）+ 9 有机子类型全覆盖，无空步骤条目。

### AC-2：步骤框架与反应思维本质贴合 —— **PASS（rubric 5/5，阈值 ≥4）**

- **要求（rubric）**：每类反应的步骤精准映射该类反应的思考骨架；scale 1–5，pass ≥4。
- **证据**：
  - 以氧化还原为例：`标价态 → 算电子 → 配守恒 → 验方向` 严格对应学生分析氧还反应的标准思维链（标变价 → 算电子守恒 → 配平 → 用强弱律验方向），层级 Knowledge/L2→Application/L2→Ability/L2→Ability/L3 与认知负荷递进对齐。
  - 置换的「判活动性」、复分解的「判发生条件（沉淀/气体/水）」、燃烧的「判完全/不完全」、电极的「定电极与放电顺序」、离子平衡的「写守恒式」均精准命中该类反应的思维关节点，非通用套话。
  - 有机先经共享前置「认官能团 → 定反应类型」再进入子框架，符合有机反应「先判类型再分析」的真实思考顺序。
- **结论**：PASS，评分 **5/5**。步骤框架与反应思维本质高度贴合，无错配或泛化失真。

### AC-3：真值数据与实例化引擎 —— **PASS**

- **要求**：至少每类反应选 ≥2 条代表反应填入 `ReactionSolveData`；调用 `narrateReaction(step, entry)` 返回结合该反应真值的实例化文本（含具体物种/价态/产物等），非通用占位；无真值时回退 `step.analyze` 不崩。
- **证据**：
  - 覆盖：18 个非 generic kind 全部 ≥2 条（化合/置换/氧还各 3 条，其余各 2 条），共 39 条，**全部带 `solveData`**（TR-5.1 覆盖率 100%）。
  - 实例化：全量 174 步调用 `narrateReaction` **100% 返回非 null**，即全部命中实例化分支、无整条降级。抽验 `RX-IN-ION-012`（`5Fe²⁺ + MnO₄⁻ + 8H⁺ = 5Fe³⁺ + Mn²⁺ + 4H₂O`）运行时输出：
    - 标价态：「Fe 在 Fe²⁺ 中为 +2，反应后在 Fe³⁺ 中变为 +3；Mn 在 MnO₄⁻ 中为 +7，反应后在 Mn²⁺ 中变为 +2。化合价发生变化的元素是 Fe、Mn」——含具体物种与价态。
    - 算电子：「每个 Fe 失 1e⁻；每个 Mn 得 5e⁻。氧化剂：MnO₄⁻；还原剂：Fe²⁺」——含电子数与角色。
    - 配守恒：「失电子总数 5 = 得电子总数 5，最小公倍数 5，系数比 5:1」——含守恒真值。
  - 降级安全：`reaction-generic` 兜底模型存在（3 步），未知 kind 走 generic 不崩（Task 1 已验证）。
- **结论**：PASS。真值覆盖达标，实例化文本含本条反应具体物种/价态/电子数/系数，非占位；降级路径安全。

### AC-4：与题型侧模型对齐复用 —— **PASS**

- **要求**：氧化还原 / 离子平衡 / 电化学三类，对比题型侧与反应侧步骤标题，标题与思维顺序一致、叙述逻辑可复用。
- **证据**（源码对比 [solveModels.ts](../../../miniapp/src/engine/solveModels.ts) vs [reactionSolveModels.ts](../../../miniapp/src/engine/reactionSolveModels.ts)）：
  - **氧化还原**：题型侧 `redox-electron` = 标价态 / 算电子 / 配守恒 / 观察配平；反应侧 `reaction-redox` = 标价态 / 算电子 / 配守恒 / 验方向。前 3 步标题与顺序**完全一致**；第 4 步同为 L3 校验（观察配平 ↔ 验方向），思维顺序一致。
  - **电化学**：题型侧 `electrochem-cell` = 判装置 / 写半反应 / 析现象；反应侧 `reaction-electrode` = 判装置 / 定电极与放电顺序 / 写半反应 / 合并验守恒。「判装置」「写半反应」标题与顺序一致；反应侧在判装置后补「定电极与放电顺序」(L2)、写半反应后补「合并验守恒」(L3)，是题型侧的细化展开，思维顺序一致。
  - **离子平衡**：题型侧 `equilibrium-shift` = 写平衡 / 判条件 / 推移动 / 看 K 与转化率，`ionic-conservation` = 找粒子 / 写守恒 / 比浓度；反应侧 `reaction-ion-equilibrium` = 认平衡类型 / 写平衡式 / 判移动方向 / 写守恒式。「写平衡式」对齐「写平衡」、「判移动方向」对齐「推移动」、「写守恒式」对齐「写守恒」——反应侧以单模型整合了题型侧两个模型的核心思维，顺序一致。
- **结论**：PASS。三类重叠模型的步骤标题与思维顺序对齐，`narrateReaction` 复用题型侧叙述逻辑（裁决：共用），无双份维护漂移。

### AC-5：构建与类型质量 —— **PASS**

- **要求**：改动完成后 `GetDiagnostics` 为空、`pai.log` 编译成功。
- **证据**：
  - `GetDiagnostics` 全量返回 `[]`（2026-11-23 重新执行确认，无类型错 / 无未使用符号）。
  - PAI 云预览 `pai.log` 显示「✅ 编译成功」（19:37:35），本轮无 `compile_error`（计数 0）、无 error/fail 行。
  - Task 5 仅新增本 `review.md`（位于 `.trae/specs/`，不在 `miniapp/src/` 构建范围内），不触发重新编译，构建状态保持通过。
- **结论**：PASS。诊断为空，编译成功。

## 未闭合项

- **AC-6 ~ AC-9（轴 2）**：不在本次评审范围，对应 Task 6–9（A–F 骨架 / 条件数量分支 / 作答校验门控 / 阶梯释放）尚未开始。无未闭合 FAIL（本次评审范围内 AC-1~AC-5 全部 PASS）。

## 最终结论

**轴 1（Task 0–5 / AC-1~AC-5）全部通过，review pass。** 反应脚手架式拆解的类型注册表、步骤框架、真值数据、实例化引擎、反应卡页 UI 与构建质量均达到验收标准；39 条前端反应子集 `ReactionSolveData` 覆盖率 100%，174 步全部实例化。轴 2（Task 6–9 / AC-6~AC-9）留待后续批次独立评审。
