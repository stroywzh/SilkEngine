using System.Collections.Generic;

namespace SilkEngine.Render;

/// <summary>
/// 前向渲染管线：将渲染批次转换为 SingleDrawCommand，相机矩阵随命令携带，单 Pass 输出
/// </summary>
public sealed class ForwardPipeline : IRenderPipeline
{
    /// <summary>
    /// 构建渲染 Pass：每个含 Shader 与 Mesh 的渲染器生成一条绘制命令；无着色器/网格的渲染器跳过
    /// </summary>
    /// <param name="camera">当前相机视图（View/Projection 矩阵随命令上传）</param>
    /// <param name="batches">渲染批次列表</param>
    /// <returns>按 SortOrder 升序执行的 Pass 列表</returns>
    public IReadOnlyList<RenderPass> Build(ICameraView camera, IReadOnlyList<RenderBatch> batches)
    {
        var commands = new List<DrawCommand>();

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

        return [new RenderPass { SortOrder = 0, Commands = commands }];
    }
}
