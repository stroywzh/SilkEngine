# README
## src/SandBox介绍
- 业务黑盒模型，在SandBox.csproj内，使用者只知道业务API，公共API，公共结构体等内容，完全不需要理解API背后是如何实现。

## 特殊说明
- SandBox现在需要主动先设置Application的RenderBackend和RenderPipeline，然后才能StartMainLoop，受限于产能，Application.cs很长一段时间内不会写任何构造/工厂等方法。

## 运行与控制台
- 运行：`dotnet run --project src/SandBox/SandBox.csproj -- --frames 180`；`--frames N` 跑满 N 帧后退出，缺省则不限期运行。
- 启动时由 `ConsoleSetup` 准备控制台：手动运行时窗口标题、UTF-8、并在 Windows 上关闭 QuickEdit
  （选中文本不再暂停控制台输出）；进程没有控制台时（WinExe/发布方式变化）才 `AllocConsole`，
  已有控制台不会重复分配；所有 P/Invoke 失败都静默降级，不影响启动。
- 启动横幅打印 exe、cwd、args、解析到的资源路径、控制台状态与日志文件路径。
- 日志始终写入 `<输出目录>/Log/Log<yyyy-MM-dd>.log`（`Log.EnableFileLog()`）；引擎日志只进文件，
  控制台保留横幅与启动/加载/停止等里程碑行，控制台不可见时查日志文件即可。
