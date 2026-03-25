using Aprillz.MewUI;

namespace DisplayBlackout.Services;

internal sealed partial class BlackoutService : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly Dictionary<nint, BlackoutOverlay> _blackoutOverlays = [];
    private HashSet<string>? _selectedMonitorBounds;
    private bool _disposed;

    public ObservableValue<bool> IsBlackedOut { get; }

    public ObservableValue<int> Opacity { get; }

    public ObservableValue<bool> ClickThrough { get; }

    public BlackoutService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _selectedMonitorBounds = _settingsService.LoadSelectedMonitorBounds();

        IsBlackedOut = new(false);
        Opacity = new(settingsService.LoadOpacity());
        ClickThrough = new(settingsService.LoadClickThrough());

        IsBlackedOut.Subscribe(() =>
        {
            if (IsBlackedOut.Value)
                BlackOutInternal();
            else
                RestoreInternal();
        });

        Opacity.Subscribe(() =>
        {
            int value = Opacity.Value;
            _settingsService.SaveOpacity(value);
            foreach (var overlay in _blackoutOverlays.Values)
                overlay.SetOpacity(value);
        });

        ClickThrough.Subscribe(() =>
        {
            _settingsService.SaveClickThrough(ClickThrough.Value);
            foreach (var overlay in _blackoutOverlays.Values)
                overlay.SetClickThrough(ClickThrough.Value);
        });
    }

    /// <summary>
    /// Updates which monitors should be blacked out using their bounds as stable identifiers.
    /// Null means default (all non-primary).
    /// </summary>
    public void UpdateSelectedMonitors(HashSet<string>? monitorBounds)
    {
        _selectedMonitorBounds = monitorBounds;
        _settingsService.SaveSelectedMonitorBounds(monitorBounds);

        if (IsBlackedOut.Value)
        {
            RefreshOverlays();
        }
    }

    private void RefreshOverlays()
    {
        var monitors = MonitorHelper.GetAllMonitors();

        foreach (var monitor in monitors)
        {
            bool shouldBlackOut = _selectedMonitorBounds != null
                ? _selectedMonitorBounds.Contains(monitor.BoundsKey)
                : !monitor.IsPrimary;

            bool hasOverlay = _blackoutOverlays.ContainsKey(monitor.Handle);

            if (shouldBlackOut && !hasOverlay)
            {
                var overlay = new BlackoutOverlay(monitor.Bounds, Opacity.Value, ClickThrough.Value);
                _blackoutOverlays[monitor.Handle] = overlay;
            }
            else if (!shouldBlackOut && hasOverlay)
            {
                _blackoutOverlays[monitor.Handle].Dispose();
                _blackoutOverlays.Remove(monitor.Handle);
            }
        }
    }

    /// <summary>
    /// Gets the currently selected monitor bounds for UI initialization.
    /// </summary>
    public IReadOnlySet<string>? SelectedMonitorBounds => _selectedMonitorBounds;

    /// <summary>
    /// Brings all overlay windows to the front of the Z-order.
    /// </summary>
    public void BringAllToFront()
    {
        foreach (var overlay in _blackoutOverlays.Values)
        {
            overlay.BringToFront();
        }
    }

    public void Toggle() => IsBlackedOut.Value = !IsBlackedOut.Value;

    public void BlackOut() => IsBlackedOut.Value = true;

    public void Restore() => IsBlackedOut.Value = false;

    private void BlackOutInternal()
    {
        var monitors = MonitorHelper.GetAllMonitors();

        foreach (var monitor in monitors)
        {
            bool shouldBlackOut = _selectedMonitorBounds != null
                ? _selectedMonitorBounds.Contains(monitor.BoundsKey)
                : !monitor.IsPrimary;

            if (!shouldBlackOut)
            {
                continue;
            }

            var overlay = new BlackoutOverlay(monitor.Bounds, Opacity.Value, ClickThrough.Value);
            _blackoutOverlays[monitor.Handle] = overlay;
        }
    }

    private void RestoreInternal()
    {
        foreach (var overlay in _blackoutOverlays.Values)
        {
            overlay.Dispose();
        }
        _blackoutOverlays.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Restore();
    }
}