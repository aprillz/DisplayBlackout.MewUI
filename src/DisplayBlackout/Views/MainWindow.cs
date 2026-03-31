using System.Reflection;

using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

using DisplayBlackout.Services;

namespace DisplayBlackout.Views;

internal sealed class MainWindow : Window
{
    public MainWindow(BlackoutService blackoutService, SettingsService settingsService, bool hotkeyAvailable)
    {
        var asm = Assembly.GetExecutingAssembly();

        this.FitContentHeight(700)
            .Padding(0)
            .StartCenterScreen();

        Title = "Display Blackout";
        Icon = IconSource.FromResource(asm, "icon.ico");
        Content = new SettingsView(blackoutService, settingsService);

        Closing += e =>
        {
            e.Cancel = true;
            Hide();
        };

        if (!hotkeyAvailable)
        {
            this.OnLoaded(() =>
            {
                _ = MessageBox.NotifyAsync(
                    "Failed to register hotkey Win+Shift+B.\nIt may be in use by another application.\n\nYou can still toggle blackout from the tray icon.",
                    PromptIconKind.Warning,
                    owner: this);
            });
        }
    }
}