using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.LocalEnsemble.Misc;

namespace Vostok.ZooKeeper.LocalEnsemble
{
    /// <summary>
    /// Represents ZooKeeper instance based on java implementation.
    /// </summary>
    [PublicAPI]
    public class ZooKeeperInstance
    {
        private readonly WindowsProcessKillJob processKillJob;

        private Process process;

        /// <inheritdoc cref="ZooKeeperInstance" />
        public ZooKeeperInstance(int id, string baseDirectory, int clientPort, int peerPort, int electionPort, ILog log)
        {
            Id = id;
            BaseDirectory = baseDirectory;
            ClientPort = clientPort;
            PeerPort = peerPort;
            ElectionPort = electionPort;
            processKillJob = OsHelper.IsUnix ? null : new WindowsProcessKillJob(log);
        }

        /// <summary>
        /// Returns instance id.
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
        /// Check that instance is running.
        /// </summary>
        public bool IsRunning => process?.HasExited == false;

        /// <summary>
        /// <para>Starts instance.</para>
        /// </summary>
        public void Start()
        {
                
            var processStartInfo = new ProcessStartInfo("java")
            {
                Arguments = BuildRunZooKeeperArguments(),
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
                throw new Exception($"Failed to start process of participant '{Id}'.");

            InstancesHelper.WaitAndCheckInstancesAreRunning(new List<ZooKeeperInstance> {this});
            
            processKillJob?.AddProcess(process);
        }

        /// <summary>
        /// <para>Stops instance.</para>
        /// </summary>
        public void Stop()
        {
            if (IsRunning)
            {
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

        /// <returns>String representation of ZooKeeperInstance.</returns>
        public override string ToString() => $"localhost:{ClientPort}:{PeerPort}:{ElectionPort} (id {Id}) at '{BaseDirectory}'";

        private string BuildRunZooKeeperArguments()
        {
            var classPaths = new[] { BaseDirectory, LibDirectory, ConfDirectory };
            var joinedClassPaths = string.Join(OsHelper.PathDelimiter, classPaths.Select(p => Path.Combine(p, "*")));

            var result = $"-Dzookeeper.log.dir={BaseDirectory} -Dzookeeper.root.logger=INFO,CONSOLE -cp {joinedClassPaths} -Dcom.sun.management.jmxremote -Dcom.sun.management.jmxremote.local.only=false org.apache.zookeeper.server.quorum.QuorumPeerMain {Path.Combine(ConfDirectory, "zoo.cfg")}";

            return result;
        }
    }
}