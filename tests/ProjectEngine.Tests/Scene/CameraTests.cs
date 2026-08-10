using ProjectEngine;
using ProjectEngine.Math;

namespace ProjectEngine.Tests.Scene;

public class CameraTests
{
    [Fact] public void DefaultFOV_60() => Assert.Equal(60f, new Camera().FieldOfView);
    [Fact] public void DefaultClipPlanes() { var c = new Camera(); Assert.Equal(0.1f, c.NearClipPlane); Assert.Equal(1000f, c.FarClipPlane); }

    [Fact]
    public void UpdateMatrices_ProducesNonZero()
    {
        var go = new GameObject();
        var cam = go.AddComponent<Camera>();
        go.Transform.LocalPosition = new Vector3(0, 0, -5);
        cam.UpdateMatrices(1.5f);
        Assert.True(cam.ViewMatrix.M44 != 0);
        Assert.True(cam.ProjectionMatrix.M11 != 0);
    }
}
