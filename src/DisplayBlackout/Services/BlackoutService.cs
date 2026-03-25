namespace DisplayBlackout.Services;

internal sealed partial class BlackoutService : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly Dictionary<nint, BlackoutOverlay> _blackoutOverlays = [];
    private HashSet<string>? _selectedMonitorBounds;
    private int _opacity;
    private bool _clickThrough;
    private bool _isBlackedOut;
    private bool _disposed;

    public BlackoutService(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _selectedMonitorBounds = _settingsService.LoadSelectedMonitorBounds();
        _opacity = _settingsService.LoadOpacity();
        _clickThrough = _settingsService.LoadClickThrough();
    }

    public bool IsBlackedOut => _isBlackedOut;

    public event EventHandler<BlackoutStateChangedEventArgs>? BlackoutStateChanged;

    /// <summary>
    /// Updates which monitors should be blacked out using their bounds as stable identifiers.
    /// Null means default (all non-primary).
    /// </summary>
    public void UpdateSelectedMonitors(HashSet<string>? monitorBounds)
    {
        _selectedMonitorBounds = monitorBounds;
        _settingsService.SaveSelectedMonitorBounds(monitorBounds);

        if (_isBlackedOut)
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
                var overlay = new BlackoutOverlay(monitor.Bounds, _opacity, _clickThrough);
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
    /// Gets the current opacity percentage (0-100).
    /// </summary>
    public int Opacity => _opacity;

    /// <summary>
    /// Updates the opacity of the blackout overlays.
    /// </summary>
    public void UpdateOpacity(int opacity)
    {
        _opacity = opacity;
        _settingsService.SaveOpacity(_opacity);

        foreach (var overlay in _blackoutOverlays.Values)
        {
            overlay.SetOpacity(_opacity);
        }
    }

    /// <summary>
    /// Gets whether click-through is enabled.
    /// </summary>
    public bool ClickThrough => _clickThrough;

    /// <summary>
    /// Updates whether the overlay is click-through.
    /// </summary>
    public void UpdateClickThrough(bool clickThrough)
    {
        _clickThrough = clickThrough;
        _settingsService.SaveClickThrough(_clickThrough);

        foreach (var overlay in _blackoutOverlays.Values)
        {
            overlay.SetClickThrough(_clickThrough);
        }
    }

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

    public void Toggle()
    {
        if (_isBlackedOut)
        {
            Restore();
        }
        else
        {
            BlackOut();
        }
    }

    public void BlackOut()
    {
        if (_isBlackedOut)
        {
            return;
        }

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

            var overlay = new BlackoutOverlay(monitor.Bounds, _opacity, _clickThrough);
            _blackoutOverlays[monitor.Handle] = overlay;
        }

        _isBlackedOut = true;
        BlackoutStateChanged?.Invoke(this, new BlackoutStateChangedEventArgs(true));
    }

    public void Restore()
    {
        if (!_isBlackedOut)
        {
            return;
        }

        foreach (var overlay in _blackoutOverlays.Values)
        {
            overlay.Dispose();
        }
        _blackoutOverlays.Clear();

        _isBlackedOut = false;
        BlackoutStateChanged?.Invoke(this, new BlackoutStateChangedEventArgs(false));
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

internal sealed class BlackoutStateChangedEventArgs(bool isBlackedOut) : EventArgs
{
    public bool IsBlackedOut { get; } = isBlackedOut;
}
