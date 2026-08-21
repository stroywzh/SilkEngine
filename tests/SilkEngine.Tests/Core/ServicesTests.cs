using SilkEngine.Core;

namespace SilkEngine.Tests.Core;

// 本类写全局 Services 注册表（Shutdown 清空全部），须与注册者（资产夹具）串行
[Collection("Assets")]
public class ServicesTests
{
    private sealed class SvcA : IDisposable
    {
        public bool Disposed;
        public void Dispose() => Disposed = true;
    }

    private sealed class Tracker : IDisposable
    {
        private readonly List<string> _order;
        private readonly string _name;
        public Tracker(string name, List<string> order)
        {
            _name = name;
            _order = order;
        }
        public void Dispose() => _order.Add(_name);
    }

    // 反序 Dispose 测试需两个不同类型：同类型注册第二个会按语义抛"重复注册"
    private sealed class TrackerB : IDisposable
    {
        private readonly List<string> _order;
        private readonly string _name;
        public TrackerB(string name, List<string> order)
        {
            _name = name;
            _order = order;
        }
        public void Dispose() => _order.Add(_name);
    }

    [Fact]
    public void Register_Get_ReturnsSameInstance()
    {
        var svc = new SvcA();
        Services.Register(svc);
        try
        {
            Assert.Same(svc, Services.Get<SvcA>());
        }
        finally
        {
            Services.Unregister<SvcA>();
        }
    }

    [Fact]
    public void Get_Unregistered_ThrowsWithTypeName()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Services.Get<SvcA>());
        Assert.Contains(typeof(SvcA).FullName!, ex.Message);
    }

    [Fact]
    public void Register_Duplicate_Throws()
    {
        Services.Register(new SvcA());
        try
        {
            Assert.Throws<InvalidOperationException>(() => Services.Register(new SvcA()));
        }
        finally
        {
            Services.Unregister<SvcA>();
        }
    }

    [Fact]
    public void Shutdown_DisposesInReverseRegistrationOrder()
    {
        var order = new List<string>();
        Services.Register(new Tracker("A", order));
        Services.Register(new TrackerB("B", order));
        Services.Shutdown();
        Assert.Equal(["B", "A"], order);
    }

    [Fact]
    public void Shutdown_IsIdempotent()
    {
        Services.Register(new SvcA());
        Services.Shutdown();
        Services.Shutdown(); // 不抛
    }

    [Fact]
    public void Get_AfterShutdown_Throws()
    {
        Services.Register(new SvcA());
        Services.Shutdown();
        Assert.Throws<InvalidOperationException>(() => Services.Get<SvcA>());
    }

    [Fact]
    public void TryGet_Registered_ReturnsTrue()
    {
        Services.Register(new SvcA());
        try
        {
            Assert.True(Services.TryGet<SvcA>(out var svc));
            Assert.NotNull(svc);
        }
        finally
        {
            Services.Unregister<SvcA>();
        }
    }

    [Fact]
    public void TryGet_Unregistered_ReturnsFalse()
    {
        Assert.False(Services.TryGet<SvcA>(out var svc));
        Assert.Null(svc);
    }

    [Fact]
    public void Unregister_RemovesService()
    {
        Services.Register(new SvcA());
        Services.Unregister<SvcA>();
        Assert.Throws<InvalidOperationException>(() => Services.Get<SvcA>());
    }

    private sealed class TestWriter : ILogWriter
    {
        public List<string> Messages = new();
        public void Write(string msg) => Messages.Add(msg);
    }

    [Fact]
    public void Register_LogSwitchOn_EmitsInfo()
    {
        var tw = new TestWriter();
        var minLevel = Log.MinLevel;
        Log.MinLevel = LogLevel.Debug;
        Log.AddWriter(tw);
        try
        {
            LogConfig.Services = true;
            Services.Register(new SvcA());
            Log.Flush();
            Assert.Contains(tw.Messages, m => m.Contains("[Services]") && m.Contains("Register"));
        }
        finally
        {
            Services.Unregister<SvcA>();
            Log.RemoveWriter(tw);
            LogConfig.Services = true;
            Log.MinLevel = minLevel;
        }
    }
}
