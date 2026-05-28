using System.Reflection;

namespace DisplayBlackout.Platform.Win32;

internal sealed class Win32PlatformServices
{
    public IDisplayService DisplayService { get; } = new Win32DisplayService();

    public IBlackoutOverlayFactory OverlayFactory { get; } = new Win32BlackoutOverlayFactory();

    public ISystemEventService SystemEvents { get; } = SystemEventService.Instance;

    public IAppIndicator CreateAppIndicator(Assembly assembly)
        => TrayIconService.FromResources(
            assembly,
            "icon.ico",
            "icon-inactive.ico",
            "DisplayBlackout");
}
