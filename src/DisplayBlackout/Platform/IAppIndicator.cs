namespace DisplayBlackout.Platform;

internal interface IAppIndicator : IDisposable
{
    event Action? Clicked;

    event Action? DoubleClicked;

    void Show();

    void SetActive(bool isActive);
}
