using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using RandomFractal.Text;
using System.Runtime.InteropServices;

namespace RandomFractal
{
  internal class Window : GameWindow
  {

    private const int N = 10_000_000;
    private Shader Shader;
    private float[] Vertices = new float[N * 3];
    private int Vbo;
    private int Vao;

    private Vector2 Center = new Vector2(0f, 0f);
    private float Scale = 1f;


    private Vector2 View => new Vector2((float)Size.Y / Size.X, 1.0f) / Scale;
    private static Vector2 InvertMouseX = new Vector2(-1.0f, 1.0f);
    private static Vector2 One = new Vector2(1.0f, 1.0f);
    private Vector2 WorldMousePos => Center + (MouseState.Position / Size * 2.0f - One) / View * InvertMouseX;

    int prevCount = 3;
    int nextCount = 3;
    private int prevNotPrevCount = 0;
    private int nextNotPrevPrevCount = 0;
    int prevDiv = 3;
    int nextDiv = 3;

    private TextRenderer TextRenderer;

    public Window(
  GameWindowSettings gameWindowSettings,
  NativeWindowSettings nativeWindowSettings) :
  base(gameWindowSettings, nativeWindowSettings)
    {
      Shader = new Shader(
        "RandomFractal.Shaders.shader.vert",
        "RandomFractal.Shaders.shader.frag");

      Vbo = GL.GenBuffer();
      GL.BindBuffer(BufferTarget.ArrayBuffer, Vbo);
      GenerateVertices(prevCount, prevNotPrevCount, prevDiv);

      Vao = GL.GenVertexArray();
      GL.BindVertexArray(Vao);
      GL.VertexAttribPointer(
        0,
        2,
        VertexAttribPointerType.Float,
        false,
        3 * sizeof(float),
        0);
      GL.EnableVertexAttribArray(0);
      GL.VertexAttribPointer(
        1,
        1,
        VertexAttribPointerType.Float,
        false,
        3 * sizeof(float),
        2 * sizeof(float));
      GL.EnableVertexAttribArray(1);
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        TextRenderer = new TextRenderer();
    }

    private void GenerateVertices(int vertices, int notPrev, int div)
    {
      notPrev = Math.Min(vertices - 1, notPrev);
      var points = new Vector3[vertices];
      var prevs = new int[notPrev];
      var random = new Random();

      var angle = Math.PI * 2.0 / vertices;
      var factor = 1f - (float)(vertices) / (vertices + div);
      for (int i = 0; i < vertices; i++)
      {
        points[i] = new Vector3(1, 0, 0);
        var rotation = Matrix3.CreateRotationZ((float)angle * i);
        points[i] = rotation * points[i]
          //+ new Vector3(
          //  (float)(random.NextDouble() - 0.5) * 1.1f,
          //  (float)(random.NextDouble() - 0.5) * 1.1f,
          //  (float)(random.NextDouble() - 0.5) * 1.1f)
          ;
      }
      var prevVertex = new Vector3(
        (float)random.NextDouble(),
        (float)random.NextDouble(), 0);
      Vertices[0] = prevVertex.X;
      Vertices[1] = prevVertex.Y;
      //int i = 1;
      for (int i=2; i < N;i++)
      {
        var nextRandom = random.Next(vertices);
        if (notPrev > 0)
        {
          while (prevs.Contains(nextRandom))
            nextRandom = random.Next(vertices);
          for (int j = notPrev - 1; j > 0; j--)
          {
            prevs[j - 1] = prevs[j];
          }
          prevs[notPrev - 1] = nextRandom;
        }
        var point = points[nextRandom];
        var nextPoint = (prevVertex + point) * factor
          //* new Vector3(
          //  1f + (float)(random.NextDouble() - 0.5) * 0.1f,
          //  1f + (float)(random.NextDouble() - 0.5) * 0.1f,
          //  0f)
          ;

        Vertices[i * 3] = nextPoint.X;
        Vertices[i * 3 + 1] = nextPoint.Y;
        Vertices[i * 3 + 2] = (float)nextRandom / ((float)vertices);
        prevVertex = nextPoint;
      }

      GL.BindBuffer(
        BufferTarget.ArrayBuffer,
        Vbo);

      GL.BufferData(
        BufferTarget.ArrayBuffer,
        Vertices.Length * sizeof(float),
        Vertices,
        BufferUsageHint.StreamDraw);
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

      GL.Enable(EnableCap.PointSprite);
      GL.Enable(EnableCap.VertexProgramPointSize);
      Shader.Use();
      Shader.SetVector2(nameof(Center), Center);
      Shader.SetVector2(nameof(View), View);
      GL.BindBuffer(BufferTarget.ArrayBuffer, Vbo);
      GL.BindVertexArray(Vao);
      GL.Enable(EnableCap.Blend);
      GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
      GL.DrawArrays(PrimitiveType.Points, 0, N);
      GL.Disable(EnableCap.PointSprite);
      GL.Disable(EnableCap.VertexProgramPointSize);

      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      {
        TextRenderer.RenderText(
        $"Fps: {1.0 / e.Time:F1}\n" +
        $"Number of vertices (N): {prevCount} (Up, Down)\n" +
        $"Avoid hystory {prevNotPrevCount} (Left, Right)\n" +
        $"Distance factor (N / (N + {prevDiv}))  (-, +)\n",
        0, 0, 1, new Vector2(1.0f, 0),
        Size, new Vector3(1f, 1f, 1f));
      }
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

      if (input.IsKeyDown(Keys.Up))
      {
        nextCount++;
      }
      if (input.IsKeyDown(Keys.Down))
      {
        nextCount--;
        if (nextCount < 3) nextCount = 3;
      }

      if (input.IsKeyDown(Keys.Right))
      {
        nextNotPrevPrevCount++;
      }
      if (input.IsKeyDown(Keys.Left))
      {
        nextNotPrevPrevCount--;
        if (nextNotPrevPrevCount < 0) nextNotPrevPrevCount = 0;
      }
      if (input.IsKeyDown(Keys.KeyPadAdd))
      {
        nextDiv++;
      }
      if (input.IsKeyDown(Keys.KeyPadSubtract))
      {
        nextDiv--;
        if (nextDiv < 0) nextDiv = 0;
      }

      if (input.IsKeyDown(Keys.Space))
      {
        GenerateVertices(prevCount, prevNotPrevCount, prevDiv);
      }

      if (input.IsKeyDown(Keys.F11))
      {
        if (WindowState == WindowState.Fullscreen)
          WindowState = WindowState.Normal;
        else WindowState = WindowState.Fullscreen;
      }

      if (MouseState[0])
      {
        Center = Center + MouseState.Delta * InvertMouseX / Size / View * 2.0f;
      }

      if (MouseState.ScrollDelta.Y != 0.0f)
      {
        var delta = Scale * 0.1f * MouseState.ScrollDelta.Y;
        var mouseDistance = Center - WorldMousePos;
        Scale += delta;
        var mouseDistanceScaled = Center - WorldMousePos;
        Center = Center - (mouseDistance - mouseDistanceScaled);
      }

      if (prevCount != nextCount ||
        prevNotPrevCount != nextNotPrevPrevCount ||
        prevDiv != nextDiv)
      {
        GenerateVertices(nextCount, nextNotPrevPrevCount, nextDiv);
        prevCount = nextCount;
        prevNotPrevCount = nextNotPrevPrevCount;
        prevDiv = nextDiv;
      }
    }

    protected override void OnResize(ResizeEventArgs e)
    {
      base.OnResize(e);
      GL.Viewport(0, 0, Size.X, Size.Y);
    }

  }
}
