# 化学可视化实现计划

## 仓库调研结论

前端是纯静态站（`web/index.html` + `app.js` + `style.css`），数据来自 `--export-web` 生成的 `web/data/problems.json`。
当前可视化仅为文字+表格+卡片，缺乏图形化表达。

**数据可用性评估**（决定哪些可视化可直接做、哪些需补后端）：

| 可视化 | 数据来源 | 可行性 |
|--------|----------|--------|
| 电化学装置图 | `solution.electrodes` + `given.cell` | ✅ 纯前端 |
| 转化链流程图 | `solution.substanceMap` + `given.steps` | ✅ 纯前端 |
| 粒子浓度柱状图 | `solution.aqueous.order`（仅有顺序，无值） | ⚠️ 定性柱状图（按顺序赋相对高度） |
| 离子共存冲突图 | `solution.equations`（无结构化冲突对） | ⚠️ 需后端补 `conflicts` 字段 |
| 摩尔计算关系图 | `solution.numericValue/Unit/Species` | ✅ 纯前端 |
| 三段式表格增强 | `solution.equilibrium.table` | ✅ 纯前端（已有表格，增强） |
| 方程式配平原守恒 | `solution.equations` | ⚠️ 前端解析方程式统计原子 |
| 推理步骤时间线 | `solution.steps` | ✅ 纯前端 |
| 浓度-时间图 / 滴定曲线 | 无时序数据 | ❌ 需引擎输出 |
| 实验装置图 | 无装置结构化数据 | ❌ 需 SVG 装置库 |
| 分子3D / 晶体 / 杂化轨道 | 需 RDKit + 3D 库 | ❌ 暂不实现 |

## 实现范围

本次实现 **8 种可视化**（覆盖所有有数据支撑的），其余 4 种（浓度-时间、实验装置、3D结构、杂化轨道）因缺数据/依赖过重暂不实现，记录在「风险与后续」。

## 文件与模块

### 前端
- `web/app.js`：新增 8 个可视化渲染函数，接入对应类别的 `answerHtml`
- `web/style.css`：新增可视化样式（SVG 装置、流程图、柱状图、冲突图等）

### 后端（仅 1 处）
- `src/CogniThink.Chem.Core/Solving/SolutionModels.cs`：`Solution` 新增 `Conflicts` 字段
- `src/CogniThink.Chem.Core/Solving/ProblemSolver.cs`：`SolveCoexistence` 填充 `Conflicts`
- `src/CogniThink.Chem.Cli/Commands/ExportWebCommand.cs`：导出 `conflicts`

## 实现步骤

### 第 1 步：后端补离子共存冲突数据
1. `Solution` 新增 `List<CoexistenceConflict> Conflicts`
2. `SolveCoexistence` 赋值 `solution.Conflicts = MaximalConflicts(judge.Conflicts)`
3. `ExportWebCommand` 导出 `conflicts`（ions + reason + equation）
4. 重新 `--export-web`

### 第 2 步：电化学装置图（SVG）
- 函数 `electrochemDiagramHtml(cell, electrodes)`
- 原电池：左负极（氧化）/ 右正极（还原），电子 e⁻ 外电路箭头，阳离子→正极、阴离子→负极内电路箭头
- 电解池：左阳极（接电源+）/ 右阴极（接电源-），电子流向相反
- 电极旁贴推导出的电极反应式
- 介质/电解质标在中间

### 第 3 步：转化链流程图
- 函数 `processFlowHtml(steps, substanceMap, purposes)`
- 用 `given.steps` 的 from→to 构建有向链，节点显示字母 + 推断出的物质名
- 边上标注试剂/条件/现象
- 工业流程额外标注每步目的

### 第 4 步：粒子浓度定性柱状图
- 函数 `concentrationBarsHtml(order)`
- `aqueous.order` 给出浓度由大到小的顺序，按位置赋递减的相对高度
- 柱顶标粒子符号，柱底标"浓度递减"方向

### 第 5 步：离子共存冲突图
- 函数 `coexistenceGraphHtml(conflicts, species, medium)`
- 离子为节点，冲突离子对之间连边，按冲突类型着色（沉淀/气体/氧化还原/双水解/弱电解质）
- 介质离子 H⁺/OH⁻ 用不同样式区分
- 能共存时显示"无冲突"图

### 第 6 步：摩尔计算关系图
- 函数 `moleRelationHtml(numericValue, numericUnit, numericSpecies)`
- 以 n 为中心的辐射图，连接 m、N、V、c，标注换算公式
- 高亮本题实际用到的换算路径

### 第 7 步：三段式表格增强
- 转化行用 `ν·x` 着色，反应物转化为负（红）、生成物为正（绿）
- 若有扰动方向，在表下方加移动方向箭头

### 第 8 步：推理步骤时间线（通用层）
- 把 `solution.steps` 渲染为垂直时间线，每步用颜色标签区分 Evidence 类型（方程式/规则/检索）
- 增强现有 `stepHtml`，加左侧时间轴连接线

### 第 9 步：方程式配平原守恒（辅助）
- 解析 `solution.equations` 中各方程式，统计左右两侧元素原子数
- 显示"左：2H+O₂ → 右：2H₂O，H:2=2, O:2=2 ✓"的守恒校验块

### 第 10 步：重新导出 + 全量验证
- `dotnet run -- --export-web`
- `dotnet test`
- `dotnet run -- --verify-real`
- 浏览器抽查各类别可视化

## 依赖与注意事项

- 不引入第三方 JS 库（纯原生 SVG/CSS，保持零依赖静态站）
- 化学式排版复用现有 `chem()` 函数（上标/下标）
- 可视化只在对应类别且数据存在时渲染，缺数据时优雅降级
- 后端改动（conflicts 字段）需保证序列化兼容

## 验证

- 726 单元测试全绿
- 27 真题一致
- 浏览器抽查：电化学(ELEC-H01)、过程推断(PROC-001)、水溶液(AQ-H01)、离子共存(COEX-001)、定量计算(CALC-001)、化学平衡(EQ-H01) 各可视化正常渲染

## 风险与后续

- **浓度柱状图为定性**：`aqueous.order` 只有顺序无量值，柱状图高度是相对示意，不代表真实浓度比。后续可在引擎输出实际浓度值后改为定量图。
- **浓度-时间图 / 滴定曲线**：引擎当前不输出时序数据，需新增模拟计算模块。
- **实验装置图**：需构建 SVG 装置图库（蒸馏/萃取/过滤/气体制备等），工作量大，后续单独迭代。
- **3D 分子/晶体/轨道**：需引入 RDKit 的 3D 渲染或 3Dmol.js，依赖重，后续按需引入。
