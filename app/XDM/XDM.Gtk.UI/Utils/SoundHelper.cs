// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.Diagnostics;
using TraceLog;

namespace XDM.GtkUI.Utils
{
    // Plays desktop sound effects for download completion
    public static class SoundHelper
    {
        // Plays standard system download complete sound or bell
        public static void PlayDownloadCompleteSound()
        {
            try
            {
                // Play system completion sound event asynchronously via canberra-gtk-play
                var psi = new ProcessStartInfo
                {
                    FileName = "canberra-gtk-play",
                    Arguments = "-i complete -d \"FetchFlow\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var proc = Process.Start(psi);
                if (proc == null)
                {
                    Gdk.Display.Default?.Beep();
                }
            }
            catch
            {
                try
                {
                    // Fallback to Gdk display system bell
                    Gdk.Display.Default?.Beep();
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Sound playback failed: " + ex.Message);
                }
            }
        }
    }
}
