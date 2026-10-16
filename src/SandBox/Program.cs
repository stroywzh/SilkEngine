using SilkEngine;
using SilkEngine.Runtime;

Log.Info("HelloWorld!");
ThreadFactory.BuildMainThread();
Log.Info("Build结束");

Thread.Sleep(5000);
RuntimeRegistry.StopMainThread();
Log.Info("MainThreadStoped");
