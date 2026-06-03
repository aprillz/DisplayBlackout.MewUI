namespace DisplayBlackout.Platform.Win32;

internal sealed class Win32DisplayService : IDisplayService
{
    public IReadOnlyList<DisplayInfo> GetDisplays()
    {
        var monitors = MonitorHelper.GetAllMonitors();
        var displays = new List<DisplayInfo>(monitors.Count);

        for (int i = 0; i < monitors.Count; i++)
        {
            var monitor = monitors[i];
            displays.Add(new DisplayInfo(
                monitor.DeviceName,
                new DisplayBounds(
                    monitor.Bounds.Left,
                    monitor.Bounds.Top,
                    monitor.Bounds.Width,
                    monitor.Bounds.Height),
                monitor.IsPrimary,
                monitor.IsPrimary ? "Primary Display" : $"Display {i + 1}",
                i + 1));
        }

        return displays;
    }
}
