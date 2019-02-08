﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Vostok.ZooKeeper.LocalEnsemble
{
    public static class InstancesHelper
    {
        public static void WaitAndCheckInstancesAreRunning(List<ZooKeeperInstance> instances)
        {
            var timeout = TimeSpan.FromSeconds(5);
            var watch = Stopwatch.StartNew();
            var idleInstances = 0;
            while (watch.Elapsed < timeout)
            {
                idleInstances = instances.Count(instance => !instance.IsRunning());
                if (idleInstances == 0)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                    return;
                }

                Thread.Sleep(100);
            }

            throw new Exception($"{idleInstances} of {instances.Count} instances have not started.");
        }

    }
}