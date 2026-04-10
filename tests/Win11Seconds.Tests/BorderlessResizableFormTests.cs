namespace Win11Seconds.Tests;

[Apartment(ApartmentState.STA)]
public class BorderlessResizableFormTests
{
    [Test]
    public void IsResizeBorderHit_ReturnsTrueOnlyForEdgeZones()
    {
        using var form = new BorderlessResizableForm
        {
            ClientSize = new Size(200, 80)
        };

        Assert.Multiple(() =>
        {
            Assert.That(form.IsResizeBorderHit(new Point(2, 2)), Is.True);
            Assert.That(form.IsResizeBorderHit(new Point(198, 40)), Is.True);
            Assert.That(form.IsResizeBorderHit(new Point(100, 40)), Is.False);
        });
    }

    [Test]
    public void IsCloseButtonHit_RespectsVisibilityAndTopRightBounds()
    {
        using var form = new BorderlessResizableForm
        {
            ClientSize = new Size(200, 80),
            CloseButtonVisible = true
        };

        Assert.Multiple(() =>
        {
            Assert.That(form.IsCloseButtonHit(new Point(190, 10)), Is.True);
            Assert.That(form.IsCloseButtonHit(new Point(10, 10)), Is.False);
        });

        form.CloseButtonVisible = false;
        Assert.That(form.IsCloseButtonHit(new Point(190, 10)), Is.False);
    }
}
