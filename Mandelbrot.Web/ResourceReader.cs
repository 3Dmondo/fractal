using System.IO;
using System.Reflection;

namespace Mandelbrot.Web
{
    /// <summary>
    /// Utility for reading embedded resources from the Mandelbrot.Web assembly.
    /// </summary>
    internal static class ResourceReader
    {
        /// <summary>
        /// Reads the entire contents of an embedded resource as a string.
        /// </summary>
        /// <param name="name">The full resource name (including namespace and folders).</param>
        /// <returns>The resource contents as a string.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the resource is not found.</exception>
        public static string ReadString(string name)
        {
            var assembly = typeof(ResourceReader).Assembly;
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream == null) throw new FileNotFoundException($"Resource not found: {name}");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        /// <summary>
        /// Gets a stream for an embedded resource.
        /// </summary>
        /// <param name="name">The full resource name (including namespace and folders).</param>
        /// <returns>A stream for the resource.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the resource is not found.</exception>
        public static Stream GetStream(string name)
        {
            var assembly = typeof(ResourceReader).Assembly;
            var stream = assembly.GetManifestResourceStream(name);
            if (stream == null) throw new FileNotFoundException($"Resource not found: {name}");
            return stream;
        }
    }
}
