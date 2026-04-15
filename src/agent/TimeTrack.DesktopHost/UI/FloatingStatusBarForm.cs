using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace TimeTrack.DesktopHost.UI;

public sealed class FloatingStatusBarForm : Form
{
    public event EventHandler? RestoreRequested;
    public event EventHandler<string>? FocusCommandRequested;

    // Theme
    private static readonly Color BgCard = Color.FromArgb(24, 27, 38);
    private static readonly Color BorderDark = Color.FromArgb(45, 50, 65);
    private static readonly Color TextPrimary = Color.FromArgb(240, 243, 255);
    private static readonly Color TextMuted = Color.FromArgb(130, 140, 160);
    private static readonly Color Cyan = Color.FromArgb(74, 217, 255);
    private static readonly Color Green = Color.FromArgb(74, 222, 128);
    private static readonly Color Amber = Color.FromArgb(251, 191, 36);
    private static readonly Color Purple = Color.FromArgb(168, 130, 255);
    private static readonly Color RedSoft = Color.FromArgb(248, 113, 113);
    private static readonly Color SepLine = Color.FromArgb(40, 44, 60);

    private const int H = 48;
    private const int Pad = 16;
    private const int IconSize = 22;
    private const int SepGap = 12;

    // Controls
    private readonly PulsingLedControl _statusDot;
    private readonly Label _timeIcon, _timeValue;
    private readonly Label _prodIcon, _prodValue;
    private readonly Label _scoreIcon, _scoreValue;
    private readonly Label _focusDot, _focusValue;
    private readonly Label _taskDot, _taskValue;
    private readonly PictureBox _appIcon;

    // Focus action icons
    private readonly FocusActionIcon _btnStop;
    private readonly FocusActionIcon _btnSkip;

    private bool _focusVisible;
    private bool _taskVisible;

    public FloatingStatusBarForm()
    {
        SuspendLayout();

        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(16, 18, 27);
        Opacity = 0.94;
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.None;

        var bold = new Font("Segoe UI Semibold", 10f);
        var icon = new Font("Segoe UI Emoji", 10f);
        var focus = new Font("Segoe UI Semibold", 9.5f);

        // --- Status LED ---
        _statusDot = new PulsingLedControl { Size = new Size(18, 18), LedColor = Green, BackColor = Color.Transparent };
        Controls.Add(_statusDot);

        // --- Data labels ---
        _timeIcon = Lbl("\u23F1", icon, Cyan);
        _timeValue = Lbl("—h ——m", bold, TextMuted);
        _prodIcon = Lbl("\u2714", new Font("Segoe UI", 9f), Green);
        _prodValue = Lbl("—h ——m", bold, TextMuted);
        _scoreIcon = Lbl("\u26A1", icon, Amber);
        _scoreValue = Lbl("——", bold, TextMuted);

        // --- Focus info ---
        _focusDot = Lbl("\u25CF", new Font("Segoe UI", 7f), Purple); _focusDot.Visible = false;
        _focusValue = Lbl("", focus, Purple); _focusValue.Visible = false;

        // --- Kanban task info (project-colored dot + "Project · Task · 0:42") ---
        _taskDot = Lbl("\u25CF", new Font("Segoe UI", 7f), Cyan); _taskDot.Visible = false;
        _taskValue = Lbl("", focus, Cyan); _taskValue.Visible = false;
        _taskValue.Cursor = Cursors.Hand;
        _taskValue.Click += (_, _) => RestoreRequested?.Invoke(this, EventArgs.Empty);
        new ToolTip().SetToolTip(_taskValue, "Abrir quadro Kanban");

        // --- Focus action icons (clean, minimal, no button borders) ---
        _btnSkip = new FocusActionIcon("\u23ED", Cyan, "Pular pausa") { Visible = false };
        _btnSkip.Clicked += () => FocusCommandRequested?.Invoke(this, "skipBreak");
        Controls.Add(_btnSkip);

        _btnStop = new FocusActionIcon("\u2716", RedSoft, "Parar sessao") { Visible = false };
        _btnStop.Clicked += () => FocusCommandRequested?.Invoke(this, "stopFocusMode");
        Controls.Add(_btnStop);

        // --- App icon ---
        _appIcon = new PictureBox
        {
            Size = new Size(IconSize, IconSize),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
        };
        try
        {
            var p = Path.Combine(AppContext.BaseDirectory, "Resources", "app-icon.ico");
            if (File.Exists(p)) { using var ico = new Icon(p, IconSize, IconSize); _appIcon.Image = ico.ToBitmap(); }
        }
        catch { }
        _appIcon.Click += (_, _) => RestoreRequested?.Invoke(this, EventArgs.Empty);
        new ToolTip().SetToolTip(_appIcon, "Abrir ChronosX");
        Controls.Add(_appIcon);

        LayoutAll();

        // Restore saved position or default to bottom-right
        var saved = LoadPosition();
        if (saved.HasValue)
            Location = saved.Value;
        else
        {
            var wa = Screen.PrimaryScreen!.WorkingArea;
            Location = new Point(wa.Right - Width - 20, wa.Bottom - Height - 20);
        }

        ResumeLayout(true); PerformLayout();
        MakePill(); EnableRoundedCorners(); EnableDragOnAll();
    }

