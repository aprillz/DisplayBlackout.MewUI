using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

using DisplayBlackout.Services;

namespace DisplayBlackout.Views;

internal sealed class MainWindow : Window
{
    public MainWindow(BlackoutService blackoutService, SettingsService settingsService, bool hotkeyAvailable)
    {
        this.FitContentHeight(700)
            .Padding(0)
            .StartCenterScreen()
            .Title("Display Blackout.MewUI")
            .Icon(IconSource.FromResource<MainWindow>("icon.ico"))
            .Content(new SettingsView(blackoutService, settingsService))
            .OnLoaded(() =>
            {
                if (!hotkeyAvailable)
                {
                    _ = MessageBox.NotifyAsync(
                            @"Failed to register hotkey Win+Shift+B.
    It may be in use by another application.

    You can still toggle blackout from the tray icon.",
                            PromptIconKind.Warning,
                            owner: this);
                }
            });

        Closing += e =>
        {
            e.Cancel = true;
            Hide();
        };
    }
}