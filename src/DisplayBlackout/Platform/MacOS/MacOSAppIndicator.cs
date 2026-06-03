using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DisplayBlackout.Platform.MacOS;

internal sealed unsafe class MacOSAppIndicator : IAppIndicator
{
    private const string ObjCLib = "/usr/lib/libobjc.A.dylib";

    private static readonly List<object> _keepAlive = [];
    private static nint _actionDispatcher;
    private static MacOSAppIndicator? _instance;

    private nint _statusItem;
    private nint _activeImage;
    private nint _inactiveImage;
    private bool _disposed;

    public event Action? Clicked;
    public event Action? DoubleClicked;

    public MacOSAppIndicator(string activeIconPath, string inactiveIconPath)
    {
        _instance = this;
        _activeImage = LoadTemplateImage(activeIconPath);
        _inactiveImage = LoadTemplateImage(inactiveIconPath);
    }

    public void Show()
    {
        nint statusBar = objc_msgSend_nint(objc_getClass("NSStatusBar"), sel("systemStatusBar"));
        if (statusBar == 0) return;

        _statusItem = objc_msgSend_nint_double(statusBar, sel("statusItemWithLength:"), -1.0);
        if (_statusItem == 0) return;

        nint button = objc_msgSend_nint(_statusItem, sel("button"));
        if (button != 0 && _inactiveImage != 0)
            objc_msgSend_void_nint(button, sel("setImage:"), _inactiveImage);

        nint menu = BuildMenu();
        objc_msgSend_void_nint(_statusItem, sel("setMenu:"), menu);
    }

    public void SetActive(bool isActive)
    {
        if (_statusItem == 0) return;
        nint button = objc_msgSend_nint(_statusItem, sel("button"));
        if (button == 0) return;
        nint image = isActive ? _activeImage : _inactiveImage;
        if (image != 0)
            objc_msgSend_void_nint(button, sel("setImage:"), image);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _instance = null;

        if (_statusItem != 0)
        {
            nint statusBar = objc_msgSend_nint(objc_getClass("NSStatusBar"), sel("systemStatusBar"));
            if (statusBar != 0)
                objc_msgSend_void_nint(statusBar, sel("removeStatusItem:"), _statusItem);
            _statusItem = 0;
        }
    }

    private static nint BuildMenu()
    {
        nint menu = objc_msgSend_nint(
            objc_msgSend_nint(objc_getClass("NSMenu"), sel("alloc")),
            sel("init"));

        AddMenuItem(menu, "Toggle Blackout", () => _instance?.Clicked?.Invoke());
        AddMenuItem(menu, "Settings", () => _instance?.DoubleClicked?.Invoke());
        AddSeparator(menu);
        AddMenuItem(menu, "Exit", () => Environment.Exit(0));

        return menu;
    }

    private static void AddMenuItem(nint menu, string title, Action callback)
    {
        nint titleStr = CreateNSString(title);
        nint emptyStr = CreateNSString("");
        nint item = objc_msgSend_nint_nint_nint_nint(
            objc_msgSend_nint(objc_getClass("NSMenuItem"), sel("alloc")),
            sel("initWithTitle:action:keyEquivalent:"),
            titleStr,
            sel("invokeAction:"),
            emptyStr);

        _keepAlive.Add(callback);
        int idx = _keepAlive.Count - 1;
        nint nsNum = objc_msgSend_nint_nint(objc_getClass("NSNumber"), sel("numberWithInteger:"), (nint)idx);
        objc_msgSend_void_nint(item, sel("setRepresentedObject:"), nsNum);
        objc_msgSend_void_nint(item, sel("setTarget:"), GetActionDispatcher());
        objc_msgSend_void_nint(menu, sel("addItem:"), item);
    }

    private static void AddSeparator(nint menu)
    {
        nint sep = objc_msgSend_nint(objc_getClass("NSMenuItem"), sel("separatorItem"));
        objc_msgSend_void_nint(menu, sel("addItem:"), sep);
    }

