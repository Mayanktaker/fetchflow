// © Mayanktaker Computers & Web Development | https://mayanktaker.com

// GTK 4 bandwidth-limiter window, driven by the shared SpeedLimiterUIController so
// the persisted Config values and the broadcast match the GTK 3 frontend.
using System;
using Gtk;
using Translations;
using XDM.Core;
using XDM.Core.UI;
using XDM.Core.Util;

namespace XDM.Gtk4UI.Dialogs
{
    internal sealed class SpeedLimiterWindow : ISpeedLimiterWindow
    {
        // Modal window and its controls
        private readonly Window window;
        private readonly Switch enableSwitch;
        private readonly SpinButton limitSpin;
        // Preset limits in KB/s, matching the GTK 3 quick menu
        private static readonly int[] PresetsKb = { 256, 512, 1024, 2048, 5120, 10240 };

        public event EventHandler? OkClicked;

        // Limit in KB/s
        public int SpeedLimit
        {
            get => (int)limitSpin.GetValue();
            set => limitSpin.SetValue(Math.Max(0, value));
        }

        // Whether throttling is active
        public bool EnableSpeedLimit
        {
            get => enableSwitch.Active;
            set
            {
                enableSwitch.Active = value;
                limitSpin.Sensitive = value;
            }
        }

        // Builds the window shell
        public SpeedLimiterWindow(Window? parent)
        {
            window = Window.New();
            window.Title = TextResource.GetText("MENU_SPEED_LIMITER") ?? "Speed limiter";
            window.Modal = true;
            window.SetDefaultSize(420, 300);
            if (parent != null)
            {
                window.SetTransientFor(parent);
            }

            var header = HeaderBar.New();
            header.TitleWidget = Label.New(window.Title);
            var okBtn = Button.NewWithLabel(TextResource.GetText("BTN_OK") ?? "OK");
            okBtn.AddCssClass("suggested-action");
            okBtn.OnClicked += (_, _) =>
            {
                OkClicked?.Invoke(this, EventArgs.Empty);
                window.Close();
            };
            header.PackEnd(okBtn);
            window.SetTitlebar(header);

            var box = Box.New(Orientation.Vertical, 12);
            box.MarginTop = 16;
            box.MarginBottom = 16;
            box.MarginStart = 20;
            box.MarginEnd = 20;

            var enableRow = Box.New(Orientation.Horizontal, 10);
            enableRow.Append(Label.New(TextResource.GetText("MSG_SPEED_LIMIT") ?? "Enable speed limit"));
            enableSwitch = Switch.New();
            enableSwitch.Halign = Align.End;
            enableSwitch.Hexpand = true;
            enableRow.Append(enableSwitch);
            box.Append(enableRow);

            var limitRow = Box.New(Orientation.Horizontal, 10);
            limitRow.Append(Label.New(TextResource.GetText("LBL_SPEED_KB") ?? "Limit (KB/s)"));
            limitSpin = SpinButton.NewWithRange(0, 1024 * 1024, 64);
            limitSpin.Hexpand = true;
            limitSpin.Digits = 0;
            limitRow.Append(limitSpin);
            box.Append(limitRow);

            enableSwitch.OnStateSet += (_, args) =>
            {
                limitSpin.Sensitive = args.State;
                return false;
            };

            // Quick presets
            var presetLabel = Label.New(TextResource.GetText("LBL_PRESETS") ?? "Presets");
            presetLabel.Halign = Align.Start;
            presetLabel.AddCssClass("dim-label");
            box.Append(presetLabel);

            var presetGrid = Grid.New();
            presetGrid.ColumnSpacing = 8;
            presetGrid.RowSpacing = 8;
            for (var i = 0; i < PresetsKb.Length; i++)
            {
                var preset = PresetsKb[i];
                var button = Button.NewWithLabel(FormattingHelper.FormatSize(preset * 1024.0) + "/s");
                button.OnClicked += (_, _) =>
                {
                    limitSpin.SetValue(preset);
                    enableSwitch.Active = true;
                    limitSpin.Sensitive = true;
                };
                presetGrid.Attach(button, i % 3, i / 3, 1, 1);
            }
            box.Append(presetGrid);

            window.SetChild(box);
        }

        // Presents the window
        public void ShowWindow() => window.Present();
    }
}