    // Save position when user drags the bar
    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        // Only save if form is visible and not being laid out initially
        if (Visible && IsHandleCreated)
            SavePosition(Location);
    }

    private static readonly string PositionFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TimeTrack", "floating-bar-pos.txt");

    private static void SavePosition(Point p)
    {
        try
        {
            var dir = Path.GetDirectoryName(PositionFile)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(PositionFile, $"{p.X},{p.Y}");
        }
        catch { }
    }

    private static Point? LoadPosition()
    {
        try
        {
            if (!File.Exists(PositionFile)) return null;
            var parts = File.ReadAllText(PositionFile).Trim().Split(',');
            if (parts.Length == 2 && int.TryParse(parts[0], out var x) && int.TryParse(parts[1], out var y))
            {
                // Validate the position is on a visible screen
                var pt = new Point(x, y);
                foreach (var screen in Screen.AllScreens)
                {
                    if (screen.WorkingArea.Contains(pt))
                        return pt;
                }
            }
        }
        catch { }
        return null;
    }

    public void UpdateData(long activeSec, long prodSec, int score,
        string? focusState, string? focusMode, long? focusRemMs, int? cycle)
    {
        _timeValue.Text = Fmt(activeSec);
        _timeValue.ForeColor = TextPrimary;
        _prodValue.Text = Fmt(prodSec);
        _prodValue.ForeColor = Green;
        _scoreValue.Text = score.ToString();
        _scoreValue.ForeColor = score >= 80 ? Green : score >= 50 ? Amber : RedSoft;

        var hasFocus = focusState is "FocusRunning" or "BreakRunning" or "FocusPaused";
        _focusVisible = hasFocus && focusMode is not null and not "None";

        if (_focusVisible)
        {
            var rem = TimeSpan.FromMilliseconds(focusRemMs ?? 0);
            var isBreak = focusState == "BreakRunning";
            var isPaused = focusState == "FocusPaused";
            var label = isBreak ? "Pausa" : focusMode;
            var cycleStr = cycle > 0 ? $" #{cycle}" : "";
            _focusValue.Text = $"{label} {(int)rem.TotalMinutes:D2}:{rem.Seconds:D2}{cycleStr}";
            _focusDot.ForeColor = isBreak ? Green : isPaused ? Amber : Purple;
            _focusDot.Visible = true; _focusValue.Visible = true;

            _btnStop.Visible = true;
            _btnSkip.Visible = isBreak;
        }
        else
        {
            _focusDot.Visible = _focusValue.Visible = false;
            _btnStop.Visible = _btnSkip.Visible = false;
        }
        LayoutAll();
    }

    public void UpdateTrackingState(bool tracking, bool paused)
    {
        _statusDot.LedColor = tracking && !paused ? Green : RedSoft;
        _statusDot.Pulsing = tracking && !paused;
    }

    /// <summary>
    /// Updates the kanban task section. Pass all nulls to hide it.
    /// </summary>
    public void UpdateTaskData(string? projectName, string? taskTitle, long? elapsedSec, string? projectColorHex)
    {
        var show = !string.IsNullOrEmpty(taskTitle) && elapsedSec.HasValue;
        _taskVisible = show;

        if (show)
        {
            var color = ParseHexColor(projectColorHex) ?? Cyan;
            _taskDot.ForeColor = color;
            _taskValue.ForeColor = color;

            // Compact label: "Project · Task · H:MM"
            var elapsed = TimeSpan.FromSeconds(Math.Max(0, elapsedSec!.Value));
            var timeStr = elapsed.TotalHours >= 1
                ? $"{(int)elapsed.TotalHours}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}"
                : $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";

            var project = Truncate(projectName ?? "Projeto", 14);
            var task = Truncate(taskTitle!, 20);
            _taskValue.Text = $"{project} · {task} · {timeStr}";

            _taskDot.Visible = true;
            _taskValue.Visible = true;
        }
        else
        {
            _taskDot.Visible = false;
            _taskValue.Visible = false;
        }

        LayoutAll();
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s.Substring(0, Math.Max(1, max - 1)) + "…";

    private static Color? ParseHexColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        var h = hex.TrimStart('#');
        if (h.Length != 6) return null;
        try
        {
            var r = Convert.ToInt32(h.Substring(0, 2), 16);
            var g = Convert.ToInt32(h.Substring(2, 2), 16);
            var b = Convert.ToInt32(h.Substring(4, 2), 16);
            return Color.FromArgb(r, g, b);
        }
        catch { return null; }
    }

    private void LayoutAll()
    {
        var cy = H / 2; var x = Pad;
        _statusDot.Location = new Point(x, cy - 9); x += 18;

        Place(_timeIcon, ref x, cy); Place(_timeValue, ref x, cy); x += SepGap;
        Place(_prodIcon, ref x, cy); Place(_prodValue, ref x, cy); x += SepGap;
        Place(_scoreIcon, ref x, cy); Place(_scoreValue, ref x, cy);

        if (_focusVisible)
        {
            x += SepGap;
            Place(_focusDot, ref x, cy); x += 2;
            Place(_focusValue, ref x, cy); x += 4;
            PlaceIcon(_btnSkip, ref x, cy);
            PlaceIcon(_btnStop, ref x, cy);
        }

        if (_taskVisible)
        {
            x += SepGap;
            Place(_taskDot, ref x, cy); x += 2;
            Place(_taskValue, ref x, cy);
        }

        x += SepGap + 2;
        _appIcon.Location = new Point(x, cy - IconSize / 2); x += IconSize + Pad;

        if (ClientSize.Width != x)
        {
            var r = Right; ClientSize = new Size(x, H);
            if (r > 0) Left = r - Width;
            MakePill();
        }
    }

    // --- Helpers ---
    private Label Lbl(string text, Font f, Color c)
    {
        var l = new Label { Text = text, Font = f, ForeColor = c, AutoSize = true, BackColor = Color.Transparent };
        Controls.Add(l); return l;
    }
    private static void Place(Label l, ref int x, int cy)
    {
        if (!l.Visible) return;
        var s = TextRenderer.MeasureText(l.Text, l.Font);
        l.Location = new Point(x, cy - s.Height / 2); x += s.Width + 2;
    }
    private static void PlaceIcon(FocusActionIcon ic, ref int x, int cy)
    {
        if (!ic.Visible) return;
        ic.Location = new Point(x, cy - ic.Height / 2); x += ic.Width + 2;
    }
    private static string Fmt(long s) { var h = s / 3600; var m = (s % 3600) / 60; return h > 0 ? $"{h}h {m:D2}m" : $"{m}m"; }

    // --- Pill shape ---
    private void MakePill()
    {
        var gp = new GraphicsPath();
        gp.AddArc(0, 0, H, H, 90, 180);
        gp.AddArc(Width - H, 0, H, H, 270, 180);
        gp.CloseFigure(); Region = new Region(gp);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;

        // Background
        using (var b = new SolidBrush(BgCard))
        { var p = new GraphicsPath(); p.AddArc(0, 0, H, H, 90, 180); p.AddArc(Width - H, 0, H, H, 270, 180); p.CloseFigure(); g.FillPath(b, p); }

        // Glass highlight
        using (var b = new LinearGradientBrush(new Rectangle(0, 0, Width, 2), Color.FromArgb(25, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), LinearGradientMode.Vertical))
            g.FillRectangle(b, H / 2, 1, Width - H, 1);

        // Border
        using (var pen = new Pen(BorderDark, 1))
        { var p = new GraphicsPath(); p.AddArc(0, 0, H - 1, H - 1, 90, 180); p.AddArc(Width - H, 0, H - 1, H - 1, 270, 180); p.CloseFigure(); g.DrawPath(pen, p); }

        // Separators
        DrawSep(g, _timeValue); DrawSep(g, _prodValue);
        if (_focusVisible) DrawSep(g, _scoreValue);
    }

    private void DrawSep(Graphics g, Label after)
    { var sx = after.Right + SepGap / 2; using var pen = new Pen(SepLine, 1); g.DrawLine(pen, sx, 13, sx, H - 13); }

    // --- Drag ---
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x84) { base.WndProc(ref m); if (m.Result == (IntPtr)1) m.Result = (IntPtr)2; return; }
        base.WndProc(ref m);
    }
    private void EnableDragOnAll()
    {
        foreach (Control c in Controls)
        {
            if (c == _appIcon || c is FocusActionIcon) continue;
            c.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); } };
        }
    }

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams { get { var cp = base.CreateParams; cp.ExStyle |= 0x00000008 | 0x08000000 | 0x00080000; return cp; } }
    private void EnableRoundedCorners() { try { var p = 2; DwmSetWindowAttribute(Handle, 33, ref p, 4); } catch { } }

    [DllImport("dwmapi.dll", PreserveSig = true)] private static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h, int m, IntPtr w, IntPtr l);
}

