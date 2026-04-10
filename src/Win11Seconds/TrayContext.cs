using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace Win11Seconds;

sealed class TrayContext : ApplicationContext
{
    private const int BackdropFadeOutIntervalMilliseconds = 20;
    private const int BackdropFadeOutSteps = 6;

    private readonly SynchronizationContext uiContext;
    private readonly AppearanceSettingsMonitor appearanceMonitor;
    private readonly NotifyIcon tray;
    private readonly BorderlessResizableForm clockForm;
    private Font timeLabelFont;
    private readonly System.Windows.Forms.Timer timer;
    private readonly System.Windows.Forms.Timer closeHideTimer;
    private readonly System.Windows.Forms.Timer backdropFadeOutTimer;
    private readonly ContextMenuStrip menu;

    private AppearanceSettings appearance;
    private Point? lastLocation;
    private bool firstTick;
    private bool disposed;
    private bool isClockActive;
    private bool isSystemBackdropEnabled;
    private bool isBackdropFadeOutInProgress;
    private int backdropFadeOutStep;
    private float lastAppliedFontSize = 24f;

    public TrayContext()
    {
        uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        appearanceMonitor = new AppearanceSettingsMonitor();
        appearance = appearanceMonitor.Current;
        appearanceMonitor.Changed += OnAppearanceChanged;

        timeLabelFont = CreateTimeLabelFont(24f);
        tray = CreateTrayIcon();
        clockForm = CreateClockForm();

        SetupFormEvents();

        timer = new System.Windows.Forms.Timer();
        timer.Tick += Timer_Tick;

        closeHideTimer = new System.Windows.Forms.Timer { Interval = 100 };
        closeHideTimer.Tick += CloseHideTimer_Tick;

        backdropFadeOutTimer = new System.Windows.Forms.Timer { Interval = BackdropFadeOutIntervalMilliseconds };
        backdropFadeOutTimer.Tick += BackdropFadeOutTimer_Tick;

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
            backdropFadeOutTimer.Dispose();

            tray.ContextMenuStrip = null;
            clockForm.ContextMenuStrip = null;
            menu.Dispose();

            tray.Visible = false;
            tray.Icon?.Dispose();
            tray.Dispose();

            clockForm.Dispose();
            timeLabelFont.Dispose();
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
            MinimumSize = new Size(120, 48),
            DisplayFont = timeLabelFont
        };

