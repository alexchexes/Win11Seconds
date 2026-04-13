namespace Win11Seconds.Tests;

public class AppearanceSettingsTests
{
    [Test]
    public void LightThemeWithLightWindowsMode_UsesExpectedColorsAndDarkTrayIcon()
    {
        var settings = new AppearanceSettings(
            IsLightTheme: true,
            IsWindowsLightTheme: true,
            IsTransparencyEffectsEnabled: true);

        Assert.Multiple(() =>
        {
            Assert.That(settings.UseImmersiveDarkMode, Is.False);
            Assert.That(settings.PopupSolidBackgroundColor, Is.EqualTo(Color.FromArgb(238, 238, 238)));
            Assert.That(settings.PopupInactiveBackgroundColor, Is.EqualTo(Color.FromArgb(238, 238, 238)));
            Assert.That(settings.PopupInactiveBackdropOverlayColor, Is.EqualTo(Color.FromArgb(255, 238, 238, 238)));
            Assert.That(settings.PopupActiveBackdropOverlayColor, Is.EqualTo(Color.FromArgb(160, 255, 255, 255)));
            Assert.That(settings.PopupForegroundColor, Is.EqualTo(Color.Black));
            Assert.That(settings.TrayIconResourceName, Is.EqualTo("Win11Seconds.tray_dark.ico"));
            Assert.That(settings.IsTransparencyEffectsEnabled, Is.True);
        });
    }

    [Test]
    public void DarkThemeWithDarkWindowsMode_UsesExpectedColorsAndLightTrayIcon()
    {
        var settings = new AppearanceSettings(
            IsLightTheme: false,
            IsWindowsLightTheme: false,
            IsTransparencyEffectsEnabled: false);

        Assert.Multiple(() =>
        {
            Assert.That(settings.UseImmersiveDarkMode, Is.True);
            Assert.That(settings.PopupSolidBackgroundColor, Is.EqualTo(Color.FromArgb(32, 32, 32)));
            Assert.That(settings.PopupInactiveBackgroundColor, Is.EqualTo(Color.FromArgb(32, 32, 32)));
            Assert.That(settings.PopupInactiveBackdropOverlayColor, Is.EqualTo(Color.FromArgb(255, 32, 32, 32)));
            Assert.That(settings.PopupActiveBackdropOverlayColor, Is.EqualTo(Color.FromArgb(117, 0, 0, 0)));
            Assert.That(settings.PopupForegroundColor, Is.EqualTo(Color.White));
            Assert.That(settings.TrayIconResourceName, Is.EqualTo("Win11Seconds.tray_light.ico"));
            Assert.That(settings.IsTransparencyEffectsEnabled, Is.False);
        });
    }

    [Test]
    public void LightAppThemeWithDarkWindowsMode_UsesLightTrayIcon()
    {
        var settings = new AppearanceSettings(
            IsLightTheme: true,
            IsWindowsLightTheme: false,
            IsTransparencyEffectsEnabled: true);

        Assert.Multiple(() =>
        {
            Assert.That(settings.PopupForegroundColor, Is.EqualTo(Color.Black));
            Assert.That(settings.TrayIconResourceName, Is.EqualTo("Win11Seconds.tray_light.ico"));
        });
    }

    [Test]
    public void DarkWindowsModeWithLightAppThemeStillUsesAppLightColors()
    {
        var settings = new AppearanceSettings(
            IsLightTheme: true,
            IsWindowsLightTheme: false,
            IsTransparencyEffectsEnabled: true);

        Assert.Multiple(() =>
        {
            Assert.That(settings.PopupSolidBackgroundColor, Is.EqualTo(Color.FromArgb(238, 238, 238)));
            Assert.That(settings.PopupForegroundColor, Is.EqualTo(Color.Black));
            Assert.That(settings.TrayIconResourceName, Is.EqualTo("Win11Seconds.tray_light.ico"));
        });
    }
}
