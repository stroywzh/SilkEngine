using System.Text.Json;
using SilkEngine.Math;
using SilkEngine.Render;

namespace SilkEngine.Assets.Importer;

/// <summary>
/// 材质 .asset 导入器：解析 JSON 定义（shader/texture/mesh 引用 + 默认参数），
/// 输出不可变 <see cref="MaterialAsset"/>；依赖以逻辑路径声明，由 Pipeline 在任务 5 解析为句柄。
/// </summary>
public sealed class MaterialImporter : IAssetImporter
{
    private const ulong Revision = 1;

    /// <summary>导入：解析 .asset JSON 为材质载荷；失败时抛含路径的 <see cref="InvalidDataException"/>。</summary>
    /// <param name="source">JSON 源字节（UTF-8 文本）</param>
    /// <param name="context">导入上下文（Path 用于派生资产名与错误定位）</param>
    /// <returns>材质导入结果（依赖 = 声明的 shader/texture/mesh 逻辑路径）</returns>
    /// <exception cref="InvalidDataException">JSON 损坏、schema/type 不匹配或缺少着色器引用（消息含源路径）</exception>
    public AssetImportResult Import(ReadOnlyMemory<byte> source, AssetImportContext context)
    {
        var path = context.Path ?? "<unknown>";
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(source);
        }
        catch (JsonException ex)
        {
            throw Error(path, $"JSON 解析失败：{ex.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw Error(path, "材质定义必须是 JSON 对象");

            if (root.TryGetProperty("schema", out var schema)
                && (!schema.TryGetInt32(out var schemaVersion) || schemaVersion != 1))
                throw Error(path, $"不支持的材质 schema（期望 1，实际 {schema}）");
            if (root.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.String
                && type.GetString() != "material")
                throw Error(path, $"不支持的材质类型 '{type.GetString()}'");

            if (!root.TryGetProperty("shader", out var shaderElement)
                || shaderElement.ValueKind != JsonValueKind.String
                || shaderElement.GetString() is not { Length: > 0 } shaderPath)
                throw Error(path, "材质定义缺少 'shader' 引用");

            var texturePath = ReadOptionalString(root, "texture", path);
            var meshPath = ReadOptionalString(root, "mesh", path);

            var defaults = ReadParameters(root, path);
            var name = context.Path is { Length: > 0 } logical
                ? Path.GetFileNameWithoutExtension(logical)
                : "Material";

            var dependencies = new List<AssetImportDependency>(3)
            {
                new(shaderPath, AssetImporterRegistry.ShaderAssetTypeId),
            };
            if (texturePath is not null)
                dependencies.Add(new AssetImportDependency(texturePath, AssetImporterRegistry.TextureAssetTypeId));
            if (meshPath is not null)
                dependencies.Add(new AssetImportDependency(meshPath, AssetImporterRegistry.MeshAssetTypeId));

            // TODO(task 5): Pipeline 解析依赖路径→句柄后，此处以解析结果构造真实句柄（当前占位 default）
            var material = new MaterialAsset(name, default, default, default, defaults);
            return new AssetImportResult(material, dependencies, Revision);
        }
    }

    private static string? ReadOptionalString(JsonElement root, string property, string path)
    {
        if (!root.TryGetProperty(property, out var element))
            return null;
        if (element.ValueKind != JsonValueKind.String || element.GetString() is not { Length: > 0 } value)
            throw Error(path, $"材质属性 '{property}' 必须是非空字符串");
        return value;
    }

    /// <summary>解析默认参数：number → Float、三元素数组 → Vector3、十六元素数组 → Matrix4x4（行主序）；其余形状抛错</summary>
    private static MaterialParameterSnapshot ReadParameters(JsonElement root, string path)
    {
        if (!root.TryGetProperty("parameters", out var parameters))
            return new MaterialParameterSnapshot([]);
        if (parameters.ValueKind != JsonValueKind.Object)
            throw Error(path, "'parameters' 必须是 JSON 对象");

        var entries = new List<(string Name, MaterialValue Value)>();
        foreach (var property in parameters.EnumerateObject())
        {
            var element = property.Value;
            switch (element.ValueKind)
            {
                case JsonValueKind.Number when element.TryGetSingle(out var f):
                    entries.Add((property.Name, MaterialValue.Float(f)));
                    break;
                case JsonValueKind.Array:
                    entries.Add((property.Name, ReadVectorOrMatrix(element, property.Name, path)));
                    break;
                default:
                    throw Error(path, $"参数 '{property.Name}' 取值形状不支持（期望 number / 3 元素数组 / 16 元素数组）");
            }
        }
        return new MaterialParameterSnapshot(entries);
    }

    private static MaterialValue ReadVectorOrMatrix(JsonElement array, string name, string path)
    {
        var values = new float[16];
        var length = 0;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetSingle(out var value))
                throw Error(path, $"参数 '{name}' 的数组元素必须是数字");
            if (length >= values.Length)
                throw Error(path, $"参数 '{name}' 数组长度不支持（期望 3 或 16）");
            values[length++] = value;
        }
        return length switch
        {
            3 => MaterialValue.Vector3(new Vector3(values[0], values[1], values[2])),
            16 => MaterialValue.Matrix4x4(FromRowMajor(values)),
            _ => throw Error(path, $"参数 '{name}' 数组长度不支持（期望 3 或 16，实际 {length}）"),
        };
    }

    private static Matrix4x4 FromRowMajor(float[] v) => new()
    {
        M11 = v[0], M12 = v[1], M13 = v[2], M14 = v[3],
        M21 = v[4], M22 = v[5], M23 = v[6], M24 = v[7],
        M31 = v[8], M32 = v[9], M33 = v[10], M34 = v[11],
        M41 = v[12], M42 = v[13], M43 = v[14], M44 = v[15],
    };

    private static InvalidDataException Error(string path, string message) => new($"[{path}] {message}");
}