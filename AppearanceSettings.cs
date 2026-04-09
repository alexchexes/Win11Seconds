using Microsoft.Win32;

readonly record struct AppearanceSettings(
    bool IsLightTheme,
    bool IsTransparencyEffectsEnabled)
{
    public Color PopupBackgroundColor =>
        IsLightTheme ? Color.White : Color.FromArgb(32, 32, 32);

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
