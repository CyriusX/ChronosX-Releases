using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace TimeTrack.DesktopHost.Notifications;

public sealed class ActivityResumeToastForm : Form
{
    public event EventHandler<bool>? PromptResult;

    private int _elapsedTicks;
    private readonly int _totalTicks;
    private readonly System.Windows.Forms.Timer _timer;
    private bool _resultFired;

    // Theme
    private static readonly Color BgCard = Color.FromArgb(22, 25, 36);
    private static readonly Color BorderColor = Color.FromArgb(48, 52, 70);
    private static readonly Color TextWhite = Color.FromArgb(245, 247, 251);
    private static readonly Color TextBody = Color.FromArgb(180, 186, 205);
    private static readonly Color TextDim = Color.FromArgb(100, 108, 130);
    private static readonly Color Cyan = Color.FromArgb(74, 217, 255);
    private static readonly Color CyanDim = Color.FromArgb(35, 100, 155);
    private static readonly Color Green = Color.FromArgb(74, 222, 128);
    private static readonly Color BtnSecBg = Color.FromArgb(34, 38, 52);
    private static readonly Color BtnSecBorder = Color.FromArgb(55, 60, 78);
    private static readonly Color RingTrack = Color.FromArgb(32, 36, 50);

    private const int W = 380;
    private const int SidePad = 32;
    private const int RingSz = 100;
    private const int RingStroke = 5;
    private const int Radius = 18;
    private const int BtnH = 46;

    private readonly int _ringY;
    private readonly Label _countNum;

