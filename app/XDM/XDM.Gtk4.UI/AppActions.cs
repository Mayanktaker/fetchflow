// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// Application actions + keyboard accelerators for the GTK 4 frontend.
// Gio actions are the GTK 4 way to expose commands: they carry the shortcut,
// appear in the app menu on GNOME, and can be triggered from tests.
using System;
using Gio;
using SysAction = System.Action;

namespace XDM.Gtk4UI
{
    internal static class AppActions
    {
        // Action names (prefixed with "app." in accelerator registration)
        public const string NewDownload = "new-download";
        public const string Search = "search";
        public const string ToggleTheme = "toggle-theme";
        public const string Quit = "quit";
        public const string Refresh = "refresh";

        // Registers every action and its accelerator on the application
        public static void Register(Gtk.Application app, SysAction newDownload, SysAction search, SysAction toggleTheme, SysAction refresh)
        {
            Add(app, NewDownload, "<Control>n", newDownload);
            Add(app, Search, "<Control>f", search);
            Add(app, ToggleTheme, "<Control>t", toggleTheme);
            Add(app, Refresh, "F5", refresh);
            Add(app, Quit, "<Control>q", () => app.Quit());
        }

        // Adds one parameterless action with its accelerator
        private static void Add(Gtk.Application app, string name, string accelerator, SysAction handler)
        {
            var action = SimpleAction.New(name, null);
            action.OnActivate += (_, _) => handler();
            app.AddAction(action);
            app.SetAccelsForAction("app." + name, new[] { accelerator });
        }
    }
}
