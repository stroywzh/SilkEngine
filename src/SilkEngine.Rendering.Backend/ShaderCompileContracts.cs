using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SilkEngine.Rendering.Backend;

/// <summary>
/// 着色器编译请求：单 HLSL 源 + 顶点/片元入口 + profile + 编译宏 + 后端标签。
/// 不含任何资产身份；错误语义要求失败消息携带本请求全部上下文。
/// </summary>
/// <param name="SourcePath">源着色器路径/名称（错误定位与日志）</param>
/// <param name="HlslSource">HLSL 源码原文（不可变）</param>
/// <param name="VertexEntryPoint">顶点着色器入口函数名</param>
/// <param name="FragmentEntryPoint">片元着色器入口函数名</param>
/// <param name="Profile">着色模型 profile（如 "sm_6_0"）</param>
/// <param name="Defines">编译宏定义（如 "ENABLE_FOG" 或 "MAX_LIGHTS=4"）</param>
/// <param name="Backend">目标后端标签（如 "opengl"；后端驱动编译目标环境）</param>
public sealed record ShaderCompileRequest(
    string SourcePath,
    string HlslSource,
    string VertexEntryPoint,
    string FragmentEntryPoint,
    string Profile,
    IReadOnlyList<string> Defines,
    string Backend);

/// <summary>着色器编译状态。</summary>
public enum ShaderCompileState
{
    /// <summary>编译成功（SpirV 有效）。</summary>
    Succeeded,

    /// <summary>编译失败（Error 携带详情）。</summary>
    Failed,

    /// <summary>后端/工具链不支持（如未找到 DXC）；与 Failed 区别是可恢复性（补齐工具链后可重试）。</summary>
    Unsupported,
}

/// <summary>
/// 着色器编译结果：SPIR-V 二进制包（成功时非空；OpenGL 后端约定布局见 <c>DxcHlslCompiler</c>）
/// 与错误（失败/不支持时非空）。
/// </summary>
/// <param name="State">编译状态</param>
/// <param name="SpirV">SPIR-V 二进制包（未编译成功为 null）</param>
/// <param name="Error">编译错误（成功为 null）</param>
public sealed record ShaderCompileResult(
    ShaderCompileState State,
    IReadOnlyList<byte>? SpirV,
    ShaderCompileError? Error);

/// <summary>着色器编译错误：Message 须含 source path、入口、profile 与 backend 上下文。</summary>
/// <param name="Message">详细错误消息（含请求上下文）</param>
/// <param name="SourcePath">源着色器路径/名称（可空）</param>
public sealed record ShaderCompileError(string Message, string? SourcePath);

/// <summary>着色器编译器契约（HLSL → SPIR-V；实现不得改写为 GLSL）。</summary>
public interface IShaderCompiler
{
    /// <summary>编译着色器请求为 SPIR-V。</summary>
    /// <param name="request">编译请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>编译结果（成功/失败/不支持语义）</returns>
    ValueTask<ShaderCompileResult> CompileAsync(
        ShaderCompileRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// 着色器编译管线异常（GPU 创建失败路径的阶段性载体）：阶段信息 + 含
/// source path/入口/profile/backend 的错误消息，经结果批次的 Error 关联回 Main 域。
/// </summary>
public sealed class ShaderCompilationException(string stage, string message) : InvalidOperationException(message)
{
    /// <summary>失败所在编译阶段（如 "hlsl-compile"、"gl-specialize"）。</summary>
    public string Stage { get; } = stage;
}