using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Drawing.Text;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Diagnostics;  // for Process.Start

static class NativeMethods
{
    // Calls the Windows DWM API to set visual attributes on a window (e.g., rounded corners, border color)
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
        // Enable double buffering and custom painting to reduce flicker
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
    // Windows message and hit test constants for resizing
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
            // Allow resizing (WS_THICKFRAME) and enable window compositing for smoother visuals
            cp.Style |= WS_THICKFRAME;
            const int WS_EX_COMPOSITED = 0x02000000;
            cp.ExStyle |= WS_EX_COMPOSITED;
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        // Custom hit-testing for resizing from borders and corners
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

        // Enforce fixed aspect ratio on resizing
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
    // Native methods/constants for dragging borderless window
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
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
        // Read initial theme from OS
        isLightTheme = ReadOsTheme();

        // Subscribe to theme changes
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        // Initialize tray icon
        tray = CreateTrayIcon();
        UpdateTrayIcon();

        // Create popup form (hidden clock window)
        clockForm = CreateClockForm();
        timeLabel = CreateTimeLabel();
        closeLabel = CreateCloseLabel();

        clockForm.Controls.Add(timeLabel);
        clockForm.Controls.Add(closeLabel);
        closeLabel.BringToFront();

        SetupFormEvents();

        // === Setup timers ===

        // Timers for time updating
        timer = new System.Windows.Forms.Timer();
        timer.Tick += Timer_Tick;

        // Timer for auto-hiding close button
        closeHideTimer = new System.Windows.Forms.Timer { Interval = 100 };
        closeHideTimer.Tick += (s, e) =>
        {
            // Hide close button if cursor leaves popup form
            if (closeLabel.Visible && !clockForm.Bounds.Contains(Cursor.Position))
            {
                closeLabel.Visible = false;
            }
        };

        // Context menu for both tray and popup
        var menu = CreateContextMenu();
        tray.ContextMenuStrip = menu;
        clockForm.ContextMenuStrip = menu;

