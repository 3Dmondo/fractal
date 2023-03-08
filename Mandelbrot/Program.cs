using Mandelbrot;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

var nativeWindowSettings = new NativeWindowSettings()
{
  Size = new Vector2i(800, 600),
  Title = "Mandelbrot",
  Flags = ContextFlags.ForwardCompatible,
};
using var window = new Window(
  new GameWindowSettings
  {
    UpdateFrequency = 0,
    RenderFrequency = 0
  },
  nativeWindowSettings);
window.Run();