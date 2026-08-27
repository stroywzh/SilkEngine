using SilkEngine.Threading;
using Xunit;

namespace SilkEngine.Tests.Threading;

public class ThreadDomainTests
{
    [Fact]
    public void ThreadDomainException_ContainsOperationAndBothDomains()
    {
        var ex = new ThreadDomainException("AssetManager.ApplyResult", ThreadDomain.Main, ThreadDomain.Worker);

        Assert.Equal("AssetManager.ApplyResult", ex.Operation);
        Assert.Equal(ThreadDomain.Main, ex.Expected);
        Assert.Equal(ThreadDomain.Worker, ex.Actual);
        Assert.Contains("AssetManager.ApplyResult", ex.Message);
        Assert.Contains("Main", ex.Message);
        Assert.Contains("Worker", ex.Message);
    }
}
