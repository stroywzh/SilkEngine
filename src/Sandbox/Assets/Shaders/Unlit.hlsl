// -----------------------------------------------------------------------------
// Unlit.hlsl - Unlit 基础着色器（引擎正式磁盘资源）
// 约定：左手系、行主序矩阵（GL 上传 UniformMatrix4 transpose=true 转列主序）
// 输入语义：POSITION / NORMAL / TEXCOORD0
// 入口：vert（顶点 → SV_Position）/ frag（片段 → SV_Target）
// -----------------------------------------------------------------------------

struct VSInput
{
    float3 Position : POSITION;
    float3 Normal   : NORMAL;
    float2 TexCoord : TEXCOORD0;
};

struct VSOutput
{
    float4 Position : SV_Position;
    float3 Normal   : TEXCOORD1;
    float2 TexCoord : TEXCOORD0;
};

cbuffer GlobalConstants : register(b0)
{
    row_major float4x4 uModel;
    row_major float4x4 uView;
    row_major float4x4 uProjection;
    float4 BaseColor;
};

Texture2D MainTexture : register(t0);
SamplerState MainSampler : register(s0);

VSOutput vert(VSInput input) : SV_Position
{
    VSOutput output;
    float4x4 modelViewProjection = mul(uProjection, mul(uView, uModel));
    output.Position = mul(modelViewProjection, float4(input.Position, 1.0f));
    output.Normal = input.Normal;
    output.TexCoord = input.TexCoord;
    return output;
}

float4 frag(VSOutput input) : SV_Target
{
    float4 texel = MainTexture.Sample(MainSampler, input.TexCoord);
    return BaseColor * texel;
}