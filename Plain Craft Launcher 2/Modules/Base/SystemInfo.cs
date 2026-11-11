using System;
using System.Runtime.InteropServices;

namespace PCL
{
    /// <summary>
    /// 系统物理内存信息。
    /// </summary>
    public static class SystemInfo
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatusEx
        {
            public uint Length;
            public uint MemoryLoad;
            public ulong TotalPhys;
            public ulong AvailPhys;
            public ulong TotalPageFile;
            public ulong AvailPageFile;
            public ulong TotalVirtual;
            public ulong AvailVirtual;
            public ulong AvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx Buffer);

        private static MemoryStatusEx Query()
        {
            var Status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf(typeof(MemoryStatusEx)) };
            if (!GlobalMemoryStatusEx(ref Status))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            return Status;
        }

        /// <summary>当前可用的物理内存，单位为字节。</summary>
        public static ulong AvailablePhysicalMemory => Query().AvailPhys;

        /// <summary>物理内存总量，单位为字节。</summary>
        public static ulong TotalPhysicalMemory => Query().TotalPhys;
    }
}