    public ActivityResumeToastForm(int countdownSeconds = 20)
    {
        _totalTicks = countdownSeconds * 10;

        SuspendLayout();
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(14, 16, 24);
        Opacity = 0.50;
        DoubleBuffered = true;

        var cw = W - SidePad * 2;
        var y = 36;

        // --- Ring area ---
        _ringY = y;
        _countNum = new Label
        {
            Text = countdownSeconds.ToString(),
            Font = new Font("Segoe UI", 28f, FontStyle.Bold),
            ForeColor = Cyan,
            BackColor = Color.Transparent,
            AutoSize = true,
        };
        Controls.Add(_countNum);
        y += RingSz + 28;

        // --- Title (centered, auto-height) ---
        var titleFont = new Font("Segoe UI Semibold", 15f);
        var titleText = "Retomar monitoramento?";
        var titleSize = TextRenderer.MeasureText(titleText, titleFont, new Size(cw, 0), TextFormatFlags.WordBreak);
        var title = new Label
        {
            Text = titleText,
            Font = titleFont,
            ForeColor = TextWhite,
            Size = new Size(cw, titleSize.Height + 4),
            Location = new Point(SidePad, y),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        Controls.Add(title);
        y += title.Height + 8;

        // --- Subtitle (centered, auto-height) ---
        var subFont = new Font("Segoe UI", 10f);
        var subText = "Voce esta usando o computador ha algum tempo.";
        var subSize = TextRenderer.MeasureText(subText, subFont, new Size(cw, 0), TextFormatFlags.WordBreak);
        var sub = new Label
        {
            Text = subText,
            Font = subFont,
            ForeColor = TextBody,
            Size = new Size(cw, subSize.Height + 4),
            Location = new Point(SidePad, y),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        Controls.Add(sub);
        y += sub.Height + 28;

        // --- Resume button ---
        var yesBtn = new Button
        {
            Text = "\u25B6   Retomar tracking",
            Font = new Font("Segoe UI Semibold", 11f),
            Size = new Size(cw, BtnH),
            Location = new Point(SidePad, y),
            FlatStyle = FlatStyle.Flat,
            BackColor = Cyan,
            ForeColor = Color.FromArgb(10, 14, 22),
            Cursor = Cursors.Hand,
        };
        yesBtn.FlatAppearance.BorderSize = 0;
        yesBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(95, 228, 255);
        yesBtn.Click += (_, _) => FireResult(true);
        Controls.Add(yesBtn);
        y += BtnH + 10;

        // --- Dismiss button ---
        var noBtn = new Button
        {
            Text = "Manter pausado",
            Font = new Font("Segoe UI", 10f),
            Size = new Size(cw, BtnH - 6),
            Location = new Point(SidePad, y),
            FlatStyle = FlatStyle.Flat,
            BackColor = BtnSecBg,
            ForeColor = TextDim,
            Cursor = Cursors.Hand,
        };
        noBtn.FlatAppearance.BorderColor = BtnSecBorder;
        noBtn.FlatAppearance.BorderSize = 1;
        noBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(44, 48, 65);
        noBtn.Click += (_, _) => FireResult(false);
        Controls.Add(noBtn);
        y += BtnH - 6 + 36;

        ClientSize = new Size(W, y);
        var wa = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(wa.Right - Width - 28, wa.Top + 28);

        CenterCountNum();
        ResumeLayout(true);
        PerformLayout();
        ApplyRegion();
        EnableRoundedCorners();

        _timer = new System.Windows.Forms.Timer { Interval = 100 };
        _timer.Tick += OnTick;
    }

    private void CenterCountNum()
    {
        var sz = TextRenderer.MeasureText(_countNum.Text, _countNum.Font);
        var cx = W / 2;
        var cy = _ringY + RingSz / 2;
        _countNum.Location = new Point(cx - sz.Width / 2 + 2, cy - sz.Height / 2 + 2);
    }

    protected override void OnShown(EventArgs e) { base.OnShown(e); _timer.Start(); }
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop(); _timer.Dispose();
        if (!_resultFired) FireResult(false);
        base.OnFormClosed(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // Card fill
        using (var b = new SolidBrush(BgCard))
        using (var p = MakeRoundedRect(0, 0, Width, Height, Radius))
            g.FillPath(b, p);

        // Border
        using (var pen = new Pen(BorderColor, 1))
        using (var p = MakeRoundedRect(0, 0, Width - 1, Height - 1, Radius))
            g.DrawPath(pen, p);

        // Top glow line
        using (var b = new LinearGradientBrush(
            new Point(Radius, 0), new Point(Width - Radius, 0), Cyan, CyanDim))
            g.FillRectangle(b, Radius, 1, Width - Radius * 2, 2);

        // --- Ring ---
        var cx = W / 2f;
        var cy = _ringY + RingSz / 2f;
        var rr = new RectangleF(cx - RingSz / 2f, cy - RingSz / 2f, RingSz, RingSz);

        // Outer subtle glow
        using (var glowPen = new Pen(Color.FromArgb(12, Cyan), RingStroke + 8))
            g.DrawEllipse(glowPen, rr);

        // Track
        using (var pen = new Pen(RingTrack, RingStroke))
            g.DrawEllipse(pen, rr);

        // Arc
        if (_totalTicks > 0)
        {
            var pct = 1f - (float)_elapsedTicks / _totalTicks;
            var sweep = pct * 360f;
            if (sweep > 0.5f)
            {
                using var pen = new Pen(Cyan, RingStroke) { StartCap = LineCap.Round, EndCap = LineCap.Round };
                g.DrawArc(pen, rr, -90, sweep);
            }
        }
    }

    private void ApplyRegion()
    {
        using var p = MakeRoundedRect(0, 0, Width, Height, Radius);
        Region = new Region(p);
    }

    private static GraphicsPath MakeRoundedRect(int x, int y, int w, int h, int r)
    {
        var d = r * 2;
        var p = new GraphicsPath();
        p.AddArc(x, y, d, d, 180, 90);
        p.AddArc(x + w - d, y, d, d, 270, 90);
        p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        p.AddArc(x, y + h - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _elapsedTicks++;
        var rem = Math.Max(0, (_totalTicks - _elapsedTicks + 9) / 10);
        _countNum.Text = rem.ToString();
        CenterCountNum();
        Invalidate();
        if (_elapsedTicks >= _totalTicks) { _timer.Stop(); FireResult(false); }
    }

    private void FireResult(bool resume)
    {
        if (_resultFired) return;
        _resultFired = true; _timer.Stop();
        PromptResult?.Invoke(this, resume);
        Close();
    }

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x00000008 | 0x08000000 | 0x00080000; return cp; }
    }
    private void EnableRoundedCorners()
    { try { var p = 2; DwmSetWindowAttribute(Handle, 33, ref p, 4); } catch { } }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
}
