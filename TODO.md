# 已知问题


# -TODO-List-
- [x] 1.EngineLoop每帧结束时获取一个不可变快照（第一帧和初始化时需要特殊处理），然后基于快照进行逻辑/渲染等更新
- [x]2.渲染管线的抽象，现在依旧依赖于Backend自行创建等。后续让PipeLine来负责所有backend内容，backend只负责获取需要渲染的信息，然后渲染即可。
- [x]3.渲染管线收集/执行需要更进一步更新
- 4.多线程等到后续再说。现在先保持MainThread跑EngineLoop（和LogicLoop），带着两个WorkerThread和一个RenderThread跑
- 5.Log是否可以加入stopwatch？
- [ ]6.SceneManager对于MonoBehaviour的更新方式有点让人头疼，太tm狗屎了。
  - [x]部分解决，但是现在的行为实现不完整
  - [ ]现在的实际设计不满意，等待后续再说吧
- 7.（久远的后续）顺序初始化+顺序更新（这个可以参考已有的UpdateContainer和UpdateManager）

# 参考
- 1.可能的物理库：BepuPhysics 2
- 2.可能的Editor界面编写：ImGui.NET
