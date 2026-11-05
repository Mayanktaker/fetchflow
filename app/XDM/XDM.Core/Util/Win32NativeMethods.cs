// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using TraceLog;

namespace XDM.Core.Util
{
    public static class Win32NativeMethods
    {
        public static readonly string DownloadFolderGuid = "{374DE290-123F-4565-9164-39C4925E467B}";

        public static string GetDownloadDirectoryPath()
        {
            try
            {
                int result = SHGetKnownFolderPath(new Guid(DownloadFolderGuid),
                (uint)KnownFolderFlags.DontVerify, IntPtr.Zero, out IntPtr outPath);
                if (result >= 0)
                {
                    string path = Marshal.PtrToStringUni(outPath);
                    Marshal.FreeCoTaskMem(outPath);
                    return path;
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error GetDownloadDirectoryPath");
            }

#if NET35
            return Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"), "Downloads");
#else
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
#endif
        }

        [DllImport("Shell32.dll")]

        private static extern int SHGetKnownFolderPath(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken,
        out IntPtr ppszPath);

        private const uint FsctlSetSparse = 0x000900C4;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(SafeFileHandle hFile, uint dwIoControlCode,
            IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize,
            out uint lpBytesReturned, IntPtr lpOverlapped);

        // Marks an open file sparse, so NTFS does not zero-fill the holes behind positional writes
        public static bool TryMarkSparse(FileStream stream)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;
            try
            {
                return DeviceIoControl(stream.SafeFileHandle, FsctlSetSparse,
                    IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Unable to mark file sparse");
                return false;
            }
        }

        [Flags]
        private enum KnownFolderFlags : uint
        {
            SimpleIDList = 0x00000100,
            NotParentRelative = 0x00000200,
            DefaultPath = 0x00000400,
            Init = 0x00000800,
            NoAlias = 0x00001000,
            DontUnexpand = 0x00002000,
            DontVerify = 0x00004000,
            Create = 0x00008000,
            NoAppcontainerRedirection = 0x00010000,
            AliasOnly = 0x80000000
        }
    }
}
