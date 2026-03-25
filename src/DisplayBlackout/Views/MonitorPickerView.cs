using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

using DisplayBlackout.Services;

namespace DisplayBlackout.Views;

/// <summary>
/// Visual monitor layout where each monitor is a toggle button.
/// Replaces the WinUI Viewbox + ItemsControl pattern.
/// </summary>
internal sealed class MonitorPickerView : UserControl
{
    private readonly BlackoutService _blackoutService;
    private readonly StackPanel _container;
    private readonly List<MonitorToggle> _toggles = [];

    public MonitorPickerView(BlackoutService blackoutService)
    {
        _blackoutService = blackoutService;
        _container = new StackPanel().Horizontal().Spacing(4).Center();
        Content = _container;

        BuildMonitors();
    }

    public void Rebuild()
    {
        _toggles.Clear();
        _container.Clear();
        BuildMonitors();
    }

    private void BuildMonitors()
    {
        var monitors = MonitorHelper.GetAllMonitors();
        if (monitors.Count == 0)
        {
            return;
        }

        // Assign display numbers by DeviceName order (\\.\DISPLAY1, \\.\DISPLAY2, ...)
        var displayNumbers = new Dictionary<string, int>();
        var sorted = monitors.OrderBy(m => m.DeviceName, StringComparer.OrdinalIgnoreCase).ToList();
        for (int i = 0; i < sorted.Count; i++)
        {
            displayNumbers[sorted[i].BoundsKey] = i + 1;
        }

        // Sort by X then Y for visual layout
        monitors.Sort((a, b) =>
        {
            int xCompare = a.Bounds.Left.CompareTo(b.Bounds.Left);
            return xCompare != 0 ? xCompare : a.Bounds.Top.CompareTo(b.Bounds.Top);
        });

        // Find bounding box for scaling
        int maxHeight = 0;
        foreach (var m in monitors)
        {
            maxHeight = Math.Max(maxHeight, m.Bounds.Height);
        }

        // Scale so tallest monitor is ~120 DIPs
        const double targetHeight = 120;
        double scale = maxHeight > 0 ? targetHeight / maxHeight : 1;

        var selectedBounds = _blackoutService.SelectedMonitorBounds;

        for (int i = 0; i < monitors.Count; i++)
        {
            var monitor = monitors[i];
            double w = monitor.Bounds.Width * scale;
            double h = monitor.Bounds.Height * scale;

            bool isSelected = selectedBounds != null
                ? selectedBounds.Contains(monitor.BoundsKey)
                : !monitor.IsPrimary;

            var toggle = new MonitorToggle(displayNumbers[monitor.BoundsKey], monitor.BoundsKey, monitor.IsPrimary, isSelected);
            toggle.Button
                .Width(w)
                .Height(h);
            toggle.Button.CheckedChanged += _ => UpdateSelection();

            _toggles.Add(toggle);
            _container.Add(toggle.Button);
        }
    }

    private void UpdateSelection()
    {
        var selected = new HashSet<string>();
        foreach (var toggle in _toggles)
        {
            if (toggle.Button.IsChecked)
            {
                selected.Add(toggle.BoundsKey);
            }
        }
        _blackoutService.UpdateSelectedMonitors(selected);
    }
}

internal sealed class MonitorToggle
{
    public string BoundsKey { get; }
    public bool IsPrimary { get; }
    public ToggleButton Button { get; }

    public MonitorToggle(int displayNumber, string boundsKey, bool isPrimary, bool isSelected)
    {
        BoundsKey = boundsKey;
        IsPrimary = isPrimary;

        var label = new TextBlock()
            .Text(displayNumber.ToString())
            .CenterHorizontal()
            .CenterVertical()
            .FontSize(18)
            .Bold();

        Button = new ToggleButton()
            .IsChecked(isSelected)
            .BorderThickness(0)
            .CornerRadius(4)
            .Padding(0)
            .Content(label);

        void ApplyColors(Theme t)
        {
            if (Button.IsChecked)
            {
                Button.Background(Color.Black);
                label.Foreground(Color.White);
            }
            else
            {
                Button.Background(t.IsDark ? Color.Gray : Color.LightGray);
                label.Foreground(t.IsDark ? Color.White : Color.Black);
            }
        }

        Button.WithTheme((t, _) => ApplyColors(t));
        Button.CheckedChanged += _ => Button.WithTheme((t, _) => ApplyColors(t));
    }
}
