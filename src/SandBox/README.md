# README
## src/SandBox介绍
- 业务黑盒模型，在SandBox.csproj内，使用者只知道业务API，公共API，公共结构体等内容，完全不需要理解API背后是如何实现。

## 特殊说明
- SandBox现在需要主动先设置Application的RenderBackend和RenderPipeline，然后才能StartMainLoop，受限于产能，Application.cs很长一段时间内不会写任何构造/工厂等方法。
