using Evergine.Bindings.WebGPU;
using static Evergine.Bindings.WebGPU.WebGPUNative;

namespace Mandelbrot.Web;

public class WebGpuViewState
{
  public float CenterX { get; set; } = 0.5f;
  public float CenterY { get; set; } = 0.0f;
  public float Scale { get; set; } = 1.0f;
  public double CanvasWidth { get; private set; }
  public double CanvasHeight { get; private set; }
  public int MaxIter { get; set; } = 400;
  private WGPUBuffer _ubuffer;
  private WGPUQueue _queue;

  public WebGpuViewState(double canvasWidth, double canvasHeight, WGPUBuffer ubuffer, WGPUQueue queue)
  {
    CanvasWidth = canvasWidth;
    CanvasHeight = canvasHeight;
    _ubuffer = ubuffer;
    _queue = queue;
  }

  public void UpdateCanvasSize(double width, double height)
  {
    CanvasWidth = width;
    CanvasHeight = height;
  }

  public void UpdateCenter(float x, float y)
  {
    CenterX = x;
    CenterY = y;
  }

  public void UpdateScale(float scale)
  {
    Scale = scale;
  }

  public unsafe void WriteToBuffer()
  {
    float viewX = (float)CanvasHeight / (float)CanvasWidth / Scale;
    float viewY = 1.0f / Scale;
    byte* ubData = stackalloc byte[24];
    float* fptr = (float*)ubData;
    fptr[0] = CenterX;
    fptr[1] = CenterY;
    fptr[2] = viewX;
    fptr[3] = viewY;
    int* iptr = (int*)(ubData + 16);
    iptr[0] = MaxIter;
    iptr[1] = 0;
    wgpuQueueWriteBuffer(_queue, _ubuffer, 0u, ubData, 24u);
  }
}
