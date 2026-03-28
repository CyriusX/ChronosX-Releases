using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace TimeTrack.DesktopHost.UI;

/// <summary>
/// Premium floating status bar — pill-shaped, glassmorphism-inspired,
/// draggable mini widget shown when the main window is minimized.
/// Inspired by Spotify mini player, Rize bar, macOS menu bar widgets.
/// </summary>
public sealed class FloatingStatusBarForm : Form
{
    public event EventHandler? RestoreRequested;

    // --- Theme ---
    private static readonly Color BgDark = Color.FromArgb(16, 18, 27);
    private static readonly Color BgCard = Color.FromArgb(24, 27, 38);
    private static readonly Color BorderDark = Color.FromArgb(45, 50, 65);
    private static readonly Color TextPrimary = Color.FromArgb(240, 243, 255);
    private static readonly Color TextSecondary = Color.FromArgb(140, 148, 170);
    private static readonly Color Cyan = Color.FromArgb(74, 217, 255);
    private static readonly Color Green = Color.FromArgb(74, 222, 128);
    private static readonly Color Amber = Color.FromArgb(251, 191, 36);
    private static readonly Color Purple = Color.FromArgb(168, 130, 255);
    private static readonly Color SepLine = Color.FromArgb(40, 44, 60);

    private const int H = 48;
    private const int Pad = 16;
    private const int IconSize = 22;
    private const int SepGap = 12;

    // Data labels
    private readonly Label _timeIcon;
    private readonly Label _timeValue;
    private readonly Label _prodIcon;
    private readonly Label _prodValue;
    private readonly Label _scoreIcon;
    private readonly Label _scoreValue;
    private readonly Label _focusDot;
    private readonly Label _focusValue;
    private readonly PictureBox _appIcon;

    // Tracking state indicator (pulsing LED)
    private readonly PulsingLedControl _statusDot;

    private bool _focusVisible;

