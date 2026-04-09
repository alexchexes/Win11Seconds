namespace Win11Seconds.Tests;

public class AppearanceSettingsTests
{
    [Test]
    public void LightTheme_UsesExpectedColorsAndDarkTrayIcon()
    {
        var settings = new AppearanceSettings(IsLightTheme: true, IsTransparencyEffectsEnabled: true);

        Assert.Multiple(() =>
        {
            Assert.That(settings.PopupBackgroundColor, Is.EqualTo(Color.White));
            Assert.That(settings.PopupForegroundColor, Is.EqualTo(Color.Black));
            Assert.That(settings.TrayIconResourceName, Is.EqualTo("Win11Seconds.tray_dark.ico"));
            Assert.That(settings.IsTransparencyEffectsEnabled, Is.True);
        });
    }

    [Test]
    public void DarkTheme_UsesExpectedColorsAndLightTrayIcon()
    {
        var settings = new AppearanceSettings(IsLightTheme: false, IsTransparencyEffectsEnabled: false);

        Assert.Multiple(() =>
        {
            Assert.That(settings.PopupBackgroundColor, Is.EqualTo(Color.FromArgb(32, 32, 32)));
            Assert.That(settings.PopupForegroundColor, Is.EqualTo(Color.White));
            Assert.That(settings.TrayIconResourceName, Is.EqualTo("Win11Seconds.tray_light.ico"));
            Assert.That(settings.IsTransparencyEffectsEnabled, Is.False);
        });
    }
}
