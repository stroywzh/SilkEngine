using System.Security.Cryptography;
using System.Text;

namespace SilkEngine.Assets.Importer;

/// <summary>
/// 导入设置（签名扩展点）：影响导入输出的设置全部进入 <see cref="ComputeFingerprint"/>，
/// 同资产不同设置产出不同构建键；字段按固定顺序确定性序列化（不使用反射）。
/// </summary>
public sealed class ImportSettings
{
    /// <summary>资产源路径（导入器据此派生资产名）</summary>
    public string? Path { get; init; }

    /// <summary>着色模型 profile（如 "sm_6_0"；影响着色器编译输出）</summary>
    public string ShadingProfile { get; init; } = "sm_6_0";

    /// <summary>预处理器定义（编译期宏，影响输出；空串表示无）</summary>
    public string Defines { get; init; } = string.Empty;

    /// <summary>颜色空间（"srgb" 或 "linear"；影响纹理采样/线性化输出）</summary>
    public string ColorSpace { get; init; } = "srgb";

    /// <summary>
    /// 计算确定性导入设置指纹：全部字段按固定顺序拼接为 UTF-8 文本后取 SHA-256（小写十六进制）。
    /// 相同字段组合恒产生相同指纹；任一影响输出的字段变化即换指纹。
    /// </summary>
    /// <returns>64 位小写十六进制 SHA-256 指纹</returns>
    internal string ComputeFingerprint()
    {
        var material = string.Concat(
            "Path=", Path ?? string.Empty, "\n",
            "Profile=", ShadingProfile ?? string.Empty, "\n",
            "Defines=", Defines ?? string.Empty, "\n",
            "ColorSpace=", ColorSpace ?? string.Empty);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }
}