# 引擎能力扩展 · 并行任务说明（T1/T2/T3 共用）

> 本文件是 3 个并行子任务的共享契约。每个子任务另有自己的题目子集文件。
> 先读透本文件 + 模板代码，再动手写代码。**边写边验证，不许留红交付。**

## 0. 目标与不可逾越的红线

**目标**：让化学引擎（确定性校验器）能独立解出分配给你的题目子集，产出正确真值结论，不依赖 LLM。LLM 的正确答案（子集里的 `llmReference` 字段）只是**验证参照**，绝不直接灌进引擎。

**红线（违反即交付失败）**：
1. **化学引擎是唯一真值源**：结论必须由确定性规则/事实库推出，不调用 LLM。
2. **无唯一解必须返回 null，不蒙数**：任一选项无法干净判定 → 整题返回 null。
3. **精确率优先、零假阳性**：宁可漏解（返回 null），不可错解。错解 = harm = 失败。
4. **零回归**：新增/扩充不能让原本引擎解对的题变错。扩充事实库后必须跑现有护栏单测确认。
5. **事实/规则全在 JSON**：化学事实（物质、反应、常数）写进 `data/highschool/*.json`；`.cs` 只写判定逻辑，不硬编码化学事实。
6. **沿用 `PdfQuestionItem` schema、id 前缀**：新增测试题 id 用 `t3-` 前缀（T3 任务）等，不破坏现有 schema。

## 1. 代码结构（关键路径）

| 用途 | 路径 |
|---|---|
| 选择题校验器目录（你的新 validator 放这） | `src/CogniThink.TutorBrain.Api/Application/Gateway/` |
| 校验器模板（必读，照此模式写） | `src/CogniThink.TutorBrain.Api/Application/Gateway/ClassificationValidator.cs` |
| 共享基类（Outcome / ParseOptions） | `src/CogniThink.TutorBrain.Api/Application/Gateway/ChoiceVerifier.cs` |
| 接线点（**禁止你编辑**，由主控统一接） | `src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs` 的 `CallChoice` |
| 事实库目录（你的 JSON 放这） | `data/highschool/*.json` |
| 事实库加载方式 | `RuleStore.Load().Query<T>("文件名去.json")` |
| 测试项目 | 找到 `CogniThink.TutorBrain.Tests.csproj` |
| 你的题目子集（带 LLM 参照答案） | `_evalscratch/task_inputs/<你的任务>.json` |

## 2. 校验器写法（照抄 ClassificationValidator 模式）

```csharp
namespace CogniThink.TutorBrain.Application.Gateway;

public static class XxxValidator
{
    private const string ConstraintNote = "【高中限定】...事实库；任一选项无法解析则不臆断。";

    public static ChoiceVerifier.Outcome? TryDecide(EngineRequest req)
    {
        var rawText = req.RawText ?? string.Empty;
        var (stem, opts) = ChoiceVerifier.ParseOptions(rawText);
        if (opts.Count < 2) return null;                       // 解析不出选项 → 不蒙数
        // 判问法方向（correct/wrong），判不出 return null
        // 逐选项用事实库判定；任一选项判不出 return null
        // 横向唯一：满足方向的选项恰好唯一 → 返回字母；否则 return null
        return new ChoiceVerifier.Outcome(
            AnswerLetter: letter,
            FinalResult: $"正确选项为 {letter}",
            Reasons: reasons,
            ConstraintNote: ConstraintNote);
    }
}
```

- `ChoiceVerifier.Outcome` 是 `sealed record(string AnswerLetter, string FinalResult, List<string> Reasons, string ConstraintNote)`。
- `ChoiceVerifier.ParseOptions(rawText)` 返回 `(string stem, Dictionary<string,string> opts)`，key 是 "A"/"B"/"C"/"D"。
- 归一化工具：`ClassificationValidator.Norm(...)` 是 internal，若需要可在你自己的类里复制一份（上下标→ASCII、全角→半角、去空白）。
- 事实库用 `private static readonly Lazy<...> _index = new(BuildIndex)` 懒加载缓存。

