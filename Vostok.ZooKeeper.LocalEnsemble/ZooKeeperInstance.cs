using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Commons.Time;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.LocalEnsemble.Helpers;

namespace Vostok.ZooKeeper.LocalEnsemble
{
    /// <summary>
    /// Represents a locally run instance based of ZooKeeper service.
    /// </summary>
    [PublicAPI]
    public class ZooKeeperInstance
    {
        private readonly ILog log;
        private readonly WindowsProcessKillJob processKillJob;
        private readonly ZooKeeperHealthChecker healthChecker;
        private volatile Process process;

        public ZooKeeperInstance(int id, string baseDirectory, int clientPort, int peerPort, int electionPort, ILog log)
        {
            this.log = log.ForContext($"id {id}");

            Id = id;
            BaseDirectory = baseDirectory;
            ClientPort = clientPort;
            PeerPort = peerPort;
            ElectionPort = electionPort;
            processKillJob = OsHelper.IsUnix ? null : new WindowsProcessKillJob(this.log);
            healthChecker = new ZooKeeperHealthChecker(this.log, "localhost", clientPort);
        }

        /// <summary>
        /// Returns numeric instance id.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Returns instance client port.
        /// </summary>
        public int ClientPort { get; }

        /// <summary>
        /// Returns instance peer port.
        /// </summary>
        public int PeerPort { get; }

        /// <summary>
        /// Returns instance election port.
        /// </summary>
        public int ElectionPort { get; }

        /// <summary>
        /// Returns instance base directory.
        /// </summary>
        public string BaseDirectory { get; }

        /// <summary>
        /// Returns instance lib directory.
        /// </summary>
        public string LibDirectory => Path.Combine(BaseDirectory, "lib");

        /// <summary>
        /// Returns instance bin directory.
        /// </summary>
        public string BinDirectory => Path.Combine(BaseDirectory, "bin");

        /// <summary>
        /// Returns instance configuration directory.
        /// </summary>
        public string ConfDirectory => Path.Combine(BaseDirectory, "conf");

        /// <summary>
        /// Returns instance data directory.
        /// </summary>
        public string DataDirectory => Path.Combine(BinDirectory, "data");

        /// <summary>
        /// Returns whether this instance is currently running.
        /// </summary>
        public bool IsRunning => process?.HasExited == false;

        /// <summary>
        /// <para>Starts instance.</para>
        /// </summary>
        public void Start()
        {
            if (IsRunning)
                return;

            var processStartInfo = new ProcessStartInfo("java")
            {
                Arguments = BuildZooKeeperArguments(),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = BinDirectory
            };

            process = new Process
            {
                StartInfo = processStartInfo
            };

            if (!process.Start())
                throw new Exception($"Failed to start process of instance '{Id}'.");

            Task.Run(
                async () =>
                {
                    while (!process.StandardError.EndOfStream)
                        log.Error(await process.StandardError.ReadLineAsync().ConfigureAwait(false));
                });

            if (!healthChecker.WaitStarted(20.Seconds()))
                throw new Exception($"instance '{Id}' has not warmed up in 20 seconds..");

            processKillJob?.AddProcess(process);
        }

        public void Stop()
        {
            if (IsRunning)
            {
                log.Debug("Stopping..");

                try
                {
                    process.Kill();
                    process.WaitForExit();
                }
                catch
                {
                    // ignored
                }
            }

            process = null;
        }

        public override string ToString()
            => $"localhost:{ClientPort}:{PeerPort}:{ElectionPort} (id {Id}) at '{BaseDirectory}'";

        private string BuildZooKeeperArguments()
        {
            var classPaths = new[]
            {
                Path.Combine(BaseDirectory, "*"),
                Path.Combine(LibDirectory, "*"),
                ConfDirectory
            };
            var joinedClassPaths = string.Join(OsHelper.PathDelimiter, classPaths);

            var result = $"\"-Dzookeeper.log.dir={BaseDirectory}\" \"-Dzookeeper.root.logger=DEBUG,ROLLINGFILE\" -cp \"{joinedClassPaths}\" org.apache.zookeeper.server.quorum.QuorumPeerMain \"{Path.Combine(ConfDirectory, "zoo.cfg")}\"";

            return result;
        }
    }
}