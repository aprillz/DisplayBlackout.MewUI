using System.ComponentModel;
using System.Runtime.InteropServices;

using static DisplayBlackout.NativeMethods;

namespace DisplayBlackout.Services;

/// <summary>
/// Listens for system-level events (hotkeys, display changes, focus changes) using a hidden
/// Win32 window and WinEvent hooks.
/// </summary>
internal sealed partial class SystemEventService : IDisposable
{
    private const string WindowClassName = "DisplayBlackoutSystemEvents";
    private const int HotkeyId = 1;

    private static readonly WndProcDelegate _wndProc = WndProc;
    private static readonly WinEventDelegate _winEventProc = WinEventProc;

    public static SystemEventService Instance { get; } = new();

    private nint _hwnd;
    private nint _foregroundHook;
    private nint _focusHook;
    private bool _initialized;

    public event EventHandler? HotkeyPressed;

    public event EventHandler? DisplayChanged;

    public event EventHandler? FocusChanged;

    private SystemEventService()
    { }

    /// <summary>
    /// Registers the hidden window, hotkey, and WinEvent hooks.
    /// Safe to call multiple times — subsequent calls are no-ops.
    /// Returns false if hotkey registration fails (e.g. already in use by another app).
    /// </summary>
    public bool Initialize()
    {
        if (_initialized)
        {
            return true;
        }

        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = GetModuleHandleW(null),
            lpszClassName = WindowClassName
        };

        if (RegisterClassExW(ref wc) == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        // Create a hidden window to receive system broadcast messages like WM_DISPLAYCHANGE
        // (Message-only windows with HWND_MESSAGE don't receive broadcasts)
        _hwnd = CreateWindowExW(
            WINDOW_EX_STYLE.WS_EX_TOOLWINDOW,
            WindowClassName,
            null,
            0,
            0, 0, 0, 0,
            0,
            0, 0, 0);

        if (_hwnd == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        // Register Win+Shift+B hotkey — may fail if another app holds it
        if (!RegisterHotKey(_hwnd, HotkeyId,
            HOT_KEY_MODIFIERS.MOD_WIN | HOT_KEY_MODIFIERS.MOD_SHIFT, VIRTUAL_KEY.VK_B))
        {
            // Non-fatal: app still works via tray icon, just no hotkey
            Dispose();
            return false;
        }

        // Install WinEvent hooks for focus changes
        _foregroundHook = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND,
            EVENT_SYSTEM_FOREGROUND,
            0,
            _winEventProc,
            0,
            0,
            WINEVENT_OUTOFCONTEXT);

        _focusHook = SetWinEventHook(
            EVENT_OBJECT_FOCUS,
            EVENT_OBJECT_FOCUS,
            0,
            _winEventProc,
            0,
            0,
            WINEVENT_OUTOFCONTEXT);

        _initialized = true;
        return true;
    }

    private static void WinEventProc(
        nint hWinEventHook, uint eventType, nint hwnd, int idObject,
        int idChild, uint idEventThread, uint dwmsEventTime)
    {
        Instance.FocusChanged?.Invoke(Instance, EventArgs.Empty);
    }

    private static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WM_HOTKEY && (int)wParam == HotkeyId)
        {
            Instance.HotkeyPressed?.Invoke(Instance, EventArgs.Empty);
            return 0;
        }

        if (msg == WM_DISPLAYCHANGE)
        {
            Instance.DisplayChanged?.Invoke(Instance, EventArgs.Empty);
            return 0;
        }

        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_foregroundHook != 0)
        {
            UnhookWinEvent(_foregroundHook);
            _foregroundHook = 0;
        }
        if (_focusHook != 0)
        {
            UnhookWinEvent(_focusHook);
            _focusHook = 0;
        }
        if (_hwnd != 0)
        {
            UnregisterHotKey(_hwnd, HotkeyId);
            DestroyWindow(_hwnd);
            _hwnd = 0;
        }
        _initialized = false;
    }
}