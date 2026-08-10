using ProjectEngine.Render;

namespace ProjectEngine;

public class MeshRenderer : Component
{
    public Shader? Shader { get; set; }
    public Mesh? Mesh { get; set; }
    public Material? Material { get; set; }
}
