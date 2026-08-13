using SilkEngine.Core;

namespace SilkEngine.Tests.Core;

public class LogConfigTests
{
    [Fact]
    public void Defaults_StateSwitches_On()
    {
        Assert.True(LogConfig.EngineLoop);
        Assert.True(LogConfig.Render);
        Assert.True(LogConfig.Scene);
        Assert.True(LogConfig.Assets);
        Assert.True(LogConfig.Services);
    }

    [Fact]
    public void Default_Lifecycle_Off()
    {
        Assert.False(LogConfig.Lifecycle);
    }

    [Fact]
    public void Switches_AreSettable()
    {
        LogConfig.EngineLoop = false;
        try
        {
            Assert.False(LogConfig.EngineLoop);
        }
        finally
        {
            LogConfig.EngineLoop = true;
        }
    }
}
