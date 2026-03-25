using static DisplayBlackout.NativeMethods;

namespace DisplayBlackout;

/// <summary>
/// Enumerates display monitors using Win32 APIs, replacing WinAppSDK's DisplayArea.
/// </summary>
internal static class MonitorHelper
{
    public static List<MonitorInfo> GetAllMonitors()
    {
        var monitors = new List<MonitorInfo>();

        EnumDisplayMonitors(0, 0, (nint hMonitor, nint hdcMonitor, ref RECT lprcMonitor, nint dwData) =>
        {
            var info = MONITORINFOEXW.Create();
            if (GetMonitorInfo(hMonitor, ref info))
            {
                monitors.Add(new MonitorInfo(
                    hMonitor,
                    info.rcMonitor,
                    info.rcWork,
                    (info.dwFlags & MONITORINFOF_PRIMARY) != 0,
                    info.szDevice));
            }
            return true;
        }, 0);

        return monitors;
    }
}

internal sealed record MonitorInfo(nint Handle, RECT Bounds, RECT WorkArea, bool IsPrimary, string DeviceName)
{
    public string BoundsKey => $"{Bounds.Left},{Bounds.Top},{Bounds.Width},{Bounds.Height}";
}