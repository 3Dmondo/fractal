using Evergine.Bindings.WebGPU;
using static Evergine.Bindings.WebGPU.WebGPUNative;

namespace Mandelbrot.Web;

/// <summary>
/// Helper class for building WebGPU pipeline and related layouts.
/// </summary>
public static class WebGpuPipelineBuilder
{
  /// <summary>
  /// Creates a bind group layout for a single uniform buffer.
  /// </summary>
  public static unsafe WGPUBindGroupLayout CreateBindGroupLayout(WGPUDevice device)
  {
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
    var desc = new WGPUBindGroupLayoutDescriptor() {
      entryCount = 1,
      entries = entries,
    };
    return wgpuDeviceCreateBindGroupLayout(device, &desc);
  }

  /// <summary>
  /// Creates a pipeline layout from a bind group layout.
  /// </summary>
  public static unsafe WGPUPipelineLayout CreatePipelineLayout(WGPUDevice device, WGPUBindGroupLayout bindGroupLayout)
  {
    var desc = new WGPUPipelineLayoutDescriptor() {
      bindGroupLayoutCount = 1,
      bindGroupLayouts = &bindGroupLayout,
    };
    return wgpuDeviceCreatePipelineLayout(device, &desc);
  }

  /// <summary>
  /// Creates a render pipeline for Mandelbrot rendering.
  /// </summary>
  public static unsafe WGPURenderPipeline CreateRenderPipeline(
      WGPUDevice device,
      WGPUPipelineLayout pipelineLayout,
      WGPUShaderModule shaderModule,
      WGPUVertexBufferLayout* vertexBufferLayout,
      WGPUColorTargetState* targetState)
  {
    var fragmentState = new WGPUFragmentState() {
      module = shaderModule,
      entryPoint = "fs_main".ToPointer(),
      targetCount = 1,
      targets = targetState,
    };
    var renderPipelineDescriptor = new WGPURenderPipelineDescriptor() {
      layout = pipelineLayout,
      vertex = new WGPUVertexState() {
        module = shaderModule,
        entryPoint = "vs_main".ToPointer(),
        bufferCount = 1,
        buffers = vertexBufferLayout,
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
    return wgpuDeviceCreateRenderPipeline(device, &renderPipelineDescriptor);
  }
}