        form.HandleCreated += OnClockFormHandleCreated;
        return form;
    }

    private static Font CreateTimeLabelFont(float size)
    {
        return new Font("Segoe UI", size, FontStyle.Regular);
    }

    private void SetupFormEvents()
    {
        clockForm.Resize += OnClockFormResize;
        clockForm.FormClosing += OnClockFormClosing;
        clockForm.Move += OnClockFormMove;
        clockForm.WindowActivationChanged += OnClockFormActivationChanged;
        clockForm.MouseDown += DragWindow;
        clockForm.MouseDown += OnPopupMouseDown;
        clockForm.MouseMove += OnPopupMouseMove;
        clockForm.MouseLeave += OnClockFormMouseLeave;
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
        UpdateWindowAppearance(forceFrameRefresh: true);
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

    private bool ApplyWindowChrome()
    {
        bool shouldEnableBackdrop = appearance.IsTransparencyEffectsEnabled
            && (isClockActive || isBackdropFadeOutInProgress);
        NativeMethods.TrySetRoundedCorners(clockForm.Handle);
        NativeMethods.TrySetImmersiveDarkMode(clockForm.Handle, appearance.UseImmersiveDarkMode);
        NativeMethods.TryHideBorder(clockForm.Handle);

        bool useSystemBackdrop = shouldEnableBackdrop
            && NativeMethods.TrySetSystemBackdropType(
                clockForm.Handle,
                NativeMethods.DwmSystemBackdropType.TransientWindow);

        if (!useSystemBackdrop)
        {
            NativeMethods.TrySetSystemBackdropType(
                clockForm.Handle,
                NativeMethods.DwmSystemBackdropType.None);
        }

        NativeMethods.TryExtendFrameIntoClientArea(clockForm.Handle, useSystemBackdrop);
        clockForm.UseSystemBackdropBackground = useSystemBackdrop;
        if (useSystemBackdrop)
        {
            NativeMethods.TryResetCaptionColor(clockForm.Handle);
        }
        else
        {
            NativeMethods.TrySetCaptionColor(clockForm.Handle, GetPopupWindowBackgroundColor(useSystemBackdrop));
        }

        return useSystemBackdrop;
    }

    private void ApplyPopupColors(bool useSystemBackdrop)
    {
        clockForm.BackColor = GetPopupWindowBackgroundColor(useSystemBackdrop);
        clockForm.BackdropOverlayColor = useSystemBackdrop
            ? GetPopupBackdropOverlayColor()
            : Color.Empty;
        clockForm.DisplayForeColor = appearance.PopupForegroundColor;
        clockForm.CloseButtonForeColor = appearance.PopupForegroundColor;
    }

    private Color GetPopupWindowBackgroundColor(bool useSystemBackdrop)
    {
        if (useSystemBackdrop)
        {
            return appearance.PopupSolidBackgroundColor;
        }

        return appearance.IsTransparencyEffectsEnabled && !isClockActive
            ? appearance.PopupInactiveBackgroundColor
            : appearance.PopupSolidBackgroundColor;
    }

    private Color GetPopupBackdropOverlayColor()
    {
        if (!isBackdropFadeOutInProgress)
        {
            return appearance.PopupActiveBackdropOverlayColor;
        }

        float progress = Math.Clamp(backdropFadeOutStep / (float)BackdropFadeOutSteps, 0f, 1f);
        return InterpolateColor(
            appearance.PopupActiveBackdropOverlayColor,
            appearance.PopupInactiveBackdropOverlayColor,
            progress);
    }

    private static Color InterpolateColor(Color start, Color end, float progress)
    {
        static byte Lerp(byte from, byte to, float amount)
        {
            return (byte)Math.Clamp((int)Math.Round(from + ((to - from) * amount)), 0, 255);
        }

        return Color.FromArgb(
            Lerp(start.A, end.A, progress),
            Lerp(start.R, end.R, progress),
            Lerp(start.G, end.G, progress),
            Lerp(start.B, end.B, progress));
    }

    private void StartSynchronizedTimer()
    {
        firstTick = true;
        timer.Interval = ClockLayout.GetFirstTickIntervalMilliseconds(DateTime.Now.Millisecond);
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
        CancelBackdropFadeOut();
        clockForm.DisplayText = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        clockForm.CloseButtonVisible = false;

        if (
            lastLocation.HasValue
            && ClockLayout.IsStoredLocationVisible(
                lastLocation.Value,
                clockForm.Size,
                Screen.AllScreens.Select(screen => screen.WorkingArea))
        )
        {
            clockForm.Location = lastLocation.Value;
        }
        else
        {
            var cursorPosition = Cursor.Position;
            var screen = Screen.FromPoint(cursorPosition);
            clockForm.Location = ClockLayout.CalculatePopupLocation(
                cursorPosition,
                screen.WorkingArea,
                clockForm.Size);
        }

        clockForm.Show();
        clockForm.BringToFront();
        _ = NativeMethods.SetForegroundWindow(clockForm.Handle);
        clockForm.Activate();
        isClockActive = true;
        UpdateWindowAppearance(forceFrameRefresh: false);
        StartSynchronizedTimer();
        closeHideTimer.Start();
    }

    private void HideClock()
    {
        CancelBackdropFadeOut();
        timer.Stop();
        closeHideTimer.Stop();
        clockForm.CloseButtonVisible = false;
        clockForm.Hide();
    }

    private void AutoResizeClockFont()
    {
        string sampleText = string.IsNullOrWhiteSpace(clockForm.DisplayText) ? "00:00:00" : clockForm.DisplayText;
        int measurementFontSize = Math.Max(1, clockForm.ClientSize.Height - ClockLayout.FontPadding * 2);

        using var graphics = clockForm.CreateGraphics();
        using var testFont = new Font("Segoe UI", measurementFontSize, FontStyle.Regular);
        SizeF measurement = graphics.MeasureString(sampleText, testFont);
        float? newSize = ClockLayout.CalculateFontSize(clockForm.ClientSize, measurement, testFont.Size);
        if (newSize.HasValue && Math.Abs(lastAppliedFontSize - newSize.Value) >= 0.25f)
        {
            Font nextFont = CreateTimeLabelFont(newSize.Value);
            Font previousFont = timeLabelFont;
            timeLabelFont = nextFont;
            clockForm.DisplayFont = nextFont;
            lastAppliedFontSize = newSize.Value;
            previousFont.Dispose();
        }
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

        var clickPoint = clockForm.PointToClient(Cursor.Position);
        if (clockForm.IsResizeBorderHit(clickPoint) || clockForm.IsCloseButtonHit(clickPoint))
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
        clockForm.DisplayText = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        if (clockForm.Visible)
        {
            clockForm.Invalidate();
            clockForm.Update();
        }

        if (firstTick)
        {
            firstTick = false;
            timer.Interval = 1000;
        }
    }

    private void CloseHideTimer_Tick(object? sender, EventArgs e)
    {
        if (clockForm.CloseButtonVisible && !clockForm.Bounds.Contains(Cursor.Position))
        {
            clockForm.CloseButtonVisible = false;
            InvalidateClockBackdrop();
        }
    }

    private void BackdropFadeOutTimer_Tick(object? sender, EventArgs e)
    {
        if (!isBackdropFadeOutInProgress)
        {
            backdropFadeOutTimer.Stop();
            return;
        }

        backdropFadeOutStep = Math.Min(backdropFadeOutStep + 1, BackdropFadeOutSteps);
        UpdateWindowAppearance(forceFrameRefresh: false);

        if (backdropFadeOutStep < BackdropFadeOutSteps)
        {
            return;
        }

        backdropFadeOutTimer.Stop();
        isBackdropFadeOutInProgress = false;
        UpdateWindowAppearance(forceFrameRefresh: false);
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
        UpdateWindowAppearance(forceFrameRefresh: true);
    }

    private void OnClockFormResize(object? sender, EventArgs e)
    {
        AutoResizeClockFont();
        InvalidateClockBackdrop();
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
        InvalidateClockBackdrop();
    }

    private void OnClockFormMouseLeave(object? sender, EventArgs e)
    {
        if (clockForm.CloseButtonVisible)
        {
            clockForm.CloseButtonVisible = false;
            InvalidateClockBackdrop();
        }
    }

    private void OnPopupMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        var clickPoint = clockForm.PointToClient(Cursor.Position);
        if (clockForm.IsCloseButtonHit(clickPoint))
        {
            HideClock();
            return;
        }

        if (e.Clicks != 2)
        {
            return;
        }

        ToggleWindowState();
        clockForm.PerformLayout();
    }

    private void OnPopupMouseMove(object? sender, MouseEventArgs e)
    {
        var cursorPosition = clockForm.PointToClient(Cursor.Position);
        bool shouldShow = ClockLayout.ShouldShowCloseButton(cursorPosition, clockForm.ClientRectangle);
        if (clockForm.CloseButtonVisible != shouldShow)
        {
            clockForm.CloseButtonVisible = shouldShow;
            InvalidateClockBackdrop();
        }
    }

    private void OnClockFormActivationChanged(bool nextIsActive)
    {
        if (nextIsActive == isClockActive)
        {
            return;
        }

        if (nextIsActive)
        {
            CancelBackdropFadeOut();
        }

        isClockActive = nextIsActive;

        if (!nextIsActive && appearance.IsTransparencyEffectsEnabled)
        {
            isBackdropFadeOutInProgress = true;
            backdropFadeOutStep = 0;
            backdropFadeOutTimer.Start();
        }

        UpdateWindowAppearance(forceFrameRefresh: false);
    }

    private void InvalidateClockBackdrop()
    {
        if (!clockForm.Visible)
        {
            return;
        }

        if (clockForm.IsHandleCreated)
        {
            if (isSystemBackdropEnabled)
            {
                NativeMethods.RefreshWindow(clockForm.Handle, includeFrame: false);
            }
            else
            {
                clockForm.Invalidate();
                clockForm.Update();
            }
        }
    }

    private void UpdateWindowAppearance(bool forceFrameRefresh)
    {
        if (clockForm.IsHandleCreated)
        {
            isSystemBackdropEnabled = ApplyWindowChrome();
        }
        else
        {
            isSystemBackdropEnabled = appearance.IsTransparencyEffectsEnabled
                && (isClockActive || isBackdropFadeOutInProgress);
        }

        ApplyPopupColors(isSystemBackdropEnabled);

        if (!clockForm.IsHandleCreated || !clockForm.Visible)
        {
            return;
        }

        // Toggling the backdrop at runtime does not need a non-client frame refresh.
        // Asking Windows to restage the frame during focus transitions is what exposes
        // the temporary border/caption flash the user sees.
        if (forceFrameRefresh)
        {
            _ = NativeMethods.TryNotifyFrameChanged(clockForm.Handle);
        }

        if (isSystemBackdropEnabled)
        {
            NativeMethods.RefreshWindow(clockForm.Handle, includeFrame: forceFrameRefresh);
        }
        else
        {
            clockForm.Invalidate();
            clockForm.Update();
        }
    }

    private void CancelBackdropFadeOut()
    {
        backdropFadeOutTimer.Stop();
        isBackdropFadeOutInProgress = false;
        backdropFadeOutStep = 0;
    }
}
