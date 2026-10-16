using SilkEngine.Runtime.Host;

namespace SilkEngine.Runtime;

//这个类可能很长一段时间只是占位，后续要用来做主线程断言和线程登记管理
public static class RuntimeRegistry
{
    private static MainLoop? _mainThread;
    private static readonly List<RuntimeBase> _runtimes = new();

    //暂时可能没什么用
    public static bool Register(RuntimeBase runtime)
    {
        if (runtime is MainLoop m)
        {
            if (_mainThread is not null)
            {
                throw new ArgumentException("不能有两个主线程");
            }
            _mainThread = m;
        }

        _runtimes.Add(runtime);

        return true;
    }

    public static void StopMainThread()
    {
        _mainThread?.RequestStop();
        _mainThread?.Dispose();
        _mainThread = null;
    }
}
