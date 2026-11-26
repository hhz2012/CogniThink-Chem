using System.Runtime.CompilerServices;
using System.Text.Json;
using CogniThink.TutorBrain.Application.Gateway;
using Xunit;

namespace CogniThink.TutorBrain.Tests;

/// <summary>
/// T3：弱电解质稀溶液复合子问求解（浓度精确表示式 + 电离程度增大选择）。
/// 全部用例经 <c>CallEngineAsync</c>（equilibrium 路由）端到端验证；反例断言不产出浓度表达式（求解器域外 null）。
/// </summary>
public class WeakAcidIonizationSolverTests
{
    private static LlmGatewayService NewGateway() =>
        (LlmGatewayService)RuntimeHelpers.GetUninitializedObject(typeof(LlmGatewayService));

    private static async Task<EngineResponse> Solve(string text)
    {
        var tt = LlmGatewayService.ClassifyTaskDeterministic(text);
        return await NewGateway().CallEngineAsync(new EngineRequest { RawText = text, TaskType = tt }, null);
    }

    private static string LoadPdf278Sub5()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "pdf_questions.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var x in doc.RootElement.EnumerateArray())
            if (x.GetProperty("id").GetString() == "pdf-278")
            {
                var q = x.GetProperty("question").GetString() ?? "";
                foreach (var (idx, sub) in MultiPartUtility.SplitSubQuestions(q))
                    if (idx == 5) return sub;
            }
        throw new InvalidOperationException("pdf-278 子问(5) 未找到");
    }

    // ---- TR-3.1 / TR-3.3：pdf-278 子问(5) 浓度 + 选择 BD ----

    [Fact]
    public async Task Pdf278_子问5_浓度10负5减10负9且选择BD()
    {
        var sub5 = LoadPdf278Sub5();

        var resp = await Solve(sub5);

        Assert.Equal(true, resp.Ok);
        Assert.Contains("10^-5 - 10^-9", resp.FinalResult);
        Assert.Contains("BD", resp.FinalResult);
        // 浓度表达式与字母连续相邻（仅空格）→ 命中答案键归一化子串 10^-5-10^-9bd
        Assert.Contains("10^-5 - 10^-9 BD", resp.FinalResult);
    }

    // ---- TR-3.5：变体 1 —— pH=4 稀 HF ----

    [Fact]
    public async Task 变体_pH4稀HF_浓度10负4减10负10且BD()
    {
        var text = "25 ℃，在 pH=4 的稀 HF 溶液中，c(F⁻)__________ mol·L⁻¹(写出精确值的表示式)。" +
                   "下列方法中，可以使 0.10 mol·L⁻¹ HF 的电离程度增大的是______(填字母)。 " +
                   "A.加入少量稀盐酸 B.加热该 HF 溶液 C.加入少量冰醋酸 D.加水稀释至 0.010 mol·L⁻¹";

        var resp = await Solve(text);

        Assert.Equal(true, resp.Ok);
        Assert.Contains("10^-4 - 10^-10 BD", resp.FinalResult);
    }

    // ---- TR-3.5：变体 2 —— pH=3 稀 HCN ----

    [Fact]
    public async Task 变体_pH3稀HCN_浓度10负3减10负11且BD()
    {
        var text = "25 ℃，在 pH=3 的稀 HCN 溶液中，c(CN⁻)__________ mol·L⁻¹(写出精确值的表示式)。" +
                   "下列方法中，可以使 HCN 的电离程度增大的是______(填字母)。 " +
                   "A.加入少量盐酸 B.加热该 HCN 溶液 C.加入少量冰醋酸 D.加水稀释至 0.010 mol·L⁻¹";

        var resp = await Solve(text);

        Assert.Equal(true, resp.Ok);
        Assert.Contains("10^-3 - 10^-11 BD", resp.FinalResult);
    }

    // ---- TR-3.2：反例 → 求解器域外 null（不产出浓度表达式）----

    [Fact]
    public async Task 反例_无pH_不产出浓度表达式()
    {
        var text = "稀醋酸溶液中，c(CH3COO⁻)__________ mol·L⁻¹(写出精确值的表示式)。" +
                   "下列方法中，可以使 CH3COOH 的电离程度增大的是______ A.加热 B.加水稀释";

        var resp = await Solve(text);

        Assert.DoesNotContain("10^-", resp.FinalResult ?? "");
    }

    [Fact]
    public async Task 反例_无c离子问法_不产出浓度表达式()
    {
        var text = "25 ℃，在 pH=5 的稀醋酸溶液中，写出精确值的表示式。" +
                   "下列方法中，可以使 CH3COOH 的电离程度增大的是______ A.加热 B.加水稀释";

        var resp = await Solve(text);

        Assert.DoesNotContain("10^-", resp.FinalResult ?? "");
    }

    [Fact]
    public async Task 反例_非弱电解质_不产出浓度表达式()
    {
        var text = "25 ℃，在 pH=5 的稀 NaCl 溶液中，c(Na⁺)__________ mol·L⁻¹(写出精确值的表示式)。" +
                   "下列方法中，可以使 NaCl 的电离程度增大的是______ A.加热 B.加水稀释";

        var resp = await Solve(text);

        Assert.DoesNotContain("10^-", resp.FinalResult ?? "");
    }

    // ---- TR-3.4：不蒙数 —— 无选项 / 选项无法判定时不猜字母 ----

    [Fact]
    public async Task 不蒙数_无选项_不产出浓度加字母结论()
    {
        var text = "25 ℃，在 pH=5 的稀醋酸溶液中，c(CH3COO⁻)__________ mol·L⁻¹(写出精确值的表示式)。";

        var resp = await Solve(text);

        // 无选项 → 求解器整体 null（不输出只有浓度没有字母的半成品），最终不产出浓度表达式
        Assert.DoesNotContain("10^-", resp.FinalResult ?? "");
    }

    // ---- 审查加固 #2：被求离子非共轭碱 / 非整数 pH → 域外 null（不蒙数）----

    [Fact]
    public async Task 反例_被求离子非共轭碱_cNa_不产出浓度表达式()
    {
        var text = "25 ℃，在 pH=5 的稀醋酸溶液中，c(Na⁺)__________ mol·L⁻¹(写出精确值的表示式)。" +
                   "下列方法中，可以使 CH3COOH 的电离程度增大的是______ A.加热 B.加水稀释";

        var resp = await Solve(text);

        // c(Na⁺) 不是 CH3COOH 的共轭碱 → 不套用 c(A⁻)=c(H⁺)-c(OH⁻)，整体 null，不产错表示式
        Assert.DoesNotContain("10^-", resp.FinalResult ?? "");
    }

    [Fact]
    public async Task 反例_非整数pH_不产出取整错表示式()
    {
        var text = "25 ℃，在 pH=4.5 的稀醋酸溶液中，c(CH3COO⁻)__________ mol·L⁻¹(写出精确值的表示式)。" +
                   "下列方法中，可以使 CH3COOH 的电离程度增大的是______ A.加热 B.加水稀释";

        var resp = await Solve(text);

        // 非整数 pH 不取整蒙数 → 整体 null
        Assert.DoesNotContain("10^-", resp.FinalResult ?? "");
    }

    // ---- 审查加固 #3：弱酸加强碱（消耗 H⁺）应判电离程度增大 ----

    [Fact]
    public async Task 正例_弱酸加强碱使电离增大_NaOH与加热均被选中()
    {
        var text = "25 ℃，在 pH=5 的稀醋酸溶液中，c(CH3COO⁻)__________ mol·L⁻¹(写出精确值的表示式)。" +
                   "下列方法中，可以使 0.10 mol·L⁻¹ CH3COOH 的电离程度增大的是______(填字母)。 " +
                   "A.加入少量 NaOH 溶液 B.加入少量稀盐酸 C.加热该 CH3COOH 溶液 D.加入少量冰醋酸";

        var resp = await Solve(text);

        Assert.Equal(true, resp.Ok);
        // A(加 NaOH 消耗 H⁺ → 增大) 与 C(加热 → 增大) 被选中；B(加 H⁺)、D(增大自身浓度) 不选
        Assert.Contains("10^-5 - 10^-9 AC", resp.FinalResult);
    }
}
