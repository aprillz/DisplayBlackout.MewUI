namespace DisplayBlackout.Platform;

internal interface ISystemEventService : IDisposable
{
    event EventHandler? HotkeyPressed;

    event EventHandler? DisplayChanged;

    event EventHandler? FocusChanged;

    bool Initialize();
}