## 3. 事实库加载

新建 JSON 放 `data/highschool/`，文件名 = key（去 `.json`）。加载：
```csharp
var data = RuleStore.Load().Query<MyDataPoco>("my-facts");  // my-facts.json
```
`RuleStore.Load()` 经 `DataRoot.Resolve()` 定位仓库根 `data/highschool/`。POCO 字段名与 JSON 对应。测试时同样能加载（测试用 `ChemistryFixture.Knowledge`，但直接 `RuleStore.Load()` 也能读到仓库 data）。

> **T2 注意**：你要扩充现有的 `substance-classification.json` 和 `na-particle-facts.json`——只**追加**条目，不删改现有条目。扩充后必须重跑这两个 validator 对应的护栏单测，确认没有让原来对的题变错（零回归）。

## 4. 独立测试方法（不依赖共享 BuildTestCases）

新建你自己的测试类，直接调 `XxxValidator.TryDecide(req)` 断言。不要改 `BuildTestCases()`/护栏遍历（那是共享文件）。

```csharp
public class XxxValidatorTaskTests
{
    [Fact]
    public void Covers_pdf040()
    {
        var req = new EngineRequest { RawText = "<题干含 A/B/C/D 选项的完整文本>" };
        var o = XxxValidator.TryDecide(req);
        Assert.NotNull(o);
        Assert.Equal("B", o.AnswerLetter);   // 用子集里 llmReference / expected 校验的真值
    }
}
```

构建与测试用**独立输出目录**，避免 3 个任务争抢同一 bin：
```
dotnet build <Api.csproj> -o _evalscratch/build_T1
dotnet test <Tests.csproj> --filter "FullyQualifiedName~XxxValidatorTaskTests" -o _evalscratch/build_T1
```
（把 `T1` 换成你的任务号。）

## 5. 硬约束：禁止编辑的文件

以下文件**绝对不要碰**（主控统一处理）：
- `src/CogniThink.TutorBrain.Api/Application/Gateway/LlmGatewayService.cs`（`CallChoice` 接线）
- 任何含 `BuildTestCases()` 或全题库护栏遍历的测试文件
- `tasks.md`
- 其他任务的 validator / JSON / 测试文件

可以编辑：**新建**的 `.cs`、**新建**的 JSON（T2 还可追加式扩充 `substance-classification.json`、`na-particle-facts.json`）。

## 6. 交付物清单（交付时在回复里列出）

1. **新建/修改文件清单**：路径 + 一句话说明。
2. **覆盖的题**：子集里哪些 id 现在能被你的 validator 正确解出（列出 id 或数量）。
3. **诚实 null 的题**：子集里哪些你判断"无唯一解/信息不足/超出高中范围"，故意返回 null（这是正确行为，不是失败）。
4. **接线需求**：每个新 validator 需要主控插到 `CallChoice` 的哪个位置（建议顺序），以及事实库 key 名。
5. **测试结果**：你的测试类跑出来 0 失败的证据（贴 `dotnet test` 输出摘要）。
6. **零回归自查**：你是否跑了受影响的现有护栏单测（T2 必跑），结果如何。

## 7. 工作顺序建议

1. 读 `ClassificationValidator.cs` + `ChoiceVerifier.cs` 前 80 行，吃透模式。
2. 读你的题目子集 JSON，先人工扫一遍，归纳出 2~4 个可被同一规则覆盖的题簇（不要试图一题一规则）。
3. 为每个题簇设计事实库结构 + 判定逻辑，先写事实库 JSON，再写 validator，再写测试。
4. 每题：用 `llmReference` 里 LLM 已判对的结论反推真值（选择题就是正确字母），验证你的 validator 产出一致。
5. 全部完成后跑自己的测试 + 受影响的现有护栏，0 失败再交付。
