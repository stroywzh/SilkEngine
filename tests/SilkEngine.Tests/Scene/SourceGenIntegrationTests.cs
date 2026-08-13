using SilkEngine;

namespace SilkEngine.Tests.Scene;

/// <summary>
/// 源生成器集成测试目标组件：顶层（非嵌套）MonoBehaviour。
/// 任务 1 仅验证生成器对其产出空 partial（EmitCompilerGeneratedFiles 检查）；
/// 任务 4 由生成器生成完整 WriteTo/ReadFrom 并在此文件补充 roundtrip 集成测试。
/// </summary>
public partial class SourceGenSmokeProbe : MonoBehaviour
{
    public float Speed = 1f;
}
