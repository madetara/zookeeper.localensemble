using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Vostok.ZooKeeper.LocalEnsemble.Misc;

namespace Vostok.ZooKeeper.LocalEnsemble
{
    internal static class ZooKeeperDeployer
    {
        public static void DeployInstances(List<ZooKeeperInstance> instances)
        {
            var configs = GenerateConfigs(instances);
            for (var i = 0; i < instances.Count; i++)
                DeployInstance(instances[i], configs[i]);
        }

        public static void CleanupInstances(List<ZooKeeperInstance> instances)
        {
            foreach (var instance in instances)
            {
                for (var i = 0; i < 3; i++)
                {
                    try
                    {
                        Directory.Delete(instance.BaseDirectory, true);
                        break;
                    }
                    catch (Exception)
                    {
                        Thread.Sleep(500);
                    }
                }
            }
        }

        private static void DeployInstance(ZooKeeperInstance instance, string config)
        {
            CreateDirectories(instance);
            DeployFiles(instance, config);
        }

        private static string[] GenerateConfigs(List<ZooKeeperInstance> instances)
        {
            var serversList = string.Join(Environment.NewLine, instances.Select(instance => $"server.{instance.Id}=localhost:{instance.PeerPort}:{instance.ElectionPort}"));
            var configs = new string[instances.Count];
            for (var i = 0; i < configs.Length; i++)
            {
                var configBuilder = new StringBuilder();
                configBuilder.AppendLine($"tickTime={2000}{Environment.NewLine}");
                configBuilder.AppendLine($"initLimit={10}{Environment.NewLine}");
                configBuilder.AppendLine($"syncLimit={10}{Environment.NewLine}");
                configBuilder.AppendLine($"dataDir=data{Environment.NewLine}");
                configBuilder.AppendLine($"clientPort={instances[i].ClientPort}{Environment.NewLine}");
                configBuilder.AppendLine(serversList);
                configBuilder.AppendLine($"maxClientCnxns={0}{Environment.NewLine}");
                configs[i] = configBuilder.ToString();
            }

            return configs;
        }

        private static void CreateDirectories(ZooKeeperInstance instance)
        {
            if (Directory.Exists(instance.BaseDirectory))
                Directory.Delete(instance.BaseDirectory, true);
            Directory.CreateDirectory(instance.BaseDirectory);
            Directory.CreateDirectory(instance.BinDirectory);
            Directory.CreateDirectory(instance.DataDirectory);
            Directory.CreateDirectory(instance.LibDirectory);
            Directory.CreateDirectory(instance.ConfDirectory);
        }

        private static void DeployFiles(ZooKeeperInstance instance, string config)
        {
            // (iloktionov): Libraries.
            SaveResources(instance.LibDirectory, "jline_0_9_94.jar");
            SaveResources(instance.LibDirectory, "log4j_1_2_15.jar");
            SaveResources(instance.LibDirectory, "netty_3_2_2_Final.jar");
            SaveResources(instance.LibDirectory, "slf4j_api_1_6_1.jar");
            SaveResources(instance.LibDirectory, "slf4j_log4j12_1_6_1.jar");
            // (iloktionov): Control scripts.
            Directory.CreateDirectory(instance.BinDirectory);
            // (iloktionov): Actual ZK lib.
            SaveResources(instance.BaseDirectory, "zookeeper_3_4_5.jar");
            // (iloktionov): Configs.
            File.WriteAllText(Path.Combine(instance.ConfDirectory, "zoo.cfg"), config);
            File.WriteAllText(Path.Combine(instance.ConfDirectory, "log4j.properties"), CreateLog4jConfig(instance));
            // (iloktionov): Self id.
            File.WriteAllText(Path.Combine(instance.DataDirectory, "myid"), instance.Id.ToString());
        }

        private static void SaveResources(string directory, string fileName)
        {
            File.WriteAllBytes(Path.Combine(directory, fileName), GetResources(fileName));
        }

        private static byte[] GetResources(string fileName) => ResourceHelper.GetBytes<ZooKeeperInstance>($"Vostok.ZooKeeper.LocalEnsemble.Resources.{fileName}");

        // ReSharper disable once InconsistentNaming
        private static string CreateLog4jConfig(ZooKeeperInstance instance)
        {
            var builder = new StringBuilder();
            builder.AppendLine("log4j.rootLogger=DEBUG, ROLLINGFILE");
            builder.AppendLine("log4j.appender.ROLLINGFILE=org.apache.log4j.RollingFileAppender");
            builder.AppendLine("log4j.appender.ROLLINGFILE.File=" + $"../ZK-{instance.Id}.log");
            builder.AppendLine("log4j.appender.ROLLINGFILE.Threshold=DEBUG");
            builder.AppendLine("log4j.appender.ROLLINGFILE.layout=org.apache.log4j.PatternLayout");
            builder.AppendLine("log4j.appender.ROLLINGFILE.layout.ConversionPattern=[myid:%X{myid}] - %d %-5p [%t:%C{1}@%L] - %m%n");
            return builder.ToString();
        }
    }
}