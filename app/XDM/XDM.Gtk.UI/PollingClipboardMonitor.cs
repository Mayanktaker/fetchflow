using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Threading.Tasks;
using Gtk;
using XDM.Core;
using TraceLog;

namespace XDM.GtkUI
{
    public class PollingClipboardMonitor : IPlatformClipboardMonitor
    {
        private Timer timer;
        private string lastText;
        private Clipboard cb;
        public PollingClipboardMonitor()
        {
            cb = Clipboard.Get(Gdk.Selection.Clipboard);
            timer = new Timer(1000);
            timer.Elapsed += Timer_Elapsed;
        }

        // Fired on a ThreadPool thread — marshals to the GTK main loop. Must not throw
        // out of the GLib invoke, otherwise the timer keeps scheduling doomed callbacks.
        private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                Gtk.Application.Invoke(this.CheckGtkClipboardContents);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Clipboard Timer_Elapsed: " + ex.Message);
            }
        }

        private void CheckGtkClipboardContents(object? sender, EventArgs e)
        {
            try
            {
                if (cb == null)
                {
                    Log.Debug("Clipboard is null");
                    return;
                }
                var text = cb.WaitForText();
                if (text != lastText)
                {
                    Log.Debug("Clipboard changed");
                    lastText = text;
                    try { this.ClipboardChanged?.Invoke(this, EventArgs.Empty); }
                    catch (Exception handlerEx) { Log.Debug(handlerEx, "ClipboardChanged handler: " + handlerEx.Message); }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "CheckGtkClipboardContents: " + ex.Message);
            }
        }

        public event EventHandler? ClipboardChanged;

        public string GetClipboardText() => lastText;

        public void StartClipboardMonitoring()
        {
            timer.Start();
        }

        public void StopClipboardMonitoring()
        {
            timer.Stop();
        }
    }
}
