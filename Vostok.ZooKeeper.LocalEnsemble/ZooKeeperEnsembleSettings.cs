using JetBrains.Annotations;

namespace Vostok.ZooKeeper.LocalEnsemble
{
    /// <summary>
    /// Represents the configuration of a <see cref="ZooKeeperEnsemble"/>.
    /// </summary>
    [PublicAPI]
    public class ZooKeeperEnsembleSettings
    {
        /// <summary>
        /// If set to a non-null value, ensemble will be deployed to this directory.
        /// </summary>
        [CanBeNull]
        public string BaseDirectory { get; set; }

        /// <summary>
        /// Count of <see cref="ZooKeeperInstance"/>s in the ensemble.
        /// </summary>
        public int Size { get; set; } = 1;

        /// <summary>
        /// Id of the first <see cref="ZooKeeperInstance"/>.
        /// </summary>
        public int StartingId { get; set; } = 1;
    }
}