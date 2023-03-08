namespace Mandelbrot
{
  internal static class ResourceReader
  {
    public static string ReadString(string name)
    {
      var assembly = typeof(ResourceReader).Assembly;
      using var stream = assembly.GetManifestResourceStream(name);
      using var reader = new StreamReader(stream);
      return reader.ReadToEnd();
    }

    public static Stream GetStream(string name)
    {
      var assembly = typeof(ResourceReader).Assembly;
      return assembly.GetManifestResourceStream(name);  
    }
  }
}
