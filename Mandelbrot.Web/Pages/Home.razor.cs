using Evergine.Bindings.WebGPU;
using static Evergine.Bindings.WebGPU.WebGPUNative;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;

namespace Mandelbrot.Web.Pages
{
  public partial class Home
  {
    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
      if (firstRender)
      {
        try
        {
          // Ensure the JS shim initializes WebGPU and sets Module.preinitializedWebGPUDevice
          await JS.InvokeVoidAsync("initWebGPU");
        }
        catch
        {
          // ignore — initWebGPU may not exist or may fail; Run() will still try to use any available device
        }

        // Automatically start the WebGPU demo once initialization is attempted
        Run();
      }
    }

    public unsafe void Run()
    {
      // Based on: https://github.com/seyhajin/webgpu-wasm-c/blob/f8d718cf44d9ab3f19319efb27c87c645c46fc15/main.c
      // The main difference is instead of having a static state, we have added local variables on demand
      var device = emscripten_webgpu_get_device();
      var queue = wgpuDeviceGetQueue(device);
      double width, height;
      emscripten_get_element_css_size("canvas".ToPointer(), &width, &height);
      var surfaceDescriptorFromCanvasHTMLSelector = new WGPUSurfaceDescriptorFromCanvasHTMLSelector() {
        chain = new WGPUChainedStruct() {
          sType = WGPUSType.SurfaceDescriptorFromCanvasHTMLSelector,
        },
        selector = "canvas".ToPointer(),
      };
      var surfaceDescriptor = new WGPUSurfaceDescriptor() {
        nextInChain = (WGPUChainedStruct*)&surfaceDescriptorFromCanvasHTMLSelector,
      };
      var surface = wgpuInstanceCreateSurface(instance: IntPtr.Zero, &surfaceDescriptor);
      var swapChainDescriptor = new WGPUSwapChainDescriptor() {
        usage = WGPUTextureUsage.RenderAttachment,
        format = WGPUTextureFormat.BGRA8Unorm,
        width = (uint)width,
        height = (uint)height,
        presentMode = WGPUPresentMode.Fifo,
      };
      var swapChain = wgpuDeviceCreateSwapChain(device, surface, &swapChainDescriptor);

      // Single-precision WGSL Mandelbrot shader (vertex + fragment) with uniform view params
      var triangleWGSL = @"
// Mandelbrot WGSL (single-precision) with uniform view params

struct VertexIn {
    @location(0) aPos : vec2<f32>,
    @location(1) aCol : vec3<f32>, // kept to match buffer layout
};
struct VertexOut {
    @location(0) vPos : vec2<f32>,
    @builtin(position) Position : vec4<f32>,
};

struct ViewParams {
    center : vec2<f32>,
    view : vec2<f32>,
    maxIter : i32,
    pad : i32, // padding to make size multiple of 16 bytes
};

@group(0) @binding(0) var<uniform> uView : ViewParams;

@vertex
fn vs_main(input : VertexIn) -> VertexOut {
    var output : VertexOut;
    // scale existing [-0.5,0.5] buffer to clip space [-1,1]
    output.Position = vec4<f32>(input.aPos * 2.0, 0.0, 1.0);
    output.vPos = output.Position.xy;
    return output;
}

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
    // Map clip-space [-1,1] to complex plane using uniform view parameters
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
    // compute value mod 6.0 without using mod()
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
";

      var shader_triangle = create_shader(triangleWGSL, label: default, device);
      WGPUVertexAttribute* vertex_attrib = stackalloc WGPUVertexAttribute[]
      {
                // position: x, y
                new WGPUVertexAttribute()
                {
                    format = WGPUVertexFormat.Float32x2,
                    offset = 0,
                    shaderLocation = 0,
                },
                // color: r, g, b
                new WGPUVertexAttribute()
                {
                    format = WGPUVertexFormat.Float32x3,
                    offset = 2 * sizeof(float),
                    shaderLocation = 1,
                }
            };
      WGPUVertexBufferLayout vertex_buffer_layout = new WGPUVertexBufferLayout {
        arrayStride = 5 * sizeof(float),
        attributeCount = 2,
        attributes = vertex_attrib,
      };

      // describe pipeline layout
      var entries = stackalloc WGPUBindGroupLayoutEntry[]
      {
                new WGPUBindGroupLayoutEntry()
                {
                    binding = 0,
                    visibility = (WGPUShaderStage)(WGPUShaderStage.Vertex | WGPUShaderStage.Fragment),
                    // buffer binding layout
                    buffer = new WGPUBufferBindingLayout()
                    {
                        type = WGPUBufferBindingType.Uniform,
                    },
                },
            };
      var bindGroupLayoutDescriptor = new WGPUBindGroupLayoutDescriptor() {
        entryCount = 1,
        // bind group layout entry
        entries = entries,
      };
      WGPUBindGroupLayout bindgroup_layout = wgpuDeviceCreateBindGroupLayout(device, &bindGroupLayoutDescriptor);
      var pipelineLayoutDescriptor = new WGPUPipelineLayoutDescriptor() {
        bindGroupLayoutCount = 1,
        bindGroupLayouts = &bindgroup_layout,
      };
      WGPUPipelineLayout pipeline_layout = wgpuDeviceCreatePipelineLayout(device, &pipelineLayoutDescriptor);
      // create pipeline
      var blendState = new WGPUBlendState() {
        color = new WGPUBlendComponent() {
          operation = WGPUBlendOperation.Add,
          srcFactor = WGPUBlendFactor.One,
          dstFactor = WGPUBlendFactor.One,
        },
        alpha = {
                    operation = WGPUBlendOperation.Add,
                    srcFactor = WGPUBlendFactor.One,
                    dstFactor = WGPUBlendFactor.One,
                },
      };
      var targetState = new WGPUColorTargetState() {
        format = WGPUTextureFormat.BGRA8Unorm,
        writeMask = WGPUColorWriteMask.All,
        // blend state
        blend = &blendState,
      };
      var fragmentState = new WGPUFragmentState() {
        module = shader_triangle,
        entryPoint = "fs_main".ToPointer(),
        targetCount = 1,
        // color target state
        targets = &targetState,
      };
      var renderPipelineDescriptor = new WGPURenderPipelineDescriptor() {
        // pipeline layout
        layout = pipeline_layout,
        // vertex state
        vertex = new WGPUVertexState() {
          module = shader_triangle,
          entryPoint = "vs_main".ToPointer(),
          bufferCount = 1,
          buffers = &vertex_buffer_layout,
        },
        // primitive state
        primitive = new WGPUPrimitiveState() {
          frontFace = WGPUFrontFace.CCW,
          cullMode = WGPUCullMode.None,
          topology = WGPUPrimitiveTopology.TriangleList,
          stripIndexFormat = WGPUIndexFormat.Undefined,
        },
        // fragment state
        fragment = &fragmentState,
        // multi-sampling state
        multisample = new WGPUMultisampleState() {
          count = 1,
          mask = 0xFFFFFFFF,
          alphaToCoverageEnabled = false,
        },
        // depth-stencil state
        depthStencil = null,
      };
      var pipeline = wgpuDeviceCreateRenderPipeline(device, &renderPipelineDescriptor);
      wgpuPipelineLayoutRelease(pipeline_layout);
      wgpuShaderModuleRelease(shader_triangle);
      // create the vertex buffer (x, y, r, g, b) and index buffer
      var vertex_data = stackalloc float[]
      {
                // x, y          // r, g, b
               -0.5f, -0.5f,     1.0f, 0.0f, 0.0f, // bottom-left
                0.5f, -0.5f,     0.0f, 1.0f, 0.0f, // bottom-right
                0.5f,  0.5f,     0.0f, 0.0f, 1.0f, // top-right
               -0.5f,  0.5f,     1.0f, 1.0f, 0.0f, // top-left
            };
      var index_data = stackalloc ushort[]
      {
                0, 1, 2,
                0, 2, 3,
            };
      var vbuffer = create_buffer(vertex_data, 5 * 4 * sizeof(float), WGPUBufferUsage.Vertex, device, queue);
      var ibuffer = create_buffer(index_data, 3 * 2 * sizeof(ushort), WGPUBufferUsage.Index, device, queue);
      // create the uniform bind group
      // Prepare ViewParams: center.x, center.y, view.x, view.y, maxIter (int)
      float centerX = -0.5f;
      float centerY = 0.0f;
      float viewX = 1.5f;
      float viewY = 1.0f;
      int maxIter = 400;

      // Allocate 24 bytes to match WGSL struct: vec2 + vec2 = 16 bytes, plus i32 + padding = 8 bytes => 24 bytes total
      byte* ubData = stackalloc byte[24];
      float* fptr = (float*)ubData;
      fptr[0] = centerX;
      fptr[1] = centerY;
      fptr[2] = viewX;
      fptr[3] = viewY;
      // place the ints at offset 16
      int* iptr = (int*)(ubData + 16);
      iptr[0] = maxIter;
      iptr[1] = 0; // padding

      var ubuffer = create_buffer(ubData, 24u, WGPUBufferUsage.Uniform, device, queue);

      var bindGroupEntry = new WGPUBindGroupEntry() {
        binding = 0,
        offset = 0,
        buffer = ubuffer,
        size = 24u,
      };

      var bindGroupDescriptor = new WGPUBindGroupDescriptor() {
        // We reuse the layout created earlier because wgpuRenderPipelineGetBindGroupLayout(pipeline, 0) does not work
        layout = bindgroup_layout,
        entryCount = 1,
        // bind group entry
        entries = &bindGroupEntry,
      };
      var bindgroup = wgpuDeviceCreateBindGroup(device, &bindGroupDescriptor);
      wgpuBindGroupLayoutRelease(bindgroup_layout);
      draw(swapChain, device, queue, pipeline, bindgroup, vbuffer, ibuffer);
    }

