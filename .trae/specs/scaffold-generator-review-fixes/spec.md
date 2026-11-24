# ScaffoldGenerator 审查修复 - Product Requirements Document

## Overview
- **Summary**: 修复 ScaffoldGenerator 模块（commit `87e118e` 新增）代码内容级审查发现的 8 项问题。该 8 项经 2 个独立子代理交叉验证达成 2/2 共识（另有 2 项经共识排除：贪婪 JSON 截取失败安全、干扰项相交检测为有效兜底）。
- **Purpose**: 消除真实召回损失与状态误分类（#1 正在误拒约 17.5% 可发布 redox 题并污染 `conflict` 计数），并加固可维护性与健壮性；全程守住 precision=1 / harm=0 / 零回归。
- **Target Users**: 维护 ScaffoldGenerator 的后端开发者；消费生成产物 `miniapp/src/data/scaffolds/generated/index.ts` 的前端脚手架练习。

## Goals
- 修复 #1（MAJOR）：choice 逗号/分隔符多选（`A,C` / `A,B,C,D` / `A、C`）不再被误拒；非选择题型明确判 `unsuitable`（清晰原因），不再误报 `conflict`。
- 修复 #2（MINOR）：用类型化错误分类替代 `Contains("冲突")` 子串控制流。
- 加固 #3～#8（LOW）：sync-over-async 显式标注仅限离线、枚举未知值不静默映射到真实业务值、产物行尾跨 OS 幂等、checkpoint 坏行可审计、correct 比较器口径统一、死代码清理。
- 保持 `dotnet build` 0 错误、ScaffoldGenerator 测试全绿；不碰判分链路；不混入工作区无关的未提交改动。

## Non-Goals
- 不扩展到 `redox-reasoning` 以外的模块（Task 8 推广，留后续迭代）。
- 不修改 Chem.Core 引擎逻辑（`SplitHalfReactions` 的 `"acidic"` 介质硬编码为未确证观察项，不在本轮）。
- 不主动重生成落盘产物 `index.ts`；仅当 #1 改变发布判定导致 Task7 护栏字节一致断言失效时才重生成，并单独说明。
- 不提交 commit（除非用户明确要求）。
- 不触碰工作区已有的「反应设计清理」未提交改动（`miniapp/...reaction...`、`pdf_questions.json` 等）；本修复仅改 ScaffoldGenerator 相关源码与测试。

