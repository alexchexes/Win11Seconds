using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Drawing.Text;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Diagnostics;  // for Process.Start

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
        SetStyle(ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.UserPaint
               | ControlStyles.AllPaintingInWmPaint, true);
        DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

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
            cp.Style |= WS_THICKFRAME;
            const int WS_EX_COMPOSITED = 0x02000000;
            cp.ExStyle |= WS_EX_COMPOSITED;
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
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
            else m.Result = (IntPtr)HTCLIENT;
            return;
        }

        if (m.Msg == WM_SIZING)
        {
            float aspect = 200f / 80f;
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
    // constants for dragging
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(
        IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
    const int WM_NCLBUTTONDOWN = 0xA1, HTCAPTION = 0x2;

    private readonly NotifyIcon tray;
    private readonly BorderlessResizableForm clockForm;
    private readonly SmoothLabel timeLabel;
    private readonly Label closeLabel;
    private readonly System.Windows.Forms.Timer timer;
    private readonly System.Windows.Forms.Timer closeHideTimer;
    private bool isLightTheme;
    private Point? lastLocation;
    private bool _firstTick;


    public TrayContext()
    {
        // theme
        isLightTheme = ReadOsTheme();

        // 2) Subscribe to OS theme changes
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        // tray icon
        tray = new NotifyIcon
        {
            Text = "Win11Seconds - Click to show clock",
            Visible = true,
            // ContextMenuStrip = new ContextMenuStrip()
        };

        UpdateTrayIcon();


        tray.MouseUp += (s, e) => { if (e.Button == MouseButtons.Left) ToggleClock(); };
        // tray.ContextMenuStrip.Items.Add("Exit", null, (s, e) => { tray.Visible = false; Application.ExitThread(); });

        // popup
        clockForm = new BorderlessResizableForm
        {
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            TopMost = true,
            ClientSize = new Size(200, 80),
            MinimumSize = new Size(120, 48)
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
            // border color fix: make DWM non-client strip color match bg
            ApplyDwmCaptionColor();
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
            closeLabel!.Location = new Point(clockForm.ClientSize.Width - closeLabel.Width - 4, 0);
        };
        clockForm.FormClosing += (s, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; HideClock(); } };
        clockForm.Move += (s, e) => lastLocation = clockForm.Location;

        // clock label
        timeLabel = new SmoothLabel
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 24),
            TextAlign = ContentAlignment.MiddleCenter,
            UseCompatibleTextRendering = true
        };
        clockForm.Controls.Add(timeLabel);

        // close ✕
        closeLabel = new Label
        {
            Text = "✕",
            Font = new Font(Control.DefaultFont.FontFamily, Control.DefaultFont.Size),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = clockForm.BackColor,
            Size = new Size(24, 24),
            Location = new Point(clockForm.ClientSize.Width - 28, 4),
            Visible = false,
            Cursor = Cursors.Hand
        };
        closeLabel.Click += (s, e) => HideClock();
        clockForm.Controls.Add(closeLabel);
        closeLabel.BringToFront();

        // mouse move, clicks & drag
        clockForm.MouseDown += DragWindow;
        timeLabel.MouseDown += DragWindow;
        clockForm.MouseMove += OnPopupMouseMove;
        timeLabel.MouseMove += OnPopupMouseMove;
        closeLabel.MouseMove += OnPopupMouseMove;
        clockForm.MouseDown += OnPopupMouseDown;
        timeLabel.MouseDown += OnPopupMouseDown;
        clockForm.MouseLeave += (s, e) => closeLabel.Visible = false;

        // timers
        timer = new System.Windows.Forms.Timer();
        timer.Tick += Timer_Tick;
        closeHideTimer = new System.Windows.Forms.Timer { Interval = 100 };
        closeHideTimer.Tick += (s, e) =>
        {
            if (closeLabel.Visible && !clockForm.Bounds.Contains(Cursor.Position))
                closeLabel.Visible = false;
        };

        // 1) build one shared menu
        var menu = new ContextMenuStrip();

        // 1a) Show/Hide item
        var showHideItem = new ToolStripMenuItem();
        showHideItem.Click += (s, e) =>
        {
            if (clockForm.Visible)
                HideClock();
            else
                ShowClock();
        };
        menu.Items.Add(showHideItem);

        // 1b) Maximize/Unmaximize item (always shows first)
        var maximizeItem = new ToolStripMenuItem();
        maximizeItem.Click += (s, e) =>
        {
            if (!clockForm.Visible)
                ShowClock();    // <-- ensure form is visible
            // then toggle Maximize/Unmaximize
            clockForm.WindowState = clockForm.WindowState == FormWindowState.Normal
                ? FormWindowState.Maximized
                : FormWindowState.Normal;
        };
        menu.Items.Add(maximizeItem);

        // 1c) GitHub link
        menu.Items.Add("GitHub Repo", null, (s, e) =>
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/alexchexes/Win11Seconds",
                UseShellExecute = true
            });
        });

        // 1d) Separator + Exit
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (s, e) =>
        {
            tray.Visible = false;
            Application.ExitThread();
        });

        // 1e) Update all the dynamic texts when the menu opens
        menu.Opening += (s, e) =>
        {
            // Show/Hide
            showHideItem.Text = clockForm.Visible ? "Hide" : "Show";

            // Maximize/Unmaximize
            maximizeItem.Text = clockForm.WindowState == FormWindowState.Normal
                ? "Maximize"
                : "Unmaximize";
        };

        // 2) wire it up
        tray.ContextMenuStrip = menu;
        clockForm.ContextMenuStrip = menu;

        ApplyTheme();
    }

    private static bool ReadOsTheme()
    {
        return ((int?)Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme", 1)) != 0;
    }
    
    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General) return;

        bool nowLight = ReadOsTheme();
        if (nowLight == isLightTheme) return;

        isLightTheme = nowLight;
        ApplyTheme();
    }
    private void ApplyTheme()
    {
        // 1) Tray icon
        UpdateTrayIcon();

        // 2) Popup background
        var bg = isLightTheme
            ? Color.White
            : Color.FromArgb(32, 32, 32);
        clockForm.BackColor      = bg;
        closeLabel.BackColor     = bg;

        // 3) Foreground of our labels
        var fg = isLightTheme
            ? Color.Black
            : Color.White;
        timeLabel.ForeColor   = fg;
        closeLabel.ForeColor  = fg;

        // 4) border color fix: make DWM non-client strip color match bg
        if (clockForm.IsHandleCreated)
        {
            ApplyDwmCaptionColor();
        }
    }

    private void ApplyDwmCaptionColor()
    {
        // strip color must be RGB, drop alpha
        var bg = clockForm.BackColor;
        int colorRef = bg.ToArgb() & 0x00FFFFFF;
        NativeMethods.DwmSetWindowAttribute(
            clockForm.Handle,
            NativeMethods.DWMWA_CAPTION_COLOR,
            ref colorRef,
            sizeof(int));
    }


    private void UpdateTrayIcon()
    {
        var asm = Assembly.GetExecutingAssembly();
        // pick the right resource name
        string resName = isLightTheme
            ? "Win11Seconds.tray_dark.ico"
            : "Win11Seconds.tray_light.ico";

        using var iconStream = asm.GetManifestResourceStream(resName);
        tray.Icon = iconStream != null
            ? new Icon(iconStream)
            : SystemIcons.Application;
    }

    private void StartSynchronizedTimer()
    {
        _firstTick = true;
        timer.Interval = 1000 - DateTime.Now.Millisecond;
        timer.Start();
    }

    private void Timer_Tick(object? s, EventArgs e)
    {
        timeLabel.Text = DateTime.Now.ToString("HH:mm:ss");
        if (_firstTick)
        {
            _firstTick = false;
            timer.Interval = 1000;
        }
    }

    private void OnPopupMouseDown(object? s, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && e.Clicks == 2)
        {
            var pt = clockForm.PointToClient(Cursor.Position);
            if (closeLabel.Bounds.Contains(pt))
            {
                return;
            }
            clockForm.WindowState = clockForm.WindowState == FormWindowState.Normal
                ? FormWindowState.Maximized
                : FormWindowState.Normal;
            clockForm.PerformLayout();
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

        if (lastLocation.HasValue && Screen.AllScreens.Any(sc => sc.WorkingArea.Contains(new Rectangle(lastLocation.Value, clockForm.Size))))
        {
            clockForm.Location = lastLocation.Value;
        }
        else
        {
            const int offset = 30;
            var cursPos = Cursor.Position;
            var scr = Screen.FromPoint(cursPos);
            int x = cursPos.X - clockForm.Width / 2;
            int y = cursPos.Y - clockForm.Height - offset;
            x = Math.Max(scr.WorkingArea.Left, Math.Min(x, scr.WorkingArea.Right - clockForm.Width));
            if (y < scr.WorkingArea.Top)
            {
                y = cursPos.Y + offset;
            }
            clockForm.Location = new Point(x, y);
        }
        clockForm.Show();
        StartSynchronizedTimer();  // ← use our accurate timer
        closeHideTimer.Start();
    }

    private void HideClock()
    {
        timer.Stop();
        closeHideTimer.Stop();
        closeLabel.Visible = false;
        clockForm.Hide();
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
        closeLabel.Visible = p.X >= r.Width - region && p.Y <= region;
    }
}
