using System;

namespace Vostok.ZooKeeper.LocalEnsemble.Helpers
{
    internal static class OsHelper
    {
        public static bool IsUnix => Environment.OSVersion.Platform == PlatformID.Unix;
        public static string PathDelimiter => IsUnix ? ":" : ";";
    }
}