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

            throw new Exception($"Not found java process for {GetProcessNameSafely(parentProcess)}");
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
            Console.WriteLine($"SEARCHING CHILDREN FOR {GetProcessNameSafely(process)}");
            var processes = Process.GetProcesses();
            var result = new List<Process>();

            foreach (var possibleChild in processes)
            {
                try
                {
                    if (IsParentOf(process, possibleChild))
                    {
                        Console.WriteLine($"FOUND CHILD {GetProcessNameSafely(possibleChild)}");
                        result.Add(possibleChild);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            return result;
        }

        private static bool IsParentOf(Process possibleParent, Process possibleChild)
        {
            var childParentProcessId = GetParentProcessId(possibleChild);
            Console.WriteLine($"PROCESS {GetProcessNameSafely(possibleChild)} {childParentProcessId}");
            return possibleParent.StartTime < possibleChild.StartTime
                   && possibleParent.Id == childParentProcessId;
        }

        private static int GetParentProcessId(Process process)
        {
            string line;
            using (var reader = new StreamReader("/proc/" + process.Id + "/stat"))
                line = reader.ReadLine();

            var endOfName = line?.LastIndexOf(')') ?? throw new Exception($"Process {process} not found");
            var parts = line.Substring(endOfName).Split(new char[] { ' ' }, 4);

            if (parts.Length >= 3)
            {
                int ppid = int.Parse(parts[2]);
                return ppid;
            }

            return -1;
        }

        private static string GetProcessNameSafely(Process process)
        {
            try
            {
                return $"{process.ProcessName} {process.Id}";
            }
            catch (Exception e)
            {
                return $"Exited? {e.Message}";
            }
        }
    }
}