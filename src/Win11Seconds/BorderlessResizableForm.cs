namespace Win11Seconds;

using System.Runtime.InteropServices;

sealed class BorderlessResizableForm : Form
{
    private const int WM_NCHITTEST = 0x84;
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

    private const int ResizeBorderThickness = 6;
    private const int WS_THICKFRAME = 0x00040000;
    private const int WS_EX_COMPOSITED = 0x02000000;
    public BorderlessResizableForm()
    {
        FormBorderStyle = FormBorderStyle.None;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.Style |= WS_THICKFRAME;
            cp.ExStyle |= WS_EX_COMPOSITED;
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST)
        {
            var cursorPosition = PointToClient(Cursor.Position);
            bool left = cursorPosition.X < ResizeBorderThickness;
            bool right = cursorPosition.X >= ClientSize.Width - ResizeBorderThickness;
            bool top = cursorPosition.Y < ResizeBorderThickness;
            bool bottom = cursorPosition.Y >= ClientSize.Height - ResizeBorderThickness;

            if (left && top)
            {
                m.Result = (IntPtr)HTTOPLEFT;
                return;
            }

            if (right && top)
            {
                m.Result = (IntPtr)HTTOPRIGHT;
                return;
            }

            if (left && bottom)
            {
                m.Result = (IntPtr)HTBOTTOMLEFT;
                return;
            }

            if (right && bottom)
            {
                m.Result = (IntPtr)HTBOTTOMRIGHT;
                return;
            }

            if (left)
            {
                m.Result = (IntPtr)HTLEFT;
                return;
            }

            if (right)
            {
                m.Result = (IntPtr)HTRIGHT;
                return;
            }

            if (top)
            {
                m.Result = (IntPtr)HTTOP;
                return;
            }

            if (bottom)
            {
                m.Result = (IntPtr)HTBOTTOM;
                return;
            }

            m.Result = (IntPtr)HTCLIENT;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }
}
