namespace Win11Seconds.Tests;

public class TransparencyModeTests
{
    [Test]
    public void Normalize_ReturnsDefaultMode_ForUnknownValues()
    {
        Assert.That(
            TransparencyModeStore.Normalize(999),
            Is.EqualTo(TransparencyModeStore.DefaultMode));
    }

    [Test]
    public void ShouldEnableSystemBackdrop_RespectsEachMode()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                TransparencyModePolicy.ShouldEnableSystemBackdrop(
                    TransparencyMode.Off,
                    isOsTransparencyEnabled: true,
                    isClockActive: true,
                    isBackdropFadeOutInProgress: false),
                Is.False);

            Assert.That(
                TransparencyModePolicy.ShouldEnableSystemBackdrop(
                    TransparencyMode.AlwaysOn,
                    isOsTransparencyEnabled: true,
                    isClockActive: false,
                    isBackdropFadeOutInProgress: false),
                Is.True);

            Assert.That(
                TransparencyModePolicy.ShouldEnableSystemBackdrop(
                    TransparencyMode.OnlyActive,
                    isOsTransparencyEnabled: true,
                    isClockActive: false,
                    isBackdropFadeOutInProgress: true),
                Is.True);

            Assert.That(
                TransparencyModePolicy.ShouldEnableSystemBackdrop(
                    TransparencyMode.OnlyActive,
                    isOsTransparencyEnabled: false,
                    isClockActive: true,
                    isBackdropFadeOutInProgress: true),
                Is.False);
        });
    }

    [Test]
    public void ShouldFadeOutOnDeactivation_IsOnlyTrueForOnlyActiveMode()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                TransparencyModePolicy.ShouldFadeOutOnDeactivation(
                    TransparencyMode.Off,
                    isOsTransparencyEnabled: true),
                Is.False);

            Assert.That(
                TransparencyModePolicy.ShouldFadeOutOnDeactivation(
                    TransparencyMode.AlwaysOn,
                    isOsTransparencyEnabled: true),
                Is.False);

            Assert.That(
                TransparencyModePolicy.ShouldFadeOutOnDeactivation(
                    TransparencyMode.OnlyActive,
                    isOsTransparencyEnabled: true),
                Is.True);

            Assert.That(
                TransparencyModePolicy.ShouldFadeOutOnDeactivation(
                    TransparencyMode.OnlyActive,
                    isOsTransparencyEnabled: false),
                Is.False);
        });
    }

    [Test]
    public void AlwaysOnTopNormalize_ReturnsExpectedValues()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AlwaysOnTopPreferenceStore.Normalize(0), Is.False);
            Assert.That(AlwaysOnTopPreferenceStore.Normalize(1), Is.True);
            Assert.That(
                AlwaysOnTopPreferenceStore.Normalize(999),
                Is.EqualTo(AlwaysOnTopPreferenceStore.DefaultValue));
        });
    }
}
