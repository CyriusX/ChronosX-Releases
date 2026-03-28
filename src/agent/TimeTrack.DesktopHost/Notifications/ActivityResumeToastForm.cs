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

    private static readonly Color BgColor = Color.FromArgb(22, 24, 35);
    private static readonly Color BorderColor = Color.FromArgb(58, 62, 82);
    private static readonly Color TextWhite = Color.FromArgb(245, 247, 251);
    private static readonly Color TextBody = Color.FromArgb(210, 215, 230);
    private static readonly Color TextMuted = Color.FromArgb(155, 160, 180);
    private static readonly Color AccentCyan = Color.FromArgb(74, 217, 255);
    private static readonly Color AccentCyanDark = Color.FromArgb(45, 140, 200);
    private static readonly Color BtnNoBg = Color.FromArgb(40, 44, 60);
    private static readonly Color BtnNoBorder = Color.FromArgb(70, 74, 95);
    private static readonly Color ProgressTrack = Color.FromArgb(38, 42, 58);
    private static readonly Color SepColor = Color.FromArgb(45, 48, 65);

    private const int ToastW = 580;
    private const int Pad = 32;
    private const int ProgressH = 7;

    private readonly int _progressY;
    private readonly int _progressW;
    private readonly Label _countdownLabel;

    public ActivityResumeToastForm(int countdownSeconds = 20)
    {
        _totalTicks = countdownSeconds * 10;
        _elapsedTicks = 0;

        SuspendLayout();

        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = BgColor;
        Opacity = 0.95;
        DoubleBuffered = true;

        var cw = ToastW - Pad * 2;
        var y = Pad;

        // Icon
        var icon = new Label
        {
            Text = "\u23F1",
            Font = new Font("Segoe UI Emoji", 26f),
            ForeColor = AccentCyan,
            Size = new Size(52, 60),
            Location = new Point(Pad, y),
            TextAlign = ContentAlignment.TopCenter,
        };
        Controls.Add(icon);

        var tx = Pad + 60;
        var tw = cw - 60;

        // Title
        var title = new Label
        {
            Text = "Retomar monitoramento?",
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            ForeColor = TextWhite,
            AutoSize = true,
            MaximumSize = new Size(tw, 0),
            Location = new Point(tx, y),
        };
        Controls.Add(title);

        // Subtitle
        var subtitle = new Label
        {
            Text = "ChronosX detectou atividade no computador",
            Font = new Font("Segoe UI", 10f),
            ForeColor = TextMuted,
            AutoSize = true,
            MaximumSize = new Size(tw, 0),
            Location = new Point(tx, y + 36),
        };
        Controls.Add(subtitle);
        y += 70;

        // Separator
        var sep = new Panel { BackColor = SepColor, Size = new Size(cw, 1), Location = new Point(Pad, y) };
        Controls.Add(sep);
        y += 20;

        // Body
        var body = new Label
        {
            Text = "Voce esta usando o computador ha algum tempo.\nDeseja retomar o monitoramento de atividades?",
            Font = new Font("Segoe UI", 11f),
            ForeColor = TextBody,
            AutoSize = true,
            MaximumSize = new Size(cw, 0),
            Location = new Point(Pad, y),
        };
        Controls.Add(body);
        y += body.PreferredHeight + 20;

        // Progress bar
        _progressY = y;
        _progressW = cw;
        y += ProgressH + 14;

        // Countdown
        _countdownLabel = new Label
        {
            Text = FormatCountdown(countdownSeconds),
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = AccentCyan,
            AutoSize = true,
            MaximumSize = new Size(cw, 0),
            Location = new Point(Pad, y),
        };
        Controls.Add(_countdownLabel);
        y += 30;

        // Buttons
        var btnW = (cw - 20) / 2;
        const int btnH = 48;

        var yesBtn = new Button
        {
            Text = "Sim, retomar",
            Font = new Font("Segoe UI Semibold", 11f),
            Size = new Size(btnW, btnH),
            Location = new Point(Pad, y),
            FlatStyle = FlatStyle.Flat,
            BackColor = AccentCyan,
            ForeColor = Color.FromArgb(12, 14, 22),
            Cursor = Cursors.Hand,
        };
        yesBtn.FlatAppearance.BorderSize = 0;
        yesBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(100, 230, 255);
        yesBtn.Click += (_, _) => FireResult(true);
        Controls.Add(yesBtn);

        var noBtn = new Button
        {
            Text = "Nao, manter pausado",
            Font = new Font("Segoe UI", 10.5f),
            Size = new Size(btnW, btnH),
            Location = new Point(Pad + btnW + 20, y),
            FlatStyle = FlatStyle.Flat,
            BackColor = BtnNoBg,
            ForeColor = TextMuted,
            Cursor = Cursors.Hand,
        };
        noBtn.FlatAppearance.BorderColor = BtnNoBorder;
        noBtn.FlatAppearance.BorderSize = 1;
        noBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 54, 72);
        noBtn.Click += (_, _) => FireResult(false);
        Controls.Add(noBtn);
        y += btnH + Pad;

        ClientSize = new Size(ToastW, y);
        var wa = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(wa.Right - Width - 24, wa.Top + 24);

        ResumeLayout(true);
        PerformLayout();
        EnableRoundedCorners();

        _timer = new System.Windows.Forms.Timer { Interval = 100 };
        _timer.Tick += OnTimerTick;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _timer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        _timer.Dispose();
        if (!_resultFired) FireResult(false);
        base.OnFormClosed(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var pen = new Pen(BorderColor, 1))
            g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

        using (var brush = new LinearGradientBrush(
            new Point(0, 0), new Point(Width, 0), AccentCyan, AccentCyanDark))
            g.FillRectangle(brush, 1, 1, Width - 2, 3);

        var trackRect = new Rectangle(Pad, _progressY, _progressW, ProgressH);
        using (var b = new SolidBrush(ProgressTrack))
        using (var p = RoundedRect(trackRect, 3))
            g.FillPath(b, p);

        if (_totalTicks > 0)
        {
            var pct = 1.0f - ((float)_elapsedTicks / _totalTicks);
            var fw = Math.Max(6, (int)(_progressW * pct));
            var fillRect = new Rectangle(Pad, _progressY, fw, ProgressH);
            using var fb = new LinearGradientBrush(fillRect, AccentCyan, AccentCyanDark, LinearGradientMode.Horizontal);
            using var fp = RoundedRect(fillRect, 3);
            g.FillPath(fb, fp);
        }
    }

    private static GraphicsPath RoundedRect(Rectangle r, int rad)
    {
        var d = rad * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _elapsedTicks++;
        var remaining = Math.Max(0, (_totalTicks - _elapsedTicks + 9) / 10);
        _countdownLabel.Text = FormatCountdown(remaining);
        Invalidate(new Rectangle(Pad - 1, _progressY - 1, _progressW + 2, ProgressH + 2));
        if (_elapsedTicks >= _totalTicks) { _timer.Stop(); FireResult(true); }
    }

    private static string FormatCountdown(int s) => $"Retomando automaticamente em {s}s...";

    private void FireResult(bool resume)
    {
        if (_resultFired) return;
        _resultFired = true;
        _timer.Stop();
        PromptResult?.Invoke(this, resume);
        Close();
    }

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000008 | 0x08000000;
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
