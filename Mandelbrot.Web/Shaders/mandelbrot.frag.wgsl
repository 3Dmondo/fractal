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

fn smooth_color(iter: i32, maxIter: i32) -> vec3<f32> {
    let t = log(1.0 + f32(iter)) / log(1.0 + f32(maxIter));;
    // Non-periodic palette: interpolate between blue, cyan, green, yellow, red
    let c0 = vec3<f32>(0.0, 0.0, 1.0); // blue
    let c1 = vec3<f32>(0.0, 1.0, 1.0); // cyan
    let c2 = vec3<f32>(0.0, 1.0, 0.0); // green
    let c3 = vec3<f32>(1.0, 1.0, 0.0); // yellow
    let c4 = vec3<f32>(1.0, 0.0, 0.0); // red
    if (t < 0.25) {
        return mix(c0, c1, t * 4.0);
    } else if (t < 0.5) {
        return mix(c1, c2, (t - 0.25) * 4.0);
    } else if (t < 0.75) {
        return mix(c2, c3, (t - 0.5) * 4.0);
    } else {
        return mix(c3, c4, (t - 0.75) * 4.0);
    }
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
    let col = smooth_color(iter, maxIter);
    return vec4<f32>(col, 1.0);
}
