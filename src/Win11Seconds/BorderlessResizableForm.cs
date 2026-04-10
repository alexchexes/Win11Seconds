namespace Win11Seconds;

using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

sealed class BorderlessResizableForm : Form
{
    private const int WM_ERASEBKGND = 0x14;
    private const int WM_NCCALCSIZE = 0x83;
    private const int WM_NCHITTEST = 0x84;
    private const int WM_NCACTIVATE = 0x86;
    private const int WM_SIZING = 0x214;

    private const int HTCLIENT = 1;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;

    private static readonly Size CloseButtonSize = new(24, 24);

    private const int ResizeBorderThickness = 6;
    private const int WS_THICKFRAME = 0x00040000;

    public bool UseSystemBackdropBackground { get; set; }
    public Color BackdropOverlayColor { get; set; } = Color.Empty;
    public string DisplayText { get; set; } = string.Empty;
    public Font? DisplayFont { get; set; }
    public Color DisplayForeColor { get; set; } = Color.White;
    public bool CloseButtonVisible { get; set; }
    public Color CloseButtonForeColor { get; set; } = Color.White;
    public bool IsWindowActive { get; private set; }

    public event Action<bool>? WindowActivationChanged;

    public BorderlessResizableForm()
    {
        SetStyle(
            ControlStyles.SupportsTransparentBackColor
            | ControlStyles.UserPaint
            | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer,
            true);
        FormBorderStyle = FormBorderStyle.None;
        DoubleBuffered = true;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.Style |= WS_THICKFRAME;
            return cp;
        }
    }

    public bool IsResizeBorderHit(Point cursorPosition)
    {
        return GetHitTest(cursorPosition) != HTCLIENT;
    }

    public bool IsCloseButtonHit(Point cursorPosition)
    {
        return CloseButtonVisible && GetCloseButtonBounds().Contains(cursorPosition);
    }

    protected override void WndProc(ref Message m)
    {
        if (UseSystemBackdropBackground && m.Msg == WM_ERASEBKGND)
        {
            m.Result = (IntPtr)1;
            return;
        }

        if (m.Msg == WM_NCACTIVATE)
        {
            bool nextIsActive = m.WParam != IntPtr.Zero;
            if (nextIsActive != IsWindowActive)
            {
                IsWindowActive = nextIsActive;
                WindowActivationChanged?.Invoke(nextIsActive);
            }

            // Let the default proc update activation state, but suppress the
            // native non-client repaint that would briefly show a standard frame.
            var activationMessage = Message.Create(m.HWnd, m.Msg, m.WParam, new IntPtr(-1));
            DefWndProc(ref activationMessage);
            m.Result = activationMessage.Result;
            return;
        }

        if (m.Msg == WM_NCCALCSIZE && m.WParam != IntPtr.Zero)
        {
            // Claim the whole window as client area so DWM stops reserving a caption band.
            m.Result = IntPtr.Zero;
            return;
        }

        if (m.Msg == WM_NCHITTEST)
        {
            var cursorPosition = PointToClient(Cursor.Position);
            m.Result = (IntPtr)GetHitTest(cursorPosition);
            return;
        }

        if (m.Msg == WM_SIZING)
        {
            var rect = Marshal.PtrToStructure<RECT>(m.LParam);
            var proposedBounds = Rectangle.FromLTRB(rect.left, rect.top, rect.right, rect.bottom);
            if (Enum.IsDefined(typeof(ResizeDirection), m.WParam.ToInt32()))
            {
                var constrainedBounds = ClockLayout.CalculateConstrainedBounds(
                    proposedBounds,
                    (ResizeDirection)m.WParam.ToInt32(),
                    ClockLayout.DefaultAspectRatio,
                    MinimumSize);

                rect.left = constrainedBounds.Left;
                rect.top = constrainedBounds.Top;
                rect.right = constrainedBounds.Right;
                rect.bottom = constrainedBounds.Bottom;
            }

            Marshal.StructureToPtr(rect, m.LParam, fDeleteOld: false);
            m.Result = (IntPtr)1;
            return;
        }

        base.WndProc(ref m);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (UseSystemBackdropBackground)
        {
            CompositingMode previousMode = e.Graphics.CompositingMode;
            e.Graphics.CompositingMode = CompositingMode.SourceCopy;
            using (SolidBrush clearBrush = new(Color.FromArgb(0, 0, 0, 0)))
            {
                e.Graphics.FillRectangle(clearBrush, ClientRectangle);
            }

            e.Graphics.CompositingMode = previousMode;
            if (BackdropOverlayColor.A > 0)
            {
                using SolidBrush brush = new(BackdropOverlayColor);
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }

            return;
        }

        using SolidBrush backgroundBrush = new(BackColor);
        e.Graphics.FillRectangle(backgroundBrush, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPaint(e);

        DrawClockText(e.Graphics);
        DrawCloseButton(e.Graphics);
    }

    private void DrawClockText(Graphics graphics)
    {
        if (string.IsNullOrEmpty(DisplayText) || DisplayFont is null)
        {
            return;
        }

        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        using var brush = new SolidBrush(DisplayForeColor);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        graphics.DrawString(DisplayText, DisplayFont, brush, ClientRectangle, format);
    }

    private void DrawCloseButton(Graphics graphics)
    {
        if (!CloseButtonVisible)
        {
            return;
        }

        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var brush = new SolidBrush(CloseButtonForeColor);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        graphics.DrawString("✕", Control.DefaultFont, brush, GetCloseButtonBounds(), format);
    }

    private int GetHitTest(Point cursorPosition)
    {
        bool left = cursorPosition.X < ResizeBorderThickness;
        bool right = cursorPosition.X >= ClientSize.Width - ResizeBorderThickness;
        bool top = cursorPosition.Y < ResizeBorderThickness;
        bool bottom = cursorPosition.Y >= ClientSize.Height - ResizeBorderThickness;

        if (left && top)
        {
            return HTTOPLEFT;
        }

        if (right && top)
        {
            return HTTOPRIGHT;
        }

        if (left && bottom)
        {
            return HTBOTTOMLEFT;
        }

        if (right && bottom)
        {
            return HTBOTTOMRIGHT;
        }

        if (left)
        {
            return HTLEFT;
        }

        if (right)
        {
            return HTRIGHT;
        }

        if (top)
        {
            return HTTOP;
        }

        if (bottom)
        {
            return HTBOTTOM;
        }

        return HTCLIENT;
    }

    private Rectangle GetCloseButtonBounds()
    {
        var location = ClockLayout.CalculateCloseButtonLocation(ClientSize, CloseButtonSize);
        return new Rectangle(location, CloseButtonSize);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }
}
