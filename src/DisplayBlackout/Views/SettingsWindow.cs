using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Rendering;

using DisplayBlackout.Services;

namespace DisplayBlackout.Views;

internal sealed class SettingsView : UserControl
{
    public SettingsView(BlackoutService blackoutService, SettingsService? settingsService = null)
    {
        var monitorPicker = new MonitorPickerView(blackoutService);

        var blackoutToggle = new ToggleSwitch()
            .IsChecked(blackoutService.IsBlackedOut)
            .OnCheckedChanged(isChecked =>
            {
                if (isChecked != blackoutService.IsBlackedOut)
                {
                    blackoutService.Toggle();
                }
            });

        // Sync toggle when state changes externally (hotkey, tray icon)
        blackoutService.BlackoutStateChanged += (_, e) =>
        {
            if (Application.IsRunning)
            {
                Application.Current?.Dispatcher?.BeginInvoke(() =>
                {
                    blackoutToggle.IsChecked = e.IsBlackedOut;
                });
            }
        };

        var opacitySlider = new Slider()
            .Width(200)
            .SmallChange(1)
            .Maximum(100)
            .Value(blackoutService.Opacity)
            .OnValueChanged(v =>
            {
                int newOpacity = (int)v;
                if (newOpacity != blackoutService.Opacity)
                {
                    blackoutService.UpdateOpacity(newOpacity);
                }
            });

        var clickThroughToggle = new ToggleSwitch()
            .IsChecked(blackoutService.ClickThrough)
            .OnCheckedChanged(isChecked =>
            {
                if (isChecked != blackoutService.ClickThrough)
                {
                    blackoutService.UpdateClickThrough(isChecked);
                }
            });

        Content = new ScrollViewer()
            .AutoVerticalScroll()
            .Content(
                new StackPanel()
                    .Vertical()
                    .Padding(24)
                    .Spacing(4)
                    .Children(
                        // Instructions
                        new TextBlock()
                            .Text("Click to toggle which displays will be blacked out.")
                            .WithTheme((t, c) => c.Foreground(t.Palette.DisabledText))
                            .Margin(0, 0, 0, 12),

                        // Monitor picker
                        new Border()
                            .CornerRadius(8)
                            .Padding(24)
                            .Margin(0, 0, 0, 12)
                            .WithTheme((t, c) => c
                                .Background(t.Palette.ContainerBackground)
                                .BorderBrush(t.Palette.ControlBorder)
                                .BorderThickness(1))
                            .Child(monitorPicker),

                        // Display blackout toggle (app logo icon, matching original ImageIcon)
                        SettingsCard(
                            new Image() { Source = ImageSource.FromResource(System.Reflection.Assembly.GetExecutingAssembly(), "icon.ico") }
                                .Width(20).Height(20),
                            "Display blackout", "Black out the selected displays", blackoutToggle),

                        // Opacity slider
                        SettingsCard("\uF08C", "Opacity (%)", "Adjust how dark the blackout overlay appears", opacitySlider),

                        // Click through toggle
                        SettingsCard("\uE8B0", "Click through", "Allow mouse and touch to pass through the overlay", clickThroughToggle),

                        // Activation section header
                        new TextBlock()
                            .Text("Activation")
                            .Bold()
                            .Margin(0, 24, 0, 4),

                        // Activation shortcut display
                        SettingsCard("\uEDA7", "Activation shortcut", "Press this shortcut to toggle blackout",
                            new StackPanel()
                                .Horizontal()
                                .Spacing(4)
                                .Children(
                                    WinKeyBadge(),
                                    KeyBadge("Shift"),
                                    KeyBadge("B")
                                )),

                        // Appearance
                        new Expander()
                            .Header(new TextBlock().Text("Appearance").Bold())
                            .Margin(0, 20, 0, 0)
                            .Content(
                                new StackPanel()
                                    .Vertical()
                                    .Margin(0, 4, 0, 0)
                                    .Spacing(4)
                                    .Children(
                                        // Theme
                                        SettingsCard("\uE793", "Theme", "Choose light or dark mode",
                                            new ComboBox()
                                                .Width(120)
                                                .Items("System", "Light", "Dark")
                                                .SelectedIndex(ThemeToIndex(settingsService?.LoadTheme()))
                                                .OnSelectionChanged(selected =>
                                                {
                                                    if (selected is string s)
                                                    {
                                                        var variant = s switch
                                                        {
                                                            "Light" => ThemeVariant.Light,
                                                            "Dark" => ThemeVariant.Dark,
                                                            _ => ThemeVariant.System
                                                        };
                                                        Application.Current?.SetTheme(variant);
                                                        settingsService?.SaveTheme(s);
                                                    }
                                                })),

                                        // Accent color
                                        SettingsCard("\uE790", "Accent color", "Choose the accent color",
                                            CreateAccentPicker(settingsService))
                                    ))
                    ));
    }

