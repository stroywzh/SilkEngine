using System.Collections.Generic;
using SilkEngine.Rendering.Abstraction;
using CameraView = SilkEngine.Render.ICameraView;
using RenderPacket = SilkEngine.Rendering.Abstraction.RenderPacket;

namespace SilkEngine.Rendering.Pipeline;

/// <summary>
/// 前向渲染管线：将渲染批次转换为无资产语义的 RenderPacket 列表。
/// 只复制已解析的 Render Handle、材质参数与模型矩阵，不调用 MaterialBinding、不查询资产管理器。
/// 输出缓冲双缓冲复用（Build 每帧交替，RenderSystem 帧序同步消费——SubmitFrame 阻塞等渲染线程
/// 执行完毕后才进入下一帧 Build；RenderPacket 实例仍每帧新建）。
/// 仅由 Host/RenderSystem 内部使用（internal）。
/// </summary>
internal sealed class ForwardPipeline : IRenderPipeline
{
    private readonly List<RenderPacket> _packetsA = [];
    private readonly List<RenderPacket> _packetsB = [];
    private bool _toggled;

    /// <summary>
    /// 构建本帧渲染包列表：每个含已解析 Shader/Mesh 句柄的渲染器生成一个渲染包；
    /// 句柄未解析（default）的渲染器跳过。
    /// </summary>
    /// <param name="camera">当前相机视图（矩阵已由 RenderSystem 预计算）</param>
    /// <param name="batches">渲染批次列表</param>
    /// <returns>无资产语义的渲染包列表（双缓冲复用实例，仅帧内消费有效）</returns>
    public IReadOnlyList<RenderPacket> Build(CameraView camera, IReadOnlyList<RenderBatch> batches)
    {
        _toggled = !_toggled;
        var packets = _toggled ? _packetsB : _packetsA;
        packets.Clear();

        foreach (var batch in batches)
        {
            foreach (var r in batch.Renderers)
            {
                if (r.ShaderHandle == default || r.MeshHandle == default)
                    continue;
                packets.Add(new RenderPacket(
                    r.ShaderHandle,
                    r.MeshHandle,
                    r.TextureHandle,
                    r.MaterialParameters,
                    r.WorldMatrix));
            }
        }
        return packets;
    }
}
