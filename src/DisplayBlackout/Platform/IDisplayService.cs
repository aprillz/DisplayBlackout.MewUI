namespace DisplayBlackout.Platform;

internal interface IDisplayService
{
    IReadOnlyList<DisplayInfo> GetDisplays();
}
