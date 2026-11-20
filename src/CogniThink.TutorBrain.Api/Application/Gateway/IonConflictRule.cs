namespace CogniThink.TutorBrain.Application.Gateway;

/// <summary>ion-conflicts.json 事实库根对象。</summary>
public sealed class IonConflictData
{
    public string Version { get; set; } = "";
    public string Source { get; set; } = "";
    public List<IonConflictRule> Rules { get; set; } = new();
}

/// <summary>ion-conflicts.json 事实库：一条离子不共存冲突规则。</summary>
public sealed class IonConflictRule
{
    public string[] Ions { get; set; } = Array.Empty<string>();
    public string? Condition { get; set; } // any / acidic / alkaline；缺省 any
    public string? Type { get; set; }
    public string? Result { get; set; }
}
