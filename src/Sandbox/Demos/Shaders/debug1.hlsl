// ----------------------------------------------
// 第一部分：常量缓冲区 (Constant Buffer)
// 用于接收 CPU 传过来的数据（矩阵、光照参数等）
// ----------------------------------------------
cbuffer ConstantBuffer : register(b0)
{
    float4x4 WorldViewProj; // 世界-视图-投影矩阵
    float4   ObjectColor;   // 物体的颜色
};

// ----------------------------------------------
// 第二部分：顶点着色器输入结构体
// 描述模型每个顶点包含哪些数据
// ----------------------------------------------
struct VertexInput
{
    float3 position : POSITION;  // POSITION 语义：表示这是顶点坐标
    float3 normal   : NORMAL;    // NORMAL 语义：表示这是法线
    float2 uv       : TEXCOORD0; // TEXCOORD0 语义：表示第一套UV坐标
};

// ----------------------------------------------
// 第三部分：顶点着色器输出 / 像素着色器输入结构体
// 描述从顶点阶段传递到像素阶段的数据
// ----------------------------------------------
struct PixelInput
{
    float4 position : SV_POSITION; // SV_POSITION 语义：系统必须识别的屏幕坐标
    float2 uv       : TEXCOORD0;   // 传递UV数据
    float3 worldNormal : TEXCOORD1; // 传递法线（用TEXCOORD1来装自定义数据）
};

// ----------------------------------------------
// 第四部分：顶点着色器 (Vertex Shader)
// 作用：把3D坐标换算成屏幕2D坐标
// ----------------------------------------------
PixelInput VS(VertexInput input)
{
    PixelInput output;
    
    // mul函数计算矩阵乘法，将顶点从模型空间转换到裁剪空间
    output.position = mul(float4(input.position, 1.0f), WorldViewProj);
    output.uv = input.uv;
    output.worldNormal = input.normal; // 简单传递（实际需乘法线矩阵）
    
    return output;
}

// ----------------------------------------------
// 第五部分：像素着色器 (Pixel Shader)
// 作用：决定屏幕上每个像素最终显示的颜色
// ----------------------------------------------
float4 PS(PixelInput input) : SV_Target // SV_Target 语义：输出到渲染目标
{
    // 这里简单返回一个颜色（如果外面传了ObjectColor就用，否则用白色）
    return ObjectColor;
}