using static DisplayBlackout.Platform.Win32.NativeMethods;

namespace DisplayBlackout.Platform.Win32;

internal sealed class Win32BlackoutOverlayFactory : IBlackoutOverlayFactory
{
    public IBlackoutOverlay Create(DisplayInfo display, int opacityPercent, bool clickThrough)
    {
        var bounds = new RECT
        {
            Left = display.Bounds.Left,
            Top = display.Bounds.Top,
            Right = display.Bounds.Right,
            Bottom = display.Bounds.Bottom
        };

        return new BlackoutOverlay(bounds, opacityPercent, clickThrough);
    }
}
