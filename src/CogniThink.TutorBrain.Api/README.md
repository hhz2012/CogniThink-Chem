# 推理导学智能大脑（TutorBrain API）

步骤级自适应导学后端：面向高中化学推断，跟踪学生每一步思考，定位卡壳点，按知识点依赖图谱做启发式引导。前端独立开发，本服务只输出结构化 JSON。

## 红线约束

- **不直接给答案**：状态机按"反问 → 自查 → 子问题 → 线索 → 脚手架"逐级降级，最多到分步脚手架。
- **推理对错只由规则引擎判定**：化学推断走内置规则引擎；LLM 只生成话术，绝不参与判对错。
- **知识点有前置依赖**：前置未掌握时，错误诊断会返回薄弱前置，提示先补前置。
- **完整题目 DTO 不返回标准答案与判定表达式**（`AnswerKey`、`ExpectedExpression` 不出接口）。

## 目录结构

```
src/CogniThink.TutorBrain.Api/
├── Domain/                     实体：知识点图谱、题目/步骤、学生学情、状态机载体
├── Application/
│   ├── Reasoning/              领域推理引擎（化学规则）
│   ├── Agent/                  话术层（规则话术 / LLM 话术，可切换）
│   ├── KnowledgeGraphService.cs
│   ├── StudentModelService.cs   掌握度、卡壳轨迹、错题、FSRS 轻量复习
│   ├── VariantService.cs        同知识点可控变式
│   ├── TutorStateMachine.cs     核心状态机（流转写死在代码里）
│   └── TutorService.cs          编排：判定 → 学情 → 变式 → 结构化回复
├── Infrastructure/
│   ├── TutorDbContext.cs        EF Core + SQLite
│   ├── SeedData.cs              种子：6 个化学知识点 + 8 条依赖 + 3 道题
│   └── Migrations/              InitialCreate 迁移
├── Api/
│   ├── Contracts/               DTO 与映射
│   └── Controllers/             REST 接口
├── Program.cs
└── appsettings.json
```

## 快速启动

```bash
cd src/CogniThink.TutorBrain.Api
dotnet run
```

- Swagger：<http://localhost:5080/swagger>
- 首次启动自动执行迁移并写入种子数据，SQLite 文件为 `tutor-brain.db`
- 重置数据：停止服务后删除 `tutor-brain.db`，再次启动

迁移命令：

```bash
dotnet ef migrations add <Name> --output-dir Infrastructure/Migrations
dotnet ef database update
```

## 配置（appsettings.json）

```jsonc
{
  "Llm": {
    "Enabled": false,                                     // true 时用 LLM 生成话术
    "BaseUrl": "https://your-llm/v1",
    "ApiKey": "",
    "Model": "seed-turbo",                                // 可换 qwen-flash
    "TimeoutSeconds": 8
  }
}
```

LLM 不可用或超时会自动回退到规则话术，导学不中断。

## API 清单

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/student/{id}` | 创建 / 获取学情（掌握度、进度、错题数） |
| GET | `/api/student/{id}/weak-knowledge?threshold=0.5` | 薄弱知识点 |
| GET | `/api/student/{id}/review-suggest` | 复测建议（到期复习 + 薄弱补充） |
| GET | `/api/student/{id}/progress` | 闯关进度 |
| POST | `/api/student/{id}/progress` | 保存闯关进度 |
| GET | `/api/problem/{id}` | 加载题目（不含答案） |
| GET | `/api/problem?subject=Chemistry` | 题目列表 |
| POST | `/api/tutor/submit-step` | 提交一步思考 |
| GET | `/api/tutor/traces/{studentId}/{problemId}` | 卡壳轨迹回放 |
| GET | `/api/knowledge?subject=Chemistry` | 知识点列表 |
| GET | `/api/knowledge/{id}/dependencies` | 依赖链（递归前置） |
| GET | `/api/knowledge/{id}/weak-prerequisites?studentId=` | 薄弱前置 |

### 提交一步思考

```http
POST /api/tutor/submit-step
Content-Type: application/json

