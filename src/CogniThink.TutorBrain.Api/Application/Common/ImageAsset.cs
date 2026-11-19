namespace CogniThink.TutorBrain.Application.Common;

using System.Text.Json;

/// <summary>
/// 题目配图的结构化资产：由扫描图重绘/结构化而来，供规则引擎精确消费。
/// 放在 Application.Common 使网关层与评估层均可引用。
/// </summary>
public sealed class ImageAsset
{
    /// <summary>资产类型：curve / graph / molecule / crystal / apparatus 等</summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>原始扫描位图相对路径（相对 data/ 目录）</summary>
    public string Source { get; set; } = string.Empty;
    /// <summary>重绘矢量 SVG 相对路径（相对 data/ 目录），无则空</summary>
    public string Svg { get; set; } = string.Empty;
    /// <summary>结构化数据载荷（结构随 Type 不同）；引擎按 Type 解析</summary>
    public JsonElement? Payload { get; set; }
}
