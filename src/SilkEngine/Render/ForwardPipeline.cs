using System.Collections.Generic;
using SilkEngine.Scene;

namespace SilkEngine.Render;

public sealed class ForwardPipeline : IRenderPipeline
{
    public IReadOnlyList<RenderPass> Build(Camera camera, IReadOnlyList<RenderBatch> batches)
    {
        var commands = new List<DrawCommand>();

        foreach (var batch in batches)
        {
            foreach (var mr in batch.Renderers)
            {
                if (mr.Shader == null || mr.Mesh == null)
                    continue;

                commands.Add(new SingleDrawCommand
                {
                    Shader = mr.Shader,
                    Mesh = mr.Mesh,
                    Material = mr.Material,
                    Enabled = mr.Enabled,
                    ModelMatrix = mr.Transform.LocalToWorldMatrix,
                    ViewMatrix = camera.ViewMatrix,
                    ProjectionMatrix = camera.ProjectionMatrix,
                });
            }
        }

        return [new RenderPass { SortOrder = 0, Commands = commands }];
    }
}