## Background & Context
- 本轮审查报告（会话内产出）：8 项问题（1 MAJOR / 1 MINOR / 6 LOW），详见审查表格。
- 原始功能 spec [dynamic-scaffold-generation/spec.md](file:///D:/github/CogniThink-Chem/.trae/specs/dynamic-scaffold-generation/spec.md) 已决 Open Question：
  - F 步仅选择题门控，填空/计算 F 步只读揭示；
  - 关键步（A/B/E/F）无可靠真值 → 整题 `unsuitable`，不发布猜测 correct。
  本修复与之一致：非 choice → F 关键步无选择真值 → `unsuitable`（非 `conflict`）。
- 工作区 `git status` 显示大量与本任务无关的未提交改动；本修复严格限定文件范围，不 stage 无关文件。

## Functional Requirements

- **FR-1 (#1)**: `StepTruthExtractor.ExtractAnswer` 对 choice 题型归一化 answer：支持逗号 `,`、中文逗号 `，`/`、`、分号 `;` 分隔的多选（`"A,C"` / `"A,B,C,D"` / `"A、C"` → 集合 `{A,B,C,D}`），并保留纯大写连写（`"AC"` → `{A,C}`）与单选（`"A"`）。
- **FR-2 (#1)**: 非 choice 题型（type 不含 `choice`）`ExtractAnswer` 返回 null → F 步降级（无 truth），由校验门关键步缺失判 `unsuitable`，不再把整串公式/文本存为 single-select 后误报 `conflict`。
- **FR-3 (#1)**: `ScaffoldValidator.CorrectMatchesAnswer` 使用与 `ExtractAnswer` 一致的归一化口径比较 F correct 与存储 answer；两侧口径统一后 choice 分隔符多选对齐通过。
- **FR-4 (#2)**: `ScaffoldBuilder` 不再通过错误消息含「冲突」判定 `status`；`ScaffoldValidator` / `ScaffoldValidationSummary` 提供类型化错误分类（如错误码枚举或分类字段），`status=conflict` 仅由「F correct 与 answer 真实冲突」这一分类触发。
- **FR-5 (#4)**: `EnumMemberJsonConverter.Read` 遇到未知枚举字符串时不静默回退 `default(T)`（= `RedoxReasoning`）；对 `ScaffoldModuleKind` 等引入 `Unknown=0` 哨兵或显式抛错，使数据损坏可见；补未知值反序列化单测。
- **FR-6 (#5)**: `ScaffoldArtifactWriter` 产物行尾与 OS 无关（统一 `\n`），并在根 `.gitattributes` 钉 `*.ts text eol=lf`，保证跨 OS 字节幂等。
- **FR-7 (#6)**: `Program.cs LoadCheckpoint` 对反序列化失败的坏行打印可审计信息（行号 / id）并计入计数，不再 `catch { }` 静默吞噬。
- **FR-8 (#7)**: `ScaffoldValidator` 与 `ScaffoldGeneratorUtils.CorrectSet` 的 correct 集合比较器统一为 `OrdinalIgnoreCase`（与前端判分对选项字母忽略大小写的口径一致）。
- **FR-9 (#8)**: 移除死代码：`EngineArtifactsBuilder.GetEquationText` 与常量 `KeyEquationText`（`equation_text` 写而不读）；评估 `LlmProxyScaffoldClient`（当前生产/测试未实例化）——若控制台不接入 LLM 则保留为预留并加注释，若确定不用则移除。

## Non-Functional Requirements
- **NFR-1**: `dotnet build` 0 错误；ScaffoldGenerator 全部测试（41 项）全绿，新增回归测试通过。
- **NFR-2**: 零回归：不改判分链路；eval `gatewayCorrect` / `precision` / `harm` 字节级不变。
- **NFR-3**: precision=1 / harm=0 红线不被破坏：#1 修复不得发布任何错误 correct（非 choice → `unsuitable`，绝不猜测发布）。
- **NFR-4 (rubric)**: 修复后口径一致性与可维护性提升，无新增低价值注释。

## Constraints
- **Technical**: dotnet 8；仅修改 ScaffoldGenerator 相关源码 + 测试；`#5` 可触及根 `.gitattributes`。
- **Business**: 不破坏现有脚手架产物消费；不混入无关未提交改动；不主动 commit。
- **Dependencies**: 依赖现有 `dynamic-scaffold-generation` spec 的已决红线与降级策略。

## Assumptions
- 非 choice 题型当前不在 MVP 发布范围内（F 为关键步且无选择字母真值）→ 判 `unsuitable` 是 spec-consistent，不视为召回目标；#1 的核心是消除「误报 conflict」与「choice 逗号多选被误拒」。
- #1 修复可能改变发布判定（部分此前被误拒的 choice 逗号多选题现在会发布）；若导致落盘 `index.ts` 变化，需重生成并使 Task7 护栏字节一致断言重新通过。
- `#4` 引入 `Unknown` 哨兵不得出现在序列化产物中（仅作反序列化容错），避免破坏前端契约。

## Acceptance Criteria

### AC-1: choice 逗号/分隔符多选不再被误拒
- **Type**: `rule`
- **Given**: 一道 redox 选择题，answer 为 `"A,C"` 或 `"A,B,C,D"` 或 `"A、C"`
- **When**: 跑 `ScaffoldBuilder.BuildDetailedAsync`
- **Then**: `status == "published"`，F 步 correct 集合等于归一化后的字母集合
- **Pass Condition**: 新增单测断言通过
- **Evidence**: 单元测试

### AC-2: 非 choice 题型不再误报 conflict
- **Type**: `rule`
- **Given**: 一道 redox 题，type 不含 choice（如 fill / calc / judge），或 answer 为空
- **When**: 跑管线
- **Then**: `status == "unsuitable"`（不得为 `"conflict"`），错误原因清晰指向 F 步无选择真值 / 非选择题型
- **Pass Condition**: 新增单测断言 `status != "conflict"`
- **Evidence**: 单元测试

### AC-3: status 判定不依赖错误文案子串
- **Type**: `rule`
- **Given**: 校验失败任意场景
- **When**: `ScaffoldBuilder` 判定 `status`
- **Then**: 控制流不使用 `e.Contains("冲突")`；改用类型化分类
- **Pass Condition**: 源码 grep 无 `Contains("冲突")` 用于 status 控制流
- **Evidence**: 源码审查 + grep

### AC-4: 枚举未知值不静默映射到真实业务值
- **Type**: `rule`
- **Given**: 反序列化一个含未知模块字符串（如 `"foo-module"`）的 scaffold JSON
- **When**: `EnumMemberJsonConverter<ScaffoldModuleKind>.Read` 处理
- **Then**: 不返回 `RedoxReasoning`；返回 `Unknown` 哨兵或抛错（数据损坏可见）
- **Pass Condition**: 新增单测断言未知值 ≠ RedoxReasoning
- **Evidence**: 单元测试

### AC-5: 产物行尾跨 OS 一致
- **Type**: `rule`
- **Given**: `ScaffoldArtifactWriter.ToTs` 输出
- **When**: 检查行尾字节
- **Then**: 全行尾统一为 `\n`，不含 `\r\n`；`.gitattributes` 含 `*.ts text eol=lf`
- **Pass Condition**: 单测断言产物不含 `\r`，且 `git check-attr` 确认
- **Evidence**: 单元测试 + 文件内容

### AC-6: checkpoint 坏行可审计
- **Type**: `rule`
- **Given**: checkpoint 含一行非法 JSON
- **When**: `LoadCheckpoint` 读取
- **Then**: 该坏行被跳过但打印行号/id 并计入计数，不中断整批
- **Pass Condition**: 源码无空 `catch { }`；新增单测或日志断言
- **Evidence**: 源码审查 + 单元测试

### AC-7: correct 比较器口径统一
- **Type**: `rule`
- **Given**: correct 与 option 仅大小写不同（如 `Fe` vs `fe`）
- **When**: 校验门比较
- **Then**: `StoredCorrectSet` 与 `CorrectSet` 均为 `OrdinalIgnoreCase`，行为一致
- **Pass Condition**: 源码两侧均使用 `OrdinalIgnoreCase`；现有大小写碰撞拦截测试仍通过
- **Evidence**: 源码审查 + 单元测试

### AC-8: 死代码清理
- **Type**: `rule`
- **Given**: 修复完成
- **When**: grep `GetEquationText` / `KeyEquationText`
- **Then**: 无写而不读的死代码残留；`LlmProxyScaffoldClient` 有明确处置（接入或标注预留）
- **Pass Condition**: grep 无死引用
- **Evidence**: 源码审查 + grep

### AC-9: 构建与测试零回归
- **Type**: `rule`
- **Given**: 全部修复完成
- **When**: 跑 `dotnet build` 与 `dotnet test --filter "FullyQualifiedName~ScaffoldGenerator"`
- **Then**: build 0 错误；ScaffoldGenerator 测试 100% 通过（含新增回归测试）
- **Pass Condition**: 测试全绿
- **Evidence**: 测试日志

### AC-10: 不混入无关改动
- **Type**: `rule`
- **Given**: 修复完成
- **When**: `git status`
- **Then**: 仅有 ScaffoldGenerator 相关文件与 `.gitattributes` 被修改，`miniapp/...reaction...`、`pdf_questions.json` 等无关文件未被本任务触碰
- **Pass Condition**: `git diff --name-only` 仅含预期文件
- **Evidence**: git status

## Open Questions
- [ ] #1 修复后是否需要重生成落盘 `index.ts`？取决于 choice 逗号多选题是否出现在 `gatewayCorrect=true` 的 redox 子集中（子代理实测当前该子集触发数为 0，但 `pdf-100`/`pdf-197` 等题存在该格式）。若重生成，将单独执行并验证 Task7 字节一致。→ 默认不主动重生成，仅当测试失败时处理。
- [ ] #8 `LlmProxyScaffoldClient` 处置：控制台 `Program.cs` 当前不接入 LLM（默认 `StaticDistractorProvider` + `NullScaffoldLlmClient`）。是接入 LLM 干扰项/文案，还是保留为预留？→ 默认保留并加注释标注「预留，控制台未启用」，不在本轮接入 LLM。
