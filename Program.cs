using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Drawing.Text;
using System.Drawing.Drawing2D;
using System.Reflection;

static class NativeMethods
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWA_BORDER_COLOR = 34;
    public const int DWMWA_CAPTION_COLOR = 35;

    public enum DwmWindowCornerPreference
    {
        DWMWCP_DEFAULT = 0,
        DWMWCP_DONOTROUND = 1,
        DWMWCP_ROUND = 2,
        DWMWCP_ROUNDSMALL = 3
    }
}

public class SmoothLabel : Label
{
    public SmoothLabel()
    {
        // full double-buffer + custom paint
        SetStyle(ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.UserPaint
               | ControlStyles.AllPaintingInWmPaint,
               true);
        DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        // fill background
        e.Graphics.Clear(BackColor);

        // text anti-aliasing
        e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // draw centered
        using var brush = new SolidBrush(ForeColor);
        var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        e.Graphics.DrawString(Text ?? "", Font, brush, ClientRectangle, sf);
    }
}


class BorderlessResizableForm : Form
{
    const int WM_NCHITTEST = 0x84;
    const int HTCLIENT = 1;
    const int HTLEFT = 10;
    const int HTRIGHT = 11;
    const int HTTOP = 12;
    const int HTTOPLEFT = 13;
    const int HTTOPRIGHT = 14;
    const int HTBOTTOM = 15;
    const int HTBOTTOMLEFT = 16;
    const int HTBOTTOMRIGHT = 17;
    const int WM_SIZING = 0x214;
    const int _border = 6;
    const int GWL_STYLE = -16;
    const int WS_THICKFRAME = 0x00040000;

    public BorderlessResizableForm()
    {
        FormBorderStyle = FormBorderStyle.None;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            // keep your sizing border
            cp.Style |= WS_THICKFRAME;

            // NEW: enable double-buffering for all child painting
            const int WS_EX_COMPOSITED = 0x02000000;
            cp.ExStyle |= WS_EX_COMPOSITED;

            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {

        const int WM_NCHITTEST = 0x84;
        if (m.Msg == WM_NCHITTEST)
        {
            var p = PointToClient(Cursor.Position);
            bool left = p.X < _border;
            bool right = p.X >= ClientSize.Width - _border;
            bool bottom = p.Y >= ClientSize.Height - _border;

            if (left && bottom) m.Result = (IntPtr)HTBOTTOMLEFT;
            else if (right && bottom) m.Result = (IntPtr)HTBOTTOMRIGHT;
            else if (left) m.Result = (IntPtr)HTLEFT;
            else if (right) m.Result = (IntPtr)HTRIGHT;
            else if (bottom) m.Result = (IntPtr)HTBOTTOM;
            else m.Result = (IntPtr)HTCLIENT;  // everything else, including top, is just client
            return;
        }

        if (m.Msg == WM_SIZING)
        {
            // maintain aspect ratio
            var aspect = 200f / 80f;
            var rect = Marshal.PtrToStructure<RECT>(m.LParam);
            int width = rect.right - rect.left;
            int height = (int)(width / aspect);
            rect.bottom = rect.top + height;
            Marshal.StructureToPtr(rect, m.LParam, true);
            m.Result = IntPtr.Zero;
            return;
        }
        base.WndProc(ref m);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int left, top, right, bottom; }
}

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(true);
        Application.Run(new TrayContext());
    }
}

class TrayContext : ApplicationContext
{
    // for dragging
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(
        IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
    const int WM_NCLBUTTONDOWN = 0xA1, HTCAPTION = 0x2;

    private readonly NotifyIcon tray;
    private readonly Form clockForm;
    private readonly Label timeLabel;
    private readonly Label closeLabel;
    private readonly System.Windows.Forms.Timer timer;
    private readonly System.Windows.Forms.Timer closeHideTimer;
    private readonly bool isLightTheme;
    private Point? lastLocation;

    public TrayContext()
    {
        isLightTheme = ((int?)Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme", 1)) != 0;

        tray = new NotifyIcon
        {
            Text = "Click to show clock",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };

        var asm = Assembly.GetExecutingAssembly();

        using Stream? iconStream = asm.GetManifestResourceStream("SimpleTrayClock.tray.ico");
        tray.Icon = (iconStream != null)
            ? new Icon(iconStream)
            : SystemIcons.Application;


        tray.MouseUp += (s, e) => { if (e.Button == MouseButtons.Left) ToggleClock(); };
        tray.ContextMenuStrip.Items.Add("Exit", null, (s, e) => { tray.Visible = false; Application.ExitThread(); });

        clockForm = new BorderlessResizableForm
        {
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            TopMost = true,
            BackColor = isLightTheme ? Color.White : Color.FromArgb(32, 32, 32),
            ClientSize = new Size(200, 80),
            MinimumSize = new Size(120, 48),  // prevent any smaller resize
        };
        clockForm.HandleCreated += (s, e) =>
        {
            // corner rounding
            int pref = (int)NativeMethods.DwmWindowCornerPreference.DWMWCP_ROUND;
            NativeMethods.DwmSetWindowAttribute(
                clockForm.Handle,
                NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
                ref pref,
                sizeof(int)
            );
            // border color fix: make the non-client caption strip at the top to match the background
            int bgColor = isLightTheme
                ? Color.White.ToArgb() & 0x00FFFFFF
                : Color.FromArgb(32, 32, 32).ToArgb() & 0x00FFFFFF;

            NativeMethods.DwmSetWindowAttribute(
                clockForm.Handle,
                NativeMethods.DWMWA_CAPTION_COLOR,
                ref bgColor,
                sizeof(int)
            );
        };
        clockForm.Resize += (s, e) =>
        {
            const int pad = 4;
            int availW = clockForm.ClientSize.Width - pad * 2;
            int availH = clockForm.ClientSize.Height - pad * 2;

            // pick a starting guess for font-size (use the height)
            float testSize = availH;
            using var g = clockForm.CreateGraphics();
            // measure at that size
            using var testFont = new Font("Segoe UI", testSize, FontStyle.Regular);
            SizeF measure = g.MeasureString(timeLabel!.Text, testFont);

            // compute scale factors so it fits both width and height
            float scaleW = availW / measure.Width;
            float scaleH = availH / measure.Height;
            float newSize = testSize * Math.Min(scaleW, scaleH);

            // apply it
            timeLabel.Font = new Font("Segoe UI", newSize, FontStyle.Regular);

            // reposition close-label
            closeLabel!.Location = new Point(
                clockForm.ClientSize.Width - closeLabel.Width - 4,
                4
            );
        };
        clockForm.FormClosing += (s, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; HideClock(); } };
        clockForm.Move += (s, e) => lastLocation = clockForm.Location;

        timeLabel = new SmoothLabel
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 24),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = isLightTheme ? Color.Black : Color.White,
            UseCompatibleTextRendering = true
        };
        clockForm.Controls.Add(timeLabel);

