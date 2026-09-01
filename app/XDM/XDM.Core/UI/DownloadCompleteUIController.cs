// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using XDM.Core;
using XDM.Core.Util;

namespace XDM.Core.UI
{
    // Coordinates download complete dialog events with core platform services
    public static class DownloadCompleteUIController
    {
        // Initializes dialog fields and wires platform open and config save handlers
        public static void ShowDialog(IDownloadCompleteDialog dwnCmpldDlg, string file, string folder)
        {
            dwnCmpldDlg.FileNameText = file;
            dwnCmpldDlg.FolderText = folder;
            dwnCmpldDlg.FileOpenClicked += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Path))
                {
                    PlatformHelper.OpenFile(args.Path!);
                }
            };
            dwnCmpldDlg.FolderOpenClicked += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Path))
                {
                    PlatformHelper.OpenFolder(args.Path!, args.FileName);
                }
            };
            dwnCmpldDlg.DontShowAgainClickd += (sender, args) =>
            {
                Config.Instance.ShowDownloadCompleteWindow = false;
                Config.SaveConfig();
            };
            dwnCmpldDlg.ShowDownloadCompleteDialog();
        }
    }
}
