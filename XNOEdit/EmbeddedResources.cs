using System.Reflection;

namespace XNOEdit
{
    public static class EmbeddedResources
    {
        private static readonly Assembly ResourceAssembly;

        static EmbeddedResources()
        {
            ResourceAssembly = Assembly.GetAssembly(typeof(EmbeddedResources))!;
        }

        public static string ReadAllText(string filename)
        {
            var (assembly, path) = ResolveManifestPath(filename);

            return ReadAllText(assembly, path);
        }

        public static string ReadAllText(Assembly assembly, string filename)
        {
            using var stream = GetStream(assembly, filename);
            if (stream == null)
            {
                throw new FileNotFoundException($"{filename} in {assembly}");
            }

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public static Stream GetStream(Assembly assembly, string filename)
        {
            var assemblyName = assembly.GetName().Name;
            var manifestUri = assemblyName + "." + filename.Replace('/', '.');

            var stream = assembly.GetManifestResourceStream(manifestUri);
            return stream!;
        }

        private static (Assembly, string) ResolveManifestPath(string filename)
        {
            var segments = filename.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length >= 2)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name == segments[0])
                    {
                        return (assembly, segments[1]);
                    }
                }
            }

            return (ResourceAssembly, filename);
        }
    }
}
