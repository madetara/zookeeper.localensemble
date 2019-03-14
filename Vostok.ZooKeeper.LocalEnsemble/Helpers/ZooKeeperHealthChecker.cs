using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Vostok.Commons.Time;
using Vostok.Logging.Abstractions;

namespace Vostok.ZooKeeper.LocalEnsemble.Helpers
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
            this.log = log;
        }

        public bool WaitStarted(TimeSpan timeout)
        {
            log.Debug("Waiting for the instance to start..");

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                if (IsStarted())
                {
                    log.Debug($"Instance has successfully started in {sw.Elapsed.TotalSeconds:0.##} second(s).");
                    return true;
                }

                Thread.Sleep(0.5.Seconds());
            }

            log.Warn($"Instance hasn't started in {timeout}.");
            return false;
        }

        private bool IsStarted()
            => SendFourLetterWord("ruok") == "imok";

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

                    log.Debug($"Response to `{word}` command is '{result}'.");
                    return result;
                }
            }
            catch (Exception error)
            {
                log.Error(error, $"Server {host}:{port} is not available.");
                return null;
            }
        }
    }
}