    private unsafe WGPUBuffer create_buffer(void* data, uint size, WGPUBufferUsage usage, WGPUDevice device, WGPUQueue queue)
    {
      var bufferDescriptor = new WGPUBufferDescriptor() {
        usage = WGPUBufferUsage.CopyDst | usage,
        size = size,
      };
      WGPUBuffer buffer = wgpuDeviceCreateBuffer(device, &bufferDescriptor);
      wgpuQueueWriteBuffer(queue, buffer, 0u, data, size);

      return buffer;
    }

    private unsafe WGPUShaderModule create_shader(string code, string? label, WGPUDevice device)
    {
      var shaderModuleWGSLDescriptor = new WGPUShaderModuleWGSLDescriptor {
        chain = new WGPUChainedStruct() {
          sType = WGPUSType.ShaderModuleWGSLDescriptor,
        },
        source = code.ToPointer(),
      };
      var shaderModuleDescriptor = new WGPUShaderModuleDescriptor() {
        nextInChain = (WGPUChainedStruct*)&shaderModuleWGSLDescriptor,
        label = label == default ? null : label.ToPointer(),
      };
      var shaderModule = wgpuDeviceCreateShaderModule(device, &shaderModuleDescriptor);

      return shaderModule;
    }

    private unsafe void draw(
        WGPUSwapChain swapchain,
        WGPUDevice device,
        WGPUQueue queue,
        WGPURenderPipeline pipeline,
        WGPUBindGroup bindgroup,
        WGPUBuffer vbuffer,
        WGPUBuffer ibuffer)
    {
      // create texture view
      WGPUTextureView back_buffer = wgpuSwapChainGetCurrentTextureView(swapchain);

      // create command encoder
      WGPUCommandEncoder cmd_encoder = wgpuDeviceCreateCommandEncoder(device, null);

      // begin render pass
      var colorAttachment = new WGPURenderPassColorAttachment() {
        view = back_buffer,
        loadOp = WGPULoadOp.Clear,
        storeOp = WGPUStoreOp.Store,
        clearValue = new WGPUColor() {
          r = 0.2f,
          g = 0.2f,
          b = 0.3f,
          a = 1.0f,
        },
      };
      var renderPassDescriptor = new WGPURenderPassDescriptor() {
        // color attachments
        colorAttachmentCount = 1,
        colorAttachments = &colorAttachment,
      };
      WGPURenderPassEncoder render_pass = wgpuCommandEncoderBeginRenderPass(cmd_encoder, &renderPassDescriptor);

      // draw quad (comment these five lines to simply clear the screen)
      wgpuRenderPassEncoderSetPipeline(render_pass, pipeline);
      wgpuRenderPassEncoderSetBindGroup(render_pass, 0, bindgroup, 0, (uint*)0);
      wgpuRenderPassEncoderSetVertexBuffer(render_pass, 0, vbuffer, 0, WGPU_WHOLE_SIZE);
      wgpuRenderPassEncoderSetIndexBuffer(render_pass, ibuffer, WGPUIndexFormat.Uint16, 0, WGPU_WHOLE_SIZE);
      wgpuRenderPassEncoderDrawIndexed(render_pass, 6, 1, 0, 0, 0);

      // end render pass
      wgpuRenderPassEncoderEnd(render_pass);

      // create command buffer
      WGPUCommandBuffer cmd_buffer = wgpuCommandEncoderFinish(cmd_encoder, null); // after 'end render pass'

      // submit commands    
      wgpuQueueSubmit(queue, 1, &cmd_buffer);

      // release all
      wgpuRenderPassEncoderRelease(render_pass);
      wgpuCommandEncoderRelease(cmd_encoder);
      wgpuCommandBufferRelease(cmd_buffer);
      wgpuTextureViewRelease(back_buffer);
    }
  }
}
