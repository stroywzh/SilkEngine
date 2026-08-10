using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjectEngine.Render;

/// <summary>
/// 默认前向渲染管线
/// <br/>按升序 SortOrder 遍历渲染 Pass，逐 Pass 筛选 MeshRenderer 并提交绘制命令
/// </summary>
public class ForwardRenderPipeline : IRenderPipeline
{
    private IRenderBackend? _backend;
    private readonly List<RenderPass> _passes = new();

    /// <inheritdoc />
    public void Initialize(IRenderBackend backend) => _backend = backend;

    /// <inheritdoc />
    public void AddPass(RenderPass pass) => _passes.Add(pass);

    /// <inheritdoc />
    public void Render(IReadOnlyList<DrawCommand> commands)
    {
        if (_backend == null)
            return;

        if (_passes.Count == 0)
        {
            _backend.SubmitCommands(commands);
            _backend.WaitForFrame();
            return;
        }

        foreach (var pass in _passes.OrderBy(p => p.SortOrder))
        {
            var passCommands = new List<DrawCommand>();
            pass.BeforeCommands?.Invoke(_backend);
            foreach (var cmd in commands)
            {
                if (pass.Filter != null && !pass.Filter(cmd))
                    continue;
                passCommands.Add(cmd);
            }
            pass.AfterCommands?.Invoke(_backend);
            _backend.SubmitCommands(passCommands);
        }

        _backend.WaitForFrame();
    }

    /// <inheritdoc />
    public void Dispose() { }
}
