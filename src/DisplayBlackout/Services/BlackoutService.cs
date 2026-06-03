using Aprillz.MewUI;

using DisplayBlackout.Platform;

namespace DisplayBlackout.Services;

internal sealed partial class BlackoutService : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly IDisplayService _displayService;
    private readonly IBlackoutOverlayFactory _overlayFactory;
    private readonly Dictionary<string, IBlackoutOverlay> _blackoutOverlays = [];
    private HashSet<string>? _selectedMonitorIds;
    private bool _disposed;

    public ObservableValue<bool> IsBlackedOut { get; }

    public ObservableValue<int> Opacity { get; }

    public ObservableValue<bool> ClickThrough { get; }

    public BlackoutService(SettingsService settingsService, IDisplayService displayService, IBlackoutOverlayFactory overlayFactory)
    {
        _settingsService = settingsService;
        _displayService = displayService;
        _overlayFactory = overlayFactory;
        _selectedMonitorIds = _settingsService.LoadSelectedMonitorIds();

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
    public void UpdateSelectedMonitors(HashSet<string>? monitorIds)
    {
        _selectedMonitorIds = monitorIds;
        _settingsService.SaveSelectedMonitorIds(monitorIds);

        if (IsBlackedOut.Value)
        {
            RefreshOverlays();
        }
    }

    private void RefreshOverlays()
    {
        var monitors = _displayService.GetDisplays();
        var liveIds = monitors.Select(static m => m.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var monitor in monitors)
        {
            bool shouldBlackOut = _selectedMonitorIds != null
                ? _selectedMonitorIds.Contains(monitor.Id)
                : !monitor.IsPrimary;

            bool hasOverlay = _blackoutOverlays.ContainsKey(monitor.Id);

            if (shouldBlackOut && !hasOverlay)
            {
                _blackoutOverlays[monitor.Id] = _overlayFactory.Create(monitor, Opacity.Value, ClickThrough.Value);
            }
            else if (!shouldBlackOut && hasOverlay)
            {
                _blackoutOverlays[monitor.Id].Dispose();
                _blackoutOverlays.Remove(monitor.Id);
            }
        }

        foreach (var id in _blackoutOverlays.Keys.Where(id => !liveIds.Contains(id)).ToArray())
        {
            _blackoutOverlays[id].Dispose();
            _blackoutOverlays.Remove(id);
        }
    }

    /// <summary>
    /// Gets the currently selected monitor bounds for UI initialization.
    /// </summary>
    public IReadOnlySet<string>? SelectedMonitorIds => _selectedMonitorIds;

    public IReadOnlyList<DisplayInfo> GetDisplays() => _displayService.GetDisplays();

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
        var monitors = _displayService.GetDisplays();

        foreach (var monitor in monitors)
        {
            bool shouldBlackOut = _selectedMonitorIds != null
                ? _selectedMonitorIds.Contains(monitor.Id)
                : !monitor.IsPrimary;

            if (!shouldBlackOut)
            {
                continue;
            }

            _blackoutOverlays[monitor.Id] = _overlayFactory.Create(monitor, Opacity.Value, ClickThrough.Value);
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