{
  "studentId": "stu-001",
  "problemId": "C-FE-01",
  "stepIndex": 1,
  "stepContent": "NaOH"
}
```

响应：

```json
{
  "status": "LocalMistake",
  "verdict": "Incorrect",
  "errorTag": "概念混淆",
  "hintLevel": 1,
  "recommendedAction": "SelfCheck",
  "message": "方向可能对，但还不完整。别急着改答案，先回头检查……",
  "relatedKnowledgeIds": ["C-FE"],
  "unlockedNextStep": false,
  "completed": false,
  "nextStepIndex": 1,
  "hintText": "回忆 Fe³⁺ 的特征检验试剂，不要与 Fe²⁺ 的检验混淆。",
  "mastery": 0.15,
  "currentStep": 1,
  "totalSteps": 3,
  "weakPrerequisites": [],
  "variant": null
}
```

## 状态机

| Status | 含义 | 前端建议 |
|---|---|---|
| `AwaitingStep` | 等待作答 | 显示当前步骤输入框 |
| `StepAccepted` | 本步正确 | 解锁下一步，播放推进动画 |
| `LocalMistake` | 局部小错 / 方向对但不完整 | 高亮自查提示，不揭示答案 |
| `KeyMistake` | 关键步骤错误 | 标记卡壳点，展示低梯度线索 |
| `EscalatingHint` | 连续卡壳 ≥ 3 次 | 播放"降级"动效，展示线索/脚手架 |
| `Completed` | 整题通关 | 播放通关动画，进入变式题 |

`recommendedAction` 取值：`Proceed` / `SelfCheck` / `AskSubQuestion` / `GiveClue` / `GiveScaffold` / `RetryStep` / `FinishLevel` / `ReviewKnowledge`。

提示梯度（`hintLevel`）：

| 级别 | 含义 | 来源字段 |
|---|---|---|
| 1 | 自查方向 | `HintSelfCheck` |
| 2 | 子问题引导 | `HintSubQuestion` |
| 3 | 关键线索 | `HintClue` |
| 4 | 分步脚手架 | `Scaffold` |

## 错误标签体系

`概念混淆` / `条件漏看` / `推理跳跃` / `公式记错` / `共存判断错误` / `计算失误` / `审题错误`

## 前端对接协议

1. `GET /api/problem/{id}` 拿步骤提示；**不要**在前端缓存或推断标准答案。
2. 每次学生提交一步，带上 `stepIndex`，服务端会校验顺序；`unlockedNextStep=false` 时禁止推进。
3. 按 `status` + `recommendedAction` + `hintLevel` 决定 UI：
   - `SelfCheck` → 只高亮题干相关条件；
   - `AskSubQuestion` → 以气泡展示 `hintText`；
   - `GiveClue` / `GiveScaffold` → 展开线索卡；
   - `FinishLevel` → 展示 `variant` 作为下一个关卡入口。
4. `weakPrerequisites` 非空时，可在画布侧边提示"建议先复习 XXX"。
5. 学情、进度、复测建议均可直接驱动闯关地图与复习面板。

## 测试流程（PowerShell）

```powershell
# 1. 加载题目
Invoke-RestMethod http://localhost:5080/api/problem/C-FE-01

# 2. 连续答错，观察提示升级
$body = @{studentId="stu-001";problemId="C-FE-01";stepIndex=1;stepContent="NaOH"} | ConvertTo-Json
Invoke-RestMethod http://localhost:5080/api/tutor/submit-step -Method Post -ContentType "application/json; charset=utf-8" -Body $body

# 3. 答对推进
$body = @{studentId="stu-001";problemId="C-FE-01";stepIndex=1;stepContent="KSCN"} | ConvertTo-Json
Invoke-RestMethod http://localhost:5080/api/tutor/submit-step -Method Post -ContentType "application/json; charset=utf-8" -Body $body

# 4. 查看学情与复测
Invoke-RestMethod http://localhost:5080/api/student/stu-001
Invoke-RestMethod http://localhost:5080/api/student/stu-001/review-suggest
```

已验证链路：连续 3 次答错依次返回 `SelfCheck → AskSubQuestion → GiveClue`，且 `unlockedNextStep=false`、掌握度递减；三步答对后 `Completed=true` 并自动生成变式题 `C-FE-01-V1`。

## 种子数据

- 知识点：化学 6 个（电解质与电离、离子反应、氧化还原、离子共存、铁及其化合物、钠及其化合物），含 8 条依赖边
- 题目：Fe³⁺ 检验、Na₂CO₃/NaHCO₃ 鉴别、酸性条件离子共存
- 每题 3 步，均为关键步骤标注 + 四级提示

## 后续可扩展

- 开启 LLM 话术（`Llm.Enabled=true`，模型可切换 Seed Turbo / Qwen Flash）
- SQLite → PostgreSQL：替换 `UseSqlite` 为 `UseNpgsql`，重新生成迁移
- 变式生成可扩展为模板化替换（换物质/换条件），需在题目模板中声明替换规则，避免自动改写化学式
