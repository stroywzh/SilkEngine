using System.Collections.Generic;
using SilkEngine.Scene;

namespace SilkEngine.Render;

public interface IRenderPipeline
{
    IReadOnlyList<RenderPass> Build(Camera camera, IReadOnlyList<RenderBatch> batches);
}
