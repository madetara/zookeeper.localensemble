using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        private string ServerScriptName => OsHelper.IsUnix ? "zkServer.sh" : "zkServer.cmd";

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
        public bool IsRunning => process != null && !process.HasExited;

        /// <summary>
        /// <para>Starts instance.</para>
        /// </summary>
        public void Start()
        {
            if (OsHelper.IsUnix)
            {
                process = Process.Start($"/bin/bash", $"-lc \"chmod u+x {Path.Combine(BinDirectory, ServerScriptName)}\"");
                process?.WaitForExit();
                if (process?.ExitCode != 0)
                    throw new Exception($"Failed to make script executable.");
            }
            
            var processStartInfo = new ProcessStartInfo(Path.Combine(BinDirectory, ServerScriptName))
            {
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
            ProcessHelper.WaitTillJavaProcessSpawns(process, TimeSpan.FromSeconds(1));

            foreach (var childProcess in ProcessHelper.GetChildJavaProcesses(process))
                processKillJob?.AddProcess(childProcess);

            processKillJob?.AddProcess(process);
        }

        /// <summary>
        /// <para>Stops instance.</para>
        /// </summary>
        public void Stop()
        {
            if (process == null)
                return;
            if (!process.HasExited)
            {
                foreach (var childProcess in ProcessHelper.GetChildJavaProcesses(process))
                {
                    if (!childProcess.HasExited)
                    {
                        try
                        {
                            childProcess.Kill();
                            childProcess.WaitForExit();
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }

                if (!process.HasExited)
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
            }

            process = null;
        }

        /// <returns>String representation of ZooKeeperInstance.</returns>
        public override string ToString() => $"localhost:{ClientPort}:{PeerPort}:{ElectionPort} (id {Id}) at '{BaseDirectory}'";
    }
}