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

        public ZooKeeperEnsemble(int size, [NotNull] ILog log)
            : this(1, size, log)
        {
        }

        public ZooKeeperEnsemble(int startingId, int size, [NotNull] ILog log)
        {
            this.log = (log ?? throw new ArgumentNullException(nameof(log))).ForContext<ZooKeeperEnsemble>();

            if (size < 1)
                throw new ArgumentOutOfRangeException(nameof(size));

            Instances = CreateInstances(startingId, size, this.log);

            this.log.Info("Created instances: \n\t" + string.Join("\n\t", Instances.Select(i => i.ToString())));
        }

        public static ZooKeeperEnsemble DeployNew(int size, ILog log, bool startInstances = true)
        {
            return DeployNew(1, size, log, startInstances);
        }

        public static ZooKeeperEnsemble DeployNew(int startingId, int size, ILog log, bool startInstances = true)
        {
            var ensemble = new ZooKeeperEnsemble(startingId, size, log);
            ensemble.Deploy(startInstances);
            return ensemble;
        }

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
        /// Deploys this ensemble's files to current working folder.
        /// </summary>
        public void Deploy(bool startInstances = true)
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

        private static List<ZooKeeperInstance> CreateInstances(int from, int size, ILog log)
        {
            var instances = new List<ZooKeeperInstance>(size);
            for (var i = 0; i < size; i++)
            {
                var clientPort = FreeTcpPortFinder.GetFreePort();
                var peerPort = FreeTcpPortFinder.GetFreePort();
                var electionPort = FreeTcpPortFinder.GetFreePort();
                var index = from + i;
                instances.Add(new ZooKeeperInstance(index, new DirectoryInfo("ZK-" + index).FullName, clientPort, peerPort, electionPort, log));
            }

            return instances;
        }
    }
}