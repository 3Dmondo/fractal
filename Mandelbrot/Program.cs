using System.Drawing;
using Mandelbrot;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

int size = 48;
using var bmp = new Bitmap(size, size);
for (int y = 0; y < size; y++) {
  for (int x = 0; x < size; x++) {
    double cx = -0.7 + (x - size / 2.0) * 3.0 / size;
    double cy = (y - size / 2.0) * 3.0 / size;
    cx *= 0.7;
    cy *= 0.7;
    double zx = 0, zy = 0;
    int i = 0, maxIter = 32;
    while (zx * zx + zy * zy < 4.0 && i < maxIter) {
      double tmp = zx * zx - zy * zy + cx;
      zy = 2 * zx * zy + cy;
      zx = tmp;
      i++;
    }

    Color color = i == maxIter
        ? Color.Black
        : Color.FromArgb(0, 0, 0, 0);
    bmp.SetPixel(x, y, color);
  }
}
bmp.Save("favicon.png");


//var nativeWindowSettings = new NativeWindowSettings()
//{
//  Size = new Vector2i(800, 600),
//  Title = "Mandelbrot",
//  Flags = ContextFlags.ForwardCompatible,
//};
//using var window = new Window(
//  new GameWindowSettings
//  {
//    UpdateFrequency = 0,
//    RenderFrequency = 0
//  },
//  nativeWindowSettings);
//window.Run();