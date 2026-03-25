using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;

using static DisplayBlackout.NativeMethods;

namespace DisplayBlackout.Services;

/// <summary>
/// Win32 Shell_NotifyIcon wrapper for system tray icon with context menu.
/// Icons are loaded from embedded resources; two states (active/inactive) are cached as HICONs.
/// </summary>
internal sealed class TrayIconService : IDisposable
{
    private const string WindowClassName = "DisplayBlackoutTray";
    private const uint WM_TRAYICON = WM_APP + 1;
    private const uint MENU_ID_SETTINGS = 1;
    private const uint MENU_ID_TOGGLE = 2;
    private const uint MENU_ID_EXIT = 3;

    private static WndProcDelegate? _wndProc;
    private static TrayIconService? _instance;

    private readonly string _tooltip;
    private readonly nint _hIconActive;
    private readonly nint _hIconInactive;
    private nint _hwnd;
    private bool _isActive;
    private bool _disposed;

    public event Action? Clicked;
    public event Action? DoubleClicked;

    private TrayIconService(byte[] activeIco, byte[] inactiveIco, string tooltip)
    {
        _instance = this;
        _tooltip = tooltip;

        _wndProc = TrayWndProc;

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

        _hwnd = CreateWindowExW(0, WindowClassName, null, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        if (_hwnd == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        _hIconActive = CreateHIconFromIcoBytes(activeIco);
        _hIconInactive = CreateHIconFromIcoBytes(inactiveIco);
    }

    /// <summary>
    /// Creates a <see cref="TrayIconService"/> from embedded resources.
    /// </summary>
    public static TrayIconService FromResources(Assembly assembly, string activeResourceName, string inactiveResourceName, string tooltip)
    {
        var activeIco = LoadResource(assembly, activeResourceName);
        var inactiveIco = LoadResource(assembly, inactiveResourceName);
        return new TrayIconService(activeIco, inactiveIco, tooltip);
    }

    public void Show()
    {
        var nid = CreateNotifyIconData();
        nid.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
        nid.uCallbackMessage = WM_TRAYICON;
        nid.hIcon = _hIconInactive;
        nid.szTip = _tooltip;

        Shell_NotifyIconW(NIM_ADD, ref nid);

        nid.uVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIconW(NIM_SETVERSION, ref nid);
    }

    /// <summary>
    /// Switches the tray icon between active and inactive states.
    /// </summary>
    public void SetActive(bool isActive)
    {
        if (_isActive == isActive)
        {
            return;
        }

        _isActive = isActive;

        var nid = CreateNotifyIconData();
        nid.uFlags = NIF_ICON;
        nid.hIcon = isActive ? _hIconActive : _hIconInactive;
        Shell_NotifyIconW(NIM_MODIFY, ref nid);
    }

    private NOTIFYICONDATAW CreateNotifyIconData()
    {
        return new NOTIFYICONDATAW
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = _hwnd,
            uID = 1,
            szTip = string.Empty,
            szInfo = string.Empty,
            szInfoTitle = string.Empty
        };
    }

    private void ShowContextMenu()
    {
        var hMenu = CreatePopupMenu();
        if (hMenu == 0)
        {
            return;
        }

        try
        {
            AppendMenuW(hMenu, MF_STRING, MENU_ID_SETTINGS, "Settings");
            AppendMenuW(hMenu, MF_STRING, MENU_ID_TOGGLE, "Toggle");
            AppendMenuW(hMenu, MF_SEPARATOR, 0, null);
            AppendMenuW(hMenu, MF_STRING, MENU_ID_EXIT, "Exit");

            GetCursorPos(out var pt);
            SetForegroundWindow(_hwnd);
            TrackPopupMenu(hMenu, TPM_RIGHTBUTTON, pt.X, pt.Y, 0, _hwnd, 0);
        }
        finally
        {
            DestroyMenu(hMenu);
        }
    }

    private static nint TrayWndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (_instance is not { } instance)
        {
            return DefWindowProcW(hwnd, msg, wParam, lParam);
        }

        if (msg == WM_TRAYICON)
        {
            uint eventId = (uint)(lParam & 0xFFFF);
            switch (eventId)
            {
                case WM_LBUTTONUP:
                    instance.Clicked?.Invoke();
                    return 0;
                case WM_LBUTTONDBLCLK:
                    instance.DoubleClicked?.Invoke();
                    return 0;
                case WM_RBUTTONUP:
                    instance.ShowContextMenu();
                    return 0;
            }
        }
        else if (msg == WM_COMMAND)
        {
            uint menuId = (uint)(wParam & 0xFFFF);
            switch (menuId)
            {
                case MENU_ID_SETTINGS:
                    instance.DoubleClicked?.Invoke();
                    return 0;
                case MENU_ID_TOGGLE:
                    instance.Clicked?.Invoke();
                    return 0;
                case MENU_ID_EXIT:
                    Environment.Exit(0);
                    return 0;
            }
        }

        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    private static unsafe nint CreateHIconFromIcoBytes(byte[] icoData)
    {
        fixed (byte* pData = icoData)
        {
            int id = LookupIconIdFromDirectoryEx((nint)pData, true, 0, 0, LR_DEFAULTCOLOR);
            if (id == 0)
            {
                return 0;
            }

            int count = icoData[4] | (icoData[5] << 8);
            for (int i = 0; i < count; i++)
            {
                int entryOffset = 6 + i * 16;
                int dataOffset = icoData[entryOffset + 12] |
                                 (icoData[entryOffset + 13] << 8) |
                                 (icoData[entryOffset + 14] << 16) |
                                 (icoData[entryOffset + 15] << 24);

                if (dataOffset == id)
                {
                    int size = icoData[entryOffset + 8] |
                               (icoData[entryOffset + 9] << 8) |
                               (icoData[entryOffset + 10] << 16) |
                               (icoData[entryOffset + 11] << 24);

                    return CreateIconFromResourceEx((nint)(pData + id), (uint)size, true, 0x00030000, 0, 0, LR_DEFAULTCOLOR);
                }
            }

            return 0;
        }
    }

    private static byte[] LoadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new FileNotFoundException($"Embedded resource '{name}' not found.");
        var data = new byte[stream.Length];
        stream.ReadExactly(data);
        return data;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        var nid = CreateNotifyIconData();
        Shell_NotifyIconW(NIM_DELETE, ref nid);

        if (_hIconActive != 0)
        {
            DestroyIcon(_hIconActive);
        }

        if (_hIconInactive != 0)
        {
            DestroyIcon(_hIconInactive);
        }

        if (_hwnd != 0)
        {
            DestroyWindow(_hwnd);
            _hwnd = 0;
        }

        _instance = null;
    }
}
