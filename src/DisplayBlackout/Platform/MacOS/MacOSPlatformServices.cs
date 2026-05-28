using System.Reflection;

namespace DisplayBlackout.Platform.MacOS;

internal sealed class MacOSPlatformServices
{
    public IDisplayService DisplayService { get; } = new MacOSDisplayService();

    public IBlackoutOverlayFactory OverlayFactory { get; } = new MacOSBlackoutOverlayFactory();

    public ISystemEventService SystemEvents { get; } = new MacOSSystemEventService();

    public IAppIndicator CreateAppIndicator(Assembly assembly)
        => MacOSAppIndicator.FromResources(assembly);
}
