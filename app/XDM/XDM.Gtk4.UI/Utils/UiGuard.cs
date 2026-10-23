// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// Central guard for UI commit paths that touch configuration, the database or the
// filesystem. An exception escaping a GObject signal handler terminates the whole
// process, so those paths run through here: log the failure and show the reason
// instead of taking the app down.
using System;
using Gtk;
using TraceLog;
using Translations;
using XDM.Gtk4UI.Dialogs;

namespace XDM.Gtk4UI.Utils
{
    internal static class UiGuard
    {
        // Runs a UI action, converting an escaping exception into a log entry and a dialog
        public static void Run(Window? parent, string context, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Debug(ex, context + " failed: " + ex.Message);
                try
                {
                    Gtk4DialogHelper.ShowAlert(parent,
                        (TextResource.GetText("MSG_ACTION_FAILED") ?? "This action could not be completed.")
                        + "\n\n" + ex.Message);
                }
                catch (Exception alertEx)
                {
                    // Reporting must never be the thing that crashes the app
                    Log.Debug(alertEx, "Failed to show the action-error dialog");
                }
            }
        }
    }
}
