namespace Win11Seconds.Tests;

[Apartment(ApartmentState.STA)]
public class SmoothLabelTests
{
    [Test]
    public void OnPaint_DoesNotThrow_ForTinyClientArea()
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        using var label = new TestSmoothLabel
        {
            Size = new Size(1, 1),
            Text = "12:34:56",
            Font = new Font("Segoe UI", 1f, FontStyle.Regular),
            BackColor = Color.Black,
            ForeColor = Color.White
        };

        Assert.DoesNotThrow(() => label.PaintForTest(graphics));
    }

    [Test]
    public void OnPaint_DoesNotThrow_ForZeroSizedClientArea()
    {
        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        using var label = new TestSmoothLabel
        {
            Size = Size.Empty,
            Text = "12:34:56",
            Font = new Font("Segoe UI", 12f, FontStyle.Regular),
            BackColor = Color.Black,
            ForeColor = Color.White
        };

        Assert.DoesNotThrow(() => label.PaintForTest(graphics));
    }

    private sealed class TestSmoothLabel : SmoothLabel
    {
        public void PaintForTest(Graphics graphics)
        {
            using var paintEventArgs = new PaintEventArgs(graphics, ClientRectangle);
            OnPaint(paintEventArgs);
        }
    }
}
