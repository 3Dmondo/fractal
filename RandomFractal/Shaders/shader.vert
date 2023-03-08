#version 330 core

layout(location = 0) in vec2 aPosition;
layout(location = 1) in float vertexIndex;

uniform vec2 Center;
uniform vec2 View;
out vec3 vertColor; // output a color to the fragment shader

void main(void)
{
  gl_Position = vec4((aPosition - Center) * View, 1.0, 1.0);
  gl_PointSize = 1;
  vec3 col;

  float value = vertexIndex * 6.;
  
  float x = (value - trunc(value));

  if(value < 1.)
    col = vec3(1, x, 0);

  else if (value < 2.)
    col = vec3(1. - x, 1, 0);

  else if (value < 3.)
    col = vec3(0, 1, x);

  else if (value < 4.)
    col = vec3(0, 1. - x, 1);

  else if (value < 5.)
    col = vec3(x, 0, 1);

  else
    col = vec3(1, 0, 1. - x);

   vertColor = vec3(col);
}

