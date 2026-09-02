using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SilkEngine.Rendering.Backend;

namespace SilkEngine.Rendering.OpenGL;

/// <summary>
/// DXC（DirectX Shader Compiler）HLSL → SPIR-V 编译器：经外部 dxc 可执行文件把单源码按
/// 顶点/片元两个入口各编译一次，产出两阶段 SPIR-V 包。绝不把 HLSL 改写为 GLSL——编译目标
/// 直接是 SPIR-V（GL 4.6 由 <see cref="OpenGLShaderCompiler"/> 经 glShaderBinary/glSpecializeShader 加载）。
/// 找不到 DXC 时返回 <see cref="ShaderCompileState.Unsupported"/>（消息携带原因与请求上下文）。
/// </summary>
public sealed class DxcHlslCompiler : IShaderCompiler
{
    private const string DxcExecutableName = "dxc.exe";

    // DXC 输出目标环境：本任务验证 SPIR-V magic 头与入口存在性；实际 GL 加载语义由任务 12 的窗口上下文测试覆盖。
    private const string TargetEnvironment = "vulkan1.1";

    /// <summary>SPIR-V 包布局：4 字节小端顶点字节数 + 顶点模块 + 片元模块。</summary>
    private const int PackedHeaderBytes = 4;

    private readonly string? _configuredDxcPath;

    /// <summary>创建 DXC 编译器。</summary>
    /// <param name="dxcPath">dxc.exe 路径或所在目录；为空或相对不可解析时按 PATH 探测（见 <see cref="ResolveDxcPath"/>）</param>
    public DxcHlslCompiler(string? dxcPath = null) => _configuredDxcPath = dxcPath;

    /// <inheritdoc />
    public async ValueTask<ShaderCompileResult> CompileAsync(
        ShaderCompileRequest request,
        CancellationToken cancellationToken)
    {
        var context = CompileContext(request);
        var dxc = ResolveDxcPath(_configuredDxcPath);
        if (dxc is null)
        {
            var reason = string.IsNullOrWhiteSpace(_configuredDxcPath)
                ? $"PATH 中未找到 {DxcExecutableName}"
                : $"配置路径未找到可执行文件: {_configuredDxcPath}";
            return Unsupported(request, $"{context}：{reason}");
        }

        var vertexProfile = MapStageProfile(request.Profile, isVertex: true);
        var fragmentProfile = MapStageProfile(request.Profile, isVertex: false);
        if (vertexProfile is null || fragmentProfile is null)
            return Unsupported(
                request,
                $"{context}：无法从 profile '{request.Profile}' 推导顶点/片元阶段 profile（期望形如 sm_6_0）");

        var vertex = await RunDxcStageAsync(
            dxc, request, context, vertexProfile, request.VertexEntryPoint, cancellationToken);
        if (vertex.Error is not null)
            return new ShaderCompileResult(ShaderCompileState.Failed, null, vertex.Error);

        var fragment = await RunDxcStageAsync(
            dxc, request, context, fragmentProfile, request.FragmentEntryPoint, cancellationToken);
        if (fragment.Error is not null)
            return new ShaderCompileResult(ShaderCompileState.Failed, null, fragment.Error);

        return new ShaderCompileResult(ShaderCompileState.Succeeded, PackStages(vertex.Bytes!, fragment.Bytes!), null);
    }

