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
 
    while (iterations < MAX_ITERATIONS)
    {
        double tmp_real = real;
        real = (real * real - imag * imag) + const_real;
        imag = (2.0 * tmp_real * imag) + const_imag;
         
        double dist = real * real + imag * imag;
         
        if (dist > 4.0)
        {
          //iterations = MAX_ITERATIONS;
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
    level=log(level);
    level *= 3.14159265 / 2.0 * 10;
    vec3 col;
    col.r = sin(level);
    col.g = sin(level*2.);
    col.b = cos(level);
    outputColor = vec4(col, 1.0);
  }
}

