using System.Reflection;

using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

using DisplayBlackout.Services;
using DisplayBlackout.Views;

Win32Platform.Register();
GdiBackend.Register();

// For Windows 7
//FontResources.Register(typeof(SettingsView).Assembly!.GetManifestResourceStream("DisplayBlackout.Resources.SEGMDL2.TTF")!, "Segoe MDL2 Assets");

// Parse command-line arguments
var cliArgs = Environment.GetCommandLineArgs().Skip(1).ToArray();
bool openSettings = cliArgs.Any(arg => arg.Equals("/OpenSettings", StringComparison.OrdinalIgnoreCase));
bool resetSettings = cliArgs.Any(arg => arg.Equals("/ResetSettings", StringComparison.OrdinalIgnoreCase));

// Initialize services
var settingsService = new SettingsService();
if (resetSettings)
{
    settingsService.ResetAll();
}

var blackoutService = new BlackoutService(settingsService);

// System events (hotkey, display change, focus change)
bool hotkeyAvailable = SystemEventService.Instance.Initialize();
SystemEventService.Instance.HotkeyPressed += (_, _) => blackoutService.Toggle();
SystemEventService.Instance.DisplayChanged += (_, _) =>
{
    if (blackoutService.IsBlackedOut.Value)
    {
        blackoutService.Restore();
    }
};
SystemEventService.Instance.FocusChanged += (_, _) => blackoutService.BringAllToFront();

// Tray icon (loads active/inactive icons from embedded resources)
var asm = Assembly.GetExecutingAssembly();
var trayIcon = TrayIconService.FromResources(asm, "icon.ico", "icon-inactive.ico",
    "Display Blackout - Click to toggle (Win+Shift+B), double-click to open settings");
trayIcon.Clicked += () => blackoutService.Toggle();
trayIcon.Show();

blackoutService.IsBlackedOut.Subscribe(() => trayIcon.SetActive(blackoutService.IsBlackedOut.Value));

// Restore saved theme/accent
var savedAccent = settingsService.LoadAccent();
var accent = Enum.TryParse<Accent>(savedAccent, out var parsed) ? parsed : Accent.Pink;
var savedTheme = settingsService.LoadTheme() switch
{
    "Light" => ThemeVariant.Light,
    "Dark" => ThemeVariant.Dark,
    _ => ThemeVariant.System
};

Application.Create()
    .UseTheme(savedTheme)
    .UseAccent(accent)
    .BuildMainWindow(() =>
    {
        var window = new MainWindow(blackoutService, settingsService, hotkeyAvailable);
        trayIcon.DoubleClicked += () => window.Show();
        return window;
    })
    .Run();

// Cleanup
trayIcon.Dispose();
blackoutService.Dispose();
SystemEventService.Instance.Dispose();
