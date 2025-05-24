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
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
    const int WM_NCLBUTTONDOWN = 0xA1, HTCAPTION = 0x2;

    private readonly NotifyIcon tray;
    private readonly Form clockForm;
    private readonly Label timeLabel;
    private readonly Button btnClose;
    private readonly System.Windows.Forms.Timer timer;
    private readonly bool isLightTheme;
    private Point? lastLocation;

    public TrayContext()
    {
        // detect light/dark
        isLightTheme = ((int?)Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme", 1)) != 0;

        // tray icon + menu
        tray = new NotifyIcon {
            Icon             = SystemIcons.Application,
            Text             = "Click to show seconds",
            Visible          = true,
            ContextMenuStrip = new ContextMenuStrip()
        };
        tray.MouseUp += (s,e) => {
            if(e.Button==MouseButtons.Left) ToggleClock();
        };
        tray.ContextMenuStrip.Items.Add("Exit", null, (s,e) => {
            tray.Visible = false;
            Application.ExitThread();
        });

        // popup window
        clockForm = new Form {
            FormBorderStyle = FormBorderStyle.None,
            ShowInTaskbar   = false,
            StartPosition   = FormStartPosition.Manual,
            TopMost         = true,
            BackColor       = isLightTheme ? Color.White : Color.FromArgb(32,32,32),
            ClientSize      = new Size(200,80)
        };
        clockForm.Load   += (_,__) => ApplyRoundedCorners(12);
        clockForm.Resize += (_,__) => ApplyRoundedCorners(12);
        clockForm.FormClosing += (s,e) => {
            if(e.CloseReason==CloseReason.UserClosing) {
                e.Cancel = true;
                HideClock();
            }
        };
        clockForm.Move += (_,__) => lastLocation = clockForm.Location;

        // clock label
        timeLabel = new Label {
            Dock                      = DockStyle.Fill,
            Font                      = new Font("Segoe UI",24),
            TextAlign                 = ContentAlignment.MiddleCenter,
            ForeColor                 = isLightTheme ? Color.Black : Color.White,
            UseCompatibleTextRendering= true
        };
        clockForm.Controls.Add(timeLabel);

        // close button
        btnClose = new Button {
            Text      = "✕",
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = isLightTheme ? Color.Black : Color.White,
            Size      = new Size(24,24),
            Location  = new Point(clockForm.ClientSize.Width-28,4),
            Visible   = false,
            TabStop   = false
        };
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.Click += (s,e) => HideClock();
        clockForm.Controls.Add(btnClose);
        btnClose.BringToFront();

        // make draggable
        clockForm.MouseDown += DragWindow;
        timeLabel.MouseDown  += DragWindow;
        // (no DragWindow on btnClose)

        // show/hide close button
        clockForm.MouseMove  += OnPopupMouseMove;
        timeLabel.MouseMove  += OnPopupMouseMove;  // fixed +=
        clockForm.MouseLeave += (s,e) => btnClose.Visible = false;
        // removed timeLabel.MouseLeave

        // timer
        timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (_,__) => timeLabel.Text = DateTime.Now.ToString("HH:mm:ss");
    }

    private void ToggleClock()
    {
        if (clockForm.Visible) HideClock();
        else                 ShowClock();
    }

    private void ShowClock()
    {
        timeLabel.Text = DateTime.Now.ToString("HH:mm:ss");
        btnClose.Visible = false;

        if (lastLocation.HasValue &&
            Screen.AllScreens.Any(sc =>
                sc.WorkingArea.Contains(new Rectangle(lastLocation.Value, clockForm.Size))))
        {
            clockForm.Location = lastLocation.Value;
        }
        else
        {
            const int off = 8;
            var cur = Cursor.Position;
            var scr = Screen.FromPoint(cur);
            int x = cur.X - clockForm.Width/2;
            int y = cur.Y - clockForm.Height - off;
            x = Math.Max(scr.WorkingArea.Left, Math.Min(x, scr.WorkingArea.Right - clockForm.Width));
            if(y < scr.WorkingArea.Top) y = cur.Y + off;
            clockForm.Location = new Point(x,y);
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
        if (e.Button!=MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(clockForm.Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
    }

    private void OnPopupMouseMove(object? s, MouseEventArgs e)
    {
        var r = clockForm.ClientRectangle;
        btnClose.Visible = (e.X >= r.Width - 30 && e.Y <= 30);
    }

    private void ApplyRoundedCorners(int r)
    {
        var b = new Rectangle(0,0,clockForm.Width,clockForm.Height);
        using var path = new GraphicsPath();
        path.AddArc(b.Left,    b.Top,    r*2, r*2, 180, 90);
        path.AddArc(b.Right-r*2,b.Top,    r*2, r*2, 270, 90);
        path.AddArc(b.Right-r*2,b.Bottom-r*2,r*2,r*2,   0, 90);
        path.AddArc(b.Left,    b.Bottom-r*2,r*2,r*2,   90, 90);
        path.CloseFigure();
        clockForm.Region = new Region(path);
        btnClose.Location = new Point(clockForm.ClientSize.Width-28,4);
    }
}
