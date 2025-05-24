using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

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
    // P/Invoke for dragging
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
    const int WM_NCLBUTTONDOWN = 0xA1, HTCAPTION = 0x2;

    private readonly NotifyIcon tray;
    private readonly Form clockForm;
    private readonly Label timeLabel;
    private readonly Label closeLabel;     // replaced Button with Label
    private readonly System.Windows.Forms.Timer timer;
    private readonly bool isLightTheme;
    private Point? lastLocation;

    public TrayContext()
    {
        // detect theme
        isLightTheme = ((int?)Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme", 1)) != 0;

        // tray icon
        tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Click to show seconds",
            Visible = true,
            ContextMenuStrip = new ContextMenuStrip()
        };
        tray.MouseUp += (s, e) => { if (e.Button == MouseButtons.Left) ToggleClock(); };
        tray.ContextMenuStrip.Items.Add("Exit", null, (s, e) => { tray.Visible = false; Application.ExitThread(); });

        // popup form
        clockForm = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            ShowInTaskbar   = false,
            StartPosition   = FormStartPosition.Manual,
            TopMost         = true,
            BackColor       = isLightTheme ? Color.White : Color.FromArgb(32, 32, 32),
            ClientSize      = new Size(200, 80)
        };
        clockForm.Load   += (_, __) => ApplyRoundedCorners(12);
        clockForm.Resize += (_, __) => ApplyRoundedCorners(12);
        clockForm.FormClosing += (s, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideClock();
            }
        };
        clockForm.Move += (_, __) => lastLocation = clockForm.Location;

        // clock label
        timeLabel = new Label
        {
            Dock                      = DockStyle.Fill,
            Font                      = new Font("Segoe UI", 24),
            TextAlign                 = ContentAlignment.MiddleCenter,
            ForeColor                 = isLightTheme ? Color.Black : Color.White,
            UseCompatibleTextRendering = true
        };
        clockForm.Controls.Add(timeLabel);

        // close label (pure ✕, no background or border)
        closeLabel = new Label
        {
            Text      = "✕",
            Font      = new Font(Control.DefaultFont.FontFamily, Control.DefaultFont.Size, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
            ForeColor = isLightTheme ? Color.Black : Color.White,
            Size      = new Size(24, 24),
            Location  = new Point(clockForm.ClientSize.Width - 28, 4),
            Visible   = false
        };
        closeLabel.Click += (s, e) => HideClock();
        clockForm.Controls.Add(closeLabel);
        closeLabel.BringToFront();

        // draggable everywhere except close label
        clockForm.MouseDown += DragWindow;
        timeLabel.MouseDown  += DragWindow;

        // hover region with margin
        clockForm.MouseMove  += OnPopupMouseMove;
        timeLabel.MouseMove  += OnPopupMouseMove;
        clockForm.MouseLeave += (s, e) => closeLabel.Visible = false;

        // timer
        timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (_, __) => timeLabel.Text = DateTime.Now.ToString("HH:mm:ss");
    }

    private void ToggleClock()
    {
        if (clockForm.Visible) HideClock();
        else                     ShowClock();
    }

    private void ShowClock()
    {
        timeLabel.Text      = DateTime.Now.ToString("HH:mm:ss");
        closeLabel.Visible = false;

        if (lastLocation.HasValue &&
            Screen.AllScreens.Any(sc => sc.WorkingArea.Contains(new Rectangle(lastLocation.Value, clockForm.Size))))
        {
            clockForm.Location = lastLocation.Value;
        }
        else
        {
            const int offset = 8;
            var cur = Cursor.Position;
            var scr = Screen.FromPoint(cur);
            int x = cur.X - clockForm.Width / 2;
            int y = cur.Y - clockForm.Height - offset;
            x = Math.Max(scr.WorkingArea.Left, Math.Min(x, scr.WorkingArea.Right - clockForm.Width));
            if (y < scr.WorkingArea.Top) y = cur.Y + offset;
            clockForm.Location = new Point(x, y);
        }

        clockForm.Show();
        timer.Start();
    }

    private void HideClock()
    {
        timer.Stop();
        clockForm.Hide();
    }

    private void DragWindow(object? s, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(clockForm.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
    }

    private void OnPopupMouseMove(object? s, MouseEventArgs e)
    {
        var r = clockForm.ClientRectangle;
        const int baseSize = 30, margin = 7;
        int region = baseSize + margin;
        closeLabel.Visible = (e.X >= r.Width - region && e.Y <= region);
    }

    private void ApplyRoundedCorners(int r)
    {
        var b = new Rectangle(0, 0, clockForm.Width, clockForm.Height);
        using var path = new GraphicsPath();
        path.AddArc(b.Left, b.Top, r*2, r*2, 180, 90);
        path.AddArc(b.Right - r*2, b.Top, r*2, r*2, 270, 90);
        path.AddArc(b.Right - r*2, b.Bottom - r*2, r*2, r*2,   0, 90);
        path.AddArc(b.Left, b.Bottom - r*2, r*2, r*2,  90, 90);
        path.CloseFigure();
        clockForm.Region = new Region(path);
        closeLabel.Location = new Point(clockForm.ClientSize.Width - 28, 4);
    }
}
