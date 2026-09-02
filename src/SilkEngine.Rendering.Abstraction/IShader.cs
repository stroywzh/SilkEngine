namespace SilkEngine.Rendering.Abstraction;

/// <summary>
/// 只读着色器消费契约（Rendering 域定义）：向消费方暴露不可变的着色器定义信息。
/// 本契约不引用任何 Assets 类型，也不携带源码或编译状态。
/// 只读编译状态（编译结果/句柄映射等）由任务 7 的 ShaderCompileContracts 接续定义，本任务不含。
/// </summary>
public interface IShader
{
    /// <summary>着色器名称</summary>
    string Name { get; }

    /// <summary>顶点着色器入口函数名</summary>
    string VertexEntryPoint { get; }

    /// <summary>片段着色器入口函数名</summary>
    string FragmentEntryPoint { get; }

    /// <summary>着色模型配置文件（如 "sm_6_0"）</summary>
    string Profile { get; }
}