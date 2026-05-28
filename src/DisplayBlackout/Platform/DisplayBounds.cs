namespace DisplayBlackout.Platform;

internal readonly record struct DisplayBounds(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;

    public int Bottom => Top + Height;

    public string Key => $"{Left},{Top},{Width},{Height}";
}
