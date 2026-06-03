namespace DisplayBlackout.Platform;

internal interface IBlackoutOverlay : IDisposable
{
    void SetOpacity(int opacityPercent);

    void SetClickThrough(bool clickThrough);

    void BringToFront();
}
