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
    // P/Invoke for window dragging
    [DllImport("user32.dll")] 
    private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] 
    private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HTCAPTION      = 0x2;

    private readonly NotifyIcon tray;
    private readonly Form clockForm;
    private readonly Label timeLabel;
    private readonly Button btnClose;
    private readonly System.Windows.Forms.Timer timer;
    private readonly bool isLightTheme;
    private Point? lastLocation;

    public TrayContext()
    {
        // Detect Windows app theme
        isLightTheme = ((int?)Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme", 1)) != 0;

        // ── Tray Icon + Exit ───────────────────────────
        tray = new NotifyIcon {
            Icon            = SystemIcons.Application,
            Text            = "Click to show seconds",
            Visible         = true,
            ContextMenuStrip= new ContextMenuStrip()
        };
        tray.MouseUp += Tray_MouseUp;
        tray.ContextMenuStrip.Items.Add("Exit", null, (_,__)=>
            { tray.Visible=false; Application.ExitThread(); });

        // ── Popup Window ───────────────────────────────
        clockForm = new Form {
            FormBorderStyle = FormBorderStyle.None,
            ShowInTaskbar   = false,
            StartPosition   = FormStartPosition.Manual,
            TopMost         = true,
            BackColor       = isLightTheme ? Color.White : Color.FromArgb(32,32,32),
            ClientSize      = new Size(200,80)
        };
        clockForm.Load += (_,__) => ApplyRoundedCorners(12);
        clockForm.Resize += (_,__) => ApplyRoundedCorners(12);

        // Intercept native close so we never Dispose
        clockForm.FormClosing += (s,e)=> {
            if(e.CloseReason==CloseReason.UserClosing){
                e.Cancel=true;
                HideClock();
            }
        };
        clockForm.Move += (_,__)=> lastLocation = clockForm.Location;

        // ── Close Button ──────────────────────────────
        btnClose = new Button {
            Text            = "✕",
            FlatStyle       = FlatStyle.Flat,
            ForeColor       = isLightTheme ? Color.Black : Color.White,
            BackColor       = Color.Transparent,
            Size            = new Size(24,24),
            Location        = new Point(clockForm.ClientSize.Width-28,4),
            Visible         = false,
            TabStop         = false
        };
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.Click += (_,__)=> HideClock();
        clockForm.Controls.Add(btnClose);

        // ── Clock Label ───────────────────────────────
        timeLabel = new Label {
            Dock                      = DockStyle.Fill,
            Font                      = new Font("Segoe UI",24),
            TextAlign                 = ContentAlignment.MiddleCenter,
            ForeColor                 = isLightTheme ? Color.Black : Color.White,
            UseCompatibleTextRendering= true
        };
        clockForm.Controls.Add(timeLabel);

        // ── Make both form & label draggable ──────────
        clockForm.MouseDown += DragWindow;
        timeLabel.MouseDown  += DragWindow;

        // ── Show/hide close button on hover ──────────
        clockForm.MouseMove += OnPopupMouseMove;
        clockForm.MouseLeave+= (_,__)=> btnClose.Visible = false;

        // ── Timer ────────────────────────────────────
        timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (_,__)=> timeLabel.Text = DateTime.Now.ToString("HH:mm:ss");
    }

    private void Tray_MouseUp(object? s, MouseEventArgs e)
    {
        if (e.Button==MouseButtons.Left) ToggleClock();
    }

    private void ToggleClock()
    {
        if (clockForm.Visible) HideClock();
        else ShowClock();
    }

    private void ShowClock()
    {
        timeLabel.Text = DateTime.Now.ToString("HH:mm:ss");
        btnClose.Visible = false;

        // reuse last dragged pos if valid
        if (lastLocation.HasValue && 
            Screen.AllScreens.Any(sc=> sc.WorkingArea.Contains(new Rectangle(lastLocation.Value,clockForm.Size))))
        {
            clockForm.Location = lastLocation.Value;
        }
        else
        {
            const int offset=8;
            var cur=Cursor.Position;
            var scr=Screen.FromPoint(cur);
            int x=cur.X-clockForm.Width/2;
            int y=cur.Y-clockForm.Height-offset;
            x=Math.Max(scr.WorkingArea.Left,Math.Min(x,scr.WorkingArea.Right-clockForm.Width));
            if(y<scr.WorkingArea.Top) y=cur.Y+offset;
            clockForm.Location=new Point(x,y);
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
        if(e.Button!=MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(clockForm.Handle,WM_NCLBUTTONDOWN,(IntPtr)HTCAPTION,IntPtr.Zero);
    }

    private void OnPopupMouseMove(object? s, MouseEventArgs e)
    {
        // if mouse within 30×30px of top-right corner
        var r = clockForm.ClientRectangle;
        if (e.X >= r.Width-30 && e.Y <= 30)
            btnClose.Visible = true;
        else 
            btnClose.Visible = false;
    }

    private void ApplyRoundedCorners(int radius)
    {
        var bounds = new Rectangle(0, 0, clockForm.Width, clockForm.Height);
        using var path = new GraphicsPath();
        path.AddArc(bounds.Left,  bounds.Top,     2*radius, 2*radius, 180, 90);
        path.AddArc(bounds.Right-2*radius, bounds.Top,     2*radius, 2*radius, 270, 90);
        path.AddArc(bounds.Right-2*radius, bounds.Bottom-2*radius, 2*radius, 2*radius, 0, 90);
        path.AddArc(bounds.Left,  bounds.Bottom-2*radius, 2*radius, 2*radius, 90, 90);
        path.CloseFigure();
        clockForm.Region = new Region(path);
        // ensure close button stays at top-right
        btnClose.Location = new Point(clockForm.ClientSize.Width-28, 4);
    }
}
