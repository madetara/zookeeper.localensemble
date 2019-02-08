using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;
using JetBrains.Annotations;
using Vostok.Commons.Time;
using Vostok.Logging.Abstractions;

namespace Vostok.ZooKeeper.LocalEnsemble
{
    /// <summary>
    /// Represents ZooKeeper instance based on java implementation.
    /// </summary>
    [PublicAPI]
    public class ZooKeeperInstance
    {
        private string ServerScriptName => OsIsUnix() ? "zkServer.sh" : "zkServer.cmd";

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
            processKillJob = OsIsUnix() ? null : new WindowsProcessKillJob(log);
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
            var processStartInfo = new ProcessStartInfo(Path.Combine(BinDirectory, ServerScriptName))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                WorkingDirectory = BinDirectory
            };
            process = new Process
            {
                StartInfo = processStartInfo
            };
            if (!process.Start())
                throw new Exception($"Failed to start process of participant '{Id}'.");

            InstancesHelper.WaitAndCheckInstancesAreRunning(new List<ZooKeeperInstance> {this});
            WaitTillJavaProcessSpawns(process, TimeSpan.FromSeconds(1));

            foreach (var childProcess in GetChildJavaProcesses(process.Id))
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
                foreach (var childProcess in GetChildJavaProcesses(process.Id))
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
                    }
                }
            }

            process = null;
        }

        /// <returns>String representation of ZooKeeperInstance.</returns>
        public override string ToString() => $"localhost:{ClientPort}:{PeerPort}:{ElectionPort} (id {Id}) at '{BaseDirectory}'";

        private static bool OsIsUnix() => Environment.OSVersion.Platform == PlatformID.Unix;

        private static void WaitTillJavaProcessSpawns(Process parentProcess, TimeSpan timeout)
        {
            var budget = TimeBudget.StartNew(timeout);

            while (!budget.HasExpired)
            {
                if (GetChildJavaProcesses(parentProcess.Id).Any())
                    return;

                Thread.Sleep(TimeSpan.FromMilliseconds(10));
            }
        }

        private static IEnumerable<Process> GetChildJavaProcesses(int parentId)
        {
            using (var searcher = new ManagementObjectSearcher("select ProcessId from Win32_Process where ParentProcessId=" + parentId))
            {
                return searcher
                    .Get()
                    .Cast<ManagementBaseObject>()
                    .Select(m => Convert.ToInt32(m["ProcessId"]))
                    .Select(Process.GetProcessById)
                    .Where(p => p.ProcessName.Contains("java"));
            }
        }
    }
}