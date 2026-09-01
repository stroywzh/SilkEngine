using System;
using SilkEngine.Host;

namespace SilkEngine.Tests.Host;

/// <summary>
/// EngineHost 生命周期状态机边界：Create 只装配数据（不启动运行时、不访问 Services），
/// Initialize 单次生效（重复调用抛错），Stop/Dispose 幂等。
/// 与 ServicesTests/ThreadRuntimeTests 同集合串行（Initialize 装配真实对象图并自注册、Dispose 触发 Services.Shutdown）。
/// </summary>
[Collection("Assets")]
public class EngineHostTests
{
    [Fact]
    public void Host_BuildDoesNotStartRuntime_InitializeStartsItOnce()
    {
        using var host = EngineHost.Create(b => b.UseHeadlessForTests());

        Assert.False(host.IsInitialized);
        host.Initialize();
        Assert.True(host.IsInitialized);
        Assert.Throws<InvalidOperationException>(() => host.Initialize());
    }

    [Fact]
    public void Host_StopAndDisposeAreIdempotent()
    {
        using var host = EngineHost.Create(b => b.UseHeadlessForTests());

        host.Initialize();
        host.Stop();
        host.Stop();
        host.Dispose();
        host.Dispose();

        Assert.True(host.IsDisposed);
    }

    [Fact]
    public void Host_InitializeAfterDispose_Throws()
    {
        var host = EngineHost.Create(b => b.UseHeadlessForTests());
        host.Dispose();

        Assert.Throws<InvalidOperationException>(() => host.Initialize());
    }

    [Fact]
    public void Host_NotInitialized_StopIsSafe()
    {
        using var host = EngineHost.Create();

        host.Stop();

        Assert.False(host.IsInitialized);
        Assert.False(host.IsDisposed);
    }

    [Fact]
    public void Host_DefaultOptions_AreDeterministic()
    {
        using var host = EngineHost.Create();

        Assert.Equal("Assets", host.Options.AssetRoot);
        Assert.Equal("Library", host.Options.LibraryRoot);
        Assert.False(host.Options.Headless);
        Assert.Equal(GraphicsBackend.OpenGL, host.Options.GraphicsBackend);
    }

    [Fact]
    public void Host_UseLibraryRoot_StoresConfiguration()
    {
        using var host = EngineHost.Create(b => b.UseLibraryRoot("AssetLibrary"));

        Assert.Equal("AssetLibrary", host.Options.LibraryRoot);
    }

    [Fact]
    public void Host_BlankLibraryRoot_InitializeThrows()
    {
        using var host = EngineHost.Create(b =>
        {
            b.UseHeadlessForTests();
            b.UseLibraryRoot(" ");
        });

        Assert.Throws<ArgumentException>(host.Initialize);
    }
}