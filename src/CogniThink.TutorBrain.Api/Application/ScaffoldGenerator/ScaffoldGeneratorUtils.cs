namespace CogniThink.TutorBrain.Application.ScaffoldGenerator;

// ============================================================
// Task 2: 生成器共享工具 —— correct 集合化 / 序列化值 / 交互类型名 / 稳定哈希与打乱。
// 纯函数，无副作用，便于 OptionsBuilder 与单测复用、保证可复现。
// ============================================================

public static class ScaffoldGeneratorUtils
{
    /// <summary>把 StepCorrect 转成字符串集合（multi-check/order 取 Set；single-select 取单元素）。</summary>
    public static HashSet<string> CorrectSet(StepCorrect c)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (c.Set is not null)
            foreach (var v in c.Set) set.Add(v);
        else if (!string.IsNullOrWhiteSpace(c.Value))
            set.Add(c.Value);
        return set;
    }

    /// <summary>
    /// 产出写入 StepGroundTruth.Correct 的运行时值：
    /// multi-check / order → 排序后的 List&lt;string&gt;；single-select → string。
    /// 排序保证可复现；multi-check 校验无序，order 校验有序（D 步本轮降级，order 暂未使用）。
    /// </summary>
    public static object? CorrectValue(StepCorrect c) => c.Interaction switch
    {
        AtomicTaskInteraction.SingleSelect => c.Value,
        _ => c.Set?.OrderBy(x => x, StringComparer.Ordinal).ToList(),
    };

    /// <summary>交互类型 → 前端字面量（与 types/scaffold.ts AtomicTaskInteraction 对齐）。</summary>
    public static string InteractionTypeName(AtomicTaskInteraction i) => i switch
    {
        AtomicTaskInteraction.MultiCheck => "multi-check",
        AtomicTaskInteraction.SingleSelect => "single-select",
        AtomicTaskInteraction.Order => "order",
        _ => "multi-check",
    };

    /// <summary>
    /// 跨进程字节级稳定的哈希（不依赖 string.GetHashCode 的进程随机化），用于可复现打乱。
    /// 注意：.NET Core 起 string.GetHashCode 按进程随机化，仅在单进程内稳定；
    /// 批量生成跨进程重跑必须字节一致，故手动 FNV-1a over chars。
    /// </summary>
    public static int StableSeed(AtomicTaskKind kind, IEnumerable<string> values)
    {
        unchecked
        {
            uint hash = 2166136261u;   // FNV-1a offset basis
            foreach (var ch in kind.ToString())
            {
                hash ^= ch;
                hash *= 16777619u;
            }
            // 先排序保证不同输入顺序 → 同种子（与 correct 集合无序一致）
            foreach (var v in values.OrderBy(x => x, StringComparer.Ordinal))
            {
                foreach (var ch in v)
                {
                    hash ^= ch;
                    hash *= 16777619u;
                }
            }
            return (int)hash;
        }
    }

    /// <summary> seeded Fisher–Yates 打乱，同一种子同输入 → 同输出（可复现，便于单测）。</summary>
    public static void Shuffle<T>(IList<T> list, int seed)
    {
        var rng = new Random(seed);
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
