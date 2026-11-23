# 题型差异化解题模型与讲解 UI - Independent Review

> 评审方式：实现者在最终还原临时代码后，基于①浏览器运行时真实截图（由 browser_use 子代理只读采集 + 主代理直连源站补采）、②独立源码复核、③工具输出（GetDiagnostics / pai.log / grep 计数）进行。截图与工具输出为客观证据，不作为实现者自验的替代品。

## Checkpoints

- [x] CP-R1: 题型模型注册表完整且构建/类型质量达标
  - **Type**: `rule`
  - **Covers**: AC-1, AC-9, NFR-1, NFR-2, NFR-3, NFR-4
  - **Evidence**: 源码 [solveModels.ts](file:///d:/github/CogniThink-Chem/miniapp/src/engine/solveModels.ts) 导出 `SOLVE_MODELS`（6 类全覆盖：氧化还原 `redox-electron` / 离子 `ionic-conservation` / 平衡 `equilibrium-shift` 为专属 panelKind，电化学/无机/有机为 `generic` 但各有专属 steps）与 `getSolveModel`/`getPanelKind`/`getModelChecks`；[types/chem.ts](file:///d:/github/CogniThink-Chem/miniapp/src/types/chem.ts) 新增 `SolveData` 判别联合与 `Challenge.solveData?` 可选字段。`GetDiagnostics` 返回 `[]`；`pai.log` 末尾「✅ 编译成功 12:03:49」无 `compile_error`。6 个新建 `.module.scss` 顶部均含 `@use '@/styles/variables.scss' as *;`，6 个 `.tsx` 均仅从 `@tarojs/components` 导入；未新增依赖、未改 `app.config.ts`。

- [x] CP-R2: 题目页讲解区双位置渲染且 preview 不泄答案
  - **Type**: `rule`
  - **Covers**: AC-2, FR-4, FR-5, TR-4.1, TR-4.2
  - **Evidence**: [challenge/index.tsx L461-474](file:///d:/github/CogniThink-Chem/miniapp/src/pages/challenge/index.tsx#L461-L474) 题干下渲染可折叠「🧩 这类题怎么想」(`mode="preview"`)；[L517-523](file:///d:/github/CogniThink-Chem/miniapp/src/pages/challenge/index.tsx#L517-L523) `result!=='pending'` 时渲染「📐 完整解题模型」(`mode="full"`)。调度器 [SolvePanels/index.tsx L27-42](file:///d:/github/CogniThink-Chem/miniapp/src/components/SolvePanels/index.tsx#L27-L42) preview 分支仅渲染 `modelHead + StepsView`，**不读取 `solveData`**。运行时截图 [AC2_R2_preview_不泄答案.jpg](file:///d:/github/CogniThink-Chem/.trae/specs/differentiated-solve-models/evidence/AC2_R2_preview_不泄答案.jpg)：R2 页题干下「🧩 这类题怎么想」默认展开，仅显示「⚡ 电子守恒配平模型 · 思考骨架 · ①标价态 ②算电子 ③配守恒 ④观察配平」，无半反应表、无电子数、无正确答案。

- [x] CP-R3: 氧化还原专属面板可视化完整且数值与解析一致
  - **Type**: `rule`
  - **Covers**: AC-3, FR-7, TR-2.1, TR-3.2
  - **Evidence**: 运行时截图 [AC3_R2_氧化还原_半反应表与电子守恒.jpg](file:///d:/github/CogniThink-Chem/.trae/specs/differentiated-solve-models/evidence/AC3_R2_氧化还原_半反应表与电子守恒.jpg)：电子转移分析表 2 行（Mn 7→+2 还原 5e⁻ / Fe 2→+3 氧化 5e⁻，得失着色），电子守恒汇总「失电子总数 5 = 得电子总数 5」「最小公倍数 5」「系数比 1:5」四字段全非空。数值与 R2 explanation「Mn:+7→+2 得 5e⁻；Fe:+2→+3 失 1e⁻；最小公倍数 5；系数比 1:5」一致。源码 [RedoxElectronPanel](file:///d:/github/CogniThink-Chem/miniapp/src/components/SolvePanels/RedoxElectronPanel/index.tsx) 失/得电子总数由 `halfReactions` 按 role 求和，`balance` 缺失时优雅降级不渲染 lcm/ratio。

- [x] CP-R4: 离子平衡专属面板可视化完整
  - **Type**: `rule`
  - **Covers**: AC-4, FR-8, TR-2.2
  - **Evidence**: 运行时截图 [AC4_I3_离子平衡_粒子清单与三大守恒.jpg](file:///d:/github/CogniThink-Chem/.trae/specs/differentiated-solve-models/evidence/AC4_I3_离子平衡_粒子清单与三大守恒.jpg)：I3「单一溶液离子浓度比较」渲染「溶液粒子清单」6 种粒子（Na⁺/S²⁻/HS⁻/H₂S/OH⁻/H⁺，各带来源）+「三大守恒关系」卡片：物料守恒（蓝）`c(Na⁺)=2[c(S²⁻)+c(HS⁻)+c(H₂S)]`、电荷守恒（红）`c(Na⁺)+c(H⁺)=2c(S²⁻)+c(HS⁻)+c(OH⁻)`、质子守恒（绿）`c(OH⁻)=c(H⁺)+c(HS⁻)+2c(H₂S)`，表达式均非空且按类型着色。

- [x] CP-R5: 化学平衡专属面板可视化完整
  - **Type**: `rule`
  - **Covers**: AC-5, FR-9, TR-2.3
  - **Evidence**: 运行时截图 [AC5_E1_化学平衡_平衡移动推演表.jpg](file:///d:/github/CogniThink-Chem/.trae/specs/differentiated-solve-models/evidence/AC5_E1_化学平衡_平衡移动推演表.jpg)：E1「勒夏特列原理」渲染「平衡移动推演」表 4 行（温度/压强/催化剂/浓度），列含「影响因素/改变方式/移动方向/K 变化/转化率」；移动方向着色（逆向红 / 正向绿 / 不移动黑），K 变化（减小/不变）与转化率字段均非空。

- [x] CP-R6: 代表题结构化数据覆盖达标
  - **Type**: `rule`
  - **Covers**: AC-6, FR-6, TR-3.1
  - **Evidence**: grep `solveData` 计数：`redox.ts` 6 题（R1–R6）、`ionic.ts` 4 题（I1/I3/I4/I5）、`equilibrium.ts` 4 题（E1/E3/E6/E7），合计 14 题，每类 ≥4、合计 ≥12。`GetDiagnostics` 为空（TR-3.3 通过）。

- [x] CP-R7: 无 solveData 题目降级通用且无回归
  - **Type**: `rule`
  - **Covers**: AC-7, FR-10, TR-2.4, TR-4.3
  - **Evidence**: 运行时截图 [AC7_R7_无solveData_通用降级.jpg](file:///d:/github/CogniThink-Chem/.trae/specs/differentiated-solve-models/evidence/AC7_R7_无solveData_通用降级.jpg)：R7「多元素变价配平」（氧化还原类但无 solveData）答题后，面板降级为通用视图——显示「⚡ 电子守恒配平模型」标准步骤（带完整描述）+「校验要点」4 条（化合价升降总数相等/失电子=得电子/最小公倍数与系数比/配平后原子电荷守恒），**无半反应表、无电子汇总数值**，页面无报错。调度器 [L45-72](file:///d:/github/CogniThink-Chem/miniapp/src/components/SolvePanels/index.tsx#L45-L72) 因 `panelKind==='redox-electron'` 但 `sd` 为 undefined 走 `GenericSolvePanel` 分支。原有 6 步引导/诊断/守恒校验代码未改动（仅新增展示区块），`getModelChecks` 已并入守恒校验区（[challenge L595](file:///d:/github/CogniThink-Chem/miniapp/src/pages/challenge/index.tsx#L595)）。

- [x] CP-U1: 题型差异化讲解的清晰度与思维贴合度
  - **Type**: `rubric`
  - **Covers**: AC-8, NFR-5, TR-2.5, TR-5.3
  - **Scale**: 1-5
  - **Anchors**: 1 = 仍是纯文本无差异化；3 = 有结构化展示但贴合度一般；5 = 可视化精准映射题型思维骨架，清晰易用
  - **Pass Threshold**: >= 4
  - **Score**: 5
  - **Rationale**: 三类专属面板均精准映射题型思维本质——氧化还原的「半反应电子转移表 + 电子守恒汇总」正是电子守恒配平法的可视骨架；离子平衡的「粒子来源清单 + 三大守恒式卡片」直接对应粒子浓度比较题的思考路径（找粒子→写守恒→比浓度），且守恒式按电荷/物料/质子着色降低认知负担；化学平衡的「条件-移动-K-转化率推演表」把勒夏特列判断结构化。preview 模式的「思考骨架」标签 + 编号步骤让学生答题前即可建立框架，full 模式再填入该题结构化数据，分层清晰。在本轮落地的 3 个代表题型范围内达到 anchor 5。
  - **Evidence**: 上述 AC2/AC3/AC4/AC5/AC7 五张运行时截图。

## Review History

### Review R1
- **Result**: `pass`
- **Evidence**: 7 个 rule 检查点（CP-R1~CP-R7）全部 `pass`，1 个 rubric 检查点（CP-U1）得分 5（≥4 阈值）；每个 AC（AC-1~AC-9）均有独立客观证据（运行时截图 / 源码 / 工具输出）；无 `actionable` 或 `blocked` 项。临时调试代码（`debugAnswered` state + 置顶面板块）已在截图后全部移除，grep 确认零残留，还原后 `GetDiagnostics` 为空、编译成功。
- **Blocked By**: 无
- **Resume When**: 不适用

## Independent Review Report

# Review R1
- **Result**: `pass`
- **Checks Performed**:
  - 读 [solveModels.ts](file:///d:/github/CogniThink-Chem/miniapp/src/engine/solveModels.ts) 与 [types/chem.ts](file:///d:/github/CogniThink-Chem/miniapp/src/types/chem.ts) 确认注册表 6 类覆盖与判别联合类型。
  - 读 [SolvePanels/index.tsx](file:///d:/github/CogniThink-Chem/miniapp/src/components/SolvePanels/index.tsx) 与 [RedoxElectronPanel](file:///d:/github/CogniThink-Chem/miniapp/src/components/SolvePanels/RedoxElectronPanel/index.tsx) 复核调度逻辑、降级健壮性与数据计算。
  - grep 校验 NFR-3（scss `@use` 前缀、`@tarojs/components` 导入）、`solveData` 题数计数、临时代码零残留。
  - 直连预览源站（apigateway，hash 路由深链）采集 R2/I3/E1/R7 运行时截图；browser_use 子代理独立只读核验 R2。
  - 运行 `GetDiagnostics`（空）、tail `pai.log`（编译成功）。
- **Evidence**:
  - 5 张固化截图见 `.trae/specs/differentiated-solve-models/evidence/`。
  - 工具输出：`GetDiagnostics = []`；`pai.log` 末尾 `✅ 编译成功`；grep 计数 redox=6/ionic=4/equilibrium=4。
- **Checkpoint Results**:
  - CP-R1 (`rule`): `pass`
  - CP-R2 (`rule`): `pass`
  - CP-R3 (`rule`): `pass`
  - CP-R4 (`rule`): `pass`
  - CP-R5 (`rule`): `pass`
  - CP-R6 (`rule`): `pass`
  - CP-R7 (`rule`): `pass`
  - CP-U1 (`rubric`): `pass`; score 5; 三类专属面板精准映射题型思维骨架（见 rationale）
- **Findings**:
  - F-1: `advisory`; severity low; 电化学/无机推断/有机 3 类目前走 `generic` 面板——此为 spec 明确的非目标（Non-Goals），留作后续迭代，非本轮缺陷。建议下一轮优先推广电化学（原电池/电解池半反应可视化与氧化还原面板可复用电子转移表思路）。
- **Recommended Issues**:
  - 无 `actionable` finding，无需生成修复 issue。F-1 为后续迭代方向，不阻塞本轮通过。
