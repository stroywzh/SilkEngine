using SilkEngine.Assets;
using SilkEngine.Core;
using SilkEngine.InputSystem;
using SilkEngine.Math;
using SilkEngine.Render;
using SilkEngine.Scene;

namespace SandBox.Demos;

public static class TestThirdPerson3D
{
    public static void Run(EngineLoop engine)
    {
        var scene = new Scene("ThirdPerson3D");
        engine.SceneManager.LoadScene(scene);

        var shader = new ShaderAsset("Lit", ShaderSources.LitVertex, ShaderSources.LitFragment);
        var cubeMesh = MeshFactory.CreateCube(1f);

        var ground = new GameObject("Ground");
        ground.Transform.LocalScale = new Vector3(20, 1, 20);
        var groundMr = ground.AddComponent<MeshRenderer>();
        groundMr.SetShader(new AssetHandle<ShaderAsset>(DemoAssets.NewId()));
        groundMr.SetMesh(new AssetHandle<MeshAsset>(DemoAssets.NewId()));
        scene.AddRootObject(ground);

        for (int i = 0; i < 5; i++)
        for (int j = 0; j < 5; j++)
        {
            var cube = new GameObject($"Cube_{i}_{j}");
            cube.Transform.LocalPosition = new Vector3(i * 3 - 6, 1, j * 3 - 6);
            var mr = cube.AddComponent<MeshRenderer>();
            mr.SetShader(new AssetHandle<ShaderAsset>(DemoAssets.NewId()));
            mr.SetMesh(new AssetHandle<MeshAsset>(DemoAssets.NewId()));
            scene.AddRootObject(cube);
        }

        var player = new GameObject("Player");
        player.Transform.LocalPosition = new Vector3(0, 0.5f, 0);
        var playerMr = player.AddComponent<MeshRenderer>();
        playerMr.SetShader(new AssetHandle<ShaderAsset>(DemoAssets.NewId()));
        playerMr.SetMesh(new AssetHandle<MeshAsset>(DemoAssets.NewId()));
        var controller = player.AddComponent<PlayerController>();
        scene.AddRootObject(player);

        Log.Info($"[TestThirdPerson3D] {shader.Name} + {cubeMesh.Name} 已装配（GPU 句柄待创建请求接线后发布）");

        var camObj = new GameObject("FollowCam");
        var cam = camObj.AddComponent<Camera>();
        var follow = camObj.AddComponent<CameraFollow>();
        follow.Target = player;
        controller.Camera = follow;
        scene.AddRootObject(camObj);
    }
}
