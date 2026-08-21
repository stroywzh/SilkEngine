using System.Collections.Generic;

namespace SilkEngine.Render;

/// <summary>
/// 前向渲染管线：将渲染批次转换为 SingleDrawCommand，相机矩阵随命令携带，单 Pass 输出。
/// 输出缓冲双缓冲复用（Build 每帧交替，RenderSystem 帧序同步消费——SubmitFrame 阻塞等渲染线程
/// 执行完毕后才进入下一帧 Build；命令实例仍每帧新建）。
/// </summary>
public sealed class ForwardPipeline : IRenderPipeline
{
    private readonly List<DrawCommand> _commandsA = [];
    private readonly List<DrawCommand> _commandsB = [];
    private readonly RenderPass _passA = new() { SortOrder = 0 };
    private readonly RenderPass _passB = new() { SortOrder = 0 };
    private readonly List<RenderPass> _passesA = [];
    private readonly List<RenderPass> _passesB = [];
    private bool _toggled;

    /// <summary>
    /// 构建渲染 Pass：每个含 Shader 与 Mesh 的渲染器生成一条绘制命令；无着色器/网格的渲染器跳过
    /// </summary>
    /// <param name="camera">当前相机视图（View/Projection 矩阵随命令上传）</param>
    /// <param name="batches">渲染批次列表</param>
    /// <returns>按 SortOrder 升序执行的 Pass 列表（双缓冲复用实例，仅帧内消费有效）</returns>
    public IReadOnlyList<RenderPass> Build(ICameraView camera, IReadOnlyList<RenderBatch> batches)
    {
        _toggled = !_toggled;
        var commands = _toggled ? _commandsB : _commandsA;
        var pass = _toggled ? _passB : _passA;
        var passes = _toggled ? _passesB : _passesA;

        commands.Clear();
        passes.Clear();

        foreach (var batch in batches)
        {
            foreach (var r in batch.Renderers)
            {
                if (r.Shader == null || r.Mesh == null)
                    continue;

                commands.Add(new SingleDrawCommand
                {
                    Shader = r.Shader,
                    Mesh = r.Mesh,
                    Material = r.Material,
                    Enabled = r.Enabled,
                    ModelMatrix = r.WorldMatrix,
                    ViewMatrix = camera.ViewMatrix,
                    ProjectionMatrix = camera.ProjectionMatrix,
                });
            }
        }

        pass.Commands = commands;
        passes.Add(pass);
        return passes;
    }
}
