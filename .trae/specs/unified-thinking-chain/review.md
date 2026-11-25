# 全题型思维链 DAG 统一规范 - Review

> **阶段**：Review（实现完成后独立评审）。本文件仅在 Review 阶段可写，记录独立评审结论、评分、发现清单与证据。
> **红线复核**：precision=1 / harmCount=0 / 不编造化学事实 / 不主动 commit。

## Review 元信息

- **评审对象**：R4「全题型思维链 DAG 统一规范」实现产物（6 张题型 DAG + 19 张反应 DAG + 映射表 + 门控引擎 + 实例化绑定 + 3 个脚手架模块补齐 + 验证脚本）。
- **评审方式**：委托**只读独立子代理**（fresh context，非实现者本人）按 spec 验收点逐项核对；子代理未修改任何文件。
- **评审范围**：AC-2/3/4（结构合法性）、AC-8（向后兼容零回归）、AC-9（结构清晰度 rubric）、红线（化学事实零编造）。

## Checkpoint 1 — AC-9：6 张推导 DAG 结构清晰度与教学贴合度（rubric）

按 spec AC-9 的 1–5 分锚点（1=线性无依赖分支 / 3=有依赖但表达不清 / 5=精准映射推导逻辑），独立评审对 6 张 DAG 评分：

| DAG | 题型 | 评分 | 评语（独立评审） |
|-----|------|------|------------------|
| DAG-1 | 氧化还原（RedoxReaction） | **5** | 两阶段 stage1/stage2 分组清晰，`redox.judge-order` 的 `skipWhen=single-pair` 条件边与 `infer-products` 依赖退化准确映射氧还推导本质，medium 分支贯穿判介质/推产物/补氢氧。 |
| DAG-2 | 离子平衡（IonicEquilibrium） | **4** | 新增 `ionic.verify` 守恒校验闭合了「列粒子→写守恒→校验」自洽环；`solute-type` 分支定位准确。 |
| DAG-3 | 化学平衡（ChemicalEquilibrium） | **4** | 写平衡→判条件→推移动→看 K 与转化率的依赖链清晰；`condition-type` 与 `temperature-changed` 两个分叉点定位了平衡题的核心条件分支。 |
| DAG-4 | 电化学（Electrochemistry） | **4** | 判装置→写半反应→析现象/计算的依赖清晰，`device-type`+`medium` 双分支与半反应配平写法一致；`no-calc` 跳过计算节点合理。 |
| DAG-5 | 无机推断（InorganicInference） | **4** | 从 generic 占位提升为完整 DAG，找突破口→顺逆推→代回验证的推断骨架准确，`subtype` 分支对齐 7 种子类型。 |
| DAG-6 | 有机化学（OrganicChemistry） | **5** | 认官能团→定反应→写产物→价键/不饱和度校验闭环；`organic-subtype` 分支与反应侧 9 子类型对齐，新增校验节点补齐有机题易错点。 |

**结论**：6/6 评分均 ≥ 4（阈值 4），**AC-9 通过**。

## Checkpoint 2 — AC-8：向后兼容零回归

独立评审核对：
- 新增 6 个 `thoughtChain*.ts` 文件**未被任何现有组件导入**，现有运行时按构造零触碰（`StepsView` / `SolvePanels/index.tsx` / 各专属面板 / `pages/challenge` 消费方形状不变）。
- `types/scaffold.ts` 仅**加性**新增 3 个 `ScaffoldModuleKind` 联合成员；`scaffoldChains.ts` `MODULE_CHAINS` 补齐 3 条新链保持 `Record` exhaustive，未改旧 5 模块。
- `GetDiagnostics` 全工程 → `[]`（零 TS 诊断）。
- `npm run build:h5` → exit 0，仅预存 asset-size 警告（与本次无关）。

**结论**：R1–R3 已验证行为零回归，**AC-8 通过**。

## Checkpoint 3 — 红线：化学事实零编造

独立评审核对所有新增文案的化学正确性：
- 3 个新增 DAG 节点的 `analyze` 文案（`ionic.verify` 守恒校验、`ec.compute` 计算、`org.verify` 价键/不饱和度校验）——确认与高中化学定论一致（三式自洽、Q=It=nF、价键规则与 Ω 公式）。
- 3 条新增脚手架模块链（chemical-equilibrium / inorganic-inference / organic-reaction）的 `checks`——确认化学正确。
- 其余节点 `title/goal/analyze` 全部**复用**现有 `solveModels.ts` 真值源（单一真值源，不重写、不编造）。

**结论**：未发现化学事实编造，**红线通过**。

## 发现清单与处置

| # | 严重度 | 发现 | 处置 | 状态 |
|---|--------|------|------|------|
| 1 | 中 | spec 设计 B 要求 `ec.write-half-reactions` 的 `branchKey` 为 `device-type, medium`，但实现只赋了 `'device-type'`，且 `ThoughtChainNode.branchKey` 类型为单值 `string` 无法表达多键。 | 将 `branchKey` 类型放宽为 `string \| string[]`（[thoughtChain.ts](../miniapp/src/types/thoughtChain.ts) + `NodeOverlay` in [thoughtChainDAGs.ts](../miniapp/src/engine/thoughtChainDAGs.ts)），并把该节点改为 `['device-type', 'medium']`；同步 spec 设计 A 代码块。 | **已修复** |
| 2 | 低 | `eq.analyze-k` 的 `branchKey='only-temperature-changes-k'` 语义像原理陈述不像分支键。 | 改名为语义清晰的条件键 `temperature-changed`（impl + spec 表与注同步）。 | **已修复** |
| 3 | 低 | spec 表格标题与 solveModels 真值源微差（DAG-1「推产物+共存」、DAG-3「看K与转化率」）。 | 对齐 solveModels 真值源：`推产物`、`看 K 与转化率`（spec 表格）。 | **已修复** |

> **说明（非缺陷）**：`branchKey → resolveBranch` 的运行时接线尚未实现——符合 OQ-4 裁决（本轮只做数据层 + 纯函数引擎，DAG 图形化 UI 与分支接线留后继迭代）；当前安全降级不崩。

## 修复后复核证据

修复 #1/#2/#3 后复跑：
- `miniapp/scripts/verify-thoughtchain.ts`（ts-node，--transpile-only）→ **exit 0，「全部通过 PASS」**：6 题型 DAG 全合法、19 反应 DAG 全合法、8 模块链非空、映射表覆盖、门控解锁/skip/failed/reset 正确、实例化降级安全、previewSafe 不泄答案、8 类校验负样本全检出。
- 真实题库绑定通路：84 题全部有 solveData，共命中 **294 个节点结论**（Redox 94 / 离子 33 / 平衡 36 / 电化学 47 / 无机 42 / 有机 42）。
- 修复点可见：`ec.write-half-reactions` 输出 `branch=device-type,medium`；`eq.analyze-k` 输出 `branch=temperature-changed`。
- `GetDiagnostics` 全工程 → **[]**（零 TS 诊断）。

## 结论

- AC-1 ~ AC-8（rule）：全部通过（证据见 tasks.md 完成证据汇总 + verify 脚本）。
- AC-9（rubric）：6/6 ≥ 4，通过。
- 红线：化学事实零编造，通过。
- 3 项评审发现（1 中 + 2 低）均已修复并复核通过。

**本规格 R4 验收通过，予以关闭。**
