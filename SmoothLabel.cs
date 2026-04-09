using System.Drawing.Drawing2D;
using System.Drawing.Text;

public class SmoothLabel : Label
{
    public SmoothLabel()
    {
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint,
            true);
        DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        if (ClientRectangle.Width <= 0 || ClientRectangle.Height <= 0)
        {
            return;
        }

        var font = Font;
        if (font is null)
        {
            return;
        }

        float fontSize = font.Size;
        if (!float.IsFinite(fontSize) || fontSize <= 0f)
        {
            return;
        }

        e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        using var brush = new SolidBrush(ForeColor);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        e.Graphics.DrawString(Text ?? string.Empty, font, brush, ClientRectangle, format);
    }
}
