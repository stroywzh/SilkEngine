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

    [Fact]
    public void ViewMatrix_LookAtOrigin_IdentityRotation()
    {
        var go = new GameObject();
        var cam = go.AddComponent<Camera>();
        go.Transform.LocalPosition = new Vector3(0, 0, -1);
        cam.UpdateMatrices(1f);
        // 相机在(0,0,-1)看原点，view应把原点映射到z=1
        var result = cam.ViewMatrix * new Vector3(0, 0, 0);
        Assert.True(result.Z > 0);
    }

    [Fact]
    public void OrthographicProjection_HasCorrectSize()
    {
        var go = new GameObject();
        var cam = go.AddComponent<Camera>();
        go.Transform.LocalPosition = new Vector3(0, 0, -1);
        cam.Orthographic = true;
        cam.OrthographicSize = 5f;
        cam.UpdateMatrices(2f); // aspect=2, width=20, height=10
        // 点(10,0,1) 应在NDC x=1
        var result = cam.ProjectionMatrix * new Vector3(10, 0, 1);
        Assert.True(result.X >= 0.9f);
    }
}