    /// <summary>
    /// 解析 DXC 可执行文件：优先使用配置路径（文件或所在目录），否则按 PATH 探测。
    /// 找不到返回 null（调用方按明确 <see cref="ShaderCompileState.Unsupported"/> 语义处理）。
    /// </summary>
    /// <param name="configuredPath">配置路径（可为 null）</param>
    /// <returns>dxc.exe 绝对路径；未找到为 null</returns>
    public static string? ResolveDxcPath(string? configuredPath = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (File.Exists(configuredPath))
                return Path.GetFullPath(configuredPath);
            if (Directory.Exists(configuredPath))
            {
                var candidate = Path.Combine(Path.GetFullPath(configuredPath), DxcExecutableName);
                if (File.Exists(candidate))
                    return candidate;
            }
            return null;
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var entry in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(entry.Trim(), DxcExecutableName);
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);
            }
            catch (Exception)
            {
                // 忽略无效 PATH 段
            }
        }
        return null;
    }

    /// <summary>异步探测 DXC 可执行文件（门控测试/装配用；等价于 <see cref="ResolveDxcPath"/>）。</summary>
    /// <param name="configuredPath">配置路径（可为 null）</param>
    /// <returns>dxc.exe 绝对路径；未找到为 null</returns>
    public static ValueTask<string?> TryResolveAsync(string? configuredPath = null)
        => new(ResolveDxcPath(configuredPath));

    /// <summary>把顶点/片元两阶段 SPIR-V 打包为单字节流（4 字节小端 VS 字节数前缀）。</summary>
    internal static byte[] PackStages(byte[] vertex, byte[] fragment)
    {
        var result = new byte[PackedHeaderBytes + vertex.Length + fragment.Length];
        result[0] = (byte)vertex.Length;
        result[1] = (byte)(vertex.Length >> 8);
        result[2] = (byte)(vertex.Length >> 16);
        result[3] = (byte)(vertex.Length >> 24);
        vertex.CopyTo(result, PackedHeaderBytes);
        fragment.CopyTo(result, PackedHeaderBytes + vertex.Length);
        return result;
    }

    /// <summary>按 OpenGL 后端约定拆包两阶段 SPIR-V；布局非法抛 <see cref="InvalidDataException"/>。</summary>
    internal static (byte[] Vertex, byte[] Fragment) UnpackStages(IReadOnlyList<byte> packed)
    {
        if (packed.Count < PackedHeaderBytes)
            throw new InvalidDataException($"SPIR-V 包缺少阶段头部（长度 {packed.Count} < {PackedHeaderBytes}）");
        int vertexLength =
            packed[0]
            | (packed[1] << 8)
            | (packed[2] << 16)
            | (packed[3] << 24);
        if (vertexLength < 0 || PackedHeaderBytes + vertexLength > packed.Count)
            throw new InvalidDataException($"SPIR-V 包顶点段长度非法: {vertexLength}（总长 {packed.Count}）");
        int fragmentLength = packed.Count - PackedHeaderBytes - vertexLength;
        var vertex = new byte[vertexLength];
        var fragment = new byte[fragmentLength];
        for (int i = 0; i < vertexLength; i++)
            vertex[i] = packed[PackedHeaderBytes + i];
        for (int i = 0; i < fragmentLength; i++)
            fragment[i] = packed[PackedHeaderBytes + vertexLength + i];
        return (vertex, fragment);
    }

    /// <summary>编译请求上下文前缀（失败消息统一携带 path/入口/profile/backend）。</summary>
    private static string CompileContext(ShaderCompileRequest request) =>
        $"[{request.Backend}] DXC 编译失败 '{request.SourcePath}'（vert='{request.VertexEntryPoint}', frag='{request.FragmentEntryPoint}', profile='{request.Profile}'）";

    private static ShaderCompileResult Unsupported(ShaderCompileRequest request, string message)
        => new(ShaderCompileState.Unsupported, null, new ShaderCompileError(message, request.SourcePath));

    /// <summary>把着色模型映射为 DXC 阶段 profile；无法识别返回 null。</summary>
    private static string? MapStageProfile(string profile, bool isVertex)
    {
        const string prefix = "sm_";
        if (string.IsNullOrWhiteSpace(profile)
            || !profile.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;
        var model = profile[prefix.Length..]; // 期望 "6_0"（或 "6_0_patch" 取主次版本）
        var parts = model.Split('_');
        if (parts.Length == 0 || !int.TryParse(parts[0], out _))
            return null;
        string minor = parts.Length > 1 && int.TryParse(parts[1], out _) ? parts[1] : "0";
        return $"{(isVertex ? "vs" : "ps")}_{parts[0]}_{minor}";
    }

    /// <summary>执行单阶段 DXC 编译：源码经 stdin 传入，SPIR-V 二进制经 stdout 回收。</summary>
    private static async ValueTask<StageOutput> RunDxcStageAsync(
        string dxc,
        ShaderCompileRequest request,
        string context,
        string stageProfile,
        string entryPoint,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "-T", stageProfile,
            "-E", entryPoint,
            "-spirv",
            $"-fspv-target-env={TargetEnvironment}",
        };
        foreach (var define in request.Defines)
        {
            arguments.Add("-D");
            arguments.Add(define);
        }
        arguments.Add("-Fo");
        arguments.Add("-");
        arguments.Add("-"); // "-" = 源码经 stdin

        var psi = new ProcessStartInfo
        {
            FileName = dxc,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };
        try
        {
            process.Start();
            await process.StandardInput.WriteAsync(request.HlslSource.AsMemory(), cancellationToken);
            process.StandardInput.Close();

            var output = new MemoryStream();
            await process.StandardOutput.BaseStream.CopyToAsync(output, cancellationToken);
            var errorText = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                return new StageOutput(
                    null,
                    new ShaderCompileError(
                        $"{context} [stage={stageProfile} '{entryPoint}'] 退出码 {process.ExitCode}: {Trim(errorText)}",
                        request.SourcePath));
            }
            return new StageOutput(output.ToArray(), null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new StageOutput(
                null,
                new ShaderCompileError(
                    $"{context} [stage={stageProfile} '{entryPoint}'] 启动/执行失败: {ex.Message}",
                    request.SourcePath));
        }
    }

    private static string Trim(string? text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        return trimmed.Length > 0 ? trimmed : "（无诊断输出）";
    }

    /// <summary>单阶段编译输出（内部）。</summary>
    private sealed record StageOutput(byte[]? Bytes, ShaderCompileError? Error);
}