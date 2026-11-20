namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>ion-solution-context.json 事实库根对象。</summary>
public sealed class IonSolutionContextData
{
    public string Version { get; set; } = "";
    public string Source { get; set; } = "";
    public List<IonSolutionContext> Contexts { get; set; } = new();
}

/// <summary>ion-solution-context.json 事实库：把「在 X 溶液中」的 X 映射为该溶液引入的物种与酸碱性。</summary>
public sealed class IonSolutionContext
{
    public string Keyword { get; set; } = "";
    public string[] Species { get; set; } = Array.Empty<string>();
    public string? Condition { get; set; } // acidic / alkaline / alkaline_possible；缺省 neutral
    public bool? FreeProtons { get; set; } // 酸性语境是否含自由 H+（CO2 等弱酸为 false，避免误判弱酸根）
}