        ApplyTheme();
    }

    private NotifyIcon CreateTrayIcon()
    {
        var icon = new NotifyIcon
        {
            Text = "Win11Seconds - Click to show clock",
            Visible = true,
        };
        icon.MouseUp += (s, e) =>
        {
            if (e.Button == MouseButtons.Left) ToggleClock();
        };
        return icon;
    }
    
    private BorderlessResizableForm CreateClockForm()
    {
        var form = new BorderlessResizableForm
        {
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            TopMost = true,
            ClientSize = new Size(200, 80),
            MinimumSize = new Size(120, 48)
        };
        form.HandleCreated += (s, e) =>
        {
            // Apply rounded corners and match DWM border/caption color to form background
            int pref = (int)NativeMethods.DwmWindowCornerPreference.DWMWCP_ROUND;
            NativeMethods.DwmSetWindowAttribute(
                form.Handle,
                NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
                ref pref,
                sizeof(int)
            );
            // border color fix: make DWM non-client strip color match bg
            ApplyDwmCaptionColor();
        };
        return form;
    }

    // Create main clock label (centered time text)
    private static SmoothLabel CreateTimeLabel()
    {
        return new SmoothLabel
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 24),
            TextAlign = ContentAlignment.MiddleCenter,
            UseCompatibleTextRendering = true
        };
    }

    // Creates hidden close (✕) label at top-right corner
    private Label CreateCloseLabel()
    {
        return new Label
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
    }

    private void SetupFormEvents()
    {
        // Auto-resize font to fit form and move close button on resize
        clockForm.Resize += (s, e) => AutoResizeFontAndCloseLabel();
        
        // Intercept user-initiated closing and just hide instead
        clockForm.FormClosing += (s, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true; HideClock();
            }
        };
        clockForm.Move += (s, e) => lastLocation = clockForm.Location;

        // Add hidden close (✕) label at top-right corner
        closeLabel.Click += (s, e) => HideClock();

        // Enable dragging and mouse handling for both form and label
        clockForm.MouseDown += DragWindow;
        timeLabel.MouseDown += DragWindow;
        clockForm.MouseMove += OnPopupMouseMove;
        timeLabel.MouseMove += OnPopupMouseMove;
        closeLabel.MouseMove += OnPopupMouseMove;
        clockForm.MouseDown += OnPopupMouseDown;
        timeLabel.MouseDown += OnPopupMouseDown;
        // Hide close button when mouse leaves the form
        clockForm.MouseLeave += (s, e) => closeLabel.Visible = false;
    }

    private void AutoResizeFontAndCloseLabel()
    {
        const int pad = 4;
        int availW = clockForm.ClientSize.Width - pad * 2;
        int availH = clockForm.ClientSize.Height - pad * 2;

        // Estimate initial font size based on height
        float testSize = availH;
        using var g = clockForm.CreateGraphics();
        // measure at that size
        using var testFont = new Font("Segoe UI", testSize, FontStyle.Regular);
        SizeF measure = g.MeasureString(timeLabel!.Text, testFont);

        // Compute scale factors so it fits both width and height
        float scaleW = availW / measure.Width;
        float scaleH = availH / measure.Height;
        float newSize = testSize * Math.Min(scaleW, scaleH);

        // apply it
        timeLabel.Font = new Font("Segoe UI", newSize, FontStyle.Regular);

        // Reposition close button in top-right corner
        closeLabel!.Location = new Point(clockForm.ClientSize.Width - closeLabel.Width - 4, 0);
    }

    // Context menu for both tray and popup
    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        // Show/Hide toggle menu item
        var showHideItem = new ToolStripMenuItem();
        showHideItem.Click += (s, e) =>
        {
            if (clockForm.Visible)
                HideClock();
            else
                ShowClock();
        };
        menu.Items.Add(showHideItem);

        // Maximize/Unmaximize menu item
        var maximizeItem = new ToolStripMenuItem();
        maximizeItem.Click += (s, e) =>
        {
            if (!clockForm.Visible)
                ShowClock();  // Ensure form is visible first
            // Toggle window Maximize/Unmaximize state
            clockForm.WindowState = clockForm.WindowState == FormWindowState.Normal
                ? FormWindowState.Maximized
                : FormWindowState.Normal;
        };
        menu.Items.Add(maximizeItem);

        // GitHub repo link
        menu.Items.Add("GitHub Repo", null, (s, e) =>
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/alexchexes/Win11Seconds",
                UseShellExecute = true
            });
        });

        // Separator and Exit item
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (s, e) =>
        {
            tray.Visible = false;
            Application.ExitThread();
        });

        // Update menu item texts dynamically before menu opens
        menu.Opening += (s, e) =>
        {
            // Show/Hide
            showHideItem.Text = clockForm.Visible ? "Hide" : "Show";
            // Maximize/Unmaximize
            maximizeItem.Text = clockForm.WindowState == FormWindowState.Normal
                ? "Maximize"
                : "Unmaximize";
        };

        return menu;
    }

    // Reads Windows AppsUseLightTheme registry value (returns true for light, false for dark)
    private static bool ReadOsTheme()
    {
        return ((int?)Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme", 1)) != 0;
    }

    // Handles OS theme changes (Win11 light/dark mode)
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
        // Update tray icon
        UpdateTrayIcon();

        // Update background color of popup and close label
        var bg = isLightTheme
            ? Color.White
            : Color.FromArgb(32, 32, 32);
        clockForm.BackColor = bg;
        closeLabel.BackColor = bg;

        // Update foreground color of labels
        var fg = isLightTheme
            ? Color.Black
            : Color.White;
        timeLabel.ForeColor = fg;
        closeLabel.ForeColor = fg;

        // Make DWM non-client strip color match bg
        if (clockForm.IsHandleCreated)
        {
            ApplyDwmCaptionColor();
        }
    }

    // Updates DWM's caption color (for non-client areas) to match form's background
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

    // Loads correct tray icon resource for light/dark mode
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

    // Starts timer with a short first interval so updates always happen "on the second"
    private void StartSynchronizedTimer()
    {
        _firstTick = true;
        timer.Interval = 1000 - DateTime.Now.Millisecond;
        timer.Start();
    }

    // On every timer tick, update clock text; set exact interval after first tick
    private void Timer_Tick(object? s, EventArgs e)
    {
        timeLabel.Text = DateTime.Now.ToString("HH:mm:ss");
        if (_firstTick)
        {
            _firstTick = false;
            timer.Interval = 1000;
        }
    }

    // Handles double-click on popup to maximize/unmaximize, except on close button
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

    // Shows or hides clock popup
    private void ToggleClock()
    {
        if (clockForm.Visible) HideClock(); else ShowClock();
    }

    // Shows clock window, positions near cursor, starts timer
    private void ShowClock()
    {
        timeLabel.Text = DateTime.Now.ToString("HH:mm:ss");
        closeLabel.BringToFront();

        // Restore previous location if it's still on-screen, else show near cursor
        if (
            lastLocation.HasValue
            && Screen.AllScreens.Any(
                sc => sc.WorkingArea.Contains(new Rectangle(lastLocation.Value, clockForm.Size))
            )
        )
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
        StartSynchronizedTimer();  // Start timer aligned to the next second
        closeHideTimer.Start();
    }

    // Hides clock popup and stops timers
    private void HideClock()
    {
        timer.Stop();
        closeHideTimer.Stop();
        closeLabel.Visible = false;
        clockForm.Hide();
    }

    // Allows window dragging by sending WM_NCLBUTTONDOWN to OS
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

        // Shows close button only when mouse is in top-right region of popup (relative to the form)
        closeLabel.Visible = p.X >= r.Width - region && p.Y <= region;
    }
}
