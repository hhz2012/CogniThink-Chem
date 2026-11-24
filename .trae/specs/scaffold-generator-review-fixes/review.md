# ScaffoldGenerator 审查修复 - 独立 Review 报告

> 阶段：Spec Mode Review（实现完成后独立核验）。只读核验，未修改任何源码。
> 方法：① 派发只读子代理（agent 5548e09b）逐条定位 AC 在源码的落地证据；② 主代理补做反向 grep 确认无残留；③ 主代理实测 build + test + git 范围。

## 一、AC 核验矩阵（AC-1 ~ AC-10）

| AC | 结论 | 证据（file 链接 + 行号） |
|----|------|--------------------------|
| AC-1 choice 分隔符多选不再被误拒 | **PASS** | [StepTruthExtractor.cs#L70-L102](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/ScaffoldGenerator/StepTruthExtractor.cs#L70-L102) 仅对 choice 归一化；[ScaffoldGeneratorUtils.cs#L22-L38](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/ScaffoldGenerator/ScaffoldGeneratorUtils.cs#L22-L38) `ChoiceAnswerLetters` 支持 `A,C`/`A、C`/`AC`。单测见 [ScaffoldGeneratorTask1Tests.cs](file:///D:/github/CogniThink-Chem/tests/CogniThink.TutorBrain.Tests/ScaffoldGeneratorTask1Tests.cs)。 |
| AC-2 非 choice 不再误报 conflict | **PASS** | 同上：非 choice → `ExtractAnswer` 返回 null → F 关键步无真值 → `unsuitable`，不发布猜测 correct。单测覆盖 fill/空 answer → `unsuitable` 且 ≠`conflict`。 |
| AC-3 status 不依赖错误文案子串 | **PASS** | [ScaffoldBuilder.cs#L110-L120](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/ScaffoldGenerator/ScaffoldBuilder.cs#L110-L120) 改用 `validation.Conflict` 类型化判定；`ScaffoldGenerator` 全目录反向 grep `Contains("冲突")` → **无匹配**。 |
| AC-4 枚举未知值不静默映射 | **PASS** | [EnumMemberJsonConverter.cs#L18-L30](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/ScaffoldGenerator/EnumMemberJsonConverter.cs#L18-L30) 未知值抛 `JsonException`，不回退 `default(T)`。单测见 Task7Tests `EnumConverter_UnknownModule_ThrowsNotRedoxReasoning` + `EnumConverter_KnownModule_RoundTrips`。 |
| AC-5 产物行尾跨 OS 幂等 | **PASS** | [ScaffoldArtifactWriter.cs#L30-L50](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/ScaffoldGenerator/ScaffoldArtifactWriter.cs#L30-L50) 末尾 `.Replace("\r\n","\n").Replace("\r","")`；[.gitattributes#L1-L2](file:///D:/github/CogniThink-Chem/.gitattributes#L1-L2) 含 `*.ts text eol=lf`。单测 Task5Tests `Writer_OutputHasNoCarriageReturn`。 |
| AC-6 checkpoint 坏行可审计 | **PASS** | [Program.cs#L125-L149](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.ScaffoldGenerator/Program.cs#L125-L149) 坏行打印 `path:lineNo` 并 `badLines++`；`ScaffoldGenerator` 全目录反向 grep 空 `catch {}` → **无匹配**。 |
| AC-7 correct 比较器口径统一 | **PASS** | [ScaffoldValidator.cs#L139-L145](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/ScaffoldGenerator/ScaffoldValidator.cs#L139-L145) `CorrectMatchesAnswer` 用 `ChoiceAnswerLetters` + `OrdinalIgnoreCase`；`ScaffoldGeneratorUtils.CorrectSet` 与 `StoredCorrectSet` 均 `OrdinalIgnoreCase`。 |
| AC-8 死代码清理 | **PASS** | 全仓库 `*.cs` 反向 grep `GetEquationText`/`KeyEquationText`/`equation_text` → **无匹配**；`LlmProxyScaffoldClient` 已加预留注释；`LlmDistractorProvider` sync-over-async 已加「仅限离线批量」强标注（[LlmDistractorProvider.cs#L13-L16](file:///D:/github/CogniThink-Chem/src/CogniThink.TutorBrain.Api/Application/ScaffoldGenerator/LlmDistractorProvider.cs#L13-L16)），且未在 Api `Program.cs` DI 注册（grep 无匹配）。 |
| AC-9 构建与测试零回归 | **PASS** | 主代理实测：`dotnet build` → **0 错误**（7 警告均在既有无关文件）；`dotnet test --filter "FullyQualifiedName~ScaffoldGenerator"` → **通过 52 / 失败 0 / 跳过 0**（原 41 + 新增 11）。新增测试方法已确认存在于 Task1/Task5/Task7Tests。 |
| AC-10 不混入无关改动 | **PASS** | `git status --short` 显示本任务改动仅 15 个预期文件（ScaffoldGenerator 源码 10 + Program.cs + 3 测试 + .gitattributes）+ 1 个新建 spec 目录；`miniapp/...reaction...`、`pdf_questions.json`、`differentiated-solve-models/*` 等无关改动原封未动。 |

## 二、TR 覆盖

9 个 Task 的全部 rule 型 TR 均已通过（详见 tasks.md 的 Completion Evidence Summary）。关键负向 TR：
- TR-3.1（无 `Contains("冲突")` 控制流）→ grep 无匹配 ✓
- TR-4.1（未知值 ≠ RedoxReasoning）→ 单测断言抛 `JsonException` ✓
- TR-5.1（`ToTs` 不含 `\r`）→ 单测 `Writer_OutputHasNoCarriageReturn` ✓
- TR-6.1（无空 `catch {}`）→ grep 无匹配 ✓
- TR-7.1（无死引用）→ grep 无匹配 ✓
- TR-8.1（未在 Api DI 注册）→ grep 无匹配 ✓
- TR-9.1/9.2/9.3 → build 0 错 / 52 全绿 / git 范围干净 ✓

## 三、Spec 偏离说明（均在 spec 允许范围内）

1. **Task 4 实现选型**：spec FR-5/AC-4 允许「`Unknown=0` 哨兵」或「显式抛错」二者择一。实现选择**显式抛 `JsonException`**，不引入 `Unknown` 哨兵、不改枚举成员值 → 数据损坏可见性同等达成，且不改变前端契约（产物序列化为已知字符串字面量不受影响）。单测相应断言「抛 `JsonException`」而非「映射到 `Unknown`」。
2. **Task 5 行尾护栏**：未重生成落盘 `miniapp/.../generated/index.ts`（避免无关 diff）。`Task7Tests` 字节一致比较改为两侧归一化行尾（`.Replace("\r\n","\n")`），工作区 `autocrlf=true` 检出的 CRLF 不影响断言。`.gitattributes` 已钉 `eol=lf`，未来新检出自动 LF。

## 四、红线复核

- **precision=1 / harm=0**：#1 修复仅放宽 choice 分隔符多选的发布（此前被误拒）；非 choice 明确判 `unsuitable`、绝不猜测发布 correct → 不发布错误 correct，红线守住。
- **零回归**：原 41 项测试全绿；未触碰判分链路（`gatewayCorrect` / eval 精度逻辑未改）。
- **不混入无关改动**：git 范围确认干净。
- **未主动 commit**：遵守约束，等待用户指令。

## 五、总体结论

**ALL PASS** —— 8 项问题修复全部落地，10 条 AC 与全部 rule 型 TR 通过，构建 0 错误、52/52 测试绿、git 范围干净、红线守住。

**建议**：修复就绪，可交由用户审阅；如需提交，请用户明确指示后再 commit（建议独立 commit，不混入工作区其它未提交改动）。
