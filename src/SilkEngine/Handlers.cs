//*
//This file storage all the handle used in Engine
//*
namespace SilkEngine;

public readonly struct Handle<T>
    where T : struct
{
    public readonly T Value;

    public Handle(T value)
    {
        Value = value;
    }
}

public readonly struct MeshHandle
{
    public readonly ushort Id;

    public MeshHandle(ushort id) => Id = id;
}

public readonly struct MaterialHandle
{
    public readonly ushort Id;

    public MaterialHandle(ushort id) => Id = id;
}

public readonly struct TextureHandle
{
    public readonly ushort Id;

    public TextureHandle(ushort id) => Id = id;
}

public readonly struct RenderTargetHandle
{
    public readonly ushort Id;

    public RenderTargetHandle(ushort id) => Id = id;
}

public readonly struct ShaderHandle
{
    public readonly ushort Id;

    public ShaderHandle(ushort id) => Id = id;
}

public readonly struct ComputeShaderHandle
{
    public readonly ushort Id;

    public ComputeShaderHandle(ushort id) => Id = id;
}