    public FloatingStatusBarForm()
    {
        SuspendLayout();

        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = BgDark;
        Opacity = 0.94;
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.None;

        var boldFont = new Font("Segoe UI Semibold", 10f);
        var labelFont = new Font("Segoe UI", 8f);
        var iconFont = new Font("Segoe UI Emoji", 10f);
        var focusFont = new Font("Segoe UI Semibold", 9f);

        var cy = H / 2;
        var x = Pad;

        // --- Status LED (pulsing green=active, static red=paused) ---
        _statusDot = new PulsingLedControl
        {
            Size = new Size(18, 18),
            LedColor = Green,
            BackColor = Color.Transparent,
        };
        Controls.Add(_statusDot);
        x += 18;

        // --- Active time: icon + value ---
        _timeIcon = new Label
        {
            Text = "\u23F1", // stopwatch
            Font = iconFont,
            ForeColor = Cyan,
            AutoSize = true,
            BackColor = Color.Transparent,
        };
        Controls.Add(_timeIcon);
        PlaceAt(_timeIcon, ref x, cy);

        _timeValue = new Label
        {
            Text = "0h 00m",
            Font = boldFont,
            ForeColor = TextPrimary,
            AutoSize = true,
            BackColor = Color.Transparent,
        };
        Controls.Add(_timeValue);
        PlaceAt(_timeValue, ref x, cy);
        x += SepGap;

        // --- Productive time ---
        _prodIcon = new Label
        {
            Text = "\u2714", // checkmark
            Font = new Font("Segoe UI", 9f),
            ForeColor = Green,
            AutoSize = true,
            BackColor = Color.Transparent,
        };
        Controls.Add(_prodIcon);
        PlaceAt(_prodIcon, ref x, cy);

        _prodValue = new Label
        {
            Text = "0h 00m",
            Font = boldFont,
            ForeColor = Green,
            AutoSize = true,
            BackColor = Color.Transparent,
        };
        Controls.Add(_prodValue);
        PlaceAt(_prodValue, ref x, cy);
        x += SepGap;

        // --- Score ---
        _scoreIcon = new Label
        {
            Text = "\u26A1", // lightning
            Font = iconFont,
            ForeColor = Amber,
            AutoSize = true,
            BackColor = Color.Transparent,
        };
        Controls.Add(_scoreIcon);
        PlaceAt(_scoreIcon, ref x, cy);

        _scoreValue = new Label
        {
            Text = "--",
            Font = boldFont,
            ForeColor = Amber,
            AutoSize = true,
            BackColor = Color.Transparent,
        };
        Controls.Add(_scoreValue);
        PlaceAt(_scoreValue, ref x, cy);
        x += SepGap;

        // --- Focus session (hidden by default) ---
        _focusDot = new Label
        {
            Text = "\u25CF", // filled circle
            Font = new Font("Segoe UI", 7f),
            ForeColor = Purple,
            AutoSize = true,
            BackColor = Color.Transparent,
            Visible = false,
        };
        Controls.Add(_focusDot);

        _focusValue = new Label
        {
            Text = "",
            Font = focusFont,
            ForeColor = Purple,
            AutoSize = true,
            BackColor = Color.Transparent,
            Visible = false,
        };
        Controls.Add(_focusValue);

        // --- App icon (restore) ---
        _appIcon = new PictureBox
        {
            Size = new Size(IconSize, IconSize),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
        };
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "app-icon.ico");
            if (File.Exists(iconPath))
            {
                using var ico = new Icon(iconPath, IconSize, IconSize);
                _appIcon.Image = ico.ToBitmap();
            }
        }
        catch { }
        _appIcon.Click += (_, _) => RestoreRequested?.Invoke(this, EventArgs.Empty);

        // Tooltip
        var tip = new ToolTip();
        tip.SetToolTip(_appIcon, "Abrir ChronosX");
        Controls.Add(_appIcon);

        LayoutAll();

        var wa = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(wa.Right - Width - 20, wa.Bottom - Height - 20);

        ResumeLayout(true);
        PerformLayout();

        // Round the entire form into a pill shape
        MakePillShape();
        EnableRoundedCorners();

        // Make every child control draggable (except the clickable app icon)
        EnableDragOnAllChildren();
    }

    public void UpdateData(
        long activeSeconds, long productiveSeconds, int focusScore,
        string? focusState, string? focusMode,
        long? focusRemainingMs, int? cycleNumber)
    {
        _timeValue.Text = FormatTime(activeSeconds);
        _prodValue.Text = FormatTime(productiveSeconds);
        _scoreValue.Text = focusScore.ToString();

        // Color the score based on value
        _scoreValue.ForeColor = focusScore >= 80 ? Green : focusScore >= 50 ? Amber : Color.FromArgb(248, 113, 113);

        var hasFocus = focusState is "FocusRunning" or "BreakRunning" or "FocusPaused";
        _focusVisible = hasFocus && focusMode is not null and not "None";

        if (_focusVisible)
        {
            var remaining = TimeSpan.FromMilliseconds(focusRemainingMs ?? 0);
            var timeStr = $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
            var modeStr = focusState == "BreakRunning" ? "Pausa" : focusMode;
            var cycleStr = cycleNumber > 0 ? $" #{cycleNumber}" : "";
            _focusValue.Text = $"{modeStr} {timeStr}{cycleStr}";
            _focusDot.Visible = true;
            _focusValue.Visible = true;
            // Pulse the dot color for breaks
            _focusDot.ForeColor = focusState == "BreakRunning" ? Green : Purple;
        }
        else
        {
            _focusDot.Visible = false;
            _focusValue.Visible = false;
        }

        LayoutAll();
    }

    public void UpdateTrackingState(bool isTracking, bool isPaused)
    {
        var active = isTracking && !isPaused;
        _statusDot.LedColor = active ? Green : Color.FromArgb(248, 113, 113);
        _statusDot.Pulsing = active;
    }

    private void LayoutAll()
    {
        var cy = H / 2;
        var x = Pad;

        // Status dot — vertically centered (18x18 control, center at cy)
        _statusDot.Location = new Point(x, cy - _statusDot.Height / 2);
        x += _statusDot.Width;

        PlaceAt(_timeIcon, ref x, cy);
        PlaceAt(_timeValue, ref x, cy);
        x += SepGap;

        PlaceAt(_prodIcon, ref x, cy);
        PlaceAt(_prodValue, ref x, cy);
        x += SepGap;

        PlaceAt(_scoreIcon, ref x, cy);
        PlaceAt(_scoreValue, ref x, cy);

        if (_focusVisible)
        {
            x += SepGap;
            PlaceAt(_focusDot, ref x, cy);
            x += 2;
            PlaceAt(_focusValue, ref x, cy);
        }

        x += SepGap + 4;

        // App icon
        _appIcon.Location = new Point(x, cy - IconSize / 2);
        x += IconSize + Pad;

        if (ClientSize.Width != x)
        {
            var oldRight = Right;
            ClientSize = new Size(x, H);
            if (oldRight > 0) Left = oldRight - Width;
            MakePillShape();
        }
    }

    private static void PlaceAt(Label lbl, ref int x, int cy)
    {
        if (!lbl.Visible) return;
        var sz = TextRenderer.MeasureText(lbl.Text, lbl.Font);
        lbl.Location = new Point(x, cy - sz.Height / 2);
        x += sz.Width + 2;
    }

    private static string FormatTime(long s)
    {
        var h = s / 3600;
        var m = (s % 3600) / 60;
        return h > 0 ? $"{h}h {m:D2}m" : $"{m}m";
    }

    // --- Pill shape via Region ---
    private void MakePillShape()
    {
        var r = H / 2;
        var gp = new GraphicsPath();
        gp.AddArc(0, 0, H, H, 90, 180);           // left semicircle
        gp.AddArc(Width - H, 0, H, H, 270, 180);  // right semicircle
        gp.CloseFigure();
        Region = new Region(gp);
    }

    // --- Paint ---
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Fill with card bg (slightly lighter than form bg for depth)
        using (var bgBrush = new SolidBrush(BgCard))
        {
            var gp = new GraphicsPath();
            var r = H / 2;
            gp.AddArc(0, 0, H, H, 90, 180);
            gp.AddArc(Width - H, 0, H, H, 270, 180);
            gp.CloseFigure();
            g.FillPath(bgBrush, gp);
        }

        // Subtle top highlight (glass effect)
        using (var highlightBrush = new LinearGradientBrush(
            new Rectangle(0, 0, Width, 2),
            Color.FromArgb(30, 255, 255, 255), Color.FromArgb(0, 255, 255, 255),
            LinearGradientMode.Vertical))
        {
            g.FillRectangle(highlightBrush, H / 2, 1, Width - H, 1);
        }

        // Border
        using (var pen = new Pen(BorderDark, 1))
        {
            var gp = new GraphicsPath();
            gp.AddArc(0, 0, H - 1, H - 1, 90, 180);
            gp.AddArc(Width - H, 0, H - 1, H - 1, 270, 180);
            gp.CloseFigure();
            g.DrawPath(pen, gp);
        }

        // Draw separator lines between sections
        DrawSep(g, _timeValue, SepGap / 2);
        DrawSep(g, _prodValue, SepGap / 2);
        if (_focusVisible)
            DrawSep(g, _scoreValue, SepGap / 2);
    }

    private void DrawSep(Graphics g, Label afterLabel, int offset)
    {
        var x = afterLabel.Right + offset;
        using var pen = new Pen(SepLine, 1);
        g.DrawLine(pen, x, 12, x, H - 12);
    }

    // --- Draggable from anywhere ---
    // WndProc for the form background (areas not covered by child controls)
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x84) // WM_NCHITTEST
        {
            base.WndProc(ref m);
            if (m.Result == (IntPtr)1) m.Result = (IntPtr)2;
            return;
        }
        base.WndProc(ref m);
    }

    /// <summary>
    /// Attach drag behavior to all child controls (except the app icon which is clickable).
    /// This makes the entire bar draggable regardless of which label the user grabs.
    /// </summary>
    private void EnableDragOnAllChildren()
    {
        foreach (Control c in Controls)
        {
            if (c == _appIcon) continue; // app icon is clickable, not draggable
            c.MouseDown += OnChildMouseDown;
        }
    }

    private void OnChildMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        // Release capture and send WM_NCLBUTTONDOWN with HTCAPTION to start a form drag
        ReleaseCapture();
        SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); // WM_NCLBUTTONDOWN, HTCAPTION
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();
    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000008 | 0x08000000 | 0x00080000;
            // WS_EX_TOPMOST | WS_EX_NOACTIVATE | WS_EX_LAYERED (for opacity + region)
            return cp;
        }
    }

    private void EnableRoundedCorners()
    {
        try { var p = 2; DwmSetWindowAttribute(Handle, 33, ref p, 4); } catch { }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
}

