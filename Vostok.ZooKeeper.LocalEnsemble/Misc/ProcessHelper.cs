using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;
using Vostok.Commons.Time;

namespace Vostok.ZooKeeper.LocalEnsemble.Misc
{
    internal static class ProcessHelper
    {
        public static void WaitTillJavaProcessSpawns(Process parentProcess, TimeSpan timeout)
        {
            var budget = TimeBudget.StartNew(timeout);

            while (!budget.HasExpired)
            {
                if (GetChildJavaProcesses(parentProcess).Any())
                    return;

                Thread.Sleep(TimeSpan.FromMilliseconds(10));
            }
        }

        public static IEnumerable<Process> GetChildJavaProcesses(Process parent)
        {
            if (OsHelper.IsUnix)
            {
                var processes = GetChildProcesses(parent);
                return processes.Where(p => p.ProcessName == "java");
            }

            using (var searcher = new ManagementObjectSearcher("select ProcessId from Win32_Process where ParentProcessId=" + parent.Id))
            {
                return searcher
                    .Get()
                    .Cast<ManagementBaseObject>()
                    .Select(m => Convert.ToInt32(m["ProcessId"]))
                    .Select(Process.GetProcessById)
                    .Where(p => p.ProcessName.Contains("java"));
            }
        }

        private static List<Process> GetChildProcesses(Process process)
        {
            var processes = Process.GetProcesses();
            var result = new List<Process>();

            foreach (var possibleChild in processes)
            {
                try
                {
                    Console.WriteLine($"CHECKING {possibleChild.ProcessName} {possibleChild.Id}");
                    if (IsParentOf(possibleChild, process))
                    {
                        Console.WriteLine($"FOUND CHILD {possibleChild.ProcessName} {possibleChild.Id}");
                        result.Add(possibleChild);
                    }
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            return result;
        }

        private static bool IsParentOf(Process possibleParent, Process possibleChild)
        {
            return possibleParent.StartTime < possibleChild.StartTime
                   && possibleParent.Id == GetParentProcessId(possibleParent.Id);
        }

        private static int GetParentProcessId(int processId)
        {
            string line;
            using (var reader = new StreamReader("/proc/" + processId + "/stat"))
                line = reader.ReadLine();

            var endOfName = line?.LastIndexOf(')') ?? throw new Exception($"Process {processId} not found");
            var parts = line.Substring(endOfName).Split(new char[] { ' ' }, 4);

            if (parts.Length >= 3)
            {
                int ppid = int.Parse(parts[2]);
                return ppid;
            }

            return -1;
        }
    }
}