    private static nint GetActionDispatcher()
    {
        if (_actionDispatcher != 0) return _actionDispatcher;

        nint cls = objc_allocateClassPair(objc_getClass("NSObject"), "DBMenuActionDispatcher", 0);
        if (cls == 0) return 0;

        class_addMethod(cls, sel("invokeAction:"), &MenuItemInvoked, "v@:@");
        objc_registerClassPair(cls);

        _actionDispatcher = objc_msgSend_nint(
            objc_msgSend_nint(cls, sel("alloc")),
            sel("init"));
        return _actionDispatcher;
    }

    [UnmanagedCallersOnly]
    private static void MenuItemInvoked(nint self, nint sel, nint sender)
    {
        try
        {
            nint repObj = objc_msgSend_nint(sender, sel_registerName("representedObject"));
            if (repObj == 0) return;
            long idx = (long)objc_msgSend_nint(repObj, sel_registerName("integerValue"));
            if (idx >= 0 && idx < _keepAlive.Count && _keepAlive[(int)idx] is Action action)
                action();
        }
        catch { }
    }

    private static nint LoadTemplateImage(string path)
    {
        if (!File.Exists(path)) return 0;
        nint pathStr = CreateNSString(path);
        nint image = objc_msgSend_nint_nint(
            objc_msgSend_nint(objc_getClass("NSImage"), sel("alloc")),
            sel("initByReferencingFile:"),
            pathStr);
        if (image == 0) return 0;
        objc_msgSend_void_bool(image, sel("setTemplate:"), true);
        objc_msgSend_void_size(image, sel("setSize:"), 18.0, 18.0);
        return image;
    }

    private static nint sel(string name) => sel_registerName(name);

    private static nint CreateNSString(string s)
        => objc_msgSend_nint_str(objc_getClass("NSString"), sel("stringWithUTF8String:"), s);

    public static MacOSAppIndicator FromResources(Assembly assembly)
    {
        string cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DisplayBlackout");
        Directory.CreateDirectory(cacheDir);

        string activePath = ExtractResource(assembly, "StatusActive.png", cacheDir);
        string inactivePath = ExtractResource(assembly, "StatusInactive.png", cacheDir);
        return new MacOSAppIndicator(activePath, inactivePath);
    }

    private static string ExtractResource(Assembly assembly, string resourceSuffix, string dir)
    {
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(resourceSuffix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Embedded resource '{resourceSuffix}' not found.");
        var outPath = Path.Combine(dir, resourceSuffix);
        using var src = assembly.GetManifestResourceStream(name)!;
        using var dst = File.Create(outPath);
        src.CopyTo(dst);
        return outPath;
    }

    [DllImport(ObjCLib)] private static extern nint objc_getClass(string name);
    [DllImport(ObjCLib)] private static extern nint sel_registerName(string name);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] private static extern nint objc_msgSend_nint(nint r, nint s);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] private static extern nint objc_msgSend_nint_nint(nint r, nint s, nint a1);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] private static extern nint objc_msgSend_nint_double(nint r, nint s, double a1);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] private static extern nint objc_msgSend_nint_str(nint r, nint s, [MarshalAs(UnmanagedType.LPUTF8Str)] string str);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] private static extern nint objc_msgSend_nint_nint_nint_nint(nint r, nint s, nint a1, nint a2, nint a3);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] private static extern void objc_msgSend_void_nint(nint r, nint s, nint a1);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] private static extern void objc_msgSend_void_bool(nint r, nint s, [MarshalAs(UnmanagedType.I1)] bool a1);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] private static extern void objc_msgSend_void_size(nint r, nint s, double w, double h);
    [DllImport(ObjCLib)] private static extern nint objc_allocateClassPair(nint superclass, string name, nint extraBytes);
    [DllImport(ObjCLib)] private static extern void objc_registerClassPair(nint cls);
    [DllImport(ObjCLib)] private static extern bool class_addMethod(nint cls, nint name, delegate* unmanaged<nint, nint, nint, void> imp, string types);
}
