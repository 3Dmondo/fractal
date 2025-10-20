#version 440
#define MAX_ITERATIONS 1000
precision highp float;

uniform dvec2 Center;
uniform dvec2 View;

out vec4 outputColor;

in vec2 Z; // complexNumber

int get_iterations()
{
    
    //vec2 Zs = (Z / View - Center) ;

    double real = double(Z.x) / View.x - Center.x;
    double imag = double(Z.y) / View.y - Center.y;
 
    int iterations = 0;
    double const_real = real;
    double const_imag = imag;

    double real2 = real * real;
    double imag2 = imag * imag;
 
    while (iterations < MAX_ITERATIONS)
    {
        double tmp_real = real;

        real = real2 - imag2 + const_real;

        imag = (tmp_real + tmp_real) * imag + const_imag;

        real2 = real * real;
        imag2 = imag * imag;

        if (real2 + imag2 > 4.0)
        {
          break;
        }
 
        ++iterations;
    }
    return iterations;
}

void main()
{
    int iter = get_iterations();
    if (iter == MAX_ITERATIONS)
    {
        gl_FragDepth = 0.0f;
        outputColor = vec4(0.0f, 0.0f, 0.0f, 1.0f);
    }else { 
    float level = float(iter) / MAX_ITERATIONS;  
    float value = -log(level) * 10;
    vec3 col;
//    level *= 3.14159265 / 2.0;
//    col.r = sin(level);
//    col.g = sin(level*2.);
//    col.b = cos(level);

      
  float x = (value - trunc(value));
  value = mod(value,6);

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

    outputColor = vec4(col, 1.0);
  }
}

