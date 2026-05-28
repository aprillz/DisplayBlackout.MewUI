namespace DisplayBlackout.Platform.MacOS;

internal sealed class MacOSBlackoutOverlayFactory : IBlackoutOverlayFactory
{
    public IBlackoutOverlay Create(DisplayInfo display, int opacityPercent, bool clickThrough)
        => new MacOSBlackoutOverlay(display, opacityPercent, clickThrough);
}
