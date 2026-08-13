using SilkEngine.Core;
using SilkEngine.Core.Assets;
using SilkEngine.InputSystem;
using SilkEngine.Math;
using SilkEngine.Render;
using SilkEngine.Scene;
using SilkEngine.Scene.Serialization;

namespace SandBox.Demos;

public static class TestSceneSerialization
{
    public static void Run(EngineLoop engine)
    {
        var scene = new Scene("Serialized3D");
        var shader = new Shader
        {
            Name = "Lit",
            VertexSource = ShaderSources.LitVertex,
            FragmentSource = ShaderSources.LitFragment,
        };

        var ground = new GameObject("Ground");
        ground.Transform.LocalScale = new Vector3(20, 1, 20);
        var groundMr = ground.AddComponent<MeshRenderer>();
        groundMr.Shader = shader;
        groundMr.Mesh = MeshFactory.CreateCube(1f);
        scene.AddRootObject(ground);

        for (int i = 0; i < 5; i++)
        for (int j = 0; j < 5; j++)
        {
            var cube = new GameObject($"Cube_{i}_{j}");
            cube.Transform.LocalPosition = new Vector3(i * 3 - 6, 1, j * 3 - 6);
            var mr = cube.AddComponent<MeshRenderer>();
            mr.Shader = shader;
            mr.Mesh = MeshFactory.CreateCube(1f);
            cube.Transform.SetParent(ground.Transform); // 层级随场景保存
        }

        var camObj = new GameObject("FollowCam");
        camObj.Transform.LocalPosition = new Vector3(0, 4, -10);
        camObj.AddComponent<Camera>();
        camObj.AddComponent<CameraFollow>().Target = ground;
        camObj.AddComponent<PlayerController>().Camera = camObj.GetComponent<CameraFollow>();

        scene.AddRootObject(camObj);

        // ---- 保存（bin 输出）----
        var dir = Path.Combine(AppContext.BaseDirectory, "Resources");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "serialized_3d.scene");
        File.WriteAllText(path, SceneSerializer.Serialize(scene));
        Log.Info($"Scene saved to {path}");

        Thread.Sleep(1000);

        // ---- 加载（往返）----
        engine.SceneManager.LoadSceneFromFile(path);
        Log.Info(
            $"Scene loaded: '{engine.SceneManager.ActiveScene!.Name}', "
                + $"roots={engine.SceneManager.ActiveScene.GetRootGameObjects().Length}"
        );

        // 资产重绑：当前 Shader/Mesh 为非托管资产（GUID 为空，序列化跳过引用）
        foreach (var go in engine.SceneManager.ActiveScene.GetRootGameObjects())
            RebindAssets(go);

        // 用户组件运行时重挂：反序列化不重建未注册组件；组件引用需手动重连
        // Camera 的序列化由源生成器生成（[SerializableInternal]），加载后由反序列化重建
        var loadedCam = engine.SceneManager.ActiveScene!.GetRootGameObjects()
            .First(go => go.GetComponent<Camera>() != null);
        var follow = loadedCam.AddComponent<CameraFollow>();
        follow.Target = engine.SceneManager.ActiveScene.GetRootGameObjects()
            .First(go => go.Name == "Ground");

        void RebindAssets(GameObject go)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.Shader ??= shader;
                mr.Mesh ??= MeshFactory.CreateCube(1f);
            }
            foreach (var child in go.Transform.Children)
                RebindAssets(child.GameObject!);
        }
    }
}