    private static FrameworkElement SettingsCard(string icon, string header, string description, FrameworkElement action)
    {
        var iconElement = new TextBlock()
            .Text(icon)
            .FontFamily("Segoe MDL2 Assets")
            .FontSize(16)
            .CenterVertical()
            .WithTheme((t, c) => c.Foreground(t.Palette.WindowText));

        return SettingsCard(iconElement, header, description, action);
    }

    private static FrameworkElement SettingsCard(FrameworkElement iconElement, string header, string description, FrameworkElement action)
    {
        return new Border()
            .CornerRadius(4)
            .BorderThickness(1)
            .Padding(16)
            .WithTheme((t, c) => c
                .Background(t.Palette.ControlBackground)
                .BorderBrush(t.Palette.ControlBackground.Lerp(t.Palette.ControlBorder, 0.5)))
            .Child(
                new DockPanel()
                    .Children(
                        action.DockRight().CenterVertical(),
                        iconElement.Margin(0, 0, 12, 0),
                        new StackPanel()
                            .Vertical()
                            .Spacing(2)
                            .CenterVertical()
                            .Children(
                                new TextBlock().Text(header).SemiBold(),
                                new TextBlock().Text(description)
                                    .WithTheme((t, c) => c.Foreground(t.Palette.DisabledText))
                            )
                    ));
    }

    private static int ThemeToIndex(string? theme) => theme switch
    {
        "Light" => 1,
        "Dark" => 2,
        _ => 0
    };

    private static FrameworkElement CreateAccentPicker(SettingsService? settingsService)
    {
        var panel = new StackPanel().Horizontal().Spacing(4);

        foreach (var accent in BuiltInAccent.Accents)
        {
            var a = accent; // capture
            var color = a.GetAccentColor(false);
            var swatch = new Border()
                .Width(24).Height(24)
                .CornerRadius(12)
                .Background(color)
                .Cursor(CursorType.Hand)
                .OnMouseDown(_ =>
                {
                    Application.Current?.SetAccent(a);
                    settingsService?.SaveAccent(a.ToString());
                });

            panel.Add(swatch);
        }

        return panel;
    }

    private static FrameworkElement WinKeyBadge()
    {
        // Windows logo: 4 squares matching the original PathIcon SVG data
        var winLogo = new PathShape
        {
            Data = PathGeometry.Parse("M9 20H0V11H9V20ZM20 20H11V11H20V20ZM9 9H0V0H9V9ZM20 9H11V0H20V9Z"),
            Stretch = Stretch.Uniform,
        };
        winLogo.Width(14).Height(14).CenterHorizontal().CenterVertical()
            .WithTheme((t, _) => winLogo.Fill(t.Palette.AccentText));

        return new Border()
            .Padding(6, 4)
            .MinWidth(32)
            .MinHeight(32)
            .WithTheme((t, c) => c
                .CornerRadius(t.Metrics.ControlCornerRadius)
                .Background(t.Palette.Accent)
                .BorderBrush(t.Palette.AccentBorderHotOverlay)
                .BorderThickness(1))
            .Child(winLogo);
    }

    private static FrameworkElement KeyBadge(string text)
    {
        var label = new TextBlock()
            .Text(text)
            .CenterHorizontal()
            .CenterVertical();

        return new Border()
            .Padding(6, 4)
            .MinWidth(32)
            .MinHeight(32)
            .WithTheme((t, c) => c
                .CornerRadius(t.Metrics.ControlCornerRadius)
                .Foreground(t.Palette.AccentText)
                .Background(t.Palette.Accent)
                .BorderBrush(t.Palette.AccentBorderHotOverlay)
                .BorderThickness(1))
            .Child(label);
    }
}
