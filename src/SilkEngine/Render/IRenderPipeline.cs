using System.Collections.Generic;

namespace SilkEngine.Render;

public interface IRenderPipeline
{
    IReadOnlyList<RenderPass> Build(Camera camera, IReadOnlyList<RenderBatch> batches);
}
