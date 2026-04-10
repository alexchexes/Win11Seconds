namespace Win11Seconds;

using System.Drawing.Drawing2D;
using System.Drawing.Text;

public class SmoothLabel : Label
{
    private const int WM_NCHITTEST = 0x84;
    private const int HTTRANSPARENT = -1;

    public bool PassThroughMouse { get; set; }

    public SmoothLabel()
    {
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.SupportsTransparentBackColor,
            true);
        DoubleBuffered = true;
    }

    protected override void WndProc(ref Message m)
    {
        if (PassThroughMouse && m.Msg == WM_NCHITTEST)
        {
            m.Result = (IntPtr)HTTRANSPARENT;
            return;
        }

        base.WndProc(ref m);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        ArgumentNullException.ThrowIfNull(pevent);
        if (BackColor == Color.Transparent && Parent is not null)
        {
            var state = pevent.Graphics.Save();
            try
            {
                pevent.Graphics.TranslateTransform(-Left, -Top);
                using var paintEventArgs = new PaintEventArgs(
                    pevent.Graphics,
                    new Rectangle(Left, Top, Parent.Width, Parent.Height));

                InvokePaintBackground(Parent, paintEventArgs);
            }
            finally
            {
                pevent.Graphics.Restore(state);
            }

            return;
        }

        using var brush = new SolidBrush(BackColor);
        pevent.Graphics.FillRectangle(brush, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
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
