// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System;
using System.IO;
using Gtk;
using Translations;
using UI = Gtk.Builder.ObjectAttribute;
using XDM.Core;
using XDM.GtkUI.Utils;
using IoPath = System.IO.Path;

namespace XDM.GtkUI.Dialogs.About
{
    public class AboutDialog : Window
    {
#pragma warning disable CS0649
        [UI] private Box MainContainer, LogoContainer, LinksCard, CreditsCard, ActionBox;
        [UI] private Label TxtAppName, TxtAppVersion, TxtTagline, TxtCopyright, TxtOriginalCredit;
        [UI] private LinkButton BtnAppWebsite, BtnDevWebsite, BtnGitHub;
        [UI] private Image AppLogo;
        [UI] private Button BtnClose;
#pragma warning restore CS0649

        public bool Result { get; set; } = false;

        private AboutDialog(Builder builder, Window parent, WindowGroup group) : base(builder.GetRawOwnedObject("window"))
        {
            builder.Autoconnect(this);

            Modal = true;
            TransientFor = parent;
            group.AddWindow(this);

            GtkHelper.AttachSafeDispose(this);
            GtkHelper.SetWindowAppIcon(this);

            // CSD headerbar: themed with title, app icon, and close button
            var headerTitle = TextResource.GetText("MENU_ABOUT");
            Title = headerTitle;
            Titlebar = GtkHelper.CreateDialogHeaderBar(headerTitle);

            // Load vibrant logo mark and attach styling classes
            AppLogo.Pixbuf = GtkHelper.LoadSvg("fetchflow-mark", 80) ?? GtkHelper.LoadSvg("fetchflow-logo", 80);
            LogoContainer.StyleContext.AddClass("about-logo-badge");
            TxtAppName.StyleContext.AddClass("about-title");
            TxtAppVersion.StyleContext.AddClass("about-version-badge");
            TxtTagline.StyleContext.AddClass("about-tagline");
            LinksCard.StyleContext.AddClass("about-section-card");
            CreditsCard.StyleContext.AddClass("about-credits-card");
            TxtCopyright.StyleContext.AddClass("about-copyright-text");
            TxtOriginalCredit.StyleContext.AddClass("about-credit-text");
            BtnClose.StyleContext.AddClass("suggested-action");

            // Text and hyperlink definitions
            TxtAppName.Text = AppInfo.APP_FULL_NAME;
            TxtAppVersion.Text = AppInfo.APP_VERSION_ONLY;
            TxtCopyright.Text = AppInfo.APP_COPYRIGHT_TEXT;
            TxtOriginalCredit.Text = AppInfo.APP_ORIGINAL_AUTHOR_CREDIT;
            BtnAppWebsite.Uri = AppInfo.APP_PRODUCT_URL;
            BtnDevWebsite.Uri = AppInfo.APP_DEVELOPER_URL;
            BtnGitHub.Uri = Links.SupportUrl;

            // Wire close button and default size (+20% width)
            BtnClose.Clicked += (s, e) => Destroy();
            SetDefaultSize(600, 520);
        }

        public static AboutDialog CreateFromGladeFile(Window parent, WindowGroup group)
        {
            var builder = new Builder();
            builder.AddFromFile(IoPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "glade", "about-dialog.glade"));
            return new AboutDialog(builder, parent, group);
        }
    }
}
