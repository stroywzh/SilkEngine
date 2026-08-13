using System.Text.Json.Nodes;
using SilkEngine;
using SilkEngine.Scene.Serialization;
using SilkEngine.Math;

namespace SilkEngine.Tests.Core.Assets.Serialization;
using Scene = SilkEngine.Scene.Scene;

[Collection("Serialization")]
public class CameraSerializationTests
{
    [Fact]
    public void WriteTo_WritesAllCameraFields()
    {
        var cam = new Camera
        {
            FieldOfView = 45f,
            NearClipPlane = 0.5f,
            FarClipPlane = 500f,
            OrthographicSize = 8f,
            Orthographic = true,
        };
        var obj = new JsonObject();
        cam.WriteTo(new SerializedNode(obj));
        var json = obj.ToJsonString();
        Assert.Contains("FieldOfView", json);
        Assert.Contains("NearClipPlane", json);
        Assert.Contains("FarClipPlane", json);
        Assert.Contains("OrthographicSize", json);
        Assert.Contains("Orthographic", json);
    }

    [Fact]
    public void ReadFrom_RestoresAllCameraFields()
    {
        var cam = new Camera();
        cam.ReadFrom(new SerializedNode(JsonNode.Parse(
            """{ "FieldOfView": 45, "NearClipPlane": 0.5, "FarClipPlane": 500, "OrthographicSize": 8, "Orthographic": true }"""
        )!.AsObject()));

        Assert.Equal(45f, cam.FieldOfView);
        Assert.Equal(0.5f, cam.NearClipPlane);
        Assert.Equal(500f, cam.FarClipPlane);
        Assert.Equal(8f, cam.OrthographicSize);
        Assert.True(cam.Orthographic);
    }

    [Fact]
    public void ReadFrom_MissingFields_KeepDefaults()
    {
        var cam = new Camera();
        cam.ReadFrom(new SerializedNode(new JsonObject()));
        Assert.Equal(60f, cam.FieldOfView);        // 默认 FOV 不变
        Assert.Equal(0.1f, cam.NearClipPlane);
        Assert.Equal(1000f, cam.FarClipPlane);
        Assert.Equal(5f, cam.OrthographicSize);
        Assert.False(cam.Orthographic);            // 默认透视不变
    }

    [Fact]
    public void Roundtrip_ThroughSceneSerializer_PreservesCamera()
    {
        var scene = new Scene("CamRoundtrip");
        var camGo = new GameObject("Cam");
        camGo.Transform.LocalPosition = new Vector3(0, 4, -10);
        var cam = camGo.AddComponent<Camera>();
        cam.Orthographic = true;
        cam.OrthographicSize = 6f;
        scene.AddRootObject(camGo);

        var scene2 = SceneSerializer.Deserialize(SceneSerializer.Serialize(scene));
        var cam2 = scene2.GetRootGameObjects()[0].GetComponent<Camera>();

        Assert.NotNull(cam2);
        Assert.True(cam2!.Orthographic);
        Assert.Equal(6f, cam2.OrthographicSize);
    }
}
