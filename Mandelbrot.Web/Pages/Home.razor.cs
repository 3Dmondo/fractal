using Evergine.Bindings.WebGPU;
using static Evergine.Bindings.WebGPU.WebGPUNative;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Mandelbrot.Web.Pages;

public partial class Home
{
  [Inject]
  private IJSRuntime JS { get; set; } = default!;

  private static Home? _instance;

  // Interaction state
  private bool isPointerDown = false;
  private float lastPointerX, lastPointerY;
  private Dictionary<int, (float x, float y)> pointers = new();

  // WebGPU state
  private unsafe WGPUBuffer ubuffer;
  private unsafe WGPUDevice device;
  private unsafe WGPUQueue queue;
  private unsafe WGPUSwapChain swapChain;
  private unsafe WGPURenderPipeline pipeline;
  private unsafe WGPUBindGroup bindgroup;
  private unsafe WGPUBuffer vbuffer;
  private unsafe WGPUBuffer ibuffer;

  private WebGpuViewState? viewState;

  public Home() { _instance = this; }

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    if (firstRender) {
      try {
        await JS.InvokeVoidAsync("initWebGPU");
        await JS.InvokeVoidAsync("setupPinchHandler");
      } catch {}
      Run();
      await JS.InvokeVoidAsync("eval", "window.addEventListener('resize', () => DotNet.invokeMethodAsync('Mandelbrot.Web', 'OnCanvasResize')); ");
    }
  }

  [JSInvokable]
  public static void OnCanvasResize()
  {
    _instance?.Run();
  }

  [JSInvokable]
  public static Task OnPinch(float pinchCenterX, float pinchCenterY, float scaleDelta)
  {
    if (_instance?.viewState == null) return Task.CompletedTask;
    var newScale = _instance.viewState.Scale * scaleDelta;
    if (newScale < 0.0001f || newScale > 2.0f) return Task.CompletedTask;
    float aspect = (float)_instance.viewState.CanvasHeight / (float)_instance.viewState.CanvasWidth;
    float viewX = aspect / _instance.viewState.Scale;
    float viewY = 1.0f / _instance.viewState.Scale;
    float normX = pinchCenterX / (float)_instance.viewState.CanvasWidth * 2.0f - 1.0f;
    float normY = pinchCenterY / (float)_instance.viewState.CanvasHeight * 2.0f - 1.0f;
    normX *= -1.0f;
    float worldMouseX = _instance.viewState.CenterX + normX / viewX;
    float worldMouseY = _instance.viewState.CenterY + normY / viewY;
    _instance.viewState.UpdateScale(newScale);
    float newViewX = aspect / _instance.viewState.Scale;
    float newViewY = 1.0f / _instance.viewState.Scale;
    float newWorldMouseX = _instance.viewState.CenterX + normX / newViewX;
    float newWorldMouseY = _instance.viewState.CenterY + normY / newViewY;
    _instance.viewState.UpdateCenter(_instance.viewState.CenterX + (worldMouseX - newWorldMouseX), _instance.viewState.CenterY + (worldMouseY - newWorldMouseY));
    _instance.viewState.WriteToBuffer();
    _instance.Redraw();
    return Task.CompletedTask;
  }

  public unsafe void Run()
  {
    device = emscripten_webgpu_get_device();
    queue = wgpuDeviceGetQueue(device);
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
    swapChain = wgpuDeviceCreateSwapChain(device, surface, &swapChainDescriptor);

    // Shader and pipeline setup
    var triangleWGSL = @"
// Mandelbrot WGSL (single-precision) with uniform view params

struct VertexIn {
    @location(0) aPos : vec2<f32>,
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
    output.Position = vec4<f32>(input.aPos, 0.0, 1.0);
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
      }
    };
    WGPUVertexBufferLayout vertex_buffer_layout = new WGPUVertexBufferLayout {
      arrayStride = 2 * sizeof(float),
      attributeCount = 1,
      attributes = vertex_attrib,
    };
    var entries = stackalloc WGPUBindGroupLayoutEntry[]
    {
      new WGPUBindGroupLayoutEntry()
      {
        binding = 0,
        visibility = (WGPUShaderStage)(WGPUShaderStage.Vertex | WGPUShaderStage.Fragment),
        buffer = new WGPUBufferBindingLayout()
        {
          type = WGPUBufferBindingType.Uniform,
        },
      },
    };
    var bindGroupLayoutDescriptor = new WGPUBindGroupLayoutDescriptor() {
      entryCount = 1,
      entries = entries,
    };
    var bindgroup_layout = wgpuDeviceCreateBindGroupLayout(device, &bindGroupLayoutDescriptor);
    var pipelineLayoutDescriptor = new WGPUPipelineLayoutDescriptor() {
      bindGroupLayoutCount = 1,
      bindGroupLayouts = &bindgroup_layout,
    };
    var pipeline_layout = wgpuDeviceCreatePipelineLayout(device, &pipelineLayoutDescriptor);
    var blendState = new WGPUBlendState() {
      color = new WGPUBlendComponent() {
        operation = WGPUBlendOperation.Add,
        srcFactor = WGPUBlendFactor.One,
        dstFactor = WGPUBlendFactor.One,
      },
      alpha = new WGPUBlendComponent() {
        operation = WGPUBlendOperation.Add,
        srcFactor = WGPUBlendFactor.One,
        dstFactor = WGPUBlendFactor.One,
      },
    };
    var targetState = new WGPUColorTargetState() {
      format = WGPUTextureFormat.BGRA8Unorm,
      writeMask = WGPUColorWriteMask.All,
      blend = &blendState,
    };
    var fragmentState = new WGPUFragmentState() {
      module = shader_triangle,
      entryPoint = "fs_main".ToPointer(),
      targetCount = 1,
      targets = &targetState,
    };
    var renderPipelineDescriptor = new WGPURenderPipelineDescriptor() {
      layout = pipeline_layout,
      vertex = new WGPUVertexState() {
        module = shader_triangle,
        entryPoint = "vs_main".ToPointer(),
        bufferCount = 1,
        buffers = &vertex_buffer_layout,
      },
      primitive = new WGPUPrimitiveState() {
        frontFace = WGPUFrontFace.CCW,
        cullMode = WGPUCullMode.None,
        topology = WGPUPrimitiveTopology.TriangleList,
        stripIndexFormat = WGPUIndexFormat.Undefined,
      },
      fragment = &fragmentState,
      multisample = new WGPUMultisampleState() {
        count = 1,
        mask = 0xFFFFFFFF,
        alphaToCoverageEnabled = false,
      },
      depthStencil = null,
    };
    pipeline = wgpuDeviceCreateRenderPipeline(device, &renderPipelineDescriptor);
    wgpuPipelineLayoutRelease(pipeline_layout);
    wgpuShaderModuleRelease(shader_triangle);

    var vertex_data = stackalloc float[]
    {
      -1.0f, -1.0f,
       1.0f, -1.0f,
       1.0f,  1.0f,
      -1.0f,  1.0f,
    };
    var index_data = stackalloc ushort[]
    {
      0, 1, 2,
      0, 2, 3,
    };
    vbuffer = create_buffer(vertex_data, 4 * 2 * sizeof(float), WGPUBufferUsage.Vertex, device, queue);
    ibuffer = create_buffer(index_data, 6 * sizeof(ushort), WGPUBufferUsage.Index, device, queue);
    // Uniform buffer creation (zeroed data)
    byte* ubData = stackalloc byte[24];
    ubuffer = create_buffer(ubData, 24u, WGPUBufferUsage.Uniform, device, queue);
    viewState = new WebGpuViewState(width, height, ubuffer, queue);
    viewState.CenterX = 0.5f;
    viewState.CenterY = 0.0f;
    viewState.Scale = 1.0f;
    viewState.MaxIter = 400;
    viewState.WriteToBuffer();
    var bindGroupEntry = new WGPUBindGroupEntry() {
      binding = 0,
      offset = 0,
      buffer = ubuffer,
      size = 24u,
    };
    var bindGroupDescriptor = new WGPUBindGroupDescriptor() {
      layout = bindgroup_layout,
      entryCount = 1,
      entries = &bindGroupEntry,
    };
    bindgroup = wgpuDeviceCreateBindGroup(device, &bindGroupDescriptor);
    wgpuBindGroupLayoutRelease(bindgroup_layout);
    draw(swapChain, device, queue, pipeline, bindgroup, vbuffer, ibuffer);
  }

  // Pointer event handlers (mouse/touch/pan)
  public void OnPointerDown(PointerEventArgs e)
  {
    isPointerDown = true;
    lastPointerX = (float)e.ClientX;
    lastPointerY = (float)e.ClientY;
    pointers[(int)e.PointerId] = ((float)e.ClientX, (float)e.ClientY);
  }

  public void OnPointerMove(PointerEventArgs e)
  {
    if (!pointers.ContainsKey((int)e.PointerId) || viewState == null) return;
    pointers[(int)e.PointerId] = ((float)e.ClientX, (float)e.ClientY);
    if (pointers.Count == 1 && isPointerDown) {
      float dx = (float)e.ClientX - lastPointerX;
      float dy = (float)e.ClientY - lastPointerY;
      lastPointerX = (float)e.ClientX;
      lastPointerY = (float)e.ClientY;
      float aspect = (float)viewState.CanvasHeight / (float)viewState.CanvasWidth;
      float viewX = aspect / viewState.Scale;
      float viewY = 1.0f / viewState.Scale;
      float newCenterX = viewState.CenterX - (-1.0f * dx / (float)viewState.CanvasWidth / viewX * 2.0f);
      float newCenterY = viewState.CenterY - (dy / (float)viewState.CanvasHeight / viewY * 2.0f);
      viewState.UpdateCenter(newCenterX, newCenterY);
      viewState.WriteToBuffer();
      Redraw();
    }
  }

  public void OnPointerUp(PointerEventArgs e)
  {
    isPointerDown = false;
    pointers.Remove((int)e.PointerId);
  }

  public void OnWheel(WheelEventArgs e)
  {
    if (viewState == null) return;
    float delta = viewState.Scale * 0.1f * (float)(-e.DeltaY / 100.0f);
    var newScale = viewState.Scale + delta;
    if (newScale < 0.0001f || newScale > 2.0f) return;
    float mouseX = (float)e.ClientX;
    float mouseY = (float)e.ClientY;
    float aspect = (float)viewState.CanvasHeight / (float)viewState.CanvasWidth;
    float viewX = aspect / viewState.Scale;
    float viewY = 1.0f / viewState.Scale;
    float normX = mouseX / (float)viewState.CanvasWidth * 2.0f - 1.0f;
    float normY = mouseY / (float)viewState.CanvasHeight * 2.0f - 1.0f;
    normX *= -1.0f;
    float worldMouseX = viewState.CenterX + normX / viewX;
    float worldMouseY = viewState.CenterY + normY / viewY;
    viewState.UpdateScale(newScale);
    float newViewX = aspect / viewState.Scale;
    float newViewY = 1.0f / viewState.Scale;
    float newWorldMouseX = viewState.CenterX + normX / newViewX;
    float newWorldMouseY = viewState.CenterY + normY / newViewY;
    viewState.UpdateCenter(viewState.CenterX + (worldMouseX - newWorldMouseX), viewState.CenterY + (worldMouseY - newWorldMouseY));
    viewState.WriteToBuffer();
    Redraw();
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
        r = 0.0f,
        g = 0.0f,
        b = 0.0f,
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

  private unsafe void Redraw()
  {
    draw(swapChain, device, queue, pipeline, bindgroup, vbuffer, ibuffer);
  }
}
