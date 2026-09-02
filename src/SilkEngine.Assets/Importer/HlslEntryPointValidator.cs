using System.Text.RegularExpressions;

namespace SilkEngine.Assets.Importer;

/// <summary>
/// HLSL 入口点校验器：扫描源码文本，校验顶点/片段入口函数存在且返回语义正确。
/// 只做源码级静态校验，不解析完整 HLSL 语法；注释中的文本不参与匹配。
/// </summary>
internal static partial class HlslEntryPointValidator
{
    private static readonly (string Entry, string Semantic)[] RequiredEntries =
    [
        ("vert", "SV_Position"),
        ("frag", "SV_Target"),
    ];

    /// <summary>
    /// 校验 HLSL 源码包含 vert/frag 两个入口函数且返回语义匹配（vert→SV_Position、frag→SV_Target）。
    /// </summary>
    /// <param name="source">HLSL 源码原文</param>
    /// <exception cref="InvalidDataException">入口函数缺失或返回语义不匹配</exception>
    public static void Validate(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var code = StripComments(source);
        var matches = FunctionRegex().Matches(code);

        foreach (var (entry, semantic) in RequiredEntries)
        {
            var signature = matches
                .Cast<Match>()
                .FirstOrDefault(m => string.Equals(m.Groups[E].Value, entry, StringComparison.OrdinalIgnoreCase));
            if (signature is null)
                throw new InvalidDataException($"HLSL 入口函数 '{entry}' 缺失（期望 {semantic} 返回语义）");
            if (!string.Equals(signature.Groups[S].Value, semantic, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"HLSL 入口函数 '{entry}' 返回语义应为 {semantic}，实际 {signature.Groups[S].Value}");
            }
        }
    }

    private const string E = "entry";
    private const string S = "semantic";

    /// <summary>匹配 "返回类型 入口名(参数) : 返回语义" 形式的函数签名（参数支持嵌套括号与数组下标）</summary>
    [GeneratedRegex(@"\b[a-zA-Z_][a-zA-Z0-9_]*\s+(?<entry>[a-zA-Z_][a-zA-Z0-9_]*)\s*\((?<params>[^;{}]*?)\)\s*:\s*(?<semantic>[a-zA-Z_][a-zA-Z0-9_]*)")]
    private static partial Regex FunctionRegex();

    /// <summary>移除行注释与块注释（字符串字面量内的 // 与 /* 不动）</summary>
    private static string StripComments(string source)
    {
        var sb = new System.Text.StringBuilder(source.Length);
        var inLine = false;
        var inBlock = false;
        var inString = false;
        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            var next = i + 1 < source.Length ? source[i + 1] : '\0';
            if (inString)
            {
                sb.Append(c);
                if (c == '"')
                    inString = false;
                continue;
            }
            if (inLine)
            {
                if (c == '\n')
                {
                    inLine = false;
                    sb.Append(c);
                }
                continue;
            }
            if (inBlock)
            {
                if (c == '*' && next == '/')
                {
                    inBlock = false;
                    i++;
                }
                continue;
            }
            if (c == '"')
            {
                inString = true;
                sb.Append(c);
            }
            else if (c == '/' && next == '/')
            {
                inLine = true;
                i++;
            }
            else if (c == '/' && next == '*')
            {
                inBlock = true;
                i++;
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}