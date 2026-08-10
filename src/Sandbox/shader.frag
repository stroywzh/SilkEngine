#version 460 core
in vec3 vNormal;
in vec2 vTexCoord;

out vec4 FragColor;

void main()
{
    vec3 color = abs(vNormal);
    FragColor = vec4(color, 1.0);
}
