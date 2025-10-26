using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Mandelbrot
{
  internal class Window : GameWindow
  {
    private int Vbo;
    private int Vao;

    private Vector2d Center = new Vector2d(0.5, 0);
    private double Scale = 1;
    private Shader Shader;

    private new float AspectRatio => (float)Size.Y / Size.X;
    private Vector2d View => new Vector2d(AspectRatio, 1.0) / Scale;
    private static Vector2 InvertMouseX = new Vector2(-1.0f, 1.0f);
    private static Vector2 One = new Vector2(1.0f, 1.0f);
    private Vector2d WorldMousePos => Center + (MouseState.Position / Size * 2.0f - One) / View * InvertMouseX;

    public Window(
      GameWindowSettings gameWindowSettings,
      NativeWindowSettings nativeWindowSettings) :
      base(gameWindowSettings, nativeWindowSettings)
    {
      Shader = new Shader(
        "Mandelbrot.Shaders.shader.vert",
        "Mandelbrot.Shaders.shader.frag");
      Vbo = GL.GenBuffer();
      GL.BindBuffer(BufferTarget.ArrayBuffer, Vbo);
      GL.BufferData(
        BufferTarget.ArrayBuffer,
        8 * sizeof(float),
        new float[8],
        BufferUsageHint.StaticDraw);
      Vao = GL.GenVertexArray();
      GL.BindVertexArray(Vao);
      GL.EnableVertexAttribArray(0);
      GL.VertexAttribPointer(
        0,
        2,
        VertexAttribPointerType.Float,
        false,
        2 * sizeof(float),
        0);
    }
    protected override void OnLoad()
    {
      base.OnLoad();
      GL.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
      GL.Clear(ClearBufferMask.DepthBufferBit);
    }

    protected override void OnRenderFrame(FrameEventArgs e)
    {
      base.OnRenderFrame(e);
      GL.Clear(ClearBufferMask.ColorBufferBit);

      Shader.Use();
      Shader.SetVector2d(nameof(Center), Center);
      Shader.SetVector2d(nameof(View), View);
      //Shader.SetFloat(nameof(AspectRatio), AspectRatio);
      GL.BindVertexArray(Vao);

      GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4);

      //if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      //{
      //  TextRenderer.RenderText(
      //  $"Fps: {1.0 / e.Time:F1}\n" +
      //  $"Number of vertices (N): {prevCount} (Up, Down)\n" +
      //  $"Avoid hystory {prevNotPrevCount} (Left, Right)\n" +
      //  $"Distance factor (N / (N + {prevDiv}))  (-, +)\n",
      //  0, 0, 1, new Vector2(1.0f, 0),
      //  Size, new Vector3(1f, 1f, 1f));
      //}
      SwapBuffers();
    }

    protected override void OnUpdateFrame(FrameEventArgs e)
    {
      base.OnUpdateFrame(e);

      var input = KeyboardState;

      if (input.IsKeyDown(Keys.Escape))
      {
        Close();
      }

      if (input.IsKeyDown(Keys.F11))
      {
        if (WindowState == WindowState.Fullscreen)
          WindowState = WindowState.Normal;
        else WindowState = WindowState.Fullscreen;
      }

      if (MouseState[0])
      {
        Center = Center - MouseState.Delta * InvertMouseX / Size / View * 2f;
      }

      if (MouseState.ScrollDelta.Y != 0.0f)
      {
        var delta = Scale * 0.1f * MouseState.ScrollDelta.Y;
        var mouseDistance = Center - WorldMousePos;
        Scale += delta;
        var mouseDistanceScaled = Center - WorldMousePos;
        Center = Center - (mouseDistance - mouseDistanceScaled);
      }

    }

    protected override void OnResize(ResizeEventArgs e)
    {
      base.OnResize(e);
      GL.Viewport(0, 0, Size.X, Size.Y);
    }
  }
}
