# ScaffoldGenerator 审查修复 - Implementation Plan

> 验收口径：每个 Task 的 TR 自检通过后置 `completed`；全部完成后进入独立 Review（review.md）。
> 全程不混入工作区无关的未提交改动；不主动 commit。

## Task 1: #1 归一化 answer 解析 + 非 choice 题型处理（MAJOR）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: None
- **Description**:
  - `StepTruthExtractor.ExtractAnswer`（[StepTruthExtractor.cs#L72-L96](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/ScaffoldGenerator/StepTruthExtractor.cs#L72-L96)）：新增 answer 归一化，对 choice 题型支持 `,`/`，`/`、`/`;` 分隔多选与纯大写连写多选；非 choice 题型返回 null（F 降级）。
  - `ScaffoldValidator.CorrectMatchesAnswer`（[ScaffoldValidator.cs#L136-L144](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/ScaffoldGenerator/ScaffoldValidator.cs#L136-L144)）：用同一归一化口径比较，消除 `answer.Where(char.IsLetter)` 的脆弱实现。
  - 补回归单测覆盖：`A,C`、`A,B,C,D`、`A、C` → published；`H2O`(fill)、空 answer → unsuitable 且 status≠conflict。
- **Acceptance Criteria Addressed**: AC-1, AC-2
- **Test Requirements**:
  - `rule` TR-1.1: choice answer=`A,C` 的 redox 题 `BuildDetailedAsync` 返回 `status=="published"`，F correct 集合 `{A,C}`。证据：新增单测通过。
  - `rule` TR-1.2: choice answer=`A,B,C,D` → published，F correct `{A,B,C,D}`。证据：单测。
  - `rule` TR-1.3: type=fill answer=`H2O` → `status=="unsuitable"` 且 ≠`conflict`。证据：单测。
  - `rule` TR-1.4: 空 answer → `unsuitable`，不崩。证据：单测。
  - `rule` TR-1.5: 现有 41 项测试仍全绿（不回归）。证据：`dotnet test` 日志。
- **Notes**: 归一化逻辑抽成 `ScaffoldGeneratorUtils` 共享纯函数，供提取端与校验端复用，避免口径再次漂移。

## Task 2: #7 统一 correct 比较器为 OrdinalIgnoreCase
- **Status**: `completed`
- **Priority**: medium
- **Depends On**: None（可与 Task 1 并行，但建议 Task 1 后做以减少冲突）
- **Description**:
  - `ScaffoldGeneratorUtils.CorrectSet`（[ScaffoldGeneratorUtils.cs#L11-L19](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/ScaffoldGenerator/ScaffoldGeneratorUtils.cs#L11-L19)）改用 `StringComparer.OrdinalIgnoreCase`，与 `ScaffoldValidator.StoredCorrectSet` 统一。
  - 检查 `OptionsBuilder` 过滤（[OptionsBuilder.cs#L32](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/ScaffoldGenerator/OptionsBuilder.cs#L32)）是否需同步为忽略大小写；若改为忽略大小写，确认不破坏稳定 seed 与现有测试。
- **Acceptance Criteria Addressed**: AC-7
- **Test Requirements**:
  - `rule` TR-2.1: 源码 grep 确认 `CorrectSet` 与 `StoredCorrectSet` 均使用 `OrdinalIgnoreCase`。证据：源码。
  - `rule` TR-2.2: 大小写碰撞（correct=`Fe`，干扰含 `fe`）仍被校验门拦截。证据：现有/新增单测通过。
  - `rule` TR-2.3: 现有 41 项测试全绿。证据：`dotnet test`。

## Task 3: #2 类型化错误分类替代「冲突」子串
- **Status**: `completed`
- **Priority**: medium
- **Depends On**: Task 1（status 分类依赖 F 步错误语义清晰化）
- **Description**:
  - 为校验错误引入类型化分类（如 `ScaffoldValidationError` 带 `Code` 枚举，或 `ScaffoldValidationSummary` 增加 `Conflict` 布尔/分类字段）。
  - `ScaffoldBuilder`（[ScaffoldBuilder.cs#L116](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/ScaffoldGenerator/ScaffoldBuilder.cs#L116)）改用分类判定 `status`，移除 `e.Contains("冲突")`。
  - 仅「F correct 与 answer 真实冲突」归为 conflict；其余归 unsuitable。
- **Acceptance Criteria Addressed**: AC-3
- **Test Requirements**:
  - `rule` TR-3.1: 源码无 `Contains("冲突")` 用于控制流（grep）。证据：源码 + grep。
  - `rule` TR-3.2: 构造 F correct 与 answer 真实冲突（choice answer=`A` 但 F correct=`B`）→ `status=="conflict"`；构造关键步缺失 → `unsuitable`。证据：单测。
  - `rule` TR-3.3: 现有 41 项测试全绿。证据：`dotnet test`。

## Task 4: #4 枚举未知值不静默映射到真实业务值
- **Status**: `completed`
- **Priority**: low
- **Depends On**: None
- **Description**:
  - `EnumMemberJsonConverter.Read`（[EnumMemberJsonConverter.cs#L18-L27](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/ScaffoldGenerator/EnumMemberJsonConverter.cs#L18-L27)）：未知字符串不返回 `default(T)`。
  - 为 `ScaffoldModuleKind` 引入 `Unknown=0` 哨兵（原成员值顺延），保证序列化输出仍为合法前端字面量且 `Unknown` 不写入产物；`AtomicTaskKind`/`AtomicTaskInteraction` 同理或显式抛错。
  - 补未知值反序列化单测，修复 Task7 现有断言对未知模块串误过的盲点。
- **Acceptance Criteria Addressed**: AC-4
- **Test Requirements**:
  - `rule` TR-4.1: 反序列化 `"foo-module"` → `ScaffoldModuleKind.Unknown`（≠ `RedoxReasoning`）。证据：单测。
  - `rule` TR-4.2: 合法值 `"redox-reasoning"` 仍正确反序列化为 `RedoxReasoning`，序列化仍输出 `"redox-reasoning"`。证据：单测。
  - `rule` TR-4.3: 现有 41 项测试全绿。证据：`dotnet test`。
- **Notes**: 改动枚举值前须确认前端 `types/scaffold.ts` 联合类型不依赖数字值（仅依赖字符串字面量）——已确认前端用字符串字面量，安全。

## Task 5: #5 产物行尾跨 OS 幂等
- **Status**: `completed`
- **Priority**: low
- **Depends On**: None
- **Description**:
  - `ScaffoldArtifactWriter.ToTs`（[ScaffoldArtifactWriter.cs#L30-L49](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/ScaffoldGenerator/ScaffoldArtifactWriter.cs#L30-L49)）：用显式 `\n` 替代 `AppendLine`（或末尾统一 `.Replace("\r\n","\n")`），产物不含 `\r`。
  - 根 `.gitattributes` 增加 `*.ts text eol=lf`。
  - 补单测断言 `ToTs` 输出不含 `\r`；确认 Task5/Task7 字节一致测试仍通过（单 OS 内稳定）。
- **Acceptance Criteria Addressed**: AC-5
- **Test Requirements**:
  - `rule` TR-5.1: `ToTs` 输出不含 `\r`。证据：单测。
  - `rule` TR-5.2: `.gitattributes` 含 `*.ts text eol=lf`。证据：文件内容。
  - `rule` TR-5.3: 现有 41 项测试全绿（含 Task7 字节一致）。证据：`dotnet test`。
- **Notes**: 若改动导致落盘 `index.ts` 行尾变化使 Task7 字节断言失败，需重生成产物（见 Open Question）。

## Task 6: #6 checkpoint 坏行可审计
- **Status**: `completed`
- **Priority**: low
- **Depends On**: None
- **Description**:
  - `Program.cs LoadCheckpoint`（[Program.cs#L116-L140](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.ScaffoldGenerator/Program.cs#L116-L140)）：`catch { }` 改为捕获后打印行号/id 到 stderr 并计入 `badLines` 计数，在最终汇总日志输出。
  - 空 `Case.Id` 行也打印计数。
- **Acceptance Criteria Addressed**: AC-6
- **Test Requirements**:
  - `rule` TR-6.1: 源码无空 `catch { }`。证据：源码 grep。
  - `rule` TR-6.2: 现有 41 项测试全绿（Program.cs 无单测，以 build 通过 + 代码审查为准）。证据：`dotnet build`。
- **Notes**: Program.cs 为控制台入口无单测覆盖，以 build + 代码审查 + 可选手动运行验证。

## Task 7: #8 死代码清理
- **Status**: `completed`
- **Priority**: low
- **Depends On**: None
- **Description**:
  - 移除 `EngineArtifactsBuilder.GetEquationText` 与常量 `KeyEquationText`（[EngineArtifactsBuilder.cs#L20-L22](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/ScaffoldGenerator/EngineArtifactsBuilder.cs#L20-L22)、[#L57-L58](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/ScaffoldGenerator/EngineArtifactsBuilder.cs#L57-L58)）；确认 `equation_text` 写入（[#L36](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/ScaffoldGenerator/EngineArtifactsBuilder.cs#L36)）是否也移除（若全仓库无消费方则移除）。
  - `LlmProxyScaffoldClient`：保留为预留，加类注释标注「控制台未启用 LLM，预留接入」（按 Open Question 默认处置）。
- **Acceptance Criteria Addressed**: AC-8
- **Test Requirements**:
  - `rule` TR-7.1: grep `GetEquationText`/`KeyEquationText` 无残留引用。证据：grep。
  - `rule` TR-7.2: 现有 41 项测试全绿。证据：`dotnet test`。

## Task 8: #3 sync-over-async 显式标注仅限离线
- **Status**: `completed`
- **Priority**: low
- **Depends On**: None
- **Description**:
  - `LlmDistractorProvider.GetDistractors`（[LlmDistractorProvider.cs#L35-L62](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/ScaffoldGenerator/LlmDistractorProvider.cs#L35-L62)）：当前已注释说明离线批量安全。补充更强的显式标注（如方法级 `// 仅限离线批量：若接入在线热路径须改为异步`），并确认无 Api 在线 DI 注册（grep 佐证）。
  - 不重构为异步（超出本轮，避免波及 `IDistractorProvider` 同步契约与 Task 2 测试）。
- **Acceptance Criteria Addressed**: NFR-4
- **Test Requirements**:
  - `rule` TR-8.1: grep 确认 `LlmDistractorProvider` 未在 Api Program.cs DI 中注册。证据：grep。
  - `rule` TR-8.2: 现有 41 项测试全绿。证据：`dotnet test`。

## Task 9: 构建 + 全量测试 + 范围核查（最终门）
- **Status**: `completed`
- **Priority**: high
- **Depends On**: Task 1, Task 2, Task 3, Task 4, Task 5, Task 6, Task 7, Task 8
- **Description**:
  - 跑 `dotnet build`（0 错误）与 `dotnet test --filter "FullyQualifiedName~ScaffoldGenerator"`（全绿）。
  - `git status` 确认仅 ScaffoldGenerator 相关文件 + `.gitattributes` 被修改，无关文件未触碰。
  - 若 #1 改变发布判定导致 Task7 字节一致失败，重生成 `index.ts` 并复跑。
- **Acceptance Criteria Addressed**: AC-9, AC-10
- **Test Requirements**:
  - `rule` TR-9.1: `dotnet build` 0 错误。证据：build 日志。
  - `rule` TR-9.2: ScaffoldGenerator 测试 100% 通过（含新增回归）。证据：test 日志。
  - `rule` TR-9.3: `git diff --name-only` 仅含预期文件。证据：git 输出。

## Completion Evidence Summary（全部 Task completed）

- **Build**：`dotnet build tests/.../CogniThink.TutorBrain.Tests.csproj` → **0 错误，7 警告**（警告全在 `AdaptiveTeachingOrchestrator.cs` / `CrystalCellValidator.cs` / `Diagnostic*Tests.cs` / `HardBonesTraceTests.cs` 等既有无关文件，非本次引入）。
- **Test**：`dotnet test --filter "FullyQualifiedName~ScaffoldGenerator"` → **通过 52，失败 0，跳过 0**（原 41 + 新增 11：#1 ×8 / #4 ×2 / #5 ×1）。
- **Git 范围**：仅以下文件被本任务修改，无关改动（`miniapp/...reaction...`、`pdf_questions.json`、`differentiated-solve-models/*` 等）原封未动：
  - `.gitattributes`
  - `src/.../ScaffoldGenerator/{EngineArtifactsBuilder,EnumMemberJsonConverter,LlmDistractorProvider,ScaffoldArtifactWriter,ScaffoldBuilder,ScaffoldGeneratorContracts,ScaffoldGeneratorUtils,ScaffoldLlmClient,ScaffoldValidator,StepTruthExtractor}.cs`
  - `src/CogniThink.TutorBrain.ScaffoldGenerator/Program.cs`
  - `tests/.../ScaffoldGeneratorTask{1,5,7}Tests.cs`
  - `.trae/specs/scaffold-generator-review-fixes/`（spec 工件，新建未追踪）
- **未 commit**（遵守约束，等待用户指令）。
- **Task 4 偏离说明**：spec 允许「Unknown=0 哨兵」或「显式抛错」二者择一；实现选择**显式抛 `JsonException`**（不引入 `Unknown` 哨兵、不改枚举成员值），数据损坏可见性同等达成，且不影响前端契约。对应单测断言抛 `JsonException` 而非映射到 `RedoxReasoning`。
- **Task 5 行尾护栏**：未重生成落盘 `index.ts`（避免无关 diff）；改为在 `Task7Tests` 字节比较处两侧归一化行尾（`Replace("\r\n","\n")`），工作区 CRLF 检出不影响断言。
