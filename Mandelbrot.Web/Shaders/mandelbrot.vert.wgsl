// Mandelbrot WGSL vertex shader

struct VertexIn {
    @location(0) aPos : vec2<f32>,
};

struct VertexOut {
    @location(0) vPos : vec2<f32>,
    @builtin(position) Position : vec4<f32>,
};

@vertex
fn vs_main(input : VertexIn) -> VertexOut {
    var output : VertexOut;
    output.Position = vec4<f32>(input.aPos, 0.0, 1.0);
    output.vPos = output.Position.xy;
    return output;
}
