namespace SilkEngine.Threading;

/// <summary>依赖组合（ECS 系统编排预留）：把多个句柄聚合为一个（全部完成才完成）。</summary>
public interface IJobComposer
{
    IJobHandle Combine(params IJobHandle[] dependencies);
}
