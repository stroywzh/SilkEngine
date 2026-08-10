#version 460 core
layout(location = 0) in vec3 aPos;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoord;

uniform mat4 uView;
uniform mat4 uProjection;

out vec3 vNormal;
out vec2 vTexCoord;

void main()
{
    mat4 mvp = uProjection * uView;
    gl_Position = mvp * vec4(aPos, 1.0);
    vNormal = aNormal;
    vTexCoord = aTexCoord;
}
