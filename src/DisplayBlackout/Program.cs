using System.Reflection;

using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Platform.MacOS;

using DisplayBlackout.Platform;
using DisplayBlackout.Platform.MacOS;
using DisplayBlackout.Platform.Win32;
using DisplayBlackout.Services;
using DisplayBlackout.Views;

IDisplayService displayService;
IBlackoutOverlayFactory overlayFactory;
ISystemEventService systemEvents;
IAppIndicator appIndicator;

var asm = Assembly.GetExecutingAssembly();

if (OperatingSystem.IsMacOS())
{
    MacOSPlatform.Register();
    MewVGMacOSBackend.Register();

    var platformServices = new MacOSPlatformServices();
    displayService = platformServices.DisplayService;
    overlayFactory = platformServices.OverlayFactory;
    systemEvents = platformServices.SystemEvents;
    appIndicator = platformServices.CreateAppIndicator(asm);
}
else if (OperatingSystem.IsWindows())
{
    Win32Platform.Register();
    GdiBackend.Register();

    var platformServices = new Win32PlatformServices();
    displayService = platformServices.DisplayService;
    overlayFactory = platformServices.OverlayFactory;
    systemEvents = platformServices.SystemEvents;
    appIndicator = platformServices.CreateAppIndicator(asm);
}
else
{
    throw new PlatformNotSupportedException("DisplayBlackout.MewUI currently supports Windows and macOS.");
}

// For Windows 7
//FontResources.Register(typeof(SettingsView).Assembly!.GetManifestResourceStream("DisplayBlackout.Resources.SEGMDL2.TTF")!, "Segoe MDL2 Assets");

// Required on macOS for Segoe MDL2 Assets icons
if (OperatingSystem.IsMacOS())
    FontResources.Register(typeof(SettingsView).Assembly!.GetManifestResourceStream("DisplayBlackout.Resources.SEGMDL2.TTF")!, "Segoe MDL2 Assets");

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

var blackoutService = new BlackoutService(settingsService, displayService, overlayFactory);

// System events (hotkey, display change, focus change)
bool hotkeyAvailable = systemEvents.Initialize();
systemEvents.HotkeyPressed += (_, _) => blackoutService.Toggle();
systemEvents.DisplayChanged += (_, _) =>
{
    if (blackoutService.IsBlackedOut.Value)
    {
        blackoutService.Restore();
    }
};
systemEvents.FocusChanged += (_, _) => blackoutService.BringAllToFront();

appIndicator.Clicked += () => blackoutService.Toggle();
appIndicator.Show();

blackoutService.IsBlackedOut.Subscribe(() => appIndicator.SetActive(blackoutService.IsBlackedOut.Value));

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
        appIndicator.DoubleClicked += () => window.Show();
        return window;
    })
    .Run();

// Cleanup
appIndicator.Dispose();
blackoutService.Dispose();
systemEvents.Dispose();
