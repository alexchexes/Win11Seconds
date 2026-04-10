namespace Win11Seconds;

using Microsoft.Win32;

readonly record struct AppearanceSettings(
    bool IsLightTheme,
    bool IsTransparencyEffectsEnabled)
{
    private static readonly Color LightPopupBackground = Color.White;
    private static readonly Color DarkPopupBackground = Color.FromArgb(32, 32, 32);
    private static readonly Color LightPopupActiveBackdropOverlay = Color.FromArgb(72, 255, 255, 255);
    private static readonly Color DarkPopupActiveBackdropOverlay = Color.FromArgb(117, 0, 0, 0);

    public bool UseImmersiveDarkMode => !IsLightTheme;

    public Color PopupSolidBackgroundColor =>
        IsLightTheme ? LightPopupBackground : DarkPopupBackground;

    public Color PopupInactiveBackgroundColor =>
        IsLightTheme ? LightPopupBackground : DarkPopupBackground;

    public Color PopupInactiveBackdropOverlayColor =>
        Color.FromArgb(255, PopupInactiveBackgroundColor);

    public Color PopupActiveBackdropOverlayColor =>
        IsLightTheme
            ? LightPopupActiveBackdropOverlay
            : DarkPopupActiveBackdropOverlay;

    public Color PopupForegroundColor =>
        IsLightTheme ? Color.Black : Color.White;

    public string TrayIconResourceName =>
        IsLightTheme ? "Win11Seconds.tray_dark.ico" : "Win11Seconds.tray_light.ico";
}

static class AppearanceSettingsReader
{
    private const string PersonalizeRegistryKey =
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static AppearanceSettings ReadCurrent()
    {
        return new AppearanceSettings(
            ReadPersonalizeValue("AppsUseLightTheme", 1) != 0,
            ReadPersonalizeValue("EnableTransparency", 1) != 0);
    }

    private static int ReadPersonalizeValue(string valueName, int defaultValue)
    {
        return (int?)Registry.GetValue(PersonalizeRegistryKey, valueName, defaultValue) ?? defaultValue;
    }
}

sealed class AppearanceSettingsMonitor : IDisposable
{
    private AppearanceSettings current;

    public AppearanceSettingsMonitor()
    {
        current = AppearanceSettingsReader.ReadCurrent();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public AppearanceSettings Current => current;

    public event EventHandler<AppearanceSettings>? Changed;

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        var next = AppearanceSettingsReader.ReadCurrent();
        if (next == current)
        {
            return;
        }

        current = next;
        Changed?.Invoke(this, next);
    }
}