        closeLabel = new Label
        {
            Text = "✕",
            Font = new Font(Control.DefaultFont.FontFamily, Control.DefaultFont.Size, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = clockForm.BackColor,    // ← match the form’s background, not Transparent  
            ForeColor = isLightTheme ? Color.Black : Color.White,
            Size = new Size(24, 24),
            Location = new Point(clockForm.ClientSize.Width - 28, 4),
            Visible = false
        };
        closeLabel.Click += (s, e) => HideClock();
        clockForm.Controls.Add(closeLabel);
        closeLabel.BringToFront();          // ← ensure it’s on top of the timeLabel 

        closeLabel.Cursor = Cursors.Hand;

        clockForm.MouseDown += DragWindow;
        timeLabel.MouseDown += DragWindow;

        clockForm.MouseMove += OnPopupMouseMove;
        timeLabel.MouseMove += OnPopupMouseMove;
        closeLabel.MouseMove += OnPopupMouseMove;

        clockForm.MouseLeave += (s, e) => closeLabel.Visible = false;

        timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (s, e) => timeLabel.Text = DateTime.Now.ToString("HH:mm:ss");

        closeHideTimer = new System.Windows.Forms.Timer { Interval = 100 };
        closeHideTimer.Tick += (s, e) =>
        {
            if (closeLabel.Visible && !clockForm.Bounds.Contains(Cursor.Position))
                closeLabel.Visible = false;
        };

        clockForm.MouseDown += OnPopupMouseDown;
        timeLabel.MouseDown += OnPopupMouseDown;
    }

    private void OnPopupMouseDown(object? sender, MouseEventArgs e)
    {
        // only care about a LEFT‐button double‐click
        if (e.Button == MouseButtons.Left && e.Clicks == 2)
        {
            // get the click location relative to the form
            var pt = clockForm.PointToClient(Cursor.Position);
            // don’t toggle if the ✕ is under the mouse
            if (closeLabel.Bounds.Contains(pt)) return;

            // toggle max/normal
            if (clockForm.WindowState == FormWindowState.Normal)
                clockForm.WindowState = FormWindowState.Maximized;
            else
            {
                clockForm.WindowState = FormWindowState.Normal;
                clockForm.PerformLayout(); // reflow your Resize logic
            }
        }
    }


    private void ToggleClock()
    {
        if (clockForm.Visible) HideClock(); else ShowClock();
    }

    private void ShowClock()
    {
        timeLabel.Text = DateTime.Now.ToString("HH:mm:ss");
        closeLabel.BringToFront();

        if (lastLocation.HasValue &&
            Screen.AllScreens.Any(sc => sc.WorkingArea.Contains(new Rectangle(lastLocation.Value, clockForm.Size))))
        {
            clockForm.Location = lastLocation.Value;
        }
        else
        {
            const int offset = 14;
            var cur = Cursor.Position;
            var scr = Screen.FromPoint(cur);
            int x = cur.X - clockForm.Width / 2;
            int y = cur.Y - clockForm.Height - offset;
            x = Math.Max(scr.WorkingArea.Left, Math.Min(x, scr.WorkingArea.Right - clockForm.Width));
            if (y < scr.WorkingArea.Top)
            {
                y = cur.Y + offset;
            }
            clockForm.Location = new Point(x, y);
        }
        clockForm.Show();
        timer.Start();
        closeHideTimer.Start();
    }

    private void HideClock()
    {
        timer.Stop();
        clockForm.Hide();
        closeHideTimer.Stop();
        closeLabel.Visible = false;
    }

    private void DragWindow(object? s, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(clockForm.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
    }

    private void OnPopupMouseMove(object? sender, MouseEventArgs e)
    {
        var p = clockForm.PointToClient(Cursor.Position);
        var r = clockForm.ClientRectangle;
        const int hoverSize = 30, margin = 7;
        int region = hoverSize + margin;

        // show only if the mouse (relative to the form) is in that top-right band:
        closeLabel.Visible = (p.X >= r.Width - region && p.Y <= region);
    }
}
