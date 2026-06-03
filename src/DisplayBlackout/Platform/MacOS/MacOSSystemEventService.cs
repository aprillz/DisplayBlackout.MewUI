using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DisplayBlackout.Platform.MacOS;

internal sealed unsafe class MacOSSystemEventService : ISystemEventService
{
    private const string CarbonLib = "/System/Library/Frameworks/Carbon.framework/Carbon";
    private const string CoreGraphicsLib = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    // kEventClassKeyboard = 'keyb', kEventHotKeyPressed = 6
    private const uint kEventClassKeyboard = 0x6B657962;
    private const uint kEventHotKeyPressed = 6;

    // kVK_ANSI_B = 0x0B, cmdKey = 0x0100, shiftKey = 0x0200
    private const uint HotKeyCode = 0x0B;
    private const uint HotKeyModifiers = 0x0100 | 0x0200; // Cmd+Shift

    private static MacOSSystemEventService? _instance;

    private nint _hotKeyRef;
    private nint _eventHandlerRef;
    private bool _initialized;

    public event EventHandler? HotkeyPressed;
    public event EventHandler? DisplayChanged;
#pragma warning disable CS0067
    public event EventHandler? FocusChanged;
#pragma warning restore CS0067

    public bool Initialize()
    {
        if (_initialized) return true;
        _instance = this;

        bool hotkeyOk = RegisterHotKey();
        RegisterDisplayReconfiguration();

        _initialized = true;
        return hotkeyOk;
    }

    private bool RegisterHotKey()
    {
        var hotKeyId = new EventHotKeyID { signature = 0x44424B4F /* 'DBKO' */, id = 1 };
        nint target = GetApplicationEventTarget();
        if (target == 0) return false;

        int err = RegisterEventHotKey(HotKeyCode, HotKeyModifiers, hotKeyId, target, 0, out _hotKeyRef);
        if (err != 0) return false;

        var eventSpec = new EventTypeSpec { eventClass = kEventClassKeyboard, eventKind = kEventHotKeyPressed };
        InstallEventHandler(target, &HotKeyEventHandler, 1, &eventSpec, null, out _eventHandlerRef);
        return true;
    }

    private static void RegisterDisplayReconfiguration()
    {
        CGDisplayRegisterReconfigurationCallback(&OnDisplayReconfigured, null);
    }

    [UnmanagedCallersOnly]
    private static int HotKeyEventHandler(nint callRef, nint eventRef, void* userData)
    {
        _instance?.HotkeyPressed?.Invoke(_instance, EventArgs.Empty);
        return 0; // noErr
    }

    [UnmanagedCallersOnly]
    private static void OnDisplayReconfigured(uint displayId, uint flags, void* userInfo)
    {
        // kCGDisplayBeginConfigurationFlag = 1 — skip the begin notification, fire on completion
        if ((flags & 1u) == 0)
            _instance?.DisplayChanged?.Invoke(_instance, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (!_initialized) return;
        _initialized = false;
        _instance = null;

        CGDisplayRemoveReconfigurationCallback(&OnDisplayReconfigured, null);

        if (_eventHandlerRef != 0)
        {
            RemoveEventHandler(_eventHandlerRef);
            _eventHandlerRef = 0;
        }
        if (_hotKeyRef != 0)
        {
            UnregisterEventHotKey(_hotKeyRef);
            _hotKeyRef = 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EventHotKeyID
    {
        public uint signature;
        public uint id;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EventTypeSpec
    {
        public uint eventClass;
        public uint eventKind;
    }

    [DllImport(CarbonLib)] private static extern nint GetApplicationEventTarget();
    [DllImport(CarbonLib)] private static extern int RegisterEventHotKey(uint keyCode, uint modifiers, EventHotKeyID id, nint target, uint options, out nint outRef);
    [DllImport(CarbonLib)] private static extern int UnregisterEventHotKey(nint hotKeyRef);
    [DllImport(CarbonLib)] private static extern int InstallEventHandler(nint target, delegate* unmanaged<nint, nint, void*, int> handler, nuint numTypes, EventTypeSpec* list, void* userData, out nint outRef);
    [DllImport(CarbonLib)] private static extern int RemoveEventHandler(nint handlerRef);

    [DllImport(CoreGraphicsLib)] private static extern void CGDisplayRegisterReconfigurationCallback(delegate* unmanaged<uint, uint, void*, void> callback, void* userInfo);
    [DllImport(CoreGraphicsLib)] private static extern void CGDisplayRemoveReconfigurationCallback(delegate* unmanaged<uint, uint, void*, void> callback, void* userInfo);
}
