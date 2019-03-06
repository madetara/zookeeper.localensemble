using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Vostok.Commons.Time;
using Vostok.Logging.Abstractions;

namespace Vostok.ZooKeeper.LocalEnsemble.Misc
{
    internal class ZooKeeperHealthChecker
    {
        private readonly string host;
        private readonly int port;
        private readonly ILog log;

        public ZooKeeperHealthChecker(ILog log, string host, int port)
        {
            this.host = host;
            this.port = port;
            this.log = log.ForContext($"ZooKeeperHealthChecker[{host}:{port}]");
        }

        public bool WaitStarted(TimeSpan timeout)
        {
            log.Debug("Start waiting started.");
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                if (IsStarted())
                {
                    log.Debug($"End waiting started. Started after {sw.Elapsed}.");
                    return true;
                }

                Thread.Sleep(0.5.Seconds());
            }
            log.Debug($"End waiting started. Not started after {timeout}.");
            return false;
        }

        private bool IsStarted()
        {
            var result = SendFourLetterWord("ruok");
            return result == "imok";
        }

        private string SendFourLetterWord(string word)
        {
            log.Debug($"Sending `{word}` command to {host}:{port}.");
            try
            {
                using (var client = new TcpClient(host, port))
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream))
                {
                    var bytes = Encoding.UTF8.GetBytes(word);
                    stream.Write(bytes, 0, bytes.Length);
                    var result = reader.ReadToEnd();
                    log.Debug($"Command `{word}` responce is {result}.");
                    return result;
                }
            }
            catch (Exception e)
            {
                log.Debug(e, $"Server {host}:{port} is not available.");
                return null;
            }
        }
    }
}