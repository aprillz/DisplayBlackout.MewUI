using System.Runtime.InteropServices;

namespace DisplayBlackout.Platform.MacOS;

internal sealed class MacOSBlackoutOverlay : IBlackoutOverlay
{
    private const string ObjCLib = "/usr/lib/libobjc.A.dylib";
    private const string CoreGraphicsLib = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    // NSScreenSaverWindowLevel — above menu bar and dock
    private const int WindowLevel = 1000;
    // NSWindowCollectionBehaviorCanJoinAllSpaces
    private const int CollectionBehavior = 1;

    private nint _window;
    private bool _disposed;

    public MacOSBlackoutOverlay(DisplayInfo display, int opacityPercent, bool clickThrough)
    {
        // CG coordinates: origin = top-left of primary display, Y increases downward
        // NS coordinates: origin = bottom-left of primary display, Y increases upward
        double mainH = GetMainDisplayHeight();
        double nsX = display.Bounds.Left;
        double nsY = mainH - display.Bounds.Top - display.Bounds.Height;
        double w = display.Bounds.Width;
        double h = display.Bounds.Height;

        nint win = objc_msgSend_ret(objc_getClass("NSWindow"), sel("alloc"));
        win = objc_msgSend_init_window(win, sel("initWithContentRect:styleMask:backing:defer:"),
            nsX, nsY, w, h,
            /* NSBorderlessWindowMask */ 0,
            /* NSBackingStoreBuffered */ 2,
            /* defer */ 0);

        nint black = objc_msgSend_ret(objc_getClass("NSColor"), sel("blackColor"));
        objc_msgSend_void_nint(win, sel("setBackgroundColor:"), black);
        objc_msgSend_void_bool(win, sel("setOpaque:"), true);
        objc_msgSend_void_double(win, sel("setAlphaValue:"), Math.Clamp(opacityPercent, 0, 100) / 100.0);
        objc_msgSend_void_int(win, sel("setLevel:"), WindowLevel);
        objc_msgSend_void_int(win, sel("setCollectionBehavior:"), CollectionBehavior);
        objc_msgSend_void_bool(win, sel("setIgnoresMouseEvents:"), clickThrough);

        _window = win;
        objc_msgSend_void_nint(win, sel("orderFrontRegardless"), 0);
    }

    public void SetOpacity(int opacityPercent)
    {
        if (_window == 0) return;
        objc_msgSend_void_double(_window, sel("setAlphaValue:"), Math.Clamp(opacityPercent, 0, 100) / 100.0);
    }

    public void SetClickThrough(bool clickThrough)
    {
        if (_window == 0) return;
        objc_msgSend_void_bool(_window, sel("setIgnoresMouseEvents:"), clickThrough);
    }

    public void BringToFront()
    {
        if (_window == 0) return;
        objc_msgSend_void_int(_window, sel("setLevel:"), WindowLevel);
        objc_msgSend_void_nint(_window, sel("orderFrontRegardless"), 0);
    }

    public void Dispose()
    {
        if (_disposed || _window == 0) return;
        _disposed = true;
        var win = _window;
        _window = 0;
        objc_msgSend_void(win, sel("close"));
    }

    private static double GetMainDisplayHeight()
    {
        uint mainId = CGMainDisplayID();
        CGRect bounds = CGDisplayBounds(mainId);
        return bounds.size.height;
    }

    private static nint sel(string name) => sel_registerName(name);

    [DllImport(ObjCLib)] private static extern nint objc_getClass(string name);
    [DllImport(ObjCLib)] private static extern nint sel_registerName(string name);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] private static extern nint objc_msgSend_ret(nint r, nint s);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] private static extern void objc_msgSend_void(nint r, nint s);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] private static extern void objc_msgSend_void_nint(nint r, nint s, nint a1);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] private static extern void objc_msgSend_void_bool(nint r, nint s, [MarshalAs(UnmanagedType.I1)] bool a1);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] private static extern void objc_msgSend_void_int(nint r, nint s, int a1);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] private static extern void objc_msgSend_void_double(nint r, nint s, double a1);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend_init_window(nint r, nint s,
        double x, double y, double w, double h,
        nint styleMask, nint backing, nint defer);

    [DllImport(CoreGraphicsLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern uint CGMainDisplayID();

    [DllImport(CoreGraphicsLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern CGRect CGDisplayBounds(uint display);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CGRect
    {
        public readonly CGPoint origin;
        public readonly CGSize size;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CGPoint { public readonly double x, y; }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CGSize { public readonly double width, height; }
}
