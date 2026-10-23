// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// Single source for the project license text shipped in the GTK 4 assembly.
// The About window and the license viewer both read this embedded resource.
using System;
using System.IO;
using XDM.Core;

namespace XDM.Gtk4UI.Utils
{
    internal static class LicenseText
    {
        // Logical resource name declared in XDM.Gtk4.UI.csproj
        private const string ResourceName = "FetchFlow.LICENSE";

        // Returns the LICENSE body, or null when the resource is missing
        public static string? Load()
        {
            try
            {
                using var stream = typeof(LicenseText).Assembly.GetManifestResourceStream(ResourceName);
                if (stream == null)
                {
                    return null;
                }
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                TraceLog.Log.Debug(ex, "License text load failed: " + ex.Message);
                return null;
            }
        }
    }
}
