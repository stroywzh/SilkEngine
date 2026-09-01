using SilkEngine.Assets;

namespace SilkEngine.Render;

public static class MeshFactory
{
    public static MeshAsset CreateCube(float size = 1f)
    {
        float h = size * 0.5f;
        return new MeshAsset(
            "Cube",
            [
                -h,-h, h, 0,0,1, 0,0,  h,-h, h, 0,0,1, 1,0,  h, h, h, 0,0,1, 1,1, -h, h, h, 0,0,1, 0,1,
                 h,-h,-h, 0,0,-1,0,0, -h,-h,-h, 0,0,-1,1,0, -h, h,-h, 0,0,-1,1,1,  h, h,-h, 0,0,-1,0,1,
                 h,-h, h, 1,0,0, 0,0,  h,-h,-h, 1,0,0, 1,0,  h, h,-h, 1,0,0, 1,1,  h, h, h, 1,0,0, 0,1,
                -h,-h,-h,-1,0,0, 0,0, -h,-h, h,-1,0,0, 1,0, -h, h, h,-1,0,0, 1,1, -h, h,-h,-1,0,0, 0,1,
                -h, h, h, 0,1,0, 0,0,  h, h, h, 0,1,0, 1,0,  h, h,-h, 0,1,0, 1,1, -h, h,-h, 0,1,0, 0,1,
                -h,-h,-h,0,-1,0,0,0,  h,-h,-h,0,-1,0,1,0,  h,-h, h,0,-1,0,1,1, -h,-h, h,0,-1,0,0,1
            ],
            [3, 3, 2],
            [0,1,2,0,2,3, 4,5,6,4,6,7, 8,9,10,8,10,11, 12,13,14,12,14,15, 16,17,18,16,18,19, 20,21,22,20,22,23]);
    }

    public static MeshAsset CreatePlane(float width = 1f, float height = 1f)
    {
        float hw = width * 0.5f, hh = height * 0.5f;
        return new MeshAsset(
            "Plane",
            [-hw,0,hh, 0,1,0,0,0, hw,0,hh, 0,1,0,1,0, hw,0,-hh, 0,1,0,1,1, -hw,0,-hh, 0,1,0,0,1],
            [3, 3, 2],
            [0,1,2, 0,2,3]);
    }

    public static MeshAsset CreateQuad(float width = 2f, float height = 2f)
    {
        float hw = width * 0.5f, hh = height * 0.5f;
        return new MeshAsset(
            "Quad",
            [
                -hw, -hh, 0f, 0f, 0f,
                 hw, -hh, 0f, 1f, 0f,
                 hw,  hh, 0f, 1f, 1f,
                -hw,  hh, 0f, 0f, 1f,
            ],
            [3, 2],
            [0, 1, 2, 0, 2, 3]);
    }
}
