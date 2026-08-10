using System.Collections.Concurrent;
using ProjectEngine;

namespace ProjectEngine.Tests.Core;

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
        Log.RemoveWriter(tw);
        Assert.Empty(tw.Messages);
        Log.MinLevel = LogLevel.Debug;
    }

    [Fact]
    public void Info_Passes_WhenMinLevelDebug()
    {
        Log.MinLevel = LogLevel.Debug;
        var tw = new TestWriter();
        Log.AddWriter(tw);
        Log.Info("hello");
        Log.RemoveWriter(tw);
        Assert.Single(tw.Messages);
        Assert.Contains("hello", tw.Messages.First());
    }

    [Fact]
    public void Format_HasTimestamp()
    {
        var tw = new TestWriter();
        Log.AddWriter(tw);
        Log.Info("test");
        Log.RemoveWriter(tw);
        var msg = tw.Messages.First();
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
        Log.RemoveWriter(tw);
        Log.ShowThreadInfo = false;
        Thread.CurrentThread.Name = null;
        Assert.Contains("[TestThread]", tw.Messages.First());
    }

    [Fact]
    public void Format_NoThreadInfo_WhenDisabled()
    {
        Log.ShowThreadInfo = false;
        Thread.CurrentThread.Name = "X";
        var tw = new TestWriter();
        Log.AddWriter(tw);
        Log.Info("x");
        Log.RemoveWriter(tw);
        Assert.DoesNotContain("[X]", tw.Messages.First());
    }

    [Fact]
    public void ObjectParameter_CallsToString()
    {
        var tw = new TestWriter();
        Log.AddWriter(tw);
        Log.Info(42);
        Log.RemoveWriter(tw);
        Assert.Contains("42", tw.Messages.First());
    }

    [Fact]
    public void NullParameter_OutputsNull()
    {
        var tw = new TestWriter();
        Log.AddWriter(tw);
        Log.Info(null!);
        Log.RemoveWriter(tw);
        Assert.Contains("null", tw.Messages.First());
    }

    [Fact]
    public void StackTree_ContainsCallerFrame()
    {
        var tw = new TestWriter();
        Log.AddWriter(tw);
        Log.StackTree("check");
        Log.RemoveWriter(tw);
        var msg = tw.Messages.First();
        Assert.Contains("check", msg);
        Assert.Contains("at ", msg);
    }

    [Fact]
    public void Error_OutputsAtAllLevels()
    {
        Log.MinLevel = LogLevel.Error;
        var tw = new TestWriter();
        Log.AddWriter(tw);
        Log.Error("critical");
        Log.Info("skipped");
        Log.RemoveWriter(tw);
        Assert.Single(tw.Messages);
        Assert.Contains("critical", tw.Messages.First());
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
        Log.RemoveWriter(tw);
        foreach (var m in tw.Messages)
            Assert.StartsWith("[", m);
    }
}
