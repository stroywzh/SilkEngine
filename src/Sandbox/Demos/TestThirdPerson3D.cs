using SilkEngine.Host;
using SilkEngine.Math;
using SilkEngine.Scene;

namespace SandBox.Demos;

public static class TestThirdPerson3D
{
    public static void Run(EngineHost host)
    {
        GameplayActions.Configure();

        var scene = new Scene("ThirdPerson3D");
        host.SceneManager.LoadScene(scene);

        var shader = DemoAssetsExt.CreateLitShader(host);
        var cubeMesh = DemoAssetsExt.CreateCubeMesh(host);

        var ground = new GameObject("Ground");
        ground.Transform.LocalScale = new Vector3(20, 1, 20);
        var groundMr = ground.AddComponent<MeshRenderer>();
        groundMr.Shader = shader;
        groundMr.Mesh = cubeMesh;
        scene.AddRootObject(ground);

        for (int i = 0; i < 5; i++)
        for (int j = 0; j < 5; j++)
        {
            var cube = new GameObject($"Cube_{i}_{j}");
            cube.Transform.LocalPosition = new Vector3(i * 3 - 6, 1, j * 3 - 6);
            var mr = cube.AddComponent<MeshRenderer>();
            mr.Shader = shader;
            mr.Mesh = cubeMesh;
            scene.AddRootObject(cube);
        }

        var player = new GameObject("Player");
        player.Transform.LocalPosition = new Vector3(0, 0.5f, 0);
        var playerMr = player.AddComponent<MeshRenderer>();
        playerMr.Shader = shader;
        playerMr.Mesh = cubeMesh;
        var controller = player.AddComponent<PlayerController>();
        scene.AddRootObject(player);

        var camObj = new GameObject("FollowCam");
        var cam = camObj.AddComponent<Camera>();
        var follow = camObj.AddComponent<CameraFollow>();
        follow.Target = player;
        controller.Camera = follow;
        scene.AddRootObject(camObj);
        cam.UpdateMatrices(16f / 9f);
    }
}
