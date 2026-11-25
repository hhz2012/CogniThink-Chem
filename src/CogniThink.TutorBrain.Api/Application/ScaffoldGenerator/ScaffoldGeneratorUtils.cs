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
        // 与 ScaffoldValidator.StoredCorrectSet 统一为忽略大小写（前端判分对选项字母亦忽略大小写）。
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (c.Set is not null)
            foreach (var v in c.Set) set.Add(v);
        else if (!string.IsNullOrWhiteSpace(c.Value))
            set.Add(c.Value);
        return set;
    }

    /// <summary>
    /// 把选择题 answer 归一化为选项字母列表（保序、去重、忽略大小写）。
    /// 支持 "A"、"AC"、"A,C"、"A、C"、"A;C"、"A B C" 等常见存储格式；非字母字符忽略。
    /// 提取端（ExtractAnswer）与校验端（CorrectMatchesAnswer）共用此口径，避免漂移。
    /// </summary>
    public static List<string> ChoiceAnswerLetters(string answer)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(answer)) return list;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var ch in answer.Where(char.IsLetter))
        {
            var s = ch.ToString().ToUpperInvariant();
            if (seen.Add(s)) list.Add(s);
        }
        return list;
    }

    /// <summary>
    /// 产出写入 StepGroundTruth.Correct 的运行时值：
    /// multi-check → 排序后的 List&lt;string&gt;（校验无序，排序保证可复现）；
    /// order → 保留引擎给出的原始顺序（校验有序，禁止按字母重排，否则 D 步正确序被破坏）；
    /// single-select → string。
    /// </summary>
    public static object? CorrectValue(StepCorrect c) => c.Interaction switch
    {
        AtomicTaskInteraction.SingleSelect => c.Value,
        AtomicTaskInteraction.Order => c.Set?.ToList(),
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
