using SilkEngine;
using SilkEngine.Core.Assets;
using SilkEngine.Core.Assets.Serialization;
using SilkEngine.InputSystem;
using SilkEngine.Math;
using SilkEngine.Render;
using SilkEngine.Render.OpenGL;
using SilkEngine.Scene;

namespace SandBox;

class Program
{
    static void Main(string[] args)
    {
        var backend = new OpenGLRenderBackend();
        var engine = new EngineLoop(backend);
        Input.EnableLog = true;

        // -------------------- 逐个取消注释测试 --------------------

        // TestSingleCube();

        // TestThirdPerson3D();

        // TestPNGQuad();
        // TestNDCTriangle();
        // TestNDCQuad();
        // TestCameraOrtho();
        // TestCameraPerspective();
        TestSceneSerialization();

        // ---------------------------------------------------------

        engine.Initialize().Run();

        // ======================== 测试方法 ========================

        void TestSingleCube()
        {
            var scene = new Scene("SingleCube");
            engine.SceneManager.LoadScene(scene);

            var shader = new Shader
            {
                Name = "PerspCheck",
                VertexSource =
                    @"#version 460 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoord;
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;
out vec3 vNormal;
void main() { gl_Position = uProjection * uView * uModel * vec4(aPos, 1.0); vNormal = aNormal; }",
                FragmentSource =
                    @"#version 460 core
in vec3 vNormal;
out vec4 FragColor;
void main() { FragColor = vec4(abs(vNormal), 1.0); }",
            };

            var cube = new GameObject("Cube");
            var mr = cube.AddComponent<MeshRenderer>();
            mr.Shader = shader;
            mr.Mesh = MeshFactory.CreateCube(1f);
            scene.AddRootObject(cube);

            var camObj = new GameObject("Cam");
            camObj.Transform.LocalPosition = new Vector3(3, 2, -5);
            var cam = camObj.AddComponent<Camera>();
            scene.AddRootObject(camObj);
        }

        void TestNDCTriangle()
        {
            var scene = new Scene("NDC_Triangle");
            engine.SceneManager.LoadScene(scene);

            var shader = new Shader
            {
                Name = "NDC",
                VertexSource =
                    @"#version 460 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aColor;
out vec3 vColor;
void main() { gl_Position = vec4(aPos, 1.0); vColor = aColor; }",
                FragmentSource =
                    @"#version 460 core
in vec3 vColor;
out vec4 FragColor;
void main() { FragColor = vec4(vColor, 1.0); }",
            };

            var mesh = new Mesh
            {
                Name = "Triangle",
                Vertices = new float[]
                {
                    -0.5f,
                    -0.5f,
                    0,
                    1,
                    0,
                    0,
                    0.5f,
                    -0.5f,
                    0,
                    0,
                    1,
                    0,
                    0.0f,
                    0.5f,
                    0,
                    0,
                    0,
                    1,
                },
                Layout = new[] { 3, 3 },
            };

            var go = new GameObject("TriangleObj");
            var mr = go.AddComponent<MeshRenderer>();
            go.AddComponent<DEBUG_scripts>();

            mr.Shader = shader;
            mr.Mesh = mesh;
            // scene.AddRootObject(go);
            engine.SceneManager.AddObjectToScene(go);
        }

        void TestNDCQuad()
        {
            var scene = new Scene("NDC_Quad");
            engine.SceneManager.LoadScene(scene);

            var shader = new Shader
            {
                Name = "NDC_Quad",
                VertexSource =
                    @"#version 460 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec2 aTexCoord;
out vec2 vTexCoord;
void main() { gl_Position = vec4(aPos, 1.0); vTexCoord = aTexCoord; }",
                FragmentSource =
                    @"#version 460 core
in vec2 vTexCoord;
out vec4 FragColor;
void main() { FragColor = vec4(vTexCoord.x, vTexCoord.y, 0.3, 1.0); }",
            };

            var mesh = MeshFactory.CreateQuad(1.6f, 1.2f);
            var go = new GameObject("QuadObj");
            var mr = go.AddComponent<MeshRenderer>();
            mr.Shader = shader;
            mr.Mesh = mesh;
            scene.AddRootObject(go);
        }

        void TestCameraOrtho()
        {
            var scene = new Scene("Camera_Ortho");
            engine.SceneManager.LoadScene(scene);

            var shader = new Shader
            {
                Name = "Cam",
                VertexSource =
                    @"#version 460 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec2 aTexCoord;
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;
out vec2 vTexCoord;
void main() { gl_Position = uProjection * uView * uModel * vec4(aPos, 1.0); vTexCoord = aTexCoord; }",
                FragmentSource =
                    @"#version 460 core
in vec2 vTexCoord;
out vec4 FragColor;
void main() { FragColor = vec4(vTexCoord.x, vTexCoord.y, 0.3, 1.0); }",
            };

            var mat = new Material { Name = "Mat" };
            var quad = new GameObject("Quad");
            quad.Transform.LocalScale = new Vector3(4, 3, 1);
            var mr = quad.AddComponent<MeshRenderer>();
            mr.Shader = shader;
            mr.Mesh = MeshFactory.CreateQuad(1, 1);
            mr.Material = mat;
            scene.AddRootObject(quad);

            var camObj = new GameObject("Cam");
            camObj.Transform.LocalPosition = new Vector3(0, 0, -1);
            var cam = camObj.AddComponent<Camera>();
            cam.Orthographic = true;
            cam.OrthographicSize = 5f;
            scene.AddRootObject(camObj);
        }

        void TestCameraPerspective()
        {
            var scene = new Scene("Camera_Persp");
            engine.SceneManager.LoadScene(scene);

            var shader = new Shader
            {
                Name = "Persp",
                VertexSource =
                    @"#version 460 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoord;
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;
out vec3 vNormal;
void main() { gl_Position = uProjection * uView * uModel * vec4(aPos, 1.0); vNormal = aNormal; }",
                FragmentSource =
                    @"#version 460 core
in vec3 vNormal;
out vec4 FragColor;
void main() { FragColor = vec4(abs(vNormal), 1.0); }",
            };

            var mat = new Material { Name = "Mat" };
            var cube = new GameObject("Cube");
            cube.Transform.LocalPosition = new Vector3(0, 0, 3);
            var mr = cube.AddComponent<MeshRenderer>();
            mr.Shader = shader;
            mr.Mesh = MeshFactory.CreateCube(1f);
            mr.Material = mat;
            scene.AddRootObject(cube);

            var camObj = new GameObject("Cam");
            camObj.Transform.LocalPosition = new Vector3(0, 0, -2);
            var cam = camObj.AddComponent<Camera>();
            cam.Orthographic = false;
            scene.AddRootObject(camObj);
        }

        void TestPNGQuad()
        {
            var scene = new Scene("PNG_Quad");
            engine.SceneManager.LoadScene(scene);

            var shader = new Shader
            {
                Name = "PNG",
                VertexSource =
                    @"#version 460 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec2 aTexCoord;
uniform mat4 uMVP;
out vec2 vTexCoord;
void main() { gl_Position = uMVP * vec4(aPos, 1.0); vTexCoord = aTexCoord; }",
                FragmentSource =
                    @"#version 460 core
in vec2 vTexCoord;
out vec4 FragColor;
uniform sampler2D uMainTex;
void main() { FragColor = texture(uMainTex, vTexCoord); }",
            };

            var mat = new Material { Name = "PNGMat" };
            var quad = new GameObject("PNGQuad");
            quad.Transform.LocalScale = new Vector3(4, 3, 1);
            var mr = quad.AddComponent<MeshRenderer>();
            mr.Shader = shader;
            mr.Mesh = MeshFactory.CreateQuad(1, 1);
            mr.Material = mat;
            var demo = quad.AddComponent<TextureDemoRunner>();
            demo.Target = mat;
            demo.Engine = engine;
            scene.AddRootObject(quad);

            var camObj = new GameObject("Cam");
            camObj.Transform.LocalPosition = new Vector3(0, 0, -1);
            var cam = camObj.AddComponent<Camera>();
            cam.Orthographic = true;
            cam.OrthographicSize = 5f;
            scene.AddRootObject(camObj);
        }

        void TestThirdPerson3D()
        {
            var scene = new Scene("ThirdPerson3D");
            engine.SceneManager.LoadScene(scene);

            var shader = new Shader
            {
                Name = "Lit",
                VertexSource =
                    @"#version 460 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoord;
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;
out vec3 vNormal;
void main() { gl_Position = uProjection * uView * uModel * vec4(aPos, 1.0); vNormal = aNormal; }",
                FragmentSource =
                    @"#version 460 core
in vec3 vNormal;
out vec4 FragColor;
void main() { FragColor = vec4(abs(vNormal), 1.0); }",
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
                scene.AddRootObject(cube);
            }

            var player = new GameObject("Player");
            player.Transform.LocalPosition = new Vector3(0, 0.5f, 0);
            var playerMr = player.AddComponent<MeshRenderer>();
            playerMr.Shader = shader;
            playerMr.Mesh = MeshFactory.CreateCube(1f);
            var controller = player.AddComponent<PlayerController>();
            scene.AddRootObject(player);

            var camObj = new GameObject("FollowCam");
            var cam = camObj.AddComponent<Camera>();
            var follow = camObj.AddComponent<CameraFollow>();
            follow.Target = player;
            controller.Camera = follow;
            scene.AddRootObject(camObj);
        }

        void TestSceneSerialization()
        {
            var scene = new Scene("Serialized3D");
            var shader = new Shader
            {
                Name = "Lit",
                VertexSource =
                    @"#version 460 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoord;
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;
out vec3 vNormal;
void main() { gl_Position = uProjection * uView * uModel * vec4(aPos, 1.0); vNormal = aNormal; }",
                FragmentSource =
                    @"#version 460 core
in vec3 vNormal;
out vec4 FragColor;
void main() { FragColor = vec4(abs(vNormal), 1.0); }",
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

            // ---- 保存 ----
            Directory.CreateDirectory("Resources");
            var path = Path.Combine("Resources", "serialized_3d.scene");
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
            // Camera 已实现 ISerializableComponent，加载后由反序列化重建
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
        (string str1, string str2) GetSrtTurple()
        {
            return (string.Empty, "WC");
        }
    }

    // LazyAsync 加载 demo 纹理：未就绪时材质无主纹理 → 白色占位，就绪后贴图
    private class TextureDemoRunner : MonoBehaviour
    {
        public EngineLoop? Engine;
        public Material? Target;
        private AssetRequest<Texture2D>? _request;

        public override void OnStart()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Resources", "test.png");
            _request = Engine!.AssetManager.LoadAsync<Texture2D>(path, AsyncLoadMode.LazyAsync);
            _ = _request.Asset; // LazyAsync 首次访问即触发加载
        }

        public override void OnUpdate(float deltaTime)
        {
            if (_request is { IsDone: true, Asset: { } tex } && Target is { MainTexture: null })
            {
                Target.MainTexture = tex;
                Log.Info("[Demo] PNG texture applied");
            }
        }
    }
}
