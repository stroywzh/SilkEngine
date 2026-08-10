using ProjectEngine;
using ProjectEngine.Math;
using ProjectEngine.Render;
using ProjectEngine.Render.OpenGL;

var backend = new OpenGLRenderBackend();
var pipeline = new ForwardRenderPipeline();
var engine = new EngineLoop(backend, pipeline);

var scene = new Scene("Demo");
SceneManager.LoadScene(scene);

var shader = new Shader
{
    Name = "Standard",
    VertexSource = File.ReadAllText("shader.vert"),
    FragmentSource = File.ReadAllText("shader.frag")
};

var material = new Material { Name = "CubeMat" };

var cube = new GameObject("Cube");
cube.Transform.LocalPosition = new Vector3(0, 0, 3);
var cubeRenderer = cube.AddComponent<MeshRenderer>();
cubeRenderer.Shader = shader;
cubeRenderer.Mesh = MeshFactory.CreateCube(1f);
cubeRenderer.Material = material;
scene.AddRootObject(cube);

var cameraObj = new GameObject("MainCamera");
cameraObj.Transform.LocalPosition = new Vector3(0, 0, -2);
cameraObj.AddComponent<Camera>();
scene.AddRootObject(cameraObj);

Console.WriteLine("Setup Finished");
engine.Run();
