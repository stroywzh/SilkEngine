using SilkEngine;
using SilkEngine.InputSystem;
using SilkEngine.Math;
using SilkEngine.Render;
using SilkEngine.Render.OpenGL;

var backend = new OpenGLRenderBackend();
var engine = new EngineLoop(backend);
Input.EnableLog = true;

// -------------------- 逐个取消注释测试 --------------------

TestNDCTriangle();

// TestNDCQuad();
// TestCameraOrtho();
// TestCameraPerspective();

// ---------------------------------------------------------

engine.Initialize().Run();

// ======================== 测试方法 ========================

void TestNDCTriangle()
{
    var scene = new Scene("NDC_Triangle");
    SceneManager.LoadScene(scene);

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
    mr.Shader = shader;
    mr.Mesh = mesh;
    scene.AddRootObject(go);
}

void TestNDCQuad()
{
    var scene = new Scene("NDC_Quad");
    SceneManager.LoadScene(scene);

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
    SceneManager.LoadScene(scene);

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
    SceneManager.LoadScene(scene);

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
