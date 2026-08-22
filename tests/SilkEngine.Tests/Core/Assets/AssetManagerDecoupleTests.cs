using SilkEngine.Core;
using SilkEngine.Assets;
using SilkEngine.Tests.Core.Assets;

namespace SilkEngine.Tests.Core.Assets;

[Collection("Assets")]
public class AssetManagerDecoupleTests
{
    private sealed class RecordingTaskScheduler : ITaskScheduler
    {
        public int Calls { get; private set; }
        public void Submit(Func<CancellationToken, ValueTask> work)
        {
            Calls++;
            work(CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    [Fact]
    public void AssetManager_AcceptsCoreTaskScheduler_AndSchedules()
    {
        var scheduler = new RecordingTaskScheduler();
        var manager = new AssetManager(scheduler);
        Services.Unregister<AssetManager>(); // ctor 自注册 + 本测试实例接管（若已注册则兜底移除）
        try
        {
            var req = manager.LoadAsync<Texture2D>("missing/asset.png");
            Assert.True(scheduler.Calls > 0);
            manager.ProcessCompleted();
            Assert.True(req.IsDone);
        }
        finally
        {
            Services.Unregister<AssetManager>();
        }
    }
}
