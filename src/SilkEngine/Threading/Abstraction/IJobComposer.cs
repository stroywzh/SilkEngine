namespace SilkEngine.Threading;

/// <summary>依赖组合（ECS 系统编排预留）：把多个句柄聚合为一个（全部完成才完成）。</summary>
public interface IJobComposer
{
    /// <summary>聚合依赖句柄：全部依赖完成才完成。</summary>
    /// <param name="dependencies">依赖句柄数组</param>
    /// <returns>聚合完成句柄</returns>
    IJobHandle Combine(params IJobHandle[] dependencies);
}
