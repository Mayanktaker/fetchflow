// © Mayanktaker Computers & Web Development | https://mayanktaker.com
// Pure decision helpers for the Linux/Wayland tray icon. Kept free of GTK/D-Bus types so the

namespace XDM.Core.Util
{
    public static class TrayIconPolicy
    {
        /// <summary>
        /// The icon name to advertise over StatusNotifierItem.
        /// StatusNotifierItem hosts prefer IconName and (on GNOME especially) never fall back to
        /// IconPixmap, so advertising a name that does not resolve in the icon theme makes the
        /// host draw its generic placeholder and ignore the pixmaps we supply. Returning an empty
        /// name is the spec-correct way to say "use IconPixmap".
        /// </summary>
        public static string ResolveAdvertisedIconName(string trayIconName, bool themedIconResolves)
            => themedIconResolves ? trayIconName : string.Empty;

        /// <summary>
        /// Whether the tray glyph still needs copying into the user icon theme. Only rewrites when
        /// the source is missing-unusable or the installed copy is absent/stale, so launching the
        /// app does not touch the file on every run.
        /// </summary>
        public static bool NeedsInstall(
            bool sourceExists,
            bool targetExists,
            bool sizeDiffers = false,
            bool sourceIsNewer = false)
        {
            if (!sourceExists) return false;
            if (!targetExists) return true;
            return sizeDiffers || sourceIsNewer;
        }

        /// <summary>
        /// IconThemePath is deliberately always empty. GNOME's AppIndicator extension builds a
        /// private theme from this path alone and ignores the system theme, so pointing it at a
        /// flat application directory makes every lookup fail. Asserted here so the regression
        /// cannot come back silently.
        /// </summary>
        public static string ResolveIconThemePath() => string.Empty;
    }
}
