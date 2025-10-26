// Mandelbrot WGSL fragment shader

struct ViewParams {
    center : vec2<f32>,
    view : vec2<f32>,
    maxIter : i32,
    pad : i32,
};

@group(0) @binding(0) var<uniform> uView : ViewParams;

fn mandelbrot(c_re: f32, c_im: f32, maxIter: i32) -> i32 {
    var z_re: f32 = c_re;
    var z_im: f32 = c_im;
    var iter: i32 = 0;
    var re2: f32 = z_re * z_re;
    var im2: f32 = z_im * z_im;
    loop {
        if (iter >= maxIter) { break; }
        let tmp = z_re;
        z_re = re2 - im2 + c_re;
        z_im = (tmp + tmp) * z_im + c_im;
        re2 = z_re * z_re;
        im2 = z_im * z_im;
        if (re2 + im2 > 4.0) { break; }
        iter = iter + 1;
    }
    return iter;
}

@fragment
fn fs_main(@location(0) vPos : vec2<f32>) -> @location(0) vec4<f32> {
    let center = uView.center;
    let view = uView.view;
    let maxIter = uView.maxIter;
    let cr = vPos.x / view.x - center.x;
    let ci = vPos.y / view.y - center.y;
    let iter = mandelbrot(cr, ci, maxIter);
    if (iter == maxIter) {
        return vec4<f32>(0.0, 0.0, 0.0, 1.0);
    }
    let level = f32(iter) / f32(maxIter);
    var value = -log(max(level, 1e-9)) * 10.0;
    var x = value - floor(value);
    value = value - floor(value / 6.0) * 6.0;
    var col: vec3<f32>;
    if (value < 1.0) {
        col = vec3<f32>(1.0, x, 0.0);
    } else if (value < 2.0) {
        col = vec3<f32>(1.0 - x, 1.0, 0.0);
    } else if (value < 3.0) {
        col = vec3<f32>(0.0, 1.0, x);
    } else if (value < 4.0) {
        col = vec3<f32>(0.0, 1.0 - x, 1.0);
    } else if (value < 5.0) {
        col = vec3<f32>(x, 0.0, 1.0);
    } else {
        col = vec3<f32>(1.0, 0.0, 1.0 - x);
    }
    return vec4<f32>(col, 1.0);
}
