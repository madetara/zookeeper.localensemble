using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Vostok.Commons.Helpers.Network;
using Vostok.Logging.Abstractions;

namespace Vostok.ZooKeeper.LocalEnsemble
{
    /// <inheritdoc />
    /// <summary>
    /// Represents ensemble of multiple ZooKeeper instances.
    /// </summary>
    [PublicAPI]
    public class ZooKeeperEnsemble : IDisposable
    {
        private readonly ILog log;

        private volatile bool isRunning;
        private volatile bool isDisposed;

        /// <inheritdoc cref="ZooKeeperInstance" />
        public ZooKeeperEnsemble(int size, ILog log)
            : this(1, size, log)
        {
        }

        /// <inheritdoc cref="ZooKeeperInstance" />
        public ZooKeeperEnsemble(int from, int size, ILog log)
        {
            this.log = log.ForContext("ZooKeeperEnsemble");
            if (size < 1)
                throw new ArgumentOutOfRangeException(nameof(size));
            Instances = CreateInstances(from, size, this.log);

            log.Info("Created instances: \n\t" + string.Join("\n\t", Instances.Select(i => i.ToString())));
        }

        /// <summary>
        /// Creates and deploys new <see cref="ZooKeeperEnsemble" />
        /// </summary>
        /// <param name="size">Amount of instances.</param>
        /// <param name="log"><see cref="ILog" /> instance.</param>
        /// <param name="startInstances">Starts instances after deploy or not.</param>
        public static ZooKeeperEnsemble DeployNew(int size, ILog log, bool startInstances = true)
        {
            return DeployNew(1, size, log, startInstances);
        }

        /// <summary>
        /// Creates and deploys new <see cref="ZooKeeperEnsemble" />
        /// </summary>
        /// <param name="from">First instance index.</param>
        /// <param name="size">Amount of instances.</param>
        /// <param name="log"><see cref="ILog" /> instance.</param>
        /// <param name="startInstances">Starts instances after deploy or not.</param>
        public static ZooKeeperEnsemble DeployNew(int from, int size, ILog log, bool startInstances = true)
        {
            var ensemble = new ZooKeeperEnsemble(size, log);
            ensemble.Deploy(startInstances);
            return ensemble;
        }

        /// <summary>
        /// Check that ensemble is disposed.
        /// </summary>
        public bool IsDisposed => isDisposed;

        /// <summary>
        /// Check that ensemble is running.
        /// </summary>
        public bool IsRunning => isRunning;

        /// <summary>
        /// Returns <see cref="ZooKeeperInstance" /> instances of ensemble.
        /// </summary>
        public List<ZooKeeperInstance> Instances { get; }

        /// <summary>
        /// Returns ensemble connection string.
        /// </summary>
        public string ConnectionString
        {
            get { return string.Join(",", Instances.Select(instance => $"localhost:{instance.ClientPort}")); }
        }

        /// <summary>
        /// Deploys <see cref="ZooKeeperEnsemble" /> to folder.
        /// </summary>
        /// <param name="startInstances">Starts instances after deploy or not.</param>
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
        /// Starts <see cref="ZooKeeperEnsemble" /> to folder.
        /// </summary>
        public void Start()
        {
            if (!isRunning)
            {
                log.Info("Starting ensemble...");
                foreach (var instance in Instances)
                    instance.Start();
                log.Info("Started ensemble successfully.");
                isRunning = true;
            }
        }

        /// <summary>
        /// Stops <see cref="ZooKeeperEnsemble" /> to folder.
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

        /// <inheritdoc />
        /// <summary>
        /// Stops all <see cref="T:Vostok.ZooKeeper.LocalEnsemble.ZooKeeperInstance" /> instances and cleans folder.
        /// </summary>
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

        private static List<ZooKeeperInstance> CreateInstances(int index, int size, ILog log)
        {
            var instances = new List<ZooKeeperInstance>(size);
            for (var i = 1; i <= size; i++)
            {
                var clientPort = FreeTcpPortFinder.GetFreePort();
                var peerPort = FreeTcpPortFinder.GetFreePort();
                var electionPort = FreeTcpPortFinder.GetFreePort();
                instances.Add(new ZooKeeperInstance(index, new DirectoryInfo("ZK-" + index).FullName, clientPort, peerPort, electionPort, log));
            }

            return instances;
        }
    }
}