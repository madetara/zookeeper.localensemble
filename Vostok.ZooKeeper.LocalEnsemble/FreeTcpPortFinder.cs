using System.Net;
using System.Net.Sockets;

namespace Vostok.ZooKeeper.LocalEnsemble
{
    internal static class FreeTcpPortFinder
    {
        public static int GetFreePort()
        {
            var tcpListener = new TcpListener(IPAddress.Loopback, 0);
            try
            {
                tcpListener.Start();
                return ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            }
            finally
            {
                tcpListener.Stop();
            }
        }
    }
}