/// <summary>
/// A small circular LED control with a smooth pulsing glow animation.
/// The core dot is always visible; when Pulsing=true, a soft halo
/// expands and fades in a breathing rhythm.
/// </summary>
internal sealed class PulsingLedControl : Control
{
    private Color _ledColor = Color.FromArgb(74, 222, 128);
    private bool _pulsing = true;
    private float _phase; // 0..1 animation phase
    private readonly System.Windows.Forms.Timer _animTimer;

    public Color LedColor
    {
        get => _ledColor;
        set { _ledColor = value; Invalidate(); }
    }

    public bool Pulsing
    {
        get => _pulsing;
        set
        {
            _pulsing = value;
            if (value) _animTimer.Start(); else { _animTimer.Stop(); _phase = 0; Invalidate(); }
        }
    }

    public PulsingLedControl()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor
               | ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
        Size = new Size(18, 18);

        _animTimer = new System.Windows.Forms.Timer { Interval = 40 }; // ~25fps
        _animTimer.Tick += (_, _) =>
        {
            _phase = (_phase + 0.025f) % 1f;
            Invalidate();
        };
        _animTimer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var cx = Width / 2f;
        var cy = Height / 2f;
        var coreR = 3.5f; // core dot radius

        if (_pulsing)
        {
            // Breathing sine wave: 0→1→0
            var breath = (float)(Math.Sin(_phase * Math.PI * 2) * 0.5 + 0.5);

            // Outer glow (expanding halo)
            var glowR = coreR + 2f + breath * 4f;
            var glowAlpha = (int)(60 * (1f - breath * 0.6f));
            using var glowBrush = new SolidBrush(Color.FromArgb(glowAlpha, _ledColor));
            g.FillEllipse(glowBrush, cx - glowR, cy - glowR, glowR * 2, glowR * 2);

            // Mid ring
            var midR = coreR + 1f + breath * 2f;
            var midAlpha = (int)(100 * (1f - breath * 0.4f));
            using var midBrush = new SolidBrush(Color.FromArgb(midAlpha, _ledColor));
            g.FillEllipse(midBrush, cx - midR, cy - midR, midR * 2, midR * 2);
        }

        // Core dot (always solid)
        using var coreBrush = new SolidBrush(_ledColor);
        g.FillEllipse(coreBrush, cx - coreR, cy - coreR, coreR * 2, coreR * 2);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _animTimer.Dispose();
        base.Dispose(disposing);
    }
}
