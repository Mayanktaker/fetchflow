// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// Plays download completion sound effects for the GTK 4 frontend (parity with the GTK 3 SoundHelper).
using System;
using System.Diagnostics;
using TraceLog;

namespace XDM.Gtk4UI.Utils
{
    // Turns the "play sound when download finishes" setting into audible feedback
    public static class Gtk4SoundHelper
    {
        // Plays the freedesktop completion event, falling back to the display bell
        public static void PlayDownloadCompleteSound()
        {
            try
            {
                // Start the sound player without waiting, so the dialog never blocks on audio
                var psi = new ProcessStartInfo
                {
                    FileName = "canberra-gtk-play",
                    Arguments = "-i complete -d \"FetchFlow\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc == null)
                {
                    Beep();
                }
            }
            catch (Exception ex)
            {
                // Missing player or no sound server: log it and ring the bell instead
                Log.Debug(ex, "GTK 4 completion sound failed: " + ex.Message);
                Beep();
            }
        }

        // Rings the default display bell, ignored when the display has none
        private static void Beep()
        {
            try
            {
                Gdk.Display.GetDefault()?.Beep();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "GTK 4 completion bell failed: " + ex.Message);
            }
        }
    }
}
