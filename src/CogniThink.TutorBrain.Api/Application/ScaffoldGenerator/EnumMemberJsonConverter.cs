using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CogniThink.TutorBrain.Application.ScaffoldGenerator;

// ============================================================
// 把枚举序列化为其成员上标注的字符串（优先 [EnumMember(Value=...)]，其次 [JsonPropertyName]，回退成员名）。
// 背景：STJ 内置 JsonStringEnumConverter 在 net8.0 下实测不识别 [EnumMember] / [JsonPropertyName]，
// 只输出枚举成员原名（如 "RedoxReasoning"），与前端 ScaffoldModuleKind 联合字面量（"redox-reasoning"）
// 不一致 → 前端 MODULE_CHAINS 查不到链而崩溃。故自定义转换器，逐字对齐前端契约。
// 标注集中在 ScaffoldGeneratorContracts.cs 的枚举成员上，是唯一真源；本转换器只负责读取，不硬编码映射。
// ============================================================

public sealed class EnumMemberJsonConverter<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString();
        if (s is null) return default;
        foreach (var field in typeToConvert.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is T v && NameOf(field) == s) return v;
        }
        if (Enum.TryParse<T>(s, ignoreCase: true, out var e)) return e;
        // 未知字符串不静默回退 default(T)——ScaffoldModuleKind 默认值 0 恰好是真实业务值 RedoxReasoning，
        // 静默映射会掩盖数据损坏。显式抛错使损坏可见（仅反序列化路径；写产物走 Write，不受影响）。
        throw new JsonException($"未知枚举值 '{s}'，类型 {typeof(T).Name}");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        var field = typeof(T).GetField(value.ToString(), BindingFlags.Public | BindingFlags.Static);
        writer.WriteStringValue(field is null ? value.ToString() : NameOf(field));
    }

    private static string NameOf(FieldInfo field)
    {
        var em = field.GetCustomAttribute<EnumMemberAttribute>();
        if (em?.Value is { Length: > 0 } v) return v;
        var jpn = field.GetCustomAttribute<JsonPropertyNameAttribute>();
        if (jpn?.Name is { Length: > 0 } n) return n;
        return field.Name;
    }
}
