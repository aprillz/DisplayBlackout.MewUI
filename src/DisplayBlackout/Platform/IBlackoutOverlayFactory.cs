namespace DisplayBlackout.Platform;

internal interface IBlackoutOverlayFactory
{
    IBlackoutOverlay Create(DisplayInfo display, int opacityPercent, bool clickThrough);
}
