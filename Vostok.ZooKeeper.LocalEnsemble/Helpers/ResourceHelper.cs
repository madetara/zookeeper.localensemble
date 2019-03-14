using System;
using System.IO;

namespace Vostok.ZooKeeper.LocalEnsemble.Helpers
{
    // (kungurtsev) hack from https://github.com/Microsoft/msbuild/issues/2221#issuecomment-443439489
    internal static class ResourceHelper
    {
        public static byte[] GetBytes<T>(string name)
        {
            var assembly = typeof(T).Assembly;
            using (var stream = assembly.GetManifestResourceStream(name))
            {
                if (stream == null)
                {
                    throw new Exception(
                        $"Resource {name} not found in {assembly.FullName}.  Valid resources are: {string.Join(", ", assembly.GetManifestResourceNames())}.");
                }

                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }
    }
}