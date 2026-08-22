namespace SilkEngine.Core;

/// <summary>
/// 引擎状态日志开关（集中配置）：各子系统状态点日志统一受控。
/// 运行时可按需切换；关闭时对应日志点零开销（if 守卫短路）。
/// </summary>
public static class LogConfig
{
    /// <summary>帧循环：启动/退出/暂停/恢复</summary>
    public static bool EngineLoop { get; set; } = true;

    /// <summary>渲染线程：启动/退出/帧提交</summary>
    public static bool Render { get; set; } = true;

    /// <summary>场景：加载/卸载/对象增删/销毁处理</summary>
    public static bool Scene { get; set; } = true;

    /// <summary>资产：加载开始/完成/缓存命中/卸载</summary>
    public static bool Assets { get; set; } = true;

    /// <summary>服务：注册/注销/Shutdown</summary>
    public static bool Services { get; set; } = true;

    /// <summary>组件生命周期：添加/移除/销毁（量大，默认关）</summary>
    public static bool Lifecycle { get; set; } = false;
}
