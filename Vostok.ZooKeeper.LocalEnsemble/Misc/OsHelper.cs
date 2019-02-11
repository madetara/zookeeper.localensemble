using System;

namespace Vostok.ZooKeeper.LocalEnsemble.Misc
{
    internal static class OsHelper
    {
        public static bool IsUnix => Environment.OSVersion.Platform == PlatformID.Unix;
    }
}