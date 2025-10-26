#version 330 core
precision highp float;


uniform float AspectRatio;
layout(location = 0) in vec2 Position;

out vec2 Z; // complexNumber

void main(void)
{
  switch(gl_VertexID)
  {
    case 0:
      //Bottom-left
      gl_Position = vec4(-1,-1,1,1);
      break;
    case 1:
      //Top-left
      gl_Position = vec4(-1,1,1,1);
      break;
    case 2:
      //Bottom-right
      gl_Position = vec4(1,-1,1,1);
      break;
    case 3:
      //Top-right
      gl_Position = vec4(1,1,1,1);
      break;
  }

  Z = gl_Position.xy;// (gl_Position.xy / View - Center) ; 
  //C = gl_Position.xy * 0.000001 * C;
}

