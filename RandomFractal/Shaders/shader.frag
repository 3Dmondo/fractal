#version 330

out vec4 outputColor;
in vec3 vertColor;

void main()
{
    vec2 C = 2.0 * (gl_PointCoord - vec2(0.5, 0.5));
    float mag = dot(C,C);
    if (mag > 1.0) discard;   // kill pixels outside circle
    outputColor = vec4(vertColor, 1);
}