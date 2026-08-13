namespace SandBox.Demos;

public static class ShaderSources
{
    // uModel/uView/uProjection + 法线（SingleCube/CameraPerspective/ThirdPerson3D/Serialized3D 共用）
    public const string LitVertex = @"#version 460 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoord;
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;
out vec3 vNormal;
void main() { gl_Position = uProjection * uView * uModel * vec4(aPos, 1.0); vNormal = aNormal; }";

    public const string LitFragment = @"#version 460 core
in vec3 vNormal;
out vec4 FragColor;
void main() { FragColor = vec4(abs(vNormal), 1.0); }";

    // NDC 直通 + 顶点色（NDCTriangle）
    public const string NdcColorVertex = @"#version 460 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aColor;
out vec3 vColor;
void main() { gl_Position = vec4(aPos, 1.0); vColor = aColor; }";

    public const string NdcColorFragment = @"#version 460 core
in vec3 vColor;
out vec4 FragColor;
void main() { FragColor = vec4(vColor, 1.0); }";

    // NDC 直通 + UV 色（NDCQuad）
    public const string NdcUvVertex = @"#version 460 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec2 aTexCoord;
out vec2 vTexCoord;
void main() { gl_Position = vec4(aPos, 1.0); vTexCoord = aTexCoord; }";

    public const string NdcUvFragment = @"#version 460 core
in vec2 vTexCoord;
out vec4 FragColor;
void main() { FragColor = vec4(vTexCoord.x, vTexCoord.y, 0.3, 1.0); }";

    // uModel/uView/uProjection + UV 色（CameraOrtho）
    public const string CamUvVertex = @"#version 460 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec2 aTexCoord;
uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;
out vec2 vTexCoord;
void main() { gl_Position = uProjection * uView * uModel * vec4(aPos, 1.0); vTexCoord = aTexCoord; }";

    public const string CamUvFragment = @"#version 460 core
in vec2 vTexCoord;
out vec4 FragColor;
void main() { FragColor = vec4(vTexCoord.x, vTexCoord.y, 0.3, 1.0); }";

    // uMVP + 纹理采样（PNGQuad）
    public const string PngVertex = @"#version 460 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec2 aTexCoord;
uniform mat4 uMVP;
out vec2 vTexCoord;
void main() { gl_Position = uMVP * vec4(aPos, 1.0); vTexCoord = aTexCoord; }";

    public const string PngFragment = @"#version 460 core
in vec2 vTexCoord;
out vec4 FragColor;
uniform sampler2D uMainTex;
void main() { FragColor = texture(uMainTex, vTexCoord); }";
}
