using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Vostok.Commons.Helpers.Network;
using Vostok.Logging.Abstractions;

namespace Vostok.ZooKeeper.LocalEnsemble
{
    /// <summary>
    /// Represents a locally run ZooKeeper ensemble consisting of multiple <see cref="ZooKeeperInstance"/>s.
    /// </summary>
    [PublicAPI]
    public class ZooKeeperEnsemble : IDisposable
    {
        private readonly ILog log;

        private volatile bool isRunning;
        private volatile bool isDisposed;

        public ZooKeeperEnsemble([NotNull] ZooKeeperEnsembleSettings settings, [NotNull] ILog log)
        {
            this.log = (log ?? throw new ArgumentNullException(nameof(log))).ForContext<ZooKeeperEnsemble>();

            if (settings.Size < 1)
                throw new ArgumentOutOfRangeException(nameof(settings.Size), "Ensemble must have at least one instance.");

            Instances = CreateInstances(settings, this.log);
        }

        [NotNull]
        public static ZooKeeperEnsemble DeployNew([NotNull] ZooKeeperEnsembleSettings settings, [NotNull] ILog log, bool startInstances = true)
        {
            var ensemble = new ZooKeeperEnsemble(settings, log);

            ensemble.Deploy(startInstances);

            return ensemble;
        }

        [NotNull]
        public static ZooKeeperEnsemble DeployNew(int size, ILog log, bool startInstances = true)
            => DeployNew(new ZooKeeperEnsembleSettings {Size = size}, log, startInstances);

        [NotNull]
        public static ZooKeeperEnsemble DeployNew(int startingId, int size, ILog log, bool startInstances = true)
            => DeployNew(new ZooKeeperEnsembleSettings { StartingId = startingId, Size = size }, log, startInstances);

        /// <summary>
        /// Returns whether this ensemble has been disposed.
        /// </summary>
        public bool IsDisposed => isDisposed;

        /// <summary>
        /// Returns whether this ensemble is currently running.
        /// </summary>
        public bool IsRunning => isRunning;

        /// <summary>
        /// Returns <see cref="ZooKeeperInstance" />s of this ensemble.
        /// </summary>
        public IReadOnlyList<ZooKeeperInstance> Instances { get; }

        /// <summary>
        /// Returns ensemble connection string.
        /// </summary>
        public string ConnectionString
            => string.Join(",", Instances.Select(instance => $"localhost:{instance.ClientPort}"));

        /// <summary>
        /// Starts all instances in this ensemble.
        /// </summary>
        public void Start()
        {
            if (!isRunning)
            {
                isRunning = true;

                log.Info("Starting ensemble...");
                foreach (var instance in Instances)
                    instance.Start();
                log.Info("Started ensemble successfully.");
            }
        }

        /// <summary>
        /// Stops all instances in this ensemble.
        /// </summary>
        public void Stop()
        {
            if (isRunning)
            {
                log.Info("Stopping ensemble...");
                foreach (var instance in Instances)
                    instance.Stop();
                log.Info("Stopped ensemble successfully.");

                isRunning = false;
            }
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                Stop();
                log.Info("Cleaning directories...");
                ZooKeeperDeployer.CleanupInstances(Instances);
                log.Info("Cleaned directories successfully.");
                isDisposed = true;
            }
        }

        private void Deploy(bool startInstances)
        {
            try
            {
                ZooKeeperDeployer.DeployInstances(Instances);

                if (startInstances)
                    Start();
            }
            catch (Exception error)
            {
                log.Error(error, "Error in starting. Will try to stop.");
                Dispose();
                throw;
            }
        }

        private static List<ZooKeeperInstance> CreateInstances(ZooKeeperEnsembleSettings settings, ILog log)
        {
            var instances = new List<ZooKeeperInstance>(settings.Size);

            for (var i = 0; i < settings.Size; i++)
            {
                var clientPort = FreeTcpPortFinder.GetFreePort();
                var peerPort = FreeTcpPortFinder.GetFreePort();
                var electionPort = FreeTcpPortFinder.GetFreePort();
                var index = settings.StartingId + i;

                var instanceDirectoryPath = "ZK-" + index;

                if (!string.IsNullOrEmpty(settings.BaseDirectory))
                    instanceDirectoryPath = Path.Combine(settings.BaseDirectory, instanceDirectoryPath);

                instanceDirectoryPath = Path.GetFullPath(instanceDirectoryPath);

                instances.Add(new ZooKeeperInstance(index, instanceDirectoryPath, clientPort, peerPort, electionPort, log));
            }

            log.Info("Created instances: \n\t" + string.Join("\n\t", instances.Select(i => i.ToString())));

            return instances;
        }
    }
}