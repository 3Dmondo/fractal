using System.Drawing;
using System.Drawing.Text;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace RandomFractal.Text
{
  internal class TextRenderer
  {
    const int size = 16;

    private Dictionary<uint, Character> _characters = new Dictionary<uint, Character>();
    private int _vao;
    private int _vbo;
    private Font mono = new Font(FontFamily.GenericMonospace, size);
    private Shader TextShader;

    public float CharWithd => mono.Size;
    public float CharHeight => mono.Height;

    public TextRenderer()
    {
      TextShader = new Shader(
        "RandomFractal.Shaders.TextShader.vert",
        "RandomFractal.Shaders.TextShader.frag");

      for (byte c = 32; c < 127; c++) {
        var character = ((char)c).ToString();
        var gfx = Graphics.FromImage(new Bitmap(1, 1));
        var charSize = gfx.MeasureString(character, mono);
        var bitmap = new Bitmap((int)charSize.Width, (int)charSize.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        gfx = Graphics.FromImage(bitmap);
        gfx.TextRenderingHint = TextRenderingHint.AntiAlias;
        gfx.DrawString(character, mono, Brushes.White, new PointF());

        var data = bitmap.LockBits(new Rectangle(0, 0, (int)charSize.Width, (int)charSize.Height),
                        System.Drawing.Imaging.ImageLockMode.ReadOnly,
                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        int texObj = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, texObj);
        GL.TexImage2D(TextureTarget.Texture2D, 0,
                      PixelInternalFormat.Rgba, bitmap.Width, bitmap.Height, 0,
                      PixelFormat.Rgba, PixelType.UnsignedByte, data.Scan0);

        GL.TextureParameter(texObj, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TextureParameter(texObj, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TextureParameter(texObj, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TextureParameter(texObj, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        // add character
        Character ch = new Character();
        ch.TextureID = texObj;
        ch.Size = new Vector2(bitmap.Width, bitmap.Height);
        ch.Bearing = new Vector2(0, 0);
        ch.Advance = size;
        _characters.Add(c, ch);
  }

  float[] vquad =
      {
        // x      y      u     v    
        0.0f, -1.0f,   0.0f, 0.0f,
        0.0f,  0.0f,   0.0f, 1.0f,
        1.0f,  0.0f,   1.0f, 1.0f,
        0.0f, -1.0f,   0.0f, 0.0f,
        1.0f,  0.0f,   1.0f, 1.0f,
        1.0f, -1.0f,   1.0f, 0.0f
      };

      // Create [Vertex Buffer Object](https://www.khronos.org/opengl/wiki/Vertex_Specification#Vertex_Buffer_Object)
      _vbo = GL.GenBuffer();
      GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
      GL.BufferData(BufferTarget.ArrayBuffer, 4 * 6 * 4, vquad, BufferUsageHint.StaticDraw);

      // [Vertex Array Object](https://www.khronos.org/opengl/wiki/Vertex_Specification#Vertex_Array_Object)
      _vao = GL.GenVertexArray();
      GL.BindVertexArray(_vao);
      GL.EnableVertexAttribArray(0);
      GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * 4, 0);
      GL.EnableVertexAttribArray(1);
      GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * 4, 2 * 4);

      GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
      GL.BindVertexArray(0);
    }

    public float RenderText(
      string text,
      float x,
      float y,
      float scale,
      Vector2 dir,
      Vector2i windowSize,
      Vector3 color)
    {
      TextShader.Use();
      
      var projectionM = Matrix4.CreateOrthographicOffCenter(0.0f, windowSize.X, windowSize.Y, 0.0f, -1.0f, 1.0f);
      GL.UniformMatrix4(1, false, ref projectionM);
      GL.Uniform3(2, color);


      GL.ActiveTexture(TextureUnit.Texture0);
      GL.BindVertexArray(_vao);

      float angle_rad = (float)Math.Atan2(dir.Y, dir.X);
      Matrix4 rotateM = Matrix4.CreateRotationZ(angle_rad);
      Matrix4 transOriginM = Matrix4.CreateTranslation(new Vector3(x, y, 0f));

      // Iterate through all characters
      float char_x = 0.0f;
      float char_y = 0.0f;
      foreach (var c in text) {
        if (c == '\n') {
          char_y += CharHeight;
          char_x = 0f;
        }
          
        if (_characters.ContainsKey(c) == false)
          continue;
        Character ch = _characters[c];

        float w = ch.Size.X * scale;
        float h = ch.Size.Y * scale;
        float xrel = char_x + ch.Bearing.X * scale;
        float yrel = char_y + (ch.Size.Y - ch.Bearing.Y) * scale;

        // Now advance cursors for next glyph (note that advance is number of 1/64 pixels)
        char_x += ch.Advance * scale; // Bitshift by 6 to get value in pixels (2^6 = 64 (divide amount of 1/64th pixels by 64 to get amount of pixels))

        Matrix4 scaleM = Matrix4.CreateScale(new Vector3(w, h, 1.0f));
        Matrix4 transRelM = Matrix4.CreateTranslation(new Vector3(xrel, yrel, 0.0f));

        Matrix4 modelM = scaleM * transRelM * rotateM * transOriginM; // OpenTK `*`-operator is reversed
        GL.UniformMatrix4(0, false, ref modelM);

        // Render glyph texture over quad
        GL.BindTexture(TextureTarget.Texture2D, ch.TextureID);

        // Render quad
        GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
      }

      GL.BindVertexArray(0);
      GL.BindTexture(TextureTarget.Texture2D, 0);

      return char_y + CharHeight;
    }
  }
}
