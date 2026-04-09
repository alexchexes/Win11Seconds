namespace Win11Seconds.Tests;

public class ClockLayoutTests
{
    [Test]
    public void StoredLocationVisible_ReturnsTrueWhenWindowFitsInsideAnyWorkingArea()
    {
        Rectangle[] workingAreas =
        [
            new(0, 0, 1920, 1080),
            new(1920, 0, 1920, 1080)
        ];

        bool visible = ClockLayout.IsStoredLocationVisible(
            new Point(2000, 100),
            new Size(200, 80),
            workingAreas);

        Assert.That(visible, Is.True);
    }

    [Test]
    public void StoredLocationVisible_ReturnsFalseWhenWindowCrossesWorkingAreaBoundary()
    {
        Rectangle[] workingAreas = [new(0, 0, 1920, 1080)];

        bool visible = ClockLayout.IsStoredLocationVisible(
            new Point(1800, 1030),
            new Size(200, 80),
            workingAreas);

        Assert.That(visible, Is.False);
    }

    [Test]
    public void CalculatePopupLocation_ClampsHorizontallyAndFlipsBelowCursorWhenNeeded()
    {
        Point location = ClockLayout.CalculatePopupLocation(
            new Point(95, 10),
            new Rectangle(0, 0, 100, 100),
            new Size(20, 20));

        Assert.That(location, Is.EqualTo(new Point(80, 40)));
    }

    [Test]
    public void CalculatePopupLocation_PlacesPopupAboveCursorWhenThereIsRoom()
    {
        Point location = ClockLayout.CalculatePopupLocation(
            new Point(400, 300),
            new Rectangle(0, 0, 1920, 1080),
            new Size(200, 80));

        Assert.That(location, Is.EqualTo(new Point(300, 190)));
    }

    [Test]
    public void CalculateCloseButtonLocation_KeepsButtonInsideClientArea()
    {
        Point location = ClockLayout.CalculateCloseButtonLocation(new Size(10, 10), new Size(24, 24));

        Assert.That(location, Is.EqualTo(new Point(0, 0)));
    }

    [Test]
    public void ShouldShowCloseButton_ReturnsTrueOnlyInTopRightHoverRegion()
    {
        var clientRectangle = new Rectangle(Point.Empty, new Size(200, 80));

        Assert.Multiple(() =>
        {
            Assert.That(
                ClockLayout.ShouldShowCloseButton(new Point(170, 10), clientRectangle),
                Is.True);
            Assert.That(
                ClockLayout.ShouldShowCloseButton(new Point(150, 10), clientRectangle),
                Is.False);
            Assert.That(
                ClockLayout.ShouldShowCloseButton(new Point(170, 50), clientRectangle),
                Is.False);
        });
    }

    [TestCase(0, 1000)]
    [TestCase(1, 999)]
    [TestCase(250, 750)]
    [TestCase(999, 1)]
    public void GetFirstTickIntervalMilliseconds_ReturnsExpectedInterval(int millisecond, int expectedInterval)
    {
        int interval = ClockLayout.GetFirstTickIntervalMilliseconds(millisecond);

        Assert.That(interval, Is.EqualTo(expectedInterval));
    }

    [Test]
    public void CalculateFontSize_ReturnsScaledFontSize()
    {
        float? fontSize = ClockLayout.CalculateFontSize(
            new Size(200, 80),
            new SizeF(100, 40),
            72f);

        Assert.That(fontSize, Is.Not.Null);
        Assert.That(fontSize!.Value, Is.EqualTo(129.6f).Within(0.01f));
    }

    [Test]
    public void CalculateFontSize_ClampsExcessiveValues()
    {
        float? fontSize = ClockLayout.CalculateFontSize(
            new Size(5000, 4000),
            new SizeF(10, 10),
            20f);

        Assert.That(fontSize, Is.EqualTo(ClockLayout.MaxFontSize));
    }

    [Test]
    public void CalculateFontSize_ReturnsNullForInvalidInput()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                ClockLayout.CalculateFontSize(new Size(4, 4), new SizeF(100, 50), 40f),
                Is.Null);
            Assert.That(
                ClockLayout.CalculateFontSize(new Size(200, 80), SizeF.Empty, 40f),
                Is.Null);
            Assert.That(
                ClockLayout.CalculateFontSize(new Size(200, 80), new SizeF(100, 50), 0f),
                Is.Null);
        });
    }

    [Test]
    public void CalculateConstrainedBounds_ForBottomResize_KeepsTopFixedAndCentersWidth()
    {
        Rectangle bounds = ClockLayout.CalculateConstrainedBounds(
            new Rectangle(100, 50, 200, 120),
            ResizeDirection.Bottom,
            ClockLayout.DefaultAspectRatio,
            new Size(120, 48));

        Assert.That(bounds, Is.EqualTo(new Rectangle(50, 50, 300, 120)));
    }

    [Test]
    public void CalculateConstrainedBounds_ForTopResize_KeepsBottomFixedAndCentersWidth()
    {
        Rectangle bounds = ClockLayout.CalculateConstrainedBounds(
            new Rectangle(100, 20, 200, 120),
            ResizeDirection.Top,
            ClockLayout.DefaultAspectRatio,
            new Size(120, 48));

        Assert.That(bounds, Is.EqualTo(new Rectangle(50, 20, 300, 120)));
    }

    [Test]
    public void CalculateConstrainedBounds_ForLeftResize_KeepsRightFixedAndCentersHeight()
    {
        Rectangle bounds = ClockLayout.CalculateConstrainedBounds(
            new Rectangle(80, 50, 220, 80),
            ResizeDirection.Left,
            ClockLayout.DefaultAspectRatio,
            new Size(120, 48));

        Assert.That(bounds, Is.EqualTo(new Rectangle(80, 46, 220, 88)));
    }

    [Test]
    public void CalculateConstrainedBounds_ForCornerResize_KeepsOppositeCornerAnchored()
    {
        Rectangle bounds = ClockLayout.CalculateConstrainedBounds(
            Rectangle.FromLTRB(50, 20, 300, 130),
            ResizeDirection.TopLeft,
            ClockLayout.DefaultAspectRatio,
            new Size(120, 48));

        Assert.That(bounds, Is.EqualTo(Rectangle.FromLTRB(50, 30, 300, 130)));
    }

    [Test]
    public void CalculateConstrainedBounds_ClampsToMinimumSize()
    {
        Rectangle bounds = ClockLayout.CalculateConstrainedBounds(
            new Rectangle(100, 50, 10, 10),
            ResizeDirection.Bottom,
            ClockLayout.DefaultAspectRatio,
            new Size(120, 48));

        Assert.That(bounds, Is.EqualTo(new Rectangle(45, 50, 120, 48)));
    }
}
