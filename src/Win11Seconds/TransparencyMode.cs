namespace Win11Seconds;

using Microsoft.Win32;

enum TransparencyMode
{
    Off = 0,
    AlwaysOn = 1,
    OnlyActive = 2
}

static class TransparencyModeStore
{
    private const string SettingsRegistryKey = @"HKEY_CURRENT_USER\Software\Win11Seconds";
    private const string TransparencyModeValueName = "TransparencyMode";

    internal const TransparencyMode DefaultMode = TransparencyMode.OnlyActive;

    public static TransparencyMode ReadCurrent()
    {
        int value = (int?)Registry.GetValue(
            SettingsRegistryKey,
            TransparencyModeValueName,
            (int)DefaultMode) ?? (int)DefaultMode;

        return Normalize(value);
    }

    public static void WriteCurrent(TransparencyMode mode)
    {
        Registry.SetValue(
            SettingsRegistryKey,
            TransparencyModeValueName,
            (int)mode,
            RegistryValueKind.DWord);
    }

    internal static TransparencyMode Normalize(int value)
    {
        return value switch
        {
            (int)TransparencyMode.Off => TransparencyMode.Off,
            (int)TransparencyMode.AlwaysOn => TransparencyMode.AlwaysOn,
            (int)TransparencyMode.OnlyActive => TransparencyMode.OnlyActive,
            _ => DefaultMode
        };
    }
}

static class AlwaysOnTopPreferenceStore
{
    private const string SettingsRegistryKey = @"HKEY_CURRENT_USER\Software\Win11Seconds";
    private const string AlwaysOnTopValueName = "AlwaysOnTop";

    internal const bool DefaultValue = true;

    public static bool ReadCurrent()
    {
        int value = (int?)Registry.GetValue(
            SettingsRegistryKey,
            AlwaysOnTopValueName,
            DefaultValue ? 1 : 0) ?? (DefaultValue ? 1 : 0);

        return Normalize(value);
    }

    public static void WriteCurrent(bool value)
    {
        Registry.SetValue(
            SettingsRegistryKey,
            AlwaysOnTopValueName,
            value ? 1 : 0,
            RegistryValueKind.DWord);
    }

    internal static bool Normalize(int value)
    {
        return value switch
        {
            0 => false,
            1 => true,
            _ => DefaultValue
        };
    }
}

static class TransparencyModePolicy
{
    public static bool ShouldEnableSystemBackdrop(
        TransparencyMode mode,
        bool isOsTransparencyEnabled,
        bool isClockActive,
        bool isBackdropFadeOutInProgress)
    {
        if (!isOsTransparencyEnabled)
        {
            return false;
        }

        return mode switch
        {
            TransparencyMode.Off => false,
            TransparencyMode.AlwaysOn => true,
            TransparencyMode.OnlyActive => isClockActive || isBackdropFadeOutInProgress,
            _ => false
        };
    }

    public static bool ShouldFadeOutOnDeactivation(
        TransparencyMode mode,
        bool isOsTransparencyEnabled)
    {
        return isOsTransparencyEnabled && mode == TransparencyMode.OnlyActive;
    }
}
