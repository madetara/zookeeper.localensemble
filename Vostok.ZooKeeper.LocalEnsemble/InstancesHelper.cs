using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Vostok.Commons.Time;

namespace Vostok.ZooKeeper.LocalEnsemble
{
    internal static class InstancesHelper
    {
        public static void WaitAndCheckInstancesAreRunning(List<ZooKeeperInstance> instances)
        {
            var timeout = TimeSpan.FromSeconds(5);
            var watch = Stopwatch.StartNew();
            var idleInstances = 0;
            while (watch.Elapsed < timeout)
            {
                idleInstances = instances.Count(instance => !instance.IsRunning);
                if (idleInstances == 0)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                    return;
                }

                Thread.Sleep(5.Seconds());
            }

            throw new Exception($"{idleInstances} of {instances.Count} instances have not started.");
        }
    }
}