using System.Collections.Concurrent;
using SilkEngine.Core;

namespace SilkEngine.Tests.Core;

[CollectionDefinition("Log")]
public class LogTestsCollection { }

[Collection("Log")]
public class LogTests
{
    private class TestWriter : ILogWriter
    {
        public ConcurrentQueue<string> Messages = new();
        public void Write(string msg) => Messages.Enqueue(msg);
    }

    [Fact]
    public void Debug_Filtered_WhenMinLevelInfo()
    {
        Log.MinLevel = LogLevel.Info;
        var tw = new TestWriter();
        Log.AddWriter(tw);
        Log.Debug("should not appear");
        Log.Flush();
        Log.RemoveWriter(tw);
        Assert.DoesNotContain(tw.Messages, m => m.Contains("should not appear"));
        Log.MinLevel = LogLevel.Debug;
    }

    [Fact]
    public void Info_Passes_WhenMinLevelDebug()
    {
        Log.MinLevel = LogLevel.Debug;
        var tw = new TestWriter();
        Log.AddWriter(tw);
        Log.Info("hello");
        Log.Flush();
        Log.RemoveWriter(tw);
        Assert.Contains(tw.Messages, m => m.Contains("hello"));
    }

    [Fact]
    public void Format_HasTimestamp()
    {
        var tw = new TestWriter();
        Log.AddWriter(tw);
        Log.Info("test");
        Log.Flush();
        Log.RemoveWriter(tw);
        var msg = tw.Messages.First(m => m.Contains("test"));
        Assert.Matches(@"\[\d{2}:\d{2}:\d{2}\.\d{3}\]", msg);
    }

    [Fact]
    public void Format_HasThreadInfo_WhenEnabled()
    {
        Log.ShowThreadInfo = true;
        Thread.CurrentThread.Name = "TestThread";
        var tw = new TestWriter();
        Log.AddWriter(tw);
        Log.Info("x");
        Log.Flush();
        Log.RemoveWriter(tw);
        Log.ShowThreadInfo = false;
        Thread.CurrentThread.Name = null;
        Assert.Contains("[TestThread]", tw.Messages.First(m => m.Contains("[TestThread]")));
    }

    [Fact]
    public void Format_NoThreadInfo_WhenDisabled()
    {
        Log.ShowThreadInfo = false;
        Thread.CurrentThread.Name = "X";
        var tw = new TestWriter();
        Log.AddWriter(tw);
        Log.Info("x");
        Log.Flush();
        Log.RemoveWriter(tw);
        Assert.DoesNotContain("[X]", tw.Messages.First(m => m.EndsWith("x")));
    }

    [Fact]
    public void ObjectParameter_CallsToString()
    {
        var tw = new TestWriter();
        Log.AddWriter(tw);
        Log.Info(42);
        Log.Flush();
        Log.RemoveWriter(tw);
        Assert.Contains("42", tw.Messages.First(m => m.Contains("42")));
    }

    [Fact]
    public void NullParameter_OutputsNull()
    {
        var tw = new TestWriter();
        Log.AddWriter(tw);
        Log.Info(null!);
        Log.Flush();
        Log.RemoveWriter(tw);
        Assert.Contains("null", tw.Messages.First(m => m.Contains("null")));
    }

    [Fact]
    public void StackTree_ContainsCallerFrame()
    {
        Log.MinLevel = LogLevel.Debug;
        var tw = new TestWriter();
        Log.AddWriter(tw);
        Log.StackTree("check");
        Log.Flush();
        Log.RemoveWriter(tw);
        var msg = tw.Messages.First(m => m.Contains("check"));
        Assert.Contains("check", msg);
        Assert.True(msg.Contains('\n'), "Should contain stack trace lines after the message");
    }

    [Fact]
    public void Error_OutputsAtAllLevels()
    {
        Log.MinLevel = LogLevel.Error;
        var tw = new TestWriter();
        Log.AddWriter(tw);
        Log.Error("critical");
        Log.Info("skipped");
        Log.Flush();
        Log.RemoveWriter(tw);
        Assert.Contains(tw.Messages, m => m.Contains("critical"));
        Assert.DoesNotContain(tw.Messages, m => m.Contains("skipped"));
        Log.MinLevel = LogLevel.Debug;
    }

    [Fact]
    public void ThreadSafe_NoInterleave()
    {
        var tw = new TestWriter();
        Log.AddWriter(tw);
        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            int n = i;
            tasks.Add(Task.Run(() => Log.Info($"msg{n}")));
        }
        Task.WaitAll(tasks.ToArray());
        Log.Flush();
        Log.RemoveWriter(tw);
        foreach (var m in tw.Messages)
            Assert.StartsWith("[", m);
    }

    [Fact]
    public void ConcurrentWrites_AfterFlush_AllVisible()
    {
        var tw = new TestWriter();
        Log.AddWriter(tw);
        try
        {
            Parallel.For(0, 100, i => Log.Info($"a5-marker-{i:D4}"));
            Log.Flush();
            Assert.Equal(100, tw.Messages.Count(m => m.Contains("a5-marker-")));
            for (int i = 0; i < 100; i++)
            {
                int n = i;
                Assert.Contains(tw.Messages, m => m.EndsWith($"a5-marker-{n:D4}"));
            }
        }
        finally
        {
            Log.RemoveWriter(tw);
        }
    }
}
