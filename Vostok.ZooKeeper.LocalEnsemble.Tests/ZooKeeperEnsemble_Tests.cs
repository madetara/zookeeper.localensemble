using FluentAssertions;
using NUnit.Framework;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;

namespace Vostok.ZooKeeper.LocalEnsemble.Tests
{
    [TestFixture]
    internal class ZooKeeperEnsemble_Tests
    {
        private readonly ILog log = new SynchronousConsoleLog();

        [TestCase(1)]
        [TestCase(5)]
        public void DeployNew_should_run_instances(int instances)
        {
            using (var ensemble = ZooKeeperEnsemble.DeployNew(instances, log))
            {
                ensemble.IsRunning.Should().BeTrue();
            }
        }

        [Test]
        public void DeployNew_should_use_from_index()
        {
            using (var ensemble1 = ZooKeeperEnsemble.DeployNew(1, 3, log))
            using (var ensemble2 = ZooKeeperEnsemble.DeployNew(10, 3, log))
            {
                ensemble1.IsRunning.Should().BeTrue();
                ensemble2.IsRunning.Should().BeTrue();
            }
        }

        [TestCase(3)]
        public void DeployNew_should_not_run_instances_if_specified(int instances)
        {
            using (var ensemble = ZooKeeperEnsemble.DeployNew(instances, log, false))
            {
                ensemble.IsRunning.Should().BeFalse();
            }
        }

        [TestCase(3)]
        public void DeployNew_should_run_multiple_times(int instances)
        {
            using (ZooKeeperEnsemble.DeployNew(instances, log))
            {
            }

            using (ZooKeeperEnsemble.DeployNew(instances, log))
            {
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Instances_should_be_stoppable_and_startable(int index)
        {
            using (var ensemble = ZooKeeperEnsemble.DeployNew(3, log))
            {
                ensemble.Instances[index].IsRunning.Should().BeTrue("Before stop.");
                ensemble.Instances[index].Stop();
                ensemble.Instances[index].IsRunning.Should().BeFalse("After stop.");
                ensemble.Instances[index].Start();
                ensemble.Instances[index].IsRunning.Should().BeTrue("After restart.");
            }
        }
    }
}