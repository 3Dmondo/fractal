namespace Mandelbrot.Web
{
    internal static class ResourceReader
    {
        public static string ReadString(string name)
        {
            var assembly = typeof(ResourceReader).Assembly;
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream == null) throw new FileNotFoundException($"Resource not found: {name}");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public static Stream GetStream(string name)
        {
            var assembly = typeof(ResourceReader).Assembly;
            var stream = assembly.GetManifestResourceStream(name);
            if (stream == null) throw new FileNotFoundException($"Resource not found: {name}");
            return stream;
        }
    }
}
