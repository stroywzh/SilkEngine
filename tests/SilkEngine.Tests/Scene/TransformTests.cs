using SilkEngine.Scene;
using SilkEngine.Math;

namespace SilkEngine.Tests.Scene;

public class TransformTests
{
    [Fact] public void Defaults() { var t = new Transform(new GameObject()); Assert.Equal(Vector3.Zero, t.LocalPosition); Assert.Equal(Vector3.One, t.LocalScale); Assert.Equal(Quaternion.Identity, t.LocalRotation); }
    [Fact] public void Position_InWorldSpace_IncludesParent() { var p = new Transform(new GameObject()) { LocalPosition = new(5,0,0) }; var c = new Transform(new GameObject()) { LocalPosition = new(3,0,0) }; c.SetParent(p); Assert.Equal(new(8,0,0), c.Position); }
    [Fact] public void SetParent_DetachesOld() { var p1 = new Transform(new GameObject()); var p2 = new Transform(new GameObject()); var c = new Transform(new GameObject()); c.SetParent(p1); c.SetParent(p2); Assert.Same(p2, c.Parent); Assert.DoesNotContain(c, p1.Children); }
    [Fact] public void SetParent_Null_Detaches() { var p = new Transform(new GameObject()); var c = new Transform(new GameObject()); c.SetParent(p); c.SetParent(null); Assert.Null(c.Parent); }
    [Fact] public void Forward_AffectedByRotation() { var t = new Transform(new GameObject()) { LocalRotation = Quaternion.Euler(0,90,0) }; Assert.Equal(1f, t.Forward.X, 1e-4f); Assert.Equal(0f, t.Forward.Z, 1e-4f); }
    [Fact] public void WorldScale_NoParent_EqualsLocal() { var t = new Transform(new GameObject()) { LocalScale = new(2,3,4) }; Assert.Equal(new Vector3(2,3,4), t.WorldScale); }
    [Fact] public void WorldScale_CombinesParentChain() { var p = new Transform(new GameObject()) { LocalScale = new(2,2,2) }; var c = new Transform(new GameObject()) { LocalScale = new(3,3,3) }; c.SetParent(p); Assert.Equal(new(6,6,6), c.WorldScale); }
    [Fact] public void WorldScale_CombinesDeepChain() { var a = new Transform(new GameObject()) { LocalScale = new(2,2,2) }; var b = new Transform(new GameObject()) { LocalScale = new(3,3,3) }; var c = new Transform(new GameObject()) { LocalScale = new(4,4,4) }; b.SetParent(a); c.SetParent(b); Assert.Equal(new(24,24,24), c.WorldScale); }
    [Fact] public void LocalToWorldMatrix_IncludesWorldScale() { var p = new Transform(new GameObject()) { LocalScale = new(2,2,2) }; var c = new Transform(new GameObject()) { LocalScale = new(3,3,3) }; c.SetParent(p); var m = c.LocalToWorldMatrix; Assert.Equal(6f, (m * new Vector3(1,0,0)).X, 1e-4f); Assert.Equal(6f, m.M11, 1e-4f); }
    [Fact] public void SetParent_ToSelf_Throws() { var t = new GameObject().Transform; Assert.Throws<InvalidOperationException>(() => t.SetParent(t)); }
    [Fact] public void SetParent_Cycle_Throws() { var a = new GameObject().Transform; var b = new GameObject().Transform; var c = new GameObject().Transform; c.SetParent(b); b.SetParent(a); Assert.Throws<InvalidOperationException>(() => a.SetParent(c)); }
}
