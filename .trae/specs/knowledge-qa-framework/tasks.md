# 任务分解：化学知识问答与统一解题框架体系

## Task 1: 后端知识检索服务 QAService

**依赖：** 无
**优先级：** high
**状态：** pending

### 工作内容
- 新建 `src/CogniThink.Chem.Core/Knowledge/KnowledgeQueryService.cs`
- 方法 `SearchKnowledge(string query, KnowledgeBase kb, RuleSet rules)` 返回：
  - 匹配的知识点列表（按相关度排序：name 命中 > summary 命中 > pitfall 命中）
  - 匹配的规则列表（name 命中 > conclusion.display 命中）
- 相关度计算：简单关键词匹配（query 按 2-gram 切分，统计命中数）
- 无匹配返回空列表（不抛异常）

### 测试要求（TR）
- TR-1.1（rule）：query="勒夏特列" 返回 KH-RP-03
- TR-1.2（rule）：query="电子守恒" 返回 CONS-ELEC-001 规则
- TR-1.3（rule）：query="苯 溴水" 返回 KH-OR-01
- TR-1.4（rule）：query="不存在的关键词xyz" 返回空列表

### 验收标准
- AC-4 部分覆盖

---

## Task 2: 6 步框架引导生成器 FrameworkGuide

**依赖：** Task 1
**优先级：** high
**状态：** pending

### 工作内容
- 改造 `GuideGenerator.cs`，将 4 级引导重构为 6 级，每级对应框架一步：
  1. 审题分类：这是什么题？已知什么？求什么？
  2. 建立模型：选范式（转化链/三段式/n 中心/半反应/约束表）
  3. 知识检索：本题涉及哪些知识点（从 solution.Steps 的 KnowledgeId 提取）
  4. 规则应用：按什么顺序推理（指向具体规则编号）
  5. 精确求解：设未知数列方程（计算题指向 n 换算公式）
  6. 守恒校验：检查原子/电荷/质量守恒
- 每级 Title 包含框架步骤名
- 伴思铁律不变：不含答案

### 测试要求（TR）
- TR-2.1（rule）：任取一道平衡题，Guide.Levels 有 6 级，Title 含"守恒校验"
- TR-2.2（rule）：引导文本中不含化学方程式（用正则检测 `→|⇌|=`）
- TR-2.3（rule）：第 3 级引导引用了至少一个 KH-* 知识点编号

### 验收标准
- AC-1

---

## Task 3: 导出知识库+规则库到前端

**依赖：** 无
**优先级：** high
**状态：** pending

### 工作内容
- 修改 `ExportWebCommand.cs`，导出时额外生成 `web/data/knowledge.json`
- 导出内容：
  - 全部知识点（含 id/name/module/tier/textbookRef/prerequisites/summary/pitfall）
  - 每个知识点的 `rules` 字段填充关联规则的详情（id/name/layer/conclusion.display）
  - 全部规则（含 id/name/knowledgeId/layer/conclusion.display）
- 不修改 problems.json 结构

### 测试要求（TR）
- TR-3.1（rule）：导出后 `web/data/knowledge.json` 存在且 items 数 >= 50
- TR-3.2（rule）：KH-RP-03 的 rules 数组包含 EQ-001

### 验收标准
- AC-2、AC-5

---

## Task 4: 后端单元测试

**依赖：** Task 1, Task 2
**优先级：** high
**状态：** pending

### 工作内容
- 新建 `tests/CogniThink.Chem.Tests/KnowledgeQueryServiceTests.cs`
- 新建 `tests/CogniThink.Chem.Tests/FrameworkGuideTests.cs`
- 覆盖 Task 1 和 Task 2 的所有 TR

### 验收标准
- AC-6

---

## Task 5: 前端知识浏览页

**依赖：** Task 3
**优先级：** high
**状态：** pending

### 工作内容
- `web/app.js` 新增 `renderKnowledgePage()` 函数
- 页面结构：搜索框 + 模块筛选 tabs + 知识点卡片网格
- 知识点卡片：name、id、module、tier、textbookRef、summary、pitfall
- 点击卡片展开：prerequisites 链 + 关联规则列表
- 加载 `web/data/knowledge.json`

### 验收标准
- AC-3、AC-5

---

## Task 6: 前端知识问答面板

**依赖：** Task 1, Task 3
**优先级：** high
**状态：** pending

### 工作内容
- `web/app.js` 新增 `renderQAPanel()` 函数
- 输入框 + 搜索按钮
- 结果区：匹配的知识点卡片（要点+易错点）+ 匹配的规则（display 文本）
- 无结果时显示提示文案
- 知识点可点击跳转到知识浏览页

### 验收标准
- AC-4

---

## Task 7: 前端 6 步框架引导面板

**依赖：** Task 2
**优先级：** medium
**状态：** pending

### 工作内容
- 修改 `guideHtml(g)` 函数，按 6 步框架渲染
- 每步标注步骤编号和名称
- 当前步骤高亮（第一步默认展开）
- 保留"全部展开/收起"功能

### 验收标准
- AC-1

---

## Task 8: 集成验证

**依赖：** Task 1-7
**优先级：** high
**状态：** pending

### 工作内容
- `dotnet build` 0 错误
- `dotnet test` 全绿
- `dotnet run -- --export-web` 导出成功
- 浏览器抽查：知识浏览页搜索、问答面板输入、6 步引导展示
- 更新 demo.html 或主站入口接入新页面

### 验收标准
- AC-6（含浏览器验证）
