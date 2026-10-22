// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 frontend entry point. The GTK 3 frontend (XDM.Gtk.UI) remains the
// compatibility build for GTK 3-only desktops; this project is the primary UI.
using System;
using Gtk;
using XDM.Core;
using GtkApplication = Gtk.Application;

namespace XDM.Gtk4UI
{
    internal static class Program
    {
        // Must match the desktop file's StartupWMClass / app id
        private const string AppId = "com.mayanktaker.fetchflow";

        private static int Main(string[] args)
        {
            // Shared configuration load (same store the GTK 3 frontend uses)
            try
            {
                Config.LoadConfig();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Config load failed: " + ex.Message);
            }

            var app = GtkApplication.New(AppId, Gio.ApplicationFlags.FlagsNone);
            // --demo fills the list with sample rows (no engine is wired yet)
            var demo = Array.Exists(args, a => a == "--demo");
            app.OnActivate += (sender, _) =>
            {
                var window = MainWindow.Create((GtkApplication)sender!, demo);
                window.Present();
            };
            return app.RunWithSynchronizationContext(null);
        }
    }
}
