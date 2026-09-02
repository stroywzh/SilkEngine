using System.Globalization;

namespace SilkEngine.Assets.Importer;

/// <summary>
/// OBJ 网格导入器：导入 v/vt/vn/f 子集（三角形面），输出 position(3)+normal(3)+uv(2) 布局的 <see cref="MeshAsset"/>。
/// 负索引按 OBJ 规则换算（1-based，-i = 倒数第 i 个）；注释与未支持语句顺滑跳过。
/// </summary>
public sealed class ObjMeshImporter : IAssetImporter
{
    private const ulong Revision = 1;

    /// <summary>导入：解析 OBJ 文本为不可变网格载荷；失败时抛含路径的 <see cref="InvalidDataException"/>。</summary>
    /// <param name="source">OBJ 源文件字节（UTF-8 文本）</param>
    /// <param name="context">导入上下文（Path 用于派生资产名与错误定位）</param>
    /// <returns>网格导入结果</returns>
    /// <exception cref="InvalidDataException">索引越界、非三角面或数据损坏（消息含源路径）</exception>
    public AssetImportResult Import(ReadOnlyMemory<byte> source, AssetImportContext context)
    {
        var path = context.Path ?? "<unknown>";
        var text = System.Text.Encoding.UTF8.GetString(source.Span);
        var vertices = new List<float[]>();
        var texCoords = new List<float[]>();
        var normals = new List<float[]>();
        var meshVertices = new List<float>(64);
        var indices = new List<int>(16);
        var nextIndex = 0;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            switch (parts[0])
            {
                case "v":
                    vertices.Add(ParseNumbers(parts, 3, path, "v"));
                    break;
                case "vt":
                    texCoords.Add(ParseNumbers(parts, 2, path, "vt"));
                    break;
                case "vn":
                    normals.Add(ParseNumbers(parts, 3, path, "vn"));
                    break;
                case "f":
                    if (parts.Length - 1 != 3)
                        throw Error(path, $"面必须为三角形（3 个顶点索引），实际 {parts.Length - 1} 个");
                    foreach (var token in parts.Skip(1))
                    {
                        var fields = token.Split('/');
                        var positionIndex = ParseIndex(fields[0], vertices.Count, path, fields[0]);
                        var uvIndex = fields.Length >= 2 && fields[1].Length > 0
                            ? ParseIndex(fields[1], texCoords.Count, path, fields[1])
                            : throw Error(path, $"面顶点 '{token}' 缺少 UV 索引（布局要求 position+normal+uv）");
                        var normalIndex = fields.Length >= 3 && fields[2].Length > 0
                            ? ParseIndex(fields[2], normals.Count, path, fields[2])
                            : throw Error(path, $"面顶点 '{token}' 缺少法线索引（布局要求 position+normal+uv）");
                        var position = vertices[positionIndex];
                        var uv = texCoords[uvIndex];
                        var normal = normals[normalIndex];
                        meshVertices.Add(position[0]);
                        meshVertices.Add(position[1]);
                        meshVertices.Add(position[2]);
                        meshVertices.Add(normal[0]);
                        meshVertices.Add(normal[1]);
                        meshVertices.Add(normal[2]);
                        meshVertices.Add(uv[0]);
                        meshVertices.Add(uv[1]);
                        indices.Add(nextIndex++);
                    }
                    break;
                default:
                    // 未支持语句（o/s/g/usemtl/mtllib 等）：跳过，不做展开
                    break;
            }
        }

        var name = context.Path is { Length: > 0 } logical
            ? Path.GetFileNameWithoutExtension(logical)
            : "Mesh";
        var mesh = new MeshAsset(name, [.. meshVertices], [3, 3, 2], [.. indices]);
        return new AssetImportResult(mesh, [], Revision);
    }

    /// <summary>解析数值字段；字段不足或非数字抛含路径的 <see cref="InvalidDataException"/></summary>
    private static float[] ParseNumbers(string[] parts, int required, string path, string keyword)
    {
        if (parts.Length - 1 < required)
            throw Error(path, $"'{keyword}' 语句至少需要 {required} 个数值分量");
        try
        {
            var values = new float[parts.Length - 1];
            for (var i = 1; i < parts.Length; i++)
                values[i - 1] = float.Parse(parts[i], CultureInfo.InvariantCulture);
            return values;
        }
        catch (FormatException ex)
        {
            throw Error(path, $"'{keyword}' 语句包含非法数值：{ex.Message}");
        }
    }

    /// <summary>解析 OBJ 索引：正整数按 1-based 换算，负整数按倒数换算；越界抛含路径的 <see cref="InvalidDataException"/></summary>
    private static int ParseIndex(string token, int count, string path, string raw)
    {
        if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            throw Error(path, $"非法索引 '{raw}'");
        var index = value > 0 ? value - 1 : count + value;
        if (index < 0 || index >= count)
            throw Error(path, $"索引 {raw} 越界（当前 {count} 项）");
        return index;
    }

    private static InvalidDataException Error(string path, string message) => new($"[{path}] {message}");
}