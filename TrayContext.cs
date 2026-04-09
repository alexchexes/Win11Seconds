using System.Diagnostics;
using System.Reflection;
using System.Threading;

sealed class TrayContext : ApplicationContext
{
    private readonly SynchronizationContext uiContext;
    private readonly AppearanceSettingsMonitor appearanceMonitor;
    private readonly NotifyIcon tray;
    private readonly BorderlessResizableForm clockForm;
    private readonly SmoothLabel timeLabel;
    private readonly Label closeLabel;
    private readonly System.Windows.Forms.Timer timer;
    private readonly System.Windows.Forms.Timer closeHideTimer;
    private readonly ContextMenuStrip menu;

    private AppearanceSettings appearance;
    private Point? lastLocation;
    private bool firstTick;
    private bool disposed;
    private float lastAppliedFontSize = 24f;

    public TrayContext()
    {
        uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        appearanceMonitor = new AppearanceSettingsMonitor();
        appearance = appearanceMonitor.Current;
        appearanceMonitor.Changed += OnAppearanceChanged;

        tray = CreateTrayIcon();
        clockForm = CreateClockForm();
        timeLabel = CreateTimeLabel();
        closeLabel = CreateCloseLabel();

        clockForm.Controls.Add(timeLabel);
        clockForm.Controls.Add(closeLabel);
        closeLabel.BringToFront();

        SetupFormEvents();

        timer = new System.Windows.Forms.Timer();
        timer.Tick += Timer_Tick;

        closeHideTimer = new System.Windows.Forms.Timer { Interval = 100 };
        closeHideTimer.Tick += CloseHideTimer_Tick;

        menu = CreateContextMenu();
        tray.ContextMenuStrip = menu;
        clockForm.ContextMenuStrip = menu;

        ApplyAppearance(appearance);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposed)
        {
            base.Dispose(disposing);
            return;
        }

        if (disposing)
        {
            appearanceMonitor.Changed -= OnAppearanceChanged;
            appearanceMonitor.Dispose();

            timer.Dispose();
            closeHideTimer.Dispose();

            tray.ContextMenuStrip = null;
            clockForm.ContextMenuStrip = null;
            menu.Dispose();

            tray.Visible = false;
            tray.Icon?.Dispose();
            tray.Dispose();

            clockForm.Dispose();
        }

