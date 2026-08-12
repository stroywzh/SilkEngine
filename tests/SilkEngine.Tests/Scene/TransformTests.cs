using SilkEngine;
using SilkEngine.Math;

namespace SilkEngine.Tests.Scene;

public class TransformTests
{
    [Fact] public void Defaults() { var t = new Transform(new GameObject()); Assert.Equal(Vector3.Zero, t.LocalPosition); Assert.Equal(Vector3.One, t.LocalScale); Assert.Equal(Quaternion.Identity, t.LocalRotation); }
    [Fact] public void Position_InWorldSpace_IncludesParent() { var p = new Transform(new GameObject()) { LocalPosition = new(5,0,0) }; var c = new Transform(new GameObject()) { LocalPosition = new(3,0,0) }; c.SetParent(p); Assert.Equal(new(8,0,0), c.Position); }
    [Fact] public void SetParent_DetachesOld() { var p1 = new Transform(new GameObject()); var p2 = new Transform(new GameObject()); var c = new Transform(new GameObject()); c.SetParent(p1); c.SetParent(p2); Assert.Same(p2, c.Parent); Assert.DoesNotContain(c, p1.Children); }
    [Fact] public void SetParent_Null_Detaches() { var p = new Transform(new GameObject()); var c = new Transform(new GameObject()); c.SetParent(p); c.SetParent(null); Assert.Null(c.Parent); }
    [Fact] public void Forward_AffectedByRotation() { var t = new Transform(new GameObject()) { LocalRotation = Quaternion.Euler(0,90,0) }; Assert.Equal(1f, t.Forward.X, 1e-4f); Assert.Equal(0f, t.Forward.Z, 1e-4f); }
}
