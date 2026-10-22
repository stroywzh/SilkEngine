# TODO List
- 1. GameObject和创建逻辑：ObjectId直接放在GameObject里面，有些东西还需要继续研究一下，这会先留着下面
  - EngineObjectManager负责上层管理：
  >- 1. （实例化）：分配ObjectId，驱动GameObject到Container的注册————或者干脆Container专门做几个静态门面（反正分成两种，Normal/DontDestoryOnLoad），把所有的数据存储到自身这里，Manager只负责。（待定，只能确定数据存储肯定要分开的，ObjectId要怎么分配，通过工厂还是啥都不确定）
  >- 2. （更新）：驱动UpdateDriver的Update/Late/Fixed~（待定，也可能之后就直接内聚更新逻辑到Manager了）
  >- 3. （接线）：Manager自身直接Instance出去还是通过接口（待定）