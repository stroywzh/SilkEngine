using SilkEngine.Scene;

namespace SilkEngine.Tests.Scene;
using Scene = SilkEngine.Scene.Scene;

public class SceneTests
{
    [Fact] public void Constructor_SetsName() => Assert.Equal("Test", new Scene("Test").Name);
    [Fact] public void AddRootObject() { var s = new Scene("S"); var go = new GameObject(); s.AddRootObject(go); Assert.Single(s.GetRootGameObjects()); }
}
