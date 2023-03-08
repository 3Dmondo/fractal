#version 460
in vec2 vUV;
layout (binding = 0) uniform sampler2D u_texture;
layout (location = 2) uniform vec3 textColor;
out vec4 fragColor;
void main()
{
  vec2 uv = vUV.xy;
  float text = texture(u_texture, uv).r;
  if (text < 0.1) discard;
  fragColor = vec4(textColor.rgb * text, text);
}