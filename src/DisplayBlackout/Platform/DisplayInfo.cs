namespace DisplayBlackout.Platform;

internal sealed record DisplayInfo(
    string Id,
    DisplayBounds Bounds,
    bool IsPrimary,
    string Name,
    int SortOrder = 0);