        disposed = true;
        base.Dispose(disposing);
    }

    protected override void ExitThreadCore()
    {
        HideClock();
        tray.Visible = false;
        base.ExitThreadCore();
    }

    private NotifyIcon CreateTrayIcon()
    {
        var icon = new NotifyIcon
        {
            Text = "Win11Seconds - Click to show clock",
            Visible = true
        };

        icon.MouseUp += OnTrayMouseUp;
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

        form.HandleCreated += OnClockFormHandleCreated;
        return form;
    }

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

    private Label CreateCloseLabel()
    {
        return new Label
        {
            Text = "✕",
            Font = new Font(Control.DefaultFont.FontFamily, Control.DefaultFont.Size),
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(24, 24),
            Location = new Point(clockForm.ClientSize.Width - 28, 4),
            Visible = false,
            Cursor = Cursors.Hand
        };
    }

    private void SetupFormEvents()
    {
        clockForm.Resize += OnClockFormResize;
        clockForm.FormClosing += OnClockFormClosing;
        clockForm.Move += OnClockFormMove;
        clockForm.MouseDown += DragWindow;
        clockForm.MouseDown += OnPopupMouseDown;
        clockForm.MouseMove += OnPopupMouseMove;
        clockForm.MouseLeave += OnClockFormMouseLeave;

        timeLabel.MouseDown += DragWindow;
        timeLabel.MouseDown += OnPopupMouseDown;
        timeLabel.MouseMove += OnPopupMouseMove;

        closeLabel.Click += OnCloseLabelClick;
        closeLabel.MouseMove += OnPopupMouseMove;
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var contextMenu = new ContextMenuStrip();

        var showHideItem = new ToolStripMenuItem();
        showHideItem.Click += (s, e) => ToggleClock();
        contextMenu.Items.Add(showHideItem);

        var maximizeItem = new ToolStripMenuItem();
        maximizeItem.Click += (s, e) => ToggleWindowState();
        contextMenu.Items.Add(maximizeItem);

        contextMenu.Items.Add("GitHub Repo", null, (s, e) => OpenRepositoryUrl());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, (s, e) => ExitThread());

        contextMenu.Opening += (s, e) =>
        {
            showHideItem.Text = clockForm.Visible ? "Hide" : "Show";
            maximizeItem.Text = clockForm.WindowState == FormWindowState.Normal
                ? "Maximize"
                : "Unmaximize";
        };

        return contextMenu;
    }

    private void OnAppearanceChanged(object? sender, AppearanceSettings nextAppearance)
    {
        uiContext.Post(_ =>
        {
            if (disposed)
            {
                return;
            }

            ApplyAppearance(nextAppearance);
        }, null);
    }

    private void ApplyAppearance(AppearanceSettings nextAppearance)
    {
        appearance = nextAppearance;
        UpdateTrayIcon();

        clockForm.BackColor = appearance.PopupBackgroundColor;
        timeLabel.BackColor = appearance.PopupBackgroundColor;
        closeLabel.BackColor = appearance.PopupBackgroundColor;

        timeLabel.ForeColor = appearance.PopupForegroundColor;
        closeLabel.ForeColor = appearance.PopupForegroundColor;

        if (clockForm.IsHandleCreated)
        {
            ApplyWindowChrome();
        }
    }

    private void UpdateTrayIcon()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var iconStream = assembly.GetManifestResourceStream(appearance.TrayIconResourceName);

        Icon newIcon = iconStream != null
            ? new Icon(iconStream)
            : (Icon)SystemIcons.Application.Clone();

        var previousIcon = tray.Icon;
        tray.Icon = newIcon;
        previousIcon?.Dispose();
    }

    private void ApplyWindowChrome()
    {
        NativeMethods.TrySetRoundedCorners(clockForm.Handle);
        NativeMethods.TrySetCaptionColor(clockForm.Handle, clockForm.BackColor);
    }

    private void StartSynchronizedTimer()
    {
        firstTick = true;
        timer.Interval = 1000 - DateTime.Now.Millisecond;
        timer.Start();
    }

    private void ToggleWindowState()
    {
        if (!clockForm.Visible)
        {
            ShowClock();
        }

        clockForm.WindowState = clockForm.WindowState == FormWindowState.Normal
            ? FormWindowState.Maximized
            : FormWindowState.Normal;
    }

    private void ToggleClock()
    {
        if (clockForm.Visible)
        {
            HideClock();
        }
        else
        {
            ShowClock();
        }
    }

    private void ShowClock()
    {
        timeLabel.Text = DateTime.Now.ToString("HH:mm:ss");
        closeLabel.BringToFront();

        if (
            lastLocation.HasValue
            && Screen.AllScreens.Any(
                screen => screen.WorkingArea.Contains(new Rectangle(lastLocation.Value, clockForm.Size)))
        )
        {
            clockForm.Location = lastLocation.Value;
        }
        else
        {
            const int offset = 30;
            var cursorPosition = Cursor.Position;
            var screen = Screen.FromPoint(cursorPosition);
            int x = cursorPosition.X - clockForm.Width / 2;
            int y = cursorPosition.Y - clockForm.Height - offset;

            x = Math.Max(screen.WorkingArea.Left, Math.Min(x, screen.WorkingArea.Right - clockForm.Width));
            if (y < screen.WorkingArea.Top)
            {
                y = cursorPosition.Y + offset;
            }

            clockForm.Location = new Point(x, y);
        }

        clockForm.Show();
        StartSynchronizedTimer();
        closeHideTimer.Start();
    }

    private void HideClock()
    {
        timer.Stop();
        closeHideTimer.Stop();
        closeLabel.Visible = false;
        clockForm.Hide();
    }

    private void AutoResizeFontAndCloseLabel()
    {
        const int padding = 4;
        int availableWidth = clockForm.ClientSize.Width - padding * 2;
        int availableHeight = clockForm.ClientSize.Height - padding * 2;
        if (availableWidth <= 0 || availableHeight <= 0)
        {
            closeLabel.Location = new Point(Math.Max(0, clockForm.ClientSize.Width - closeLabel.Width - 4), 0);
            return;
        }

        string sampleText = string.IsNullOrWhiteSpace(timeLabel.Text) ? "00:00:00" : timeLabel.Text;

        using var graphics = clockForm.CreateGraphics();
        using var testFont = new Font("Segoe UI", Math.Max(1f, availableHeight), FontStyle.Regular);
        SizeF measurement = graphics.MeasureString(sampleText, testFont);
        if (measurement.Width <= 0 || measurement.Height <= 0)
        {
            closeLabel.Location = new Point(Math.Max(0, clockForm.ClientSize.Width - closeLabel.Width - 4), 0);
            return;
        }

        float scaleWidth = availableWidth / measurement.Width;
        float scaleHeight = availableHeight / measurement.Height;
        float newSize = testFont.Size * Math.Min(scaleWidth, scaleHeight);
        if (!float.IsFinite(newSize))
        {
            closeLabel.Location = new Point(Math.Max(0, clockForm.ClientSize.Width - closeLabel.Width - 4), 0);
            return;
        }

        newSize = Math.Clamp(newSize, 1f, 512f);
        if (Math.Abs(lastAppliedFontSize - newSize) >= 0.25f)
        {
            timeLabel.Font = new Font("Segoe UI", newSize, FontStyle.Regular);
            lastAppliedFontSize = newSize;
        }

        closeLabel.Location = new Point(Math.Max(0, clockForm.ClientSize.Width - closeLabel.Width - 4), 0);
    }

    private static void OpenRepositoryUrl()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/alexchexes/Win11Seconds",
            UseShellExecute = true
        });
    }

    private void DragWindow(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        NativeMethods.ReleaseCapture();
        NativeMethods.SendMessage(
            clockForm.Handle,
            NativeMethods.WM_NCLBUTTONDOWN,
            (IntPtr)NativeMethods.HTCAPTION,
            IntPtr.Zero);
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        timeLabel.Text = DateTime.Now.ToString("HH:mm:ss");
        if (firstTick)
        {
            firstTick = false;
            timer.Interval = 1000;
        }
    }

    private void CloseHideTimer_Tick(object? sender, EventArgs e)
    {
        if (closeLabel.Visible && !clockForm.Bounds.Contains(Cursor.Position))
        {
            closeLabel.Visible = false;
        }
    }

    private void OnTrayMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ToggleClock();
        }
    }

    private void OnClockFormHandleCreated(object? sender, EventArgs e)
    {
        ApplyWindowChrome();
    }

    private void OnClockFormResize(object? sender, EventArgs e)
    {
        AutoResizeFontAndCloseLabel();
    }

    private void OnClockFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideClock();
        }
    }

    private void OnClockFormMove(object? sender, EventArgs e)
    {
        lastLocation = clockForm.Location;
    }

    private void OnCloseLabelClick(object? sender, EventArgs e)
    {
        HideClock();
    }

    private void OnClockFormMouseLeave(object? sender, EventArgs e)
    {
        closeLabel.Visible = false;
    }

    private void OnPopupMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || e.Clicks != 2)
        {
            return;
        }

        var clickPoint = clockForm.PointToClient(Cursor.Position);
        if (closeLabel.Bounds.Contains(clickPoint))
        {
            return;
        }

        ToggleWindowState();
        clockForm.PerformLayout();
    }

    private void OnPopupMouseMove(object? sender, MouseEventArgs e)
    {
        var cursorPosition = clockForm.PointToClient(Cursor.Position);
        var clientRectangle = clockForm.ClientRectangle;
        const int hoverSize = 30;
        const int margin = 7;
        int region = hoverSize + margin;

        closeLabel.Visible = cursorPosition.X >= clientRectangle.Width - region
            && cursorPosition.Y <= region;
    }
}
