using System;
using System.Net;
using System.Net.Sockets;

namespace Vostok.ZooKeeper.LocalEnsemble
{
    internal static class FreeTcpPortFinder
    {
        private static int port = 43333;

        public static int GetFreePort()
        {
            for (int times = 0; times < 1000; port++, times++)
            {
                var tcpListener = new TcpListener(IPAddress.Loopback, port);
                try
                {
                    tcpListener.Start();
                    return port++;
                }
                finally
                {
                    tcpListener.Stop();
                }
            }
            throw new Exception("Free port not found.");
        }
    }
}