/// <summary>
/// Minimal, clean focus action icon — a small round circle with a symbol.
/// Hover: subtle glow. No borders, no button feel. Fits the pill bar aesthetic.
/// </summary>
internal sealed class FocusActionIcon : Control
{
    public event Action? Clicked;

    private readonly string _icon;
    private readonly Color _color;
    private readonly Color _parentBg;
    private bool _hovered;
    private const int Sz = 26;

    public FocusActionIcon(string icon, Color color, string tooltip, Color? parentBg = null)
    {
        _icon = icon; _color = color;
        _parentBg = parentBg ?? Color.FromArgb(24, 27, 38);
        Size = new Size(Sz, Sz);
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        BackColor = _parentBg;
        new ToolTip().SetToolTip(this, tooltip);
    }

    protected override void OnMouseEnter(EventArgs e) { _hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnClick(EventArgs e) { Clicked?.Invoke(); base.OnClick(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Fill entire area with parent bg to erase any border artifacts
        g.Clear(_parentBg);

        var cx = Width / 2f; var cy = Height / 2f; var r = Sz / 2f - 1;

        if (_hovered)
        {
            // Subtle filled circle + ring on hover
            using (var bg = new SolidBrush(Color.FromArgb(40, _color)))
                g.FillEllipse(bg, cx - r, cy - r, r * 2, r * 2);
            using var pen = new Pen(Color.FromArgb(50, _color), 1f);
            g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
        }

        // Icon — full color on hover, dimmed otherwise
        var iconColor = _hovered ? _color : Color.FromArgb(140, _color);
        TextRenderer.DrawText(g, _icon, new Font("Segoe UI Symbol", 9f),
            new Rectangle(0, 0, Width, Height), iconColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

/// <summary>
/// Pulsing LED with breathing glow animation.
/// </summary>
internal sealed class PulsingLedControl : Control
{
    private Color _ledColor = Color.FromArgb(74, 222, 128);
    private bool _pulsing = true;
    private float _phase;
    private readonly System.Windows.Forms.Timer _t;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color LedColor { get => _ledColor; set { _ledColor = value; Invalidate(); } }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Pulsing
    {
        get => _pulsing;
        set { _pulsing = value; if (value) _t.Start(); else { _t.Stop(); _phase = 0; Invalidate(); } }
    }

    public PulsingLedControl()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        BackColor = Color.Transparent; Size = new Size(18, 18);
        _t = new System.Windows.Forms.Timer { Interval = 40 };
        _t.Tick += (_, _) => { _phase = (_phase + 0.025f) % 1f; Invalidate(); };
        _t.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        var cx = Width / 2f; var cy = Height / 2f; var cr = 3.5f;
        if (_pulsing)
        {
            var b = (float)(Math.Sin(_phase * Math.PI * 2) * 0.5 + 0.5);
            using (var gb = new SolidBrush(Color.FromArgb((int)(60 * (1 - b * .6)), _ledColor)))
                g.FillEllipse(gb, cx - cr - 2 - b * 4, cy - cr - 2 - b * 4, (cr + 2 + b * 4) * 2, (cr + 2 + b * 4) * 2);
            using (var mb = new SolidBrush(Color.FromArgb((int)(100 * (1 - b * .4)), _ledColor)))
                g.FillEllipse(mb, cx - cr - 1 - b * 2, cy - cr - 1 - b * 2, (cr + 1 + b * 2) * 2, (cr + 1 + b * 2) * 2);
        }
        using var cb = new SolidBrush(_ledColor);
        g.FillEllipse(cb, cx - cr, cy - cr, cr * 2, cr * 2);
    }

    protected override void Dispose(bool d) { if (d) _t.Dispose(); base.Dispose(d); }
}
