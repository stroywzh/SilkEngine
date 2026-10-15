namespace SilkEngine.Runtime.Rendering.Abstraction;

public interface ICommandBuffer
{
    // 指令缓冲区
    void Draw(int MeshId, ushort MaterialId);
}
