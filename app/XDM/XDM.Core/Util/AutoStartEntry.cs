// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Launch-at-login identifiers, shared by the app and the Windows installer.
// Deliberately dependency-free so the installer-parity test can assert the Inno Setup
// script uses the same value name as the app.
namespace XDM.Core.Util
{
    public static class AutoStartEntry
    {
        /// <summary>HKCU\Software\Microsoft\Windows\CurrentVersion\Run subkey (Windows).</summary>
        public const string WindowsRunSubkey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>Value name the app writes, and that the installer must write.</summary>
        public const string WindowsRunValueName = "FetchFlow";

        /// <summary>Value name from older builds; still read for backwards compatibility.</summary>
        public const string LegacyWindowsRunValueName = "XDM";

        /// <summary>Argument that starts the app quietly instead of popping the full window.</summary>
        public const string BackgroundArgument = "--background";

        /// <summary>Linux autostart .desktop file name.</summary>
        public const string LinuxDesktopFileName = "com.mayanktaker.fetchflow.desktop";

        /// <summary>.desktop file name used by older Linux builds.</summary>
        public const string LegacyLinuxDesktopFileName = "xdm-app.desktop";

        /// <summary>
        /// Marker recording that the user deliberately turned launch-at-login OFF. The OS entry
        /// itself is not enough to tell "user opted out" from "entry vanished", so startup
        /// self-healing must consult this file before recreating one.
        /// </summary>
        public const string OptOutMarkerFileName = "autostart-optout";
    }
}
