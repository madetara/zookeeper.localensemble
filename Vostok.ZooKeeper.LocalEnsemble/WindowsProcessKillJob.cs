using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.LocalEnsemble.WinApi;

namespace Vostok.ZooKeeper.LocalEnsemble
{
    internal class WindowsProcessKillJob : IDisposable
    {
        private readonly IntPtr jobHandle;
        private readonly IntPtr jobObjectInfoPtr;
        private readonly ILog log;

        public WindowsProcessKillJob(ILog log = null)
        {
            this.log = log ?? new SilentLog();
            jobHandle = Kernel32.CreateJobObject(IntPtr.Zero, IntPtr.Zero);
            if (jobHandle == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var limitInformation = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
                {
                    BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                    {
                        LimitFlags = JOBOBJECT_BASIC_LIMIT_FLAGS.JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                    }
                };
                var cb = Marshal.SizeOf((object)limitInformation);
                jobObjectInfoPtr = Marshal.AllocHGlobal(cb);
                try
                {
                    Marshal.StructureToPtr((object)limitInformation, jobObjectInfoPtr, false);
                    if (!Kernel32.SetInformationJobObject(jobHandle, JOBOBJECTINFOCLASS.JobObjectExtendedLimitInformation, jobObjectInfoPtr, (uint)cb))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                catch (Exception)
                {
                    Marshal.FreeHGlobal(jobObjectInfoPtr);
                    throw;
                }
            }
            catch (Exception)
            {
                Kernel32.CloseHandle(jobHandle);
                throw;
            }
        }

        public void Dispose()
        {
            Kernel32.CloseHandle(jobHandle);
            Marshal.FreeHGlobal(jobObjectInfoPtr);
        }

        public void AddProcess(Process process)
        {
            if (process == null)
                throw new ArgumentNullException(nameof(process));
            AddProcess(process.Handle);
        }

        public void AddProcess(IntPtr processHandle)
        {
            if (Kernel32.AssignProcessToJobObject(jobHandle, processHandle))
                return;
            log.Error(new Win32Exception(Marshal.GetLastWin32Error()), "ProcessKillJob. Failed to add process to job.");
        }
